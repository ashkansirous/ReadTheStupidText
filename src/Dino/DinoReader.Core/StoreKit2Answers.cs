using System.Text.Json;

namespace DinoReader.Core;

/// <summary>One StoreKit 2 transaction as the Swift wrapper reports it (Slice 390). <paramref name="Jws"/> is Apple's signed transaction, the purchase proof the Worker verifies.</summary>
public sealed record StoreKitTransaction(string ProductId, DateTimeOffset? PurchasedAt, DateTimeOffset? ExpiresAt, bool Trial, string Jws, bool Revoked = false);

/// <summary>The wrapper's answer to "what does this Apple ID own": <paramref name="Current"/> are the entitlements, <paramref name="Latest"/> the newest transaction of each subscription plan (expired ones too).</summary>
public sealed record StoreKitEntitlements(IReadOnlyList<StoreKitTransaction> Current, IReadOnlyList<StoreKitTransaction> Latest)
{
    public IReadOnlySet<string> Owned => Current.Select(t => t.ProductId).ToHashSet();

    /// <summary>The proof per owned product: the JWS of its newest transaction.</summary>
    public IReadOnlyDictionary<string, string> Proofs =>
        Current.GroupBy(t => t.ProductId).ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.PurchasedAt).First().Jws);

    /// <summary>Product to the date of its newest transaction, for the subscription plans only (the tie-break between plans).</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> PurchasedAt =>
        Current.Where(t => SubscriptionProducts.Is(t.ProductId) && t.PurchasedAt is not null)
            .GroupBy(t => t.ProductId).ToDictionary(g => g.Key, g => g.Max(t => t.PurchasedAt!.Value));

    /// <summary>
    /// What the store says about the subscription now. An entitlement past its end is Apple's billing grace period (Apple still lists it), so it is <see cref="SubscriptionState.InGrace"/>; the Worker's answer then gives the real end.
    /// A plan that is only in <see cref="Latest"/> has lapsed: Expired. Nothing at all is no information (a new device before Apple has synced, or a store that was not asked): <see cref="SubscriptionState.Unknown"/> leaves the held record alone.
    /// </summary>
    public SubscriptionStatus Subscription(DateTimeOffset now)
    {
        StoreKitTransaction[] running = [.. Current.Where(t => SubscriptionProducts.Is(t.ProductId))];
        if (running.Length > 0)
        {
            StoreKitTransaction best = running.OrderByDescending(t => t.ExpiresAt ?? DateTimeOffset.MinValue).First();
            bool ahead = best.ExpiresAt is { } end && end > now;
            SubscriptionState state = ahead ? (best.Trial ? SubscriptionState.Trial : SubscriptionState.Active) : SubscriptionState.InGrace;
            return new SubscriptionStatus(state, best.ExpiresAt, best.ProductId, DeviceReadKind.Store, best.PurchasedAt);
        }

        // A revoked (refunded) transaction ranks last and carries no end date: it is not owned, whatever end date it still names (the app has one lapsed state for expired and revoked, as StatusFromWorker maps them).
        StoreKitTransaction? lapsed = Latest.Where(t => SubscriptionProducts.Is(t.ProductId)).OrderByDescending(t => t.Revoked ? DateTimeOffset.MinValue : t.ExpiresAt ?? DateTimeOffset.MinValue).FirstOrDefault();
        return lapsed is null
            ? new SubscriptionStatus(SubscriptionState.Unknown, null, null, DeviceReadKind.Estimated)
            : new SubscriptionStatus(SubscriptionState.Expired, lapsed.Revoked ? null : lapsed.ExpiresAt, lapsed.ProductId, DeviceReadKind.Store);
    }
}

/// <summary>What a Buy ended with: <paramref name="Outcome"/> is the wrapper's word (<see cref="StoreKit2Answers.Bought"/> and the others), <paramref name="Transaction"/> the verified purchase when it was bought.</summary>
public sealed record StoreKitBuyAnswer(string Outcome, StoreKitTransaction? Transaction);

/// <summary>
/// Reads the JSON the Swift wrapper in <c>Platforms/iOS/StoreKit2</c> answers with (Slice 390). Pure, so it is tested on any OS. Every reader returns null for an answer that is not <c>ok</c> or not readable: the caller
/// treats that as a failed read, never as "owns nothing".
/// </summary>
public static class StoreKit2Answers
{
    public const string Bought = "bought";
    public const string Cancelled = "cancelled";
    public const string Pending = "pending";
    public const string Failed = "failed";
    public const string Unsupported = "unsupported";

    /// <summary>The prices (product to the App Store's own text) of a products answer, with the ids Apple did not know; null when the request failed.</summary>
    public static (Dictionary<string, string> Prices, IReadOnlyList<string> Invalid)? Prices(string json)
    {
        using JsonDocument? doc = Open(json);
        if (doc is null || !doc.RootElement.TryGetProperty("products", out JsonElement products) || products.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var prices = new Dictionary<string, string>();
        foreach (JsonElement product in products.EnumerateArray())
        {
            if (Text(product, "id") is { Length: > 0 } id && Text(product, "price") is { } price)
            {
                prices[id] = price;
            }
        }

        return (prices, Strings(doc.RootElement, "invalid"));
    }

    public static StoreKitEntitlements? Entitlements(string json)
    {
        using JsonDocument? doc = Open(json);
        if (doc is null || !doc.RootElement.TryGetProperty("transactions", out JsonElement current) || current.ValueKind != JsonValueKind.Array)
        {
            return null; // no list, or not a list: a failed read, never "owns nothing"
        }

        if (doc.RootElement.TryGetProperty("incomplete", out JsonElement incomplete) && incomplete.ValueKind == JsonValueKind.True)
        {
            return null; // the wrapper saw an entitlement it could not verify: the listing is incomplete, so it is a failed read and owned books are never dropped (the last snapshot stays)
        }

        IReadOnlyList<StoreKitTransaction> latest = doc.RootElement.TryGetProperty("latest", out JsonElement last) ? Transactions(last) : [];
        return new StoreKitEntitlements([.. Transactions(current).Where(t => !t.Revoked)], latest); // a revoked transaction is never an entitlement, even if the wrapper listed one
    }

    public static StoreKitBuyAnswer? Buy(string json)
    {
        using JsonDocument? doc = Open(json);
        if (doc is null || Text(doc.RootElement, "outcome") is not { } outcome)
        {
            return null;
        }

        StoreKitTransaction? transaction = doc.RootElement.TryGetProperty("transaction", out JsonElement t) ? Transaction(t) : null;
        return new StoreKitBuyAnswer(outcome, transaction);
    }

    /// <summary>True when the answer is <c>ok</c> (restore and redeem answer nothing else); false for a failure or an unreadable answer.</summary>
    public static bool IsOk(string json)
    {
        using JsonDocument? doc = Open(json);
        return doc is not null;
    }

    /// <summary>The <c>error</c> of a failed answer (<c>domain:code</c> or <see cref="Unsupported"/>), or null when it was ok or unreadable.</summary>
    public static string? Error(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            return Text(doc.RootElement, "error");
        }
        catch (JsonException)
        {
            // no-fallback: an unreadable answer has no error text; the caller already treats it as failed
            return null;
        }
    }

    /// <summary>The product id of an update the transaction listener reported (<c>event</c> is <c>updated</c>), else null.</summary>
    public static string? UpdatedProduct(string json)
    {
        using JsonDocument? doc = Open(json);
        return doc is not null && Text(doc.RootElement, "event") == "updated" ? Text(doc.RootElement, "productId") : null;
    }

    private static JsonDocument? Open(string json)
    {
        try
        {
            JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("ok", out JsonElement ok) && ok.ValueKind == JsonValueKind.True)
            {
                return doc;
            }

            doc.Dispose();
            return null;
        }
        catch (JsonException)
        {
            // no-fallback: an unreadable answer is a failed read; IosPurchases reports it as an unreachable store
            return null;
        }
    }

    private static List<StoreKitTransaction> Transactions(JsonElement array)
    {
        var list = new List<StoreKitTransaction>();
        if (array.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (JsonElement item in array.EnumerateArray())
        {
            if (Transaction(item) is { } transaction)
            {
                list.Add(transaction);
            }
        }

        return list;
    }

    private static StoreKitTransaction? Transaction(JsonElement item)
    {
        if (Text(item, "productId") is not { Length: > 0 } product || Text(item, "jws") is not { Length: > 0 } jws)
        {
            return null; // a transaction with no product or no signed proof cannot unlock anything
        }

        bool trial = item.TryGetProperty("trial", out JsonElement t) && t.ValueKind == JsonValueKind.True;
        bool revoked = item.TryGetProperty("revoked", out JsonElement r) && r.ValueKind == JsonValueKind.True;
        return new StoreKitTransaction(product, Millis(item, "purchasedAt"), Millis(item, "expiresAt"), trial, jws, revoked);
    }

    private static DateTimeOffset? Millis(JsonElement item, string name) =>
        item.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long ms) ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;

    private static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static List<string> Strings(JsonElement item, string name) =>
        item.TryGetProperty(name, out JsonElement list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!)]
            : [];
}
