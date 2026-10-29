namespace DinoReader.Core;

/// <summary>
/// The first run after the pack record was introduced (the 2026-10-29 ML Kit-only-for-grown-ups fix): an install that downloaded translation packs earlier has an
/// empty record, and asking ML Kit would load it on a child's screen. This works out, from the story languages the grown-up picked, which packs Dino
/// would have downloaded for them, so the record starts close to the truth. A wrong guess is corrected by a grown-up screen's real check, or at translate time.
/// </summary>
public static class TranslationPackSeed
{
    private const string English = "en";

    /// <summary>
    /// Whether to guess the record at all: only on an upgrade (an older version may have downloaded packs), never on a fresh install (nothing was downloaded),
    /// and not once the guess was made or a real check already ran.
    /// </summary>
    public static bool ShouldSeed(bool freshInstall, bool alreadySeeded, bool reconciled) => !freshInstall && !alreadySeeded && !reconciled;

    /// <summary>
    /// The packs to record: for each story language other than English, whatever <paramref name="packRule"/> (source, target) needs to translate a book
    /// from English into it. Empty when only English is chosen (nothing to translate into).
    /// </summary>
    public static IReadOnlyList<string> Languages(IEnumerable<string> storyLanguages, Func<string, string, IReadOnlyList<string>> packRule) =>
        [.. storyLanguages
            .Where(language => language != English)
            .SelectMany(language => packRule(English, language))
            .Distinct(StringComparer.Ordinal)];
}
