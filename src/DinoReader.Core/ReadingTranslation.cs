using ReadTheStupidText.Application.Translation;

namespace DinoReader.Core;

/// <summary>
/// The child's-screen decisions about translation (Slice 173), worked out without the translation engine: Google ML Kit must not load until a grown-up asks
/// for a translation, so the reading screen decides from the book's tables and the app's own <see cref="TranslationPackRecord"/> only. Pure.
/// </summary>
public static class ReadingTranslation
{
    /// <summary>
    /// Whether the child can read <paramref name="story"/> in <paramref name="language"/> now with nothing to download: it is the book's own language, or (with the
    /// switch on and no "Show original text") the book's table for it covers every piece, or the engine covers the pair and the record holds every pack in
    /// <paramref name="packLanguages"/> (the pieces a table lacks are then translated by the engine).
    /// </summary>
    public static bool CanRead(Story story, string language, bool hasEngine, IReadOnlyCollection<string> engineLanguages, IEnumerable<string> packLanguages, TranslationPackRecord record)
    {
        string target = UiLanguage.Resolve(language);
        string source = UiLanguage.Resolve(story.Language);
        if (target == source)
        {
            return true;
        }

        if (story.ShowOriginalText || !story.TranslateToMyLanguage)
        {
            return false;
        }

        if (BundledTranslations.IsComplete(story, target) && BundledTranslations.Has(story, target))
        {
            return true;
        }

        return hasEngine && TranslationFlow.IsPairSupported(engineLanguages, source, target) && record.AllReady(packLanguages);
    }

    /// <summary>Whether a read of a book from <paramref name="source"/> into <paramref name="target"/> may use the engine for the pieces a table lacks: the engine exists and the record holds every pack of the pair.</summary>
    public static bool MayUseEngine(bool hasEngine, IEnumerable<string> packLanguages, TranslationPackRecord record) =>
        hasEngine && record.AllReady(packLanguages);
}
