using DinoReader.Core;

namespace DinoReader.Tests;

public class SubscriptionCardTests
{
    private const string Monthly = "£1.99";
    private const string Yearly = "£14.99";
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static SubscriptionRecord Record(SubscriptionState state, int endsInDays = 30, string? product = SubscriptionProducts.Yearly, bool confirmed = true) =>
        new(state, Now.AddDays(endsInDays), Now, product, confirmed, 1);

    private static SubscriptionCardPlan Choose(SubscriptionRecord? record, bool? trial = true, string? monthly = Monthly, string? yearly = Yearly, int locked = 0, bool sold = true) =>
        SubscriptionCard.Choose(sold, record, SubscriptionAccess.Grants(record, Now), trial, monthly, yearly, locked, Now);

    [Fact]
    public void ANewSubscriber_SeesTheOfferWithTheTrialAndTheTag()
    {
        SubscriptionCardPlan plan = Choose(null);

        Assert.Equal(SubscriptionCardKind.Offer, plan.Kind);
        Assert.True(plan.Visible && plan.ShowPlans && plan.PlansHaveTrial && plan.TermsHaveTrial && plan.ShowTerms);
        Assert.Equal(37, plan.SaveTag);
        Assert.False(plan.ShowManage || plan.ShowFixPayment);
    }

    [Fact]
    public void AStoreThatCannotSayAboutTheTrial_KeepsTryFree()
    {
        Assert.Equal(SubscriptionCardKind.Offer, Choose(null, trial: null).Kind);
    }

    [Fact]
    public void AnAccountThatUsedTheTrial_SeesPlainPricesAndTermsWithoutTheTrial()
    {
        SubscriptionCardPlan plan = Choose(null, trial: false);

        Assert.Equal(SubscriptionCardKind.TrialUsed, plan.Kind);
        Assert.False(plan.PlansHaveTrial || plan.TermsHaveTrial);
        Assert.True(plan.ShowPlans && plan.ShowTerms);
        Assert.Equal(37, plan.SaveTag);
    }

    [Fact]
    public void ARecordOfNoSubscriptionEver_IsTheOffer_NotTheEndedCard()
    {
        SubscriptionRecord none = new(SubscriptionState.Expired, null, Now, null, false, 1);

        Assert.Equal(SubscriptionCardKind.Offer, Choose(none).Kind);
    }

    [Fact]
    public void InTheTrial_TheCardSaysSubscribedWithTheTrialLineAndManage()
    {
        SubscriptionCardPlan plan = Choose(Record(SubscriptionState.Trial, endsInDays: 7));

        Assert.Equal(SubscriptionCardKind.Trial, plan.Kind);
        Assert.Equal(SubscriptionStateLine.Trial, plan.StateLine);
        Assert.False(plan.ShowPlans || plan.ShowTerms || plan.ShowFixPayment || plan.SaveTag is not null);
        Assert.True(plan.ShowManage);
        Assert.Equal(Now.AddDays(7), plan.EndsAt);
    }

    [Theory]
    [InlineData(SubscriptionProducts.Yearly, SubscriptionStateLine.RenewsYearly)]
    [InlineData(SubscriptionProducts.Monthly, SubscriptionStateLine.RenewsMonthly)]
    [InlineData(SubscriptionProducts.Subscription, SubscriptionStateLine.None)]
    public void WhilePaying_TheStateLineFollowsThePlan(string product, SubscriptionStateLine expected)
    {
        SubscriptionCardPlan plan = Choose(Record(SubscriptionState.Active, product: product));

        Assert.Equal(SubscriptionCardKind.Active, plan.Kind);
        Assert.Equal(expected, plan.StateLine);
    }

    [Fact]
    public void AnEndDateThatIsOnlyTheReadTime_IsNeverShownAsARenewalDay()
    {
        SubscriptionRecord estimated = new(SubscriptionState.Active, Now.AddHours(2), Now, SubscriptionProducts.Yearly, Confirmed: false, 1);

        Assert.Equal(SubscriptionStateLine.None, Choose(estimated).StateLine);
    }

    [Fact]
    public void InGrace_TheBooksStayOpenAndFixPaymentShows()
    {
        SubscriptionCardPlan plan = Choose(Record(SubscriptionState.InGrace, endsInDays: 3));

        Assert.Equal(SubscriptionCardKind.Grace, plan.Kind);
        Assert.True(plan.ShowFixPayment && plan.ShowManage);
        Assert.False(plan.ShowPlans);
        Assert.Equal(Now.AddDays(3), plan.EndsAt);
    }

    [Fact]
    public void InGrace_WithADeviceEstimatedEnd_ShowsNoDate()
    {
        SubscriptionCardPlan plan = Choose(Record(SubscriptionState.InGrace, endsInDays: 0, confirmed: false));

        Assert.Equal(SubscriptionCardKind.Grace, plan.Kind);
        Assert.Null(plan.EndsAt);
    }

    [Fact]
    public void AnActiveCard_WhoseStoredEndIsPast_ShowsNoRenewalDate()
    {
        SubscriptionCardPlan plan = Choose(Record(SubscriptionState.Active, endsInDays: -2));

        Assert.Equal(SubscriptionCardKind.Active, plan.Kind);
        Assert.Null(plan.EndsAt);
        Assert.Equal(SubscriptionStateLine.None, plan.StateLine);
    }

    [Fact]
    public void AnEndedSubscription_ShowsThePlansWithNoTrialAndTheLockedCount()
    {
        SubscriptionRecord ended = Record(SubscriptionState.Expired, endsInDays: -1);

        SubscriptionCardPlan plan = Choose(ended, trial: true, locked: 3);

        Assert.Equal(SubscriptionCardKind.Ended, plan.Kind);
        Assert.False(plan.PlansHaveTrial || plan.TermsHaveTrial);
        Assert.True(plan.ShowPlans && plan.ShowTerms);
        Assert.Equal(3, plan.LockedCount);
        Assert.Equal(37, plan.SaveTag);
    }

    [Fact]
    public void AnExpiredRecordThatLostItsDetails_IsEndedOnlyWhenBooksAreLocked()
    {
        SubscriptionRecord bare = new(SubscriptionState.Expired, null, Now, null, false, 1);

        Assert.Equal(SubscriptionCardKind.Ended, Choose(bare, locked: 2).Kind);
        Assert.Equal(SubscriptionCardKind.Offer, Choose(bare, locked: 0).Kind);
    }

    [Fact]
    public void AnEntitledRecordPastTheOfflineGrace_IsEnded()
    {
        SubscriptionRecord stale = new(SubscriptionState.Active, Now.AddDays(-10), Now.AddDays(-10), SubscriptionProducts.Monthly, true, 1);

        Assert.Equal(SubscriptionCardKind.Ended, Choose(stale).Kind);
    }

    [Fact]
    public void WithoutAStorePriceOrWithTheSwitchOff_TheOfferIsHidden_ButARunningSubscriptionStays()
    {
        Assert.False(Choose(null, monthly: null, yearly: null).Visible);
        Assert.False(Choose(null, sold: false).Visible);
        Assert.True(Choose(Record(SubscriptionState.Active), monthly: null, yearly: null).Visible);
        Assert.True(Choose(null, monthly: null).Visible);
    }

    [Theory]
    [InlineData("£14.99", "£1.99", 37)]
    [InlineData("£12.00", "£1.00", 0)]
    [InlineData("10.80", "1.00", 10)]
    [InlineData("10.90", "1.00", 9)]
    [InlineData("$19.99", "$2.49", 33)]
    [InlineData("14,99 €", "1,99 €", 37)]
    [InlineData("¥1,500", "¥200", 37)]
    public void TheSaveTag_IsTheWholePercentBelowTwelveMonthlyPayments(string yearly, string monthly, int expected)
    {
        int? tag = SubscriptionCard.SavePercent(yearly, monthly);

        Assert.Equal(expected >= SubscriptionCard.MinimumSavePercent ? expected : null, tag);
    }

    [Theory]
    [InlineData(null, "£1.99")]
    [InlineData("£14.99", null)]
    [InlineData("", "£1.99")]
    [InlineData("free", "£1.99")]
    [InlineData("£0.00", "£1.99")]
    [InlineData("£14.99", "£0.00")]
    public void TheSaveTag_IsHiddenWhenAPriceIsMissingOrUnreadable(string? yearly, string? monthly)
    {
        Assert.Null(SubscriptionCard.SavePercent(yearly, monthly));
    }

    [Fact]
    public void TheSaveTag_IsHiddenWhenTheYearlyPlanIsNoCheaper()
    {
        Assert.Null(SubscriptionCard.SavePercent("£23.88", "£1.99"));
        Assert.Null(SubscriptionCard.SavePercent("£30.00", "£1.99"));
    }

    [Theory]
    [InlineData("£14.99", "14.99")]
    [InlineData("14,99 €", "14.99")]
    [InlineData("1.299,00 kr", "1299.00")]
    [InlineData("¥1,500", "1500")]
    [InlineData("US$ 9.99", "9.99")]
    [InlineData("١٤٫٩٩ ر.س", "14.99")] // Arabic-Indic digits, Arabic decimal separator (ar)
    [InlineData("١٬٢٩٩٫٥٠ ر.س", "1299.50")] // with the Arabic thousands separator
    [InlineData("۱۴٫۹۹ ریال", "14.99")] // Persian digits (fa)
    [InlineData("‏۱۴,۹۹ دلار", "14.99")] // Persian digits with an ASCII comma, after a right-to-left mark
    [InlineData("١٤،٩٩ ر.س", "14.99")] // Arabic comma as the decimal mark
    [InlineData("14.99 12 mnd", "14.99")] // a plain space ends the number: the 12 is a count, never digits of the price
    [InlineData("EUR 14,99 / 12 mnd", "14.99")]
    [InlineData("1 299,50 zł", "1299.50")] // a no-break space groups
    [InlineData("1 299,50 zł", "1299.50")] // a narrow no-break space groups
    public void PriceNumber_ReadsTheStoresOwnFormats(string text, string expected)
    {
        Assert.True(PriceNumber.TryParse(text, out decimal value));
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), value);
    }

    [Fact]
    public void SavePercent_ReadsArabicIndicAndPersianPrices()
    {
        Assert.Equal(SubscriptionCard.SavePercent("14.99", "1.99"), SubscriptionCard.SavePercent("١٤٫٩٩", "١٫٩٩"));
        Assert.Equal(SubscriptionCard.SavePercent("14.99", "1.99"), SubscriptionCard.SavePercent("۱۴٫۹۹", "۱٫۹۹"));
        Assert.NotNull(SubscriptionCard.SavePercent("۱۴٫۹۹", "۱٫۹۹"));
    }

    [Theory]
    [InlineData(599, false)]
    [InlineData(600, true)]
    [InlineData(1100, true)]
    public void TheCardSplitsFrom600dp(double width, bool wide)
    {
        Assert.Equal(wide, SubscriptionCard.IsWide(width));
    }
}
