using System.Globalization;
using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader.Tests;

/// <summary>Slice 399 (mock SB9): the line and the action of the Settings "Subscription" row, chosen from the same state as the Store's card.</summary>
public class SettingsSubscriptionTests
{
    private const string Monthly = "£1.99";
    private const string Yearly = "£14.99";
    private static readonly DateTimeOffset Now = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    private static SubscriptionRecord Record(SubscriptionState state, int endsInDays = 30, string? product = SubscriptionProducts.Yearly, bool confirmed = true) =>
        new(state, Now.AddDays(endsInDays), Now, product, confirmed, 1);

    private static SettingsSubscriptionRow RowOf(SubscriptionRecord? record, bool? trial = true, int locked = 0)
    {
        SubscriptionCardPlan plan = SubscriptionCard.Choose(true, record, SubscriptionAccess.Grants(record, Now), trial, Monthly, Yearly, locked, Now);
        return SettingsSubscription.Of(plan);
    }

    [Fact]
    public void NoSubscription_IsNotSubscribed_AndOpensTheStore()
    {
        SettingsSubscriptionRow row = RowOf(null);

        Assert.Equal(SettingsSubscriptionLine.NotSubscribed, row.Line);
        Assert.True(row.OpensStore);
        Assert.Null(row.Date);
    }

    [Fact]
    public void AnAccountThatUsedTheTrial_IsStillNotSubscribed()
    {
        SettingsSubscriptionRow row = RowOf(null, trial: false);

        Assert.Equal(SettingsSubscriptionLine.NotSubscribed, row.Line);
        Assert.True(row.OpensStore);
    }

    [Fact]
    public void AFreeTrial_NamesTheDayItEnds_AndOpensTheStoresPage()
    {
        SubscriptionRecord record = Record(SubscriptionState.Trial, endsInDays: 7, product: SubscriptionProducts.Monthly);

        SettingsSubscriptionRow row = RowOf(record);

        Assert.Equal(SettingsSubscriptionLine.Trial, row.Line);
        Assert.Equal(Now.AddDays(7), row.Date);
        Assert.False(row.OpensStore);
    }

    [Fact]
    public void APayingYearlyPlan_NamesTheRenewalDay()
    {
        SettingsSubscriptionRow row = RowOf(Record(SubscriptionState.Active, 200, SubscriptionProducts.Yearly));

        Assert.Equal(SettingsSubscriptionLine.Yearly, row.Line);
        Assert.Equal(Now.AddDays(200), row.Date);
        Assert.False(row.OpensStore);
    }

    [Fact]
    public void APayingMonthlyPlan_NamesTheRenewalDay()
    {
        SettingsSubscriptionRow row = RowOf(Record(SubscriptionState.Active, 20, SubscriptionProducts.Monthly));

        Assert.Equal(SettingsSubscriptionLine.Monthly, row.Line);
        Assert.Equal(Now.AddDays(20), row.Date);
    }

    [Fact]
    public void APayingPlanWithAnUnknownProduct_ShowsNoLine_ButStillManages()
    {
        SettingsSubscriptionRow row = RowOf(Record(SubscriptionState.Active, 20, "uk.sirous.other"));

        Assert.Equal(SettingsSubscriptionLine.None, row.Line);
        Assert.False(row.OpensStore);
    }

    [Fact]
    public void ADeviceReadWithNoRealEndDate_ShowsNoLine_NeverADeviceMadeDate()
    {
        SubscriptionRecord unconfirmed = new(SubscriptionState.Active, Now, Now, SubscriptionProducts.Yearly, false, 1);

        SettingsSubscriptionRow row = RowOf(unconfirmed);

        Assert.Equal(SettingsSubscriptionLine.None, row.Line);
        Assert.Null(row.Date);
    }

    [Fact]
    public void APaymentProblem_SaysSo_AndOpensTheStoresPageToFixIt()
    {
        SettingsSubscriptionRow row = RowOf(Record(SubscriptionState.InGrace, 3));

        Assert.Equal(SettingsSubscriptionLine.Grace, row.Line);
        Assert.False(row.OpensStore);
    }

    [Fact]
    public void TheLines_UseTheYearFormat_ForRenewals_AndNoYear_ForATrial()
    {
        using CultureScope scope = new("en-GB");
        DateTimeOffset day = new(2027, 11, 15, 12, 0, 0, TimeSpan.Zero);
        DateTime local = day.ToLocalTime().DateTime;

        Assert.Equal(AppResources.Format(AppResources.Settings_SubYearly, local.ToString("d MMM yyyy", scope.Culture)), SettingsBuyingText.SubscriptionLine(new(SettingsSubscriptionLine.Yearly, day, false)));
        Assert.Equal(AppResources.Format(AppResources.Settings_SubMonthly, local.ToString("d MMM yyyy", scope.Culture)), SettingsBuyingText.SubscriptionLine(new(SettingsSubscriptionLine.Monthly, day, false)));
        Assert.Equal(AppResources.Format(AppResources.Settings_SubTrial, local.ToString("d MMM", scope.Culture)), SettingsBuyingText.SubscriptionLine(new(SettingsSubscriptionLine.Trial, day, false)));
    }

    [Fact]
    public void ADatedLineWithNoDate_ShowsNothing_AndTheFixedLinesNeedNone()
    {
        Assert.Null(SettingsBuyingText.SubscriptionLine(new(SettingsSubscriptionLine.Yearly, null, false)));
        Assert.Null(SettingsBuyingText.SubscriptionLine(new(SettingsSubscriptionLine.Trial, null, false)));
        Assert.Null(SettingsBuyingText.SubscriptionLine(new(SettingsSubscriptionLine.None, null, false)));
        Assert.Equal(AppResources.Settings_SubNone, SettingsBuyingText.SubscriptionLine(new(SettingsSubscriptionLine.NotSubscribed, null, true)));
        Assert.Equal(AppResources.Settings_SubGrace, SettingsBuyingText.SubscriptionLine(new(SettingsSubscriptionLine.Grace, null, false)));
    }

    [Fact]
    public void TheStoreName_PicksGoogleAppleOrMicrosoftWording()
    {
        Assert.Equal(AppResources.Settings_RestoreLine_Google, SettingsBuyingText.RestoreLine(PurchaseProofs.AndroidStore));
        Assert.Equal(AppResources.Settings_RestoreLine_Apple, SettingsBuyingText.RestoreLine(PurchaseProofs.IosStore));
        Assert.Equal(AppResources.Settings_RestoreLine_Microsoft, SettingsBuyingText.RestoreLine(PurchaseProofs.WindowsStore));
        Assert.Equal(AppResources.Settings_RedeemLine_Google, SettingsBuyingText.RedeemLine(PurchaseProofs.AndroidStore));
        Assert.Equal(AppResources.Settings_RedeemLine_Apple, SettingsBuyingText.RedeemLine(PurchaseProofs.IosStore));
        Assert.Equal(AppResources.Settings_RedeemLine_Microsoft, SettingsBuyingText.RedeemLine(PurchaseProofs.WindowsStore));
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _before = CultureInfo.CurrentCulture;

        public CultureScope(string name)
        {
            Culture = CultureInfo.GetCultureInfo(name);
            CultureInfo.CurrentCulture = Culture;
        }

        public CultureInfo Culture { get; }

        public void Dispose() => CultureInfo.CurrentCulture = _before;
    }

    [Fact]
    public void AnEndedSubscription_IsNotSubscribed_AndOpensTheStore()
    {
        SettingsSubscriptionRow row = RowOf(Record(SubscriptionState.Expired, -3), locked: 2);

        Assert.Equal(SettingsSubscriptionLine.NotSubscribed, row.Line);
        Assert.True(row.OpensStore);
    }
}
