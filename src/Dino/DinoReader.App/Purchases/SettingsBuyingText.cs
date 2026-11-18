using System.Globalization;
using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>The strings of the Settings "Books and buying" rows (Slice 399, mock SB9): <see cref="SettingsSubscription"/> chooses, this turns the choice into the app's text. Dates are the device's own; the store's name picks Google, Apple or Microsoft wording.</summary>
public static class SettingsBuyingText
{
    private const string DayMonth = "d MMM";
    private const string DayMonthYear = "d MMM yyyy";

    /// <summary>The line under "Subscription"; null when there is nothing to say (subscribed, but the store gave no usable date or plan).</summary>
    public static string? SubscriptionLine(SettingsSubscriptionRow row) => row.Line switch
    {
        SettingsSubscriptionLine.Yearly when row.Date is { } day => AppResources.Format(AppResources.Settings_SubYearly, Date(day, DayMonthYear)),
        SettingsSubscriptionLine.Monthly when row.Date is { } day => AppResources.Format(AppResources.Settings_SubMonthly, Date(day, DayMonthYear)),
        SettingsSubscriptionLine.Trial when row.Date is { } day => AppResources.Format(AppResources.Settings_SubTrial, Date(day, DayMonth)),
        SettingsSubscriptionLine.NotSubscribed => AppResources.Settings_SubNone,
        SettingsSubscriptionLine.Grace => AppResources.Settings_SubGrace,
        _ => null,
    };

    /// <summary>The line under "Restore purchases": which store account the purchases were made with.</summary>
    public static string RestoreLine(string store) => store switch
    {
        PurchaseProofs.IosStore => AppResources.Settings_RestoreLine_Apple,
        PurchaseProofs.WindowsStore => AppResources.Settings_RestoreLine_Microsoft,
        _ => AppResources.Settings_RestoreLine_Google,
    };

    /// <summary>The line under "Redeem a code": which store the code comes from.</summary>
    public static string RedeemLine(string store) => store switch
    {
        PurchaseProofs.IosStore => AppResources.Settings_RedeemLine_Apple,
        PurchaseProofs.WindowsStore => AppResources.Settings_RedeemLine_Microsoft,
        _ => AppResources.Settings_RedeemLine_Google,
    };

    private static string Date(DateTimeOffset date, string format) => date.ToLocalTime().ToString(format, CultureInfo.CurrentCulture);
}
