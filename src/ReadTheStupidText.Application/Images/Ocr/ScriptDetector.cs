using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Images;

/// <summary>The writing systems the OCR choice tells apart (Slice 84). <see cref="Unknown"/> is never guessed as Latin.</summary>
public enum TextScript
{
    Unknown,
    Latin,
    Arabic,
    Cyrillic,
    Cjk,
    Hangul,
    Hebrew,
    Devanagari,
    Greek,
}

/// <summary>How many letters of each <see cref="TextScript"/> a reading holds.</summary>
public sealed record ScriptShare(IReadOnlyDictionary<TextScript, int> Counts)
{
    public static ScriptShare Empty { get; } = new(new Dictionary<TextScript, int>());

    public int Total => Counts.Values.Sum();

    public int Latin => Count(TextScript.Latin);

    public int Count(TextScript script) => Counts.GetValueOrDefault(script);

    /// <summary>The script with the most letters, or null when the reading has no letters.</summary>
    public TextScript? Dominant => Total == 0 ? null : Counts.MaxBy(pair => pair.Value).Key;
}

/// <summary>
/// Tells which script a page is in from what the built-in recognizer read, so the OCR model follows
/// the page and never the app language (Slice 84, Decision 91). One table holds each script's letter
/// ranges, languages and direction. Pure.
/// </summary>
public static class ScriptDetector
{
    private sealed record ScriptSpec(TextScript Script, bool RightToLeft, string[] Languages, (char From, char To)[] Ranges);

    private static readonly ScriptSpec[] Table =
    [
        new(TextScript.Latin, false, ["en", "fr", "es", "pt", "da", "fi", "sv", "nb", "nl", "de"], [('\0', 'ɏ'), ('Ḁ', 'ỿ')]),
        new(TextScript.Greek, false, ["el"], [('Ͱ', 'Ͽ'), ('ἀ', '῿')]),
        new(TextScript.Cyrillic, false, ["ru", "uk"], [('Ѐ', 'ԯ')]),
        new(TextScript.Hebrew, true, ["he"], [('֐', '׿'), ('יִ', 'ﭏ')]),
        new(TextScript.Arabic, true, ["ar", "fa"],
            [('؀', 'ۿ'), ('ݐ', 'ݿ'), ('ࢠ', 'ࣿ'), ('ﭐ', '﷿'), ('ﹰ', '﻿')]),
        new(TextScript.Devanagari, false, ["hi"], [('ऀ', 'ॿ')]),
        new(TextScript.Hangul, false, ["ko"], [('ᄀ', 'ᇿ'), ('㄰', '㆏'), ('가', '힯')]),
        new(TextScript.Cjk, false, ["zh", "ja"],
            [('぀', 'ヿ'), ('㐀', '䶿'), ('一', '鿿'), ('豈', '﫿')]),
    ];

    /// <summary>More than this share of letters must be Latin for a reading to count as Latin.</summary>
    private const double MostlyLatinShare = 0.5;

    public static ScriptShare Analyze(IEnumerable<string> lines)
    {
        Dictionary<TextScript, int> counts = [];
        foreach (char c in lines.SelectMany(line => line).Where(char.IsLetter))
        {
            if (ScriptOf(c) is { } script)
            {
                counts[script] = counts.GetValueOrDefault(script) + 1;
            }
        }

        return new ScriptShare(counts);
    }

    public static bool IsMostlyLatin(ScriptShare share) =>
        share.Total > 0 && (double)share.Latin / share.Total > MostlyLatinShare;

    /// <summary>The script a language is written in, or <see cref="TextScript.Unknown"/> for a language the table does not list.</summary>
    public static TextScript ScriptOf(string language) =>
        SpecOf(language)?.Script ?? TextScript.Unknown;

    /// <summary>A language written in the script (the first the table lists), or null for a script the table does not hold.</summary>
    public static string? LanguageOf(TextScript script) =>
        Table.FirstOrDefault(spec => spec.Script == script)?.Languages[0];

    /// <summary>True when the language's script runs right to left.</summary>
    public static bool IsRightToLeft(string language) => SpecOf(language)?.RightToLeft ?? false;

    private static ScriptSpec? SpecOf(string language) =>
        Table.FirstOrDefault(spec => spec.Languages.Contains(AppLanguages.Canonical(language), StringComparer.OrdinalIgnoreCase));

    private static TextScript? ScriptOf(char c) =>
        Table.FirstOrDefault(spec => spec.Ranges.Any(range => c >= range.From && c <= range.To))?.Script;
}
