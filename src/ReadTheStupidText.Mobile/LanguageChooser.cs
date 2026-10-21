using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Mobile.Resources.Strings;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The settings entry for changing language after first run (Slice 53, Decision 68): an action
/// sheet of the nine endonyms. A pick is stored, applied, and the shell is rebuilt in the new
/// language and direction (no app restart). Offering that language's voice pack is Slice 51.
/// </summary>
public static class LanguageChooser
{
    public static async Task ChooseAsync(ReaderPage host, ISettingsStore settings)
    {
        string[] names = LanguageChoices.All.Select(c => c.NativeName).ToArray();
        string? picked = await host.DisplayActionSheetAsync(
            AppResources.Common_Language, AppResources.Common_Cancel, null, UiFlowDirection.ForCurrentCulture(), names);
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

        Apply(language, settings, host);
    }

    private static void Apply(AppLanguage language, ISettingsStore settings, ReaderPage outgoing)
    {
        string code = AppLanguages.Code(language);
        settings.Language = code;
        UiCulture.Apply(UiCulture.Resolve(code, System.Globalization.CultureInfo.CurrentUICulture));
        outgoing.PrepareForRebuild(); // stops the read, keeps the editor text, and detaches the page that is about to be replaced
        ((App)Microsoft.Maui.Controls.Application.Current!).ShowMain();
    }
}
