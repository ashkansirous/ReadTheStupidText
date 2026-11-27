using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Slice 455: where <see cref="BookPurchases"/> records a buy, restore or code outcome (the <c>purchase</c> usage count). <paramref name="BookId"/> and <paramref name="Language"/> are the store book's package id and language
/// for a book count and null for everything else. Never a price, an account, a transaction or a receipt.
/// </summary>
public sealed record PurchaseCount(string What, string Outcome, string? Offer = null, string? BookId = null, string? Language = null)
{
    public string Detail => PurchaseDetail.Of(What, Outcome, Offer);
}

/// <summary>Records <see cref="PurchaseCount"/>s, and remembers (on this device only) which offer-code redemptions it has counted.</summary>
public interface IPurchaseUsage
{
    /// <summary>Queues the count; true when it was queued (false when counting is off or the config has no copy yet).</summary>
    bool Record(PurchaseCount count);

    /// <summary>True once the redemptions already on this device (from before this build counted them) were noted; kept in local preferences, never sent.</summary>
    bool HasCodeBaseline { get; }

    /// <summary>Notes <paramref name="redemptionIds"/> as already there, without counting them, and sets the baseline.</summary>
    void SeedCodeBaseline(IEnumerable<string> redemptionIds);

    /// <summary>True when <paramref name="redemptionId"/> was counted or seeded; kept in local preferences, never sent.</summary>
    bool IsCodeRedemptionClaimed(string redemptionId);

    /// <summary>Notes <paramref name="redemptionId"/> as counted; call only after <see cref="Record"/> said it was queued.</summary>
    void ClaimCodeRedemption(string redemptionId);
}

/// <summary>The real recorder: every count goes through <see cref="UsageCounter"/> (production builds only, the grown-up's switch, the remote switch, as for every count).</summary>
public sealed class PurchaseUsage(UsageCounter usage) : IPurchaseUsage
{
    private const string LoggedCodesKey = "dino.purchase.codeRedemptionsLogged";
    private const string CodeBaselineKey = "dino.purchase.codeBaselineSet";
    private const char IdSeparator = ',';
    private readonly object _claimLock = new();

    public bool Record(PurchaseCount count)
    {
        if (count.BookId is { } bookId)
        {
            usage.RecordShelfBook(UsageEventName.Purchase, bookId, count.Language ?? UsageEvent.NoLanguage, count.Detail);
            return true; // a book count is never a code redemption; nothing waits on whether it queued
        }

        return usage.RecordGeneral(UsageEventName.Purchase, UsageEvent.NoLanguage, count.Detail);
    }

    public bool HasCodeBaseline => Preferences.Default.Get(CodeBaselineKey, false);

    public void SeedCodeBaseline(IEnumerable<string> redemptionIds)
    {
        lock (_claimLock)
        {
            foreach (string id in redemptionIds)
            {
                AddClaim(id);
            }

            Preferences.Default.Set(CodeBaselineKey, true);
        }
    }

    public bool IsCodeRedemptionClaimed(string redemptionId)
    {
        lock (_claimLock)
        {
            return PurchaseDetail.NotYetLogged(Claimed(), [redemptionId]).Count == 0;
        }
    }

    public void ClaimCodeRedemption(string redemptionId)
    {
        lock (_claimLock)
        {
            AddClaim(redemptionId);
        }
    }

    private static string[] Claimed() => Preferences.Default.Get(LoggedCodesKey, string.Empty).Split(IdSeparator, StringSplitOptions.RemoveEmptyEntries);

    private static void AddClaim(string redemptionId)
    {
        string[] logged = Claimed();
        if (PurchaseDetail.NotYetLogged(logged, [redemptionId]).Count > 0)
        {
            Preferences.Default.Set(LoggedCodesKey, string.Join(IdSeparator, logged.Append(redemptionId)));
        }
    }
}
