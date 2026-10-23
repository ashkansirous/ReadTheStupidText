using ReadTheStupidText.Application.Translation;

namespace DinoReader.Core;

/// <summary>
/// Looks up a story's bundled translations (Slice 95): <see cref="Story.Translations"/> maps a language code to a table of
/// source piece text to translated text. Language codes are resolved the way the screens do (<see cref="UiLanguage.Resolve"/>).
/// </summary>
public static class BundledTranslations
{
    /// <summary>The table for <paramref name="language"/>, or null when the story carries none for it.</summary>
    public static IReadOnlyDictionary<string, string>? TableFor(Story story, string language)
    {
        string wanted = Key(language);
        var table = story.Translations?.FirstOrDefault(t => Key(t.Key) == wanted && t.Value.Count > 0).Value;
        return table;
    }

    /// <summary>The key a table is filed under: the primary code of a supported language, case-insensitive ("fa-IR" and "FA" are "fa"); any other code is kept lower-cased.</summary>
    public static string Key(string? language) =>
        UiLanguage.IsSupported(language) ? UiLanguage.Resolve(language) : (language ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Whether some piece that gets translated (one with a letter) has no entry in the table for <paramref name="language"/>.</summary>
    public static bool MissesAny(Story story, string language, IEnumerable<string> pieceTexts) =>
        pieceTexts.Any(t => t.Any(char.IsLetter) && Lookup(story, language, t) is null);

    /// <summary>Whether the table for <paramref name="language"/> covers every piece the story is read in (the whole-story form of <see cref="MissesAny"/>).</summary>
    public static bool IsComplete(Story story, string language) =>
        !MissesAny(story, language, StoryPieces.TranslatablePieceTexts(story));

    /// <summary>The stored copy of a bundled story brought up to date (an app update, Slice 95): a copy that carries no translations takes the
    /// bundled ones and the switch on; everything the grown-up changed (cast, merges, cover, pages) stays. A copy that has tables is left alone.</summary>
    public static Story Adopt(Story stored, Story bundled) =>
        stored.Translations is { Count: > 0 } || bundled.Translations is not { Count: > 0 }
            ? stored
            : stored with { Translations = bundled.Translations, TranslateToMyLanguage = true };

    /// <summary>Whether the story carries a table for <paramref name="language"/>.</summary>
    public static bool Has(Story story, string language) => TableFor(story, language) is not null;

    /// <summary>The translation of <paramref name="pieceText"/> into <paramref name="language"/>, or null when the story has none.</summary>
    public static string? Lookup(Story story, string language, string pieceText) =>
        TableFor(story, language) is { } table && table.TryGetValue(pieceText, out string? translated) ? translated : null;

    /// <summary>Whether reading <paramref name="story"/> in <paramref name="myLanguage"/> goes through translation: the story's switch is on,
    /// its language differs, and it has a bundled table for <paramref name="myLanguage"/> or the engine (its languages, null for no engine) covers the pair.</summary>
    public static bool Applies(Story story, string myLanguage, IReadOnlyCollection<string>? engineLanguages)
    {
        string source = UiLanguage.Resolve(story.Language);
        return TranslationFlow.ShouldTranslate(story.TranslateToMyLanguage, source, myLanguage)
            && (Has(story, myLanguage) || (engineLanguages is not null && TranslationFlow.IsPairSupported(engineLanguages, source, myLanguage)));
    }

    /// <summary>The per-piece translator: the bundled table first, then <paramref name="engine"/> (null for none), else the source text.</summary>
    public static PieceTranslator Compose(Story story, string myLanguage, PieceTranslator? engine) =>
        async (text, cancellationToken) =>
            Lookup(story, myLanguage, text) ?? (engine is null ? text : await engine(text, cancellationToken));
}
