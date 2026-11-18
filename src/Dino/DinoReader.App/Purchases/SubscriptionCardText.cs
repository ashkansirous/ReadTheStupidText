using System.Globalization;
using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>The strings of the Store's subscription card (Slice 397): <see cref="SubscriptionCard"/> chooses, this turns the choice into the app's text. Dates are the device's own (Decision 3); prices are the store's own strings.</summary>
public static class SubscriptionCardText
{
    private const string DayMonth = "d MMM";
    private const string DayMonthYear = "d MMM yyyy";

    public static string Title(SubscriptionCardPlan plan) => plan.Kind switch
    {
        SubscriptionCardKind.Trial or SubscriptionCardKind.Active => AppResources.Sub_Active,
        SubscriptionCardKind.Grace => AppResources.Sub_GraceTitle,
        SubscriptionCardKind.Ended => AppResources.Sub_EndedTitle,
        _ => AppResources.Sub_Title,
    };

    /// <summary>The line under the title: the offer's line, the state line while subscribed (empty when the store gave no usable date or plan), the grace and ended bodies.</summary>
    public static string Line(SubscriptionCardPlan plan, string store, string? monthlyPrice, string? yearlyPrice, string? planProduct) => plan.Kind switch
    {
        SubscriptionCardKind.Trial or SubscriptionCardKind.Active => StateLine(plan, planProduct, PriceOf(planProduct, monthlyPrice, yearlyPrice)),
        SubscriptionCardKind.Grace => GraceBody(store, plan.EndsAt),
        SubscriptionCardKind.Ended => plan.LockedCount > 0 ? EndedBody(plan.LockedCount) : AppResources.Sub_Line,
        _ => AppResources.Sub_Line,
    };

    public static string Yearly(SubscriptionCardPlan plan, string price) =>
        AppResources.Format(plan.PlansHaveTrial ? AppResources.Sub_Yearly : AppResources.Sub_YearlyPlain, price);

    public static string Monthly(SubscriptionCardPlan plan, string price) =>
        AppResources.Format(plan.PlansHaveTrial ? AppResources.Sub_Monthly : AppResources.Sub_MonthlyPlain, price);

    public static string Terms(SubscriptionCardPlan plan) => plan.TermsHaveTrial ? AppResources.Sub_Terms : AppResources.Sub_TermsNoTrial;

    public static string Save(int percent) => AppResources.Format(AppResources.Sub_Save, percent);

    private static string StateLine(SubscriptionCardPlan plan, string? planProduct, string? recurringPrice)
    {
        if (plan.EndsAt is not { } ends)
        {
            return string.Empty;
        }

        return plan.StateLine switch
        {
            SubscriptionStateLine.Trial when recurringPrice is not null => AppResources.Format(TrialTemplate(planProduct), Date(ends, DayMonth), recurringPrice),
            SubscriptionStateLine.RenewsYearly => AppResources.Format(AppResources.Sub_StateRenewsYearly, Date(ends, DayMonthYear)),
            SubscriptionStateLine.RenewsMonthly => AppResources.Format(AppResources.Sub_StateRenewsMonthly, Date(ends, DayMonthYear)),
            _ => string.Empty,
        };
    }

    // The trial's "Then {1} a year": the store's price for the plan the subscription is on; null (the line is hidden, the mock's copy has no price-less form) when the plan or its price is unknown.
    private static string? PriceOf(string? planProduct, string? monthlyPrice, string? yearlyPrice)
    {
        string? price = planProduct switch
        {
            SubscriptionProducts.Yearly => yearlyPrice,
            SubscriptionProducts.Monthly => monthlyPrice,
            _ => null,
        };
        return BookAccess.HasPrice(price) ? price : null;
    }

    private static string TrialTemplate(string? planProduct) =>
        planProduct == SubscriptionProducts.Yearly ? AppResources.Sub_StateTrialYearly : AppResources.Sub_StateTrialMonthly;

    private static string GraceBody(string store, DateTimeOffset? ends)
    {
        if (ends is not { } end)
        {
            return string.Empty; // no real end date from the store: never "until ." and never a device-estimated day
        }

        string template = store switch
        {
            PurchaseProofs.IosStore => AppResources.Sub_GraceBody_Apple,
            PurchaseProofs.WindowsStore => AppResources.Sub_GraceBody_Microsoft,
            _ => AppResources.Sub_GraceBody_Google,
        };
        return AppResources.Format(template, Date(end, DayMonth));
    }

    private static string EndedBody(int locked) =>
        locked == 1 ? AppResources.Sub_EndedBody_One : AppResources.Format(AppResources.Sub_EndedBody_Other, locked);

    private static string Date(DateTimeOffset date, string format) => date.ToLocalTime().ToString(format, CultureInfo.CurrentCulture);
}
