namespace DinoReader.Core;

/// <summary>Which card the Store shows for the subscription (Slice 397, mock frames SB1-SB5).</summary>
public enum SubscriptionCardKind
{
    /// <summary>SB1: a new subscriber, plans with the free trial.</summary>
    Offer,

    /// <summary>SB2: the store says the account cannot have the trial again; plain prices.</summary>
    TrialUsed,

    /// <summary>SB3: subscribed and in the free trial.</summary>
    Trial,

    /// <summary>SB3: subscribed, paying.</summary>
    Active,

    /// <summary>SB4: the renewal payment failed and the store is still trying; the books stay open.</summary>
    Grace,

    /// <summary>SB5: the subscription ended; the plans again, with no trial.</summary>
    Ended,
}

/// <summary>The line under the card's title while subscribed (SB3).</summary>
public enum SubscriptionStateLine
{
    None,
    Trial,
    RenewsYearly,
    RenewsMonthly,
}

/// <summary>What the card shows: the app turns these choices into strings (Slice 397). <see cref="SaveTag"/> is the yearly plan's "Save N%" or null.</summary>
public sealed record SubscriptionCardPlan(
    SubscriptionCardKind Kind,
    bool Visible,
    bool ShowPlans,
    bool PlansHaveTrial,
    int? SaveTag,
    bool ShowTerms,
    bool TermsHaveTrial,
    bool ShowManage,
    bool ShowFixPayment,
    SubscriptionStateLine StateLine,
    DateTimeOffset? EndsAt,
    int LockedCount);

/// <summary>The pure rules of the subscription card (Slice 397): the state, the two store prices, whether the store allows the trial and how many books are locked choose the card.</summary>
public static class SubscriptionCard
{
    /// <summary>A saving under this many percent is not worth a tag (Decision 2).</summary>
    public const int MinimumSavePercent = 10;

    /// <summary>From this content width (dp) the plans move to a 300-wide column at the right (mock SB10; tablets and phone landscape).</summary>
    public const double WideFrom = 600;

    /// <summary>The width of the plans column of the wide layout (mock SB10).</summary>
    public const double PlansColumnWidth = 300;

    /// <summary>Whether a content width gets the wide card (words left, plans right).</summary>
    public static bool IsWide(double contentWidth) => contentWidth >= WideFrom;

    private const int MonthsInYear = 12;
    private const int Percent = 100;
    private static readonly TimeSpan RealEndAfter = TimeSpan.FromHours(24);

    /// <summary>
    /// Chooses the card. <paramref name="trialAvailable"/> is the store's answer about the free trial: null means the store cannot say (iOS until StoreKit 2 answers), which keeps "Try free". An ended subscription is one the record knows
    /// of (an end date or a product) or that locked <paramref name="lockedCount"/> books; a store with no subscription ever is the plain offer.
    /// </summary>
    public static SubscriptionCardPlan Choose(bool sold, SubscriptionRecord? record, bool grants, bool? trialAvailable, string? monthlyPrice, string? yearlyPrice, int lockedCount, DateTimeOffset now)
    {
        bool priced = HasPrice(monthlyPrice) || HasPrice(yearlyPrice);
        SubscriptionCardKind kind = KindOf(record, grants, trialAvailable, lockedCount);
        bool entitled = kind is SubscriptionCardKind.Trial or SubscriptionCardKind.Active or SubscriptionCardKind.Grace;
        bool trial = kind == SubscriptionCardKind.Offer;
        int? save = entitled ? null : SavePercent(yearlyPrice, monthlyPrice);
        DateTimeOffset? ends = entitled ? RealEnd(kind, record, now) : null;
        return new SubscriptionCardPlan(kind, sold && (entitled || priced), !entitled, trial, save, !entitled, trial, entitled, kind == SubscriptionCardKind.Grace,
            StateLineOf(kind, record, ends), ends, kind == SubscriptionCardKind.Ended ? lockedCount : 0);
    }

    private static SubscriptionCardKind KindOf(SubscriptionRecord? record, bool grants, bool? trialAvailable, int lockedCount)
    {
        if (grants && record is not null)
        {
            return record.State switch
            {
                SubscriptionState.Trial => SubscriptionCardKind.Trial,
                SubscriptionState.InGrace => SubscriptionCardKind.Grace,
                _ => SubscriptionCardKind.Active,
            };
        }

        if (record is not null && (record.EndsAt is not null || record.ProductId is not null || lockedCount > 0))
        {
            return SubscriptionCardKind.Ended;
        }

        return trialAvailable == false ? SubscriptionCardKind.TrialUsed : SubscriptionCardKind.Offer;
    }

    // The date is shown only when the store gave a real one: a device read with no end date holds "now" as the end (SubscriptionAccess), and that must never be shown as a renewal day.
    // Grace shows it too (its body names the day), and a Trial or Active card also drops a day already past (the offline grace keeps the books open after it).
    private static DateTimeOffset? RealEnd(SubscriptionCardKind kind, SubscriptionRecord? record, DateTimeOffset now)
    {
        if (record?.EndsAt is not { } ends || !(record.Confirmed || ends > record.CheckedAt + RealEndAfter))
        {
            return null;
        }

        return kind != SubscriptionCardKind.Grace && ends <= now ? null : ends;
    }

    private static SubscriptionStateLine StateLineOf(SubscriptionCardKind kind, SubscriptionRecord? record, DateTimeOffset? ends)
    {
        if (ends is null || record is null || kind is not (SubscriptionCardKind.Trial or SubscriptionCardKind.Active))
        {
            return SubscriptionStateLine.None;
        }

        return kind == SubscriptionCardKind.Trial ? SubscriptionStateLine.Trial : record.ProductId switch
        {
            SubscriptionProducts.Yearly => SubscriptionStateLine.RenewsYearly,
            SubscriptionProducts.Monthly => SubscriptionStateLine.RenewsMonthly,
            _ => SubscriptionStateLine.None,
        };
    }

    private static bool HasPrice(string? price) => !string.IsNullOrWhiteSpace(price);

    /// <summary>
    /// The yearly plan's saving against twelve monthly payments, in whole percent rounded down: 100 minus yearly / (12 x monthly) x 100, from the store's two price strings. Null when either price is missing or unreadable, or
    /// the saving is under <see cref="MinimumSavePercent"/>.
    /// </summary>
    public static int? SavePercent(string? yearlyPrice, string? monthlyPrice)
    {
        if (!PriceNumber.TryParse(yearlyPrice, out decimal yearly) || !PriceNumber.TryParse(monthlyPrice, out decimal monthly) || yearly <= 0 || monthly <= 0)
        {
            return null;
        }

        int saving = (int)Math.Floor(Percent - (yearly * Percent / (MonthsInYear * monthly)));
        return saving >= MinimumSavePercent ? saving : null;
    }
}

/// <summary>Reads the number out of a store's formatted price ("£14.99", "14,99 €", "¥1,500", "1.299,00 kr").</summary>
public static class PriceNumber
{
    private const int MaxDecimals = 2;

    public static bool TryParse(string? price, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(price))
        {
            return false;
        }

        string run = NumberRun(Latin(price));
        if (run.Length == 0)
        {
            return false;
        }

        return decimal.TryParse(Normalised(run), System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private const char ArabicDecimalSeparator = '٫';
    private const char ArabicThousandsSeparator = '٬';
    private const char ArabicComma = '،'; // the Arabic comma, which some stores print as the decimal mark

    // Arabic-Indic and Persian digits (any Unicode decimal digit) become 0-9, and the Arabic decimal and thousands separators become . and , so ar and fa stores' prices read like the others.
    private static string Latin(string price)
    {
        var text = new System.Text.StringBuilder(price.Length);
        foreach (char c in price)
        {
            text.Append(c switch
            {
                ArabicDecimalSeparator => '.',
                ArabicThousandsSeparator or ArabicComma => ',',
                _ when char.IsDigit(c) => (char)('0' + (int)char.GetNumericValue(c)),
                _ => c,
            });
        }

        return text.ToString();
    }

    // The first stretch of digits with separators (. , and the spaces some locales group with).
    private static string NumberRun(string price)
    {
        int start = price.IndexOfAny("0123456789".ToCharArray());
        if (start < 0)
        {
            return string.Empty;
        }

        int end = start;
        while (end < price.Length && (char.IsDigit(price[end]) || price[end] is '.' or ',' or '\u00A0' or '\u202F')) // a plain space ends the run: "14.99 12" is a price then a count, never one number
        {
            end++;
        }

        return price[start..end].TrimEnd('.', ',', '\u00A0', '\u202F');
    }

    // The last separator is the decimal one when 1 or 2 digits follow it; every other separator is grouping.
    private static string Normalised(string run)
    {
        string compact = run.Replace("\u00A0", string.Empty).Replace("\u202F", string.Empty);
        int last = compact.LastIndexOfAny(['.', ',']);
        if (last < 0)
        {
            return compact;
        }

        string whole = compact[..last].Replace(".", string.Empty).Replace(",", string.Empty);
        string tail = compact[(last + 1)..];
        return tail.Length is > 0 and <= MaxDecimals ? whole + "." + tail : whole + tail;
    }
}
