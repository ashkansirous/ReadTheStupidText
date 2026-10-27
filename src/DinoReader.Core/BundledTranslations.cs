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

    /// <summary>The stored copy of a bundled story brought up to date (an app update, Slice 95, 105): a copy that carries no translations takes the
    /// bundled ones and the switch on; a copy that has some gains only the languages it lacks (its own tables and switch stay);
    /// everything the grown-up changed (cast, merges, cover, pages) stays. A copy that already has every bundled language is returned as it is.</summary>
    public static Story Adopt(Story stored, Story bundled)
    {
        if (bundled.Translations is not { Count: > 0 })
        {
            return stored;
        }

        if (stored.Translations is not { Count: > 0 })
        {
            return stored with { Translations = bundled.Translations, TranslateToMyLanguage = true };
        }

        var have = stored.Translations.Keys.Select(Key).ToHashSet();
        var merged = new Dictionary<string, IReadOnlyDictionary<string, string>>(stored.Translations);
        foreach (var table in bundled.Translations.Where(t => !IsSpeakKey(t.Key) && !have.Contains(Key(t.Key))))
        {
            merged[table.Key] = table.Value;
        }

        bool changed = merged.Count != stored.Translations.Count;
        foreach (var table in bundled.Translations.Where(t => IsSpeakKey(t.Key)))
        {
            changed |= MergeSpoken(merged, bundled, table);
        }

        return changed ? stored with { Translations = merged } : stored;
    }

    // Merges one bundled "how to read" table into <paramref name="merged"/> per piece (Slice 154): a piece takes the bundled spoken text where the shown translation in the copy
    // (its own or one just gained) is still the bundled one; a copy whose text or translation the grown-up changed is not read with marks made for the old text.
    // Pieces the copy's speak table already has are refreshed the same way; the rest of its table stays. True when anything changed.
    private static bool MergeSpoken(Dictionary<string, IReadOnlyDictionary<string, string>> merged, Story bundled, KeyValuePair<string, IReadOnlyDictionary<string, string>> table)
    {
        string language = table.Key[SpeakPrefix.Length..];
        string wanted = Key(table.Key);
        IReadOnlyDictionary<string, string>? shown = merged.FirstOrDefault(t => Key(t.Key) == Key(language) && t.Value.Count > 0).Value;
        IReadOnlyDictionary<string, string>? bundledShown = TableFor(bundled, language);
        if (shown is null || bundledShown is null)
        {
            return false;
        }

        var own = merged.FirstOrDefault(t => Key(t.Key) == wanted);
        var result = own.Value is null ? new Dictionary<string, string>() : new Dictionary<string, string>(own.Value);
        bool changed = false;
        foreach (var piece in table.Value.Where(p => shown.TryGetValue(p.Key, out string? seen) && seen == bundledShown.GetValueOrDefault(p.Key)))
        {
            changed |= !result.TryGetValue(piece.Key, out string? current) || current != piece.Value;
            result[piece.Key] = piece.Value;
        }

        if (changed)
        {
            merged[own.Key ?? table.Key] = result;
        }

        return changed;
    }

    private static bool IsSpeakKey(string key) => key.StartsWith(SpeakPrefix, StringComparison.Ordinal);

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

    /// <summary>Whether the switch can do anything for <paramref name="story"/> in <paramref name="myLanguage"/> (ignoring the switch itself): the story is
    /// already in that language, it carries a table for it that covers every piece (an engine fills any holes; with none a table with holes is not used), or the engine
    /// (its languages, null for no engine) covers the pair.</summary>
    public static bool CanTranslate(Story story, string myLanguage, IReadOnlyCollection<string>? engineLanguages)
    {
        string source = UiLanguage.Resolve(story.Language);
        return source == myLanguage
            || (Has(story, myLanguage) && (engineLanguages is not null || IsComplete(story, myLanguage)))
            || (engineLanguages is not null && TranslationFlow.IsPairSupported(engineLanguages, source, myLanguage));
    }

    /// <summary>The table key of the "how to read" texts for <paramref name="language"/> (Slice 154): same piece keys as the language's own table, values spoken in place
    /// of the displayed translation (a Farsi sentence with its vowel marks). Not a language code, so it never resolves to a language.</summary>
    public static string SpeakKey(string language) => SpeakPrefix + Key(language);

    private const string SpeakPrefix = "speak:";

    /// <summary>The text the voice speaks for <paramref name="pieceText"/>: the "how to read" form when the story has one, else the displayed translation, else null.</summary>
    public static string? LookupSpoken(Story story, string language, string pieceText) =>
        TableFor(story, SpeakKey(language)) is { } table && table.TryGetValue(pieceText, out string? spoken) ? spoken : Lookup(story, language, pieceText);

    /// <summary>The per-piece translator for speech: <see cref="LookupSpoken"/> first, then <paramref name="engine"/> (null for none), else the source text.</summary>
    public static PieceTranslator ComposeSpoken(Story story, string myLanguage, PieceTranslator? engine) =>
        async (text, cancellationToken) =>
            LookupSpoken(story, myLanguage, text) ?? (engine is null ? text : await engine(text, cancellationToken));

    /// <summary>The per-piece translator: the bundled table first, then <paramref name="engine"/> (null for none), else the source text.</summary>
    public static PieceTranslator Compose(Story story, string myLanguage, PieceTranslator? engine) =>
        async (text, cancellationToken) =>
            Lookup(story, myLanguage, text) ?? (engine is null ? text : await engine(text, cancellationToken));
}
