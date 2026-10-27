using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Mobile.Resources.Strings;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The Settings page's Language row (Slice 99) for changing language after first run (Slice 53, Decision 68): an action
/// sheet of the fifteen endonyms. A pick is stored, applied, and the shell is rebuilt in the new
/// language and direction (no app restart), landing on the main page. The voice pack download follows the language change
/// (VoicePackInstaller, Slice 81).
/// </summary>
public static class LanguageChooser
{
    public static async Task ChooseAsync(ContentPage host, ISettingsStore settings, Action beforeRebuild)
    {
        string[] names = LanguageChoices.All.Select(c => c.NativeName).ToArray();
        string? picked = await host.DisplayActionSheetAsync(
            AppResources.Common_Language, AppResources.Common_Cancel, null, FlowDirection.LeftToRight, names);
        int index = Array.IndexOf(names, picked);
        if (index < 0)
        {
            return; // cancelled
        }

        AppLanguage language = LanguageChoices.All[index].Language;
        if (AppLanguages.Code(language) == settings.Language)
        {
            return;
        }

        Apply(language, settings, beforeRebuild);
    }

    private static void Apply(AppLanguage language, ISettingsStore settings, Action beforeRebuild)
    {
        string code = AppLanguages.Code(language);
        settings.Language = code;
        UiCulture.Apply(UiCulture.Resolve(code, System.Globalization.CultureInfo.CurrentUICulture));
        beforeRebuild(); // stops the read, keeps the editor text, and detaches the page that is about to be replaced

        // Lands on the main page in the new language; returning to Settings is a known limitation (Slice 99).
        ((App)Microsoft.Maui.Controls.Application.Current!).ShowMain();
    }
}
