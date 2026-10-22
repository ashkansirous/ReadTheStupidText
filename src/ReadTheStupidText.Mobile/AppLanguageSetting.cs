using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Mobile;

/// <summary>The language the app is in: the stored one, or what the UI resolved to when none is stored.</summary>
public static class AppLanguageSetting
{
    public static AppLanguage Current(ISettingsStore settings)
    {
        string code = UiCulture.Resolve(settings.Language, System.Globalization.CultureInfo.CurrentUICulture).TwoLetterISOLanguageName;
        AppLanguages.TryParse(code, out AppLanguage language);
        return language;
    }
}
