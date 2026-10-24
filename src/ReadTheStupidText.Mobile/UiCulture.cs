using System.Globalization;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Picks and applies the UI culture (Slice 53, Decision 68). Pure BCL logic, no MAUI types,
/// so the unit tests compile this file directly.
/// </summary>
public static class UiCulture
{
    private static readonly CultureInfo Fallback = CultureInfo.GetCultureInfo("en");
    private static readonly HashSet<string> RightToLeftCodes = ["fa", "ar"];

    /// <summary>
    /// The stored language if it is one of the fourteen; otherwise the device language if it is
    /// one of the fourteen (so the first-run page already speaks the user's language); otherwise English.
    /// </summary>
    public static CultureInfo Resolve(string? storedCode, CultureInfo deviceCulture)
    {
        if (AppLanguages.TryParse(storedCode, out AppLanguage stored))
        {
            return CultureInfo.GetCultureInfo(AppLanguages.Code(stored));
        }

        if (AppLanguages.TryParse(deviceCulture.TwoLetterISOLanguageName, out AppLanguage device))
        {
            return CultureInfo.GetCultureInfo(AppLanguages.Code(device));
        }

        return Fallback;
    }

    /// <summary>True for right-to-left languages (Farsi, Arabic). The explicit code list backs up a runtime without ICU data.</summary>
    public static bool IsRightToLeft(CultureInfo culture) =>
        culture.TextInfo.IsRightToLeft || RightToLeftCodes.Contains(culture.TwoLetterISOLanguageName);

    /// <summary>
    /// Makes <paramref name="culture"/> the UI culture of every thread. Only the process-wide defaults
    /// are set: <c>CurrentUICulture</c> is flowed per execution context, so assigning it inside an async
    /// continuation is undone when that continuation returns, and an explicit value would also shadow
    /// the defaults on later language changes. Never assign <c>CurrentUICulture</c> explicitly in the app.
    /// <c>CurrentCulture</c> is left alone so number and date formatting follow the device, not the app language.
    /// </summary>
    public static void Apply(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
