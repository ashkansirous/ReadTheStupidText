using System.Text.Json;

namespace DinoReader.Core;

/// <summary>What the platform store says about the family's subscription (Slice 384). <see cref="Unknown"/> is "no store answer held yet".</summary>
public enum SubscriptionState
{
    Unknown,
    Active,
    Trial,
    InGrace,
    Expired,
}

/// <summary>How much a device read can be trusted (Slice 384): <see cref="Store"/> is the store itself saying the account is entitled now (Android Play, Windows); <see cref="Estimated"/> is entitled worked out from a kept transaction (iOS StoreKit 1).</summary>
public enum DeviceReadKind
{
    Store,
    Estimated,
}

/// <summary>The store's current answer about the subscription: its state, when the paid (or trial, or grace) period ends and the product it is for. A store with no subscription for the account answers <see cref="SubscriptionState.Expired"/> with no end date. <paramref name="NewestTransactionAt"/> is the date of the newest kept transaction, given only by an <see cref="DeviceReadKind.Estimated"/> read.</summary>
public sealed record SubscriptionStatus(SubscriptionState State, DateTimeOffset? EndsAt, string? ProductId, DeviceReadKind Kind = DeviceReadKind.Store, DateTimeOffset? NewestTransactionAt = null)
{
    public static SubscriptionStatus None { get; } = new(SubscriptionState.Expired, null, null);
}

/// <summary>
/// The last store answer, kept on the device so the app can follow it with no internet: the status and when the store was last read. <paramref name="Confirmed"/> is true once the Worker (which asks Apple or Google for the real
/// end date) answered; until then the end date is the device's own estimate (iOS: the StoreKit 1 transaction date plus its period; Android: none, so the record counts from <paramref name="CheckedAt"/>).
/// </summary>
/// <param name="ReadNumber">The number of the device read that wrote this record (every device read that reached the store adds one); a Worker answer counts only while it carries this number.</param>
public sealed record SubscriptionRecord(SubscriptionState State, DateTimeOffset? EndsAt, DateTimeOffset CheckedAt, string? ProductId, bool Confirmed = false, long ReadNumber = 0);

/// <summary>An answer that can change the subscription record (Slice 384); <see cref="SubscriptionAccess.Merge"/> is the only way the record changes.</summary>
public abstract record SubscriptionAnswer
{
    /// <summary>A read of the device's own store. <paramref name="Reached"/> false: the read failed or the device is offline. A reached read with a null <paramref name="Status"/> means the store knows no subscription.</summary>
    public sealed record DeviceRead(SubscriptionStatus? Status, bool Reached = true) : SubscriptionAnswer;

    /// <summary>The Worker's answer to the check that started after device read number <paramref name="ReadNumber"/>. A null or unknown <paramref name="State"/> (unknown, error, 429, offline) is no answer.</summary>
    public sealed record Worker(long ReadNumber, string? State, DateTimeOffset? ExpiresAt) : SubscriptionAnswer;
}

/// <summary>
/// The pure rules for the subscription (Slice 384, Decisions 3 and 6). The record holds the store's last answer. Online the app follows the store at once (an expired or no
/// subscription locks the moment the store says so). With no internet, or a store read that failed, the record decides: the books lock <see cref="OfflineGrace"/> after the end date it holds.
/// </summary>
public static class SubscriptionAccess
{
    /// <summary>How long after the stored end date the subscription still counts when the store cannot be asked (Decision 6).</summary>
    public static readonly TimeSpan OfflineGrace = TimeSpan.FromDays(3);

    /// <summary>Whether a state is one the store counts as paid access: active, in a free trial, or in the billing grace period.</summary>
    public static bool IsEntitled(SubscriptionState state) => state is SubscriptionState.Active or SubscriptionState.Trial or SubscriptionState.InGrace;

    /// <summary>
    /// True while the subscription opens paid books: the held answer says entitled and its end date, plus the offline grace, is still ahead. No record (never subscribed, or the store was never read) is false.
    /// An end date the store did not give counts from the moment it was read.
    /// </summary>
    public static bool Grants(SubscriptionRecord? record, DateTimeOffset now) =>
        record is not null && IsEntitled(record.State) && now <= (record.EndsAt ?? record.CheckedAt) + OfflineGrace;

    /// <summary>The owned products as the books must see them: the subscription's products count only while <see cref="Grants"/> holds, whatever the last store read left (the offline rule can end access between reads).</summary>
    public static IReadOnlySet<string> OwnedWhile(IReadOnlySet<string> owned, bool grants) =>
        grants || !owned.Any(SubscriptionProducts.Is) ? owned : new HashSet<string>(owned.Where(p => !SubscriptionProducts.Is(p)));

    /// <summary>The purchase proofs to send, with the subscription's proof left out while <see cref="Grants"/> does not hold.</summary>
    public static IReadOnlyDictionary<string, string> ProofsWhile(IReadOnlyDictionary<string, string> proofs, bool grants) =>
        grants || !proofs.Keys.Any(SubscriptionProducts.Is) ? proofs : proofs.Where(p => !SubscriptionProducts.Is(p.Key)).ToDictionary(p => p.Key, p => p.Value);

    /// <summary>
    /// The only way the record changes (Slice 384). A device read that reached the store gets the next read number and replaces the record: entitled gives Active or Trial, unconfirmed, ending at the latest of now, the held end of an Active, Trial or InGrace record (an Expired record's end is not carried) and the device's own end estimate; an Estimated read (iOS) leaves a Worker-confirmed Expired
    /// record alone unless its newest transaction is after the record's CheckedAt, a Store read replaces it; not entitled (or no subscription listed) gives Expired. A failed or offline device read changes nothing. A Worker answer counts only when it carries the record's current read number
    /// (an older one is late and ignored): active, grace, expired and revoked give that state and end date, confirmed; an unknown state, an error, 429 or offline (state null) changes nothing.
    /// </summary>
    public static SubscriptionRecord? Merge(SubscriptionRecord? record, SubscriptionAnswer answer, DateTimeOffset now) => answer switch
    {
        SubscriptionAnswer.DeviceRead { Reached: false } => record,
        SubscriptionAnswer.DeviceRead read => FromDevice(record, read.Status ?? SubscriptionStatus.None, now),
        SubscriptionAnswer.Worker worker => FromWorker(record, worker, now),
        _ => record,
    };

    private static SubscriptionRecord? FromDevice(SubscriptionRecord? record, SubscriptionStatus status, DateTimeOffset now)
    {
        if (status is { Kind: DeviceReadKind.Estimated, State: SubscriptionState.Unknown })
        {
            return record; // iOS with no kept subscription transaction: no information (a reset preference, a Restore not yet delivered), the same as a failed read
        }

        bool entitled = IsEntitled(status.State);
        if (entitled && StaysExpired(record, status))
        {
            return record;
        }

        SubscriptionState state = entitled ? status.State : SubscriptionState.Expired;
        DateTimeOffset? ends = entitled ? LatestEnd(status.EndsAt, record, now) : status.EndsAt;
        return new SubscriptionRecord(state, ends, now, status.ProductId, Confirmed: false, (record?.ReadNumber ?? 0) + 1);
    }

    // An estimated entitled read (iOS: a kept transaction) does not undo a Worker-confirmed Expired or Revoked record unless its newest transaction is after that record's CheckedAt (a renewal or a new purchase). A Store read always does.
    private static bool StaysExpired(SubscriptionRecord? record, SubscriptionStatus status) =>
        status.Kind == DeviceReadKind.Estimated && record is { State: SubscriptionState.Expired, Confirmed: true } && !(status.NewestTransactionAt > record.CheckedAt);

    // An entitled device read ends at the latest of: now (so it always grants the 3 days of Decision 6 from the read), the held record's end (only from an Active, Trial or InGrace record, never from Expired) and the device's own estimate. It never shortens a known end.
    private static DateTimeOffset LatestEnd(DateTimeOffset? deviceEnd, SubscriptionRecord? record, DateTimeOffset now)
    {
        DateTimeOffset latest = now;
        DateTimeOffset? held = record is not null && IsEntitled(record.State) ? record.EndsAt : null;
        foreach (DateTimeOffset? candidate in new[] { held, deviceEnd })
        {
            if (candidate is { } c && c > latest)
            {
                latest = c;
            }
        }

        return latest;
    }

    /// <summary>The states the Worker's <c>/api/stories/subscription</c> answers with.</summary>
    public static class WorkerStates
    {
        public const string Active = "active";
        public const string Grace = "grace";
        public const string Expired = "expired";
        public const string Revoked = "revoked";
    }

    /// <summary>
    /// The subscription status a Worker answer means, or null for a state this app does not know (no answer, never expired). Active while the held record is a free trial that ends at the same moment stays a trial;
    /// expired and revoked are both <see cref="SubscriptionState.Expired"/>.
    /// </summary>
    public static SubscriptionStatus? StatusFromWorker(string? state, DateTimeOffset? expiresAt, SubscriptionRecord? held) => state switch
    {
        WorkerStates.Active => new SubscriptionStatus(held is { State: SubscriptionState.Trial, EndsAt: { } trialEnd } && expiresAt is { } end && Math.Abs((trialEnd - end).TotalSeconds) < 1 ? SubscriptionState.Trial : SubscriptionState.Active, expiresAt, held?.ProductId),
        WorkerStates.Grace => new SubscriptionStatus(SubscriptionState.InGrace, expiresAt, held?.ProductId),
        WorkerStates.Expired or WorkerStates.Revoked => new SubscriptionStatus(SubscriptionState.Expired, expiresAt, held?.ProductId),
        _ => null,
    };

    private static SubscriptionRecord? FromWorker(SubscriptionRecord? record, SubscriptionAnswer.Worker worker, DateTimeOffset now)
    {
        SubscriptionStatus? status = StatusFromWorker(worker.State, worker.ExpiresAt, record);
        if (record is null || status is null || worker.ReadNumber != record.ReadNumber)
        {
            return record; // no record, no usable answer, or a late answer to an older device read
        }

        // A Worker end date, earlier or later, is the truth and replaces the held one; an answer with no end date keeps the known end only while the held record is Active, Trial or InGrace (the LatestEnd rule), never an Expired record's stale end.
        DateTimeOffset? held = IsEntitled(record.State) ? record.EndsAt : null;
        return record with { State = status.State, EndsAt = status.EndsAt ?? held, CheckedAt = now, Confirmed = true };
    }

    /// <summary>
    /// The subscription product whose purchase proof the Worker is asked about (Slice 384): the one with the newest purchase or transaction time. A product with no known time ranks last; with no times at all the first listed wins. Null when none is a subscription product.
    /// </summary>
    public static string? NewestProofProduct(IEnumerable<string> products, IReadOnlyDictionary<string, DateTimeOffset>? purchasedAt)
    {
        string? best = null;
        DateTimeOffset bestAt = DateTimeOffset.MinValue;
        foreach (string product in products.Where(SubscriptionProducts.Is))
        {
            DateTimeOffset at = purchasedAt is not null && purchasedAt.TryGetValue(product, out DateTimeOffset known) ? known : DateTimeOffset.MinValue;
            if (best is null || at > bestAt)
            {
                best = product;
                bestAt = at;
            }
        }

        return best;
    }

    /// <summary>The persisted form of a record: one JSON object.</summary>
    public static string ToJson(SubscriptionRecord record) =>
        JsonSerializer.Serialize(new Dto(record.State.ToString(), record.EndsAt?.ToUnixTimeSeconds(), record.CheckedAt.ToUnixTimeSeconds(), record.ProductId, record.Confirmed, record.ReadNumber));

    /// <summary>Reads a persisted record; anything unreadable is no record (never a guess that would lock or open books).</summary>
    public static SubscriptionRecord? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            Dto? dto = JsonSerializer.Deserialize<Dto>(json);
            if (dto is null || !Enum.TryParse(dto.State, out SubscriptionState state) || !Enum.IsDefined(state))
            {
                return null;
            }

            DateTimeOffset? ends = dto.EndsAt is { } e ? DateTimeOffset.FromUnixTimeSeconds(e) : null;
            return new SubscriptionRecord(state, ends, DateTimeOffset.FromUnixTimeSeconds(dto.CheckedAt), dto.ProductId, dto.Confirmed, dto.ReadNumber);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentOutOfRangeException)
        {
            // no-fallback: a record that cannot be read is no record; the next store read writes a new one
            return null;
        }
    }

    private sealed record Dto(string State, long? EndsAt, long CheckedAt, string? ProductId, bool Confirmed = false, long ReadNumber = 0);
}

/// <summary>The links the subscription card shows beside its terms (Slice 384): Apple and Google require the terms of use and the privacy policy next to an auto-renewing subscription's price.</summary>
public static class SubscriptionLinks
{
    public const string Privacy = "https://dinoreader.sirous.uk/privacy";

    private const string AppleTerms = "https://www.apple.com/legal/internet-services/itunes/dev/stdeula/";
    private const string GoogleTerms = "https://play.google.com/about/play-terms/";
    private const string MicrosoftTerms = "https://www.microsoft.com/servicesagreement";

    /// <summary>The store's own terms of use for <paramref name="store"/> (a <see cref="PurchaseProofs"/> store name).</summary>
    public static string TermsFor(string store) => store switch
    {
        PurchaseProofs.IosStore => AppleTerms,
        PurchaseProofs.WindowsStore => MicrosoftTerms,
        _ => GoogleTerms,
    };
}

/// <summary>The persisted "what opened each book" record (Slice 384): book id to <see cref="BookUnlock"/>, one JSON object, written once at download and only ever upgraded.</summary>
public static class BookUnlockRecords
{
    public static string ToJson(IReadOnlyDictionary<string, BookUnlock> records) =>
        JsonSerializer.Serialize(records.ToDictionary(r => r.Key, r => r.Value.ToString()));

    /// <summary>Reads a persisted record; an unreadable one, or an entry with an unknown value, is dropped (a book with no entry is never locked).</summary>
    public static Dictionary<string, BookUnlock> FromJson(string? json)
    {
        var result = new Dictionary<string, BookUnlock>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            foreach ((string id, string value) in JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [])
            {
                if (Enum.TryParse(value, out BookUnlock unlock) && Enum.IsDefined(unlock))
                {
                    result[id] = unlock;
                }
            }
        }
        catch (JsonException)
        {
            // no-fallback: an unreadable record means no book is locked until the next download writes one
            result.Clear();
        }

        return result;
    }

    /// <summary>The record after a book was downloaded (again): the first record stands, except that a subscription record becomes bought when it was bought since; nothing is ever downgraded.</summary>
    public static BookUnlock Merge(BookUnlock? held, BookUnlock now) =>
        held is null ? now : BookAccess.UpgradeUnlock(held, now == BookUnlock.Bought)!.Value;
}
