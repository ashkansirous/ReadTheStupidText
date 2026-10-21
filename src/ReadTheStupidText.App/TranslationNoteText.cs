using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText_App;

/// <summary>Composes the panel's one-line note about how the last translation went (Slice 57).</summary>
internal static class TranslationNoteText
{
    /// <summary>The standing note shown while nothing needs explaining.</summary>
    public static string Default => UiStrings.Get("Translate_Note_Default");

    public static string For(ReadTranslationResult result) => result.Outcome switch
    {
        TranslationOutcome.Translated => UiStrings.Format("Translate_Note_Translated", NameOf(result.Source)),
        TranslationOutcome.ModelMissing => UiStrings.Get("Translate_Note_ModelMissing"),
        TranslationOutcome.TimedOut => UiStrings.Get("Translate_Note_TimedOut"),
        TranslationOutcome.Failed => UiStrings.Get("Translate_Note_Failed"),
        TranslationOutcome.SourceUnknown => UiStrings.Get("Translate_Note_SourceUnknown"),
        TranslationOutcome.PairUnsupported => UiStrings.Format("Translate_Note_PairUnsupported", NameOf(result.Source)),
        _ => Default,
    };

    private static string NameOf(string? code) => code is null ? string.Empty : LanguageChoices.NativeName(code);
}
