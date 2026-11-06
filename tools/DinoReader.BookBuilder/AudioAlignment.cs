using System.Text.Json;
using DinoReader.Core;

namespace DinoReader.BookBuilder;

/// <summary>One manifest entry as the check needs it: the piece position, the text hash the app compares and the text (for the message).</summary>
internal sealed record ManifestEntry(int Index, string Hash, string? Text);

/// <summary>What a check-audio run found: manifests checked, manifest problems (exit 1) and tool errors (exit 2).</summary>
internal sealed record AudioCheckResult(int Checked, List<string> Problems, List<string> ToolErrors);

/// <summary>
/// <c>check-audio [&lt;stem&gt;...]</c> (Slice 272): every book x language that has an <c>audio/&lt;lang&gt;/manifest.json</c> is compared with the sentences the current app reads (the same listing
/// <c>dump-sentences</c> prints). The app finds a clip by the piece's position and the text hash at that position (<see cref="PremadeAudio.ClipAt"/>), so a manifest whose entry at position N
/// does not hash the app's piece N makes the device voice read that piece. Slice 255 changed the count (animal sounds left out) and eight Farsi packs silently stopped fitting; this fails
/// until a pack is re-aligned. Manifests are Git LFS: the CI step fetches only them (<c>git lfs pull --include "content/dino/*/audio/*/manifest.json" --exclude ""</c>).
/// </summary>
internal static class AudioAlignment
{
    private const string AudioFolder = "audio";

    /// <summary>The first way the manifest does not fit the dump (position and both texts, plus the counts), or null when every position lines up and no entry is left over.</summary>
    public static string? FirstProblem(IReadOnlyList<ManifestEntry> manifest, IReadOnlyList<SpokenSentence> dump)
    {
        Dictionary<int, ManifestEntry> byIndex = manifest.GroupBy(e => e.Index).ToDictionary(g => g.Key, g => g.First());
        foreach (SpokenSentence piece in dump)
        {
            if (!byIndex.TryGetValue(piece.Index, out ManifestEntry? entry))
            {
                return $"first bad position {piece.Index}: the manifest has no entry (the app reads \"{Shorten(piece.Text)}\"); {Counts(manifest, dump)}";
            }

            if (entry.Hash != PremadeAudio.HashOf(piece.Text))
            {
                return $"first bad position {piece.Index}: the manifest holds \"{Shorten(entry.Text)}\", the app reads \"{Shorten(piece.Text)}\"; {Counts(manifest, dump)}";
            }
        }

        return manifest.Count == dump.Count ? null : $"the manifest has entries past the last piece (first extra position {dump.Count}, \"{Shorten(byIndex.GetValueOrDefault(dump.Count)?.Text)}\"); {Counts(manifest, dump)}";
    }

    /// <summary>Checks the named books (all with an audio folder when none is named). Prints one line per problem, naming the book and language, and returns 1 when there is any.</summary>
    public static int Run(IReadOnlyList<string> stems)
    {
        IEnumerable<BookFolder> books = stems.Count == 0 ? Books.All : stems.Select(Books.Folder);
        AudioCheckResult result = CheckBooks(books, CheckBook);
        result.Problems.ForEach(Console.Error.WriteLine);
        result.ToolErrors.ForEach(Console.Error.WriteLine);
        Console.WriteLine(SummaryOf(result));
        return ExitCodeOf(result);
    }

    /// <summary>Runs <paramref name="check"/> on each book. A manifest problem is a line in Problems; any exception the check throws is a tool bug, recorded in ToolErrors for that book so the other books are still checked.</summary>
    internal static AudioCheckResult CheckBooks(IEnumerable<BookFolder> books, Func<BookFolder, List<string>, int> check)
    {
        var problems = new List<string>();
        var toolErrors = new List<string>();
        int checkedManifests = 0;
        foreach (BookFolder book in books)
        {
            try
            {
                checkedManifests += check(book, problems);
            }
            catch (Exception e)
            {
                toolErrors.Add($"TOOL ERROR {book.Stem}: check-audio itself failed ({e.GetType().Name}: {e.Message})");
            }
        }

        return new AudioCheckResult(checkedManifests, problems, toolErrors);
    }

    /// <summary>2 when the tool itself failed, 1 for a manifest problem or when no manifest was found at all (nothing was checked), else 0.</summary>
    internal static int ExitCodeOf(AudioCheckResult result) =>
        result.ToolErrors.Count > 0 ? 2 : result.Problems.Count > 0 || result.Checked == 0 ? 1 : 0;

    private static string SummaryOf(AudioCheckResult result) =>
        result.ToolErrors.Count > 0 ? $"check-audio failed on {result.ToolErrors.Count} book(s): a tool error, not a manifest problem."
        : result.Checked == 0 ? "No audio manifest found to check (a mistyped book name, or the manifests are not pulled from Git LFS)."
        : result.Problems.Count == 0 ? $"All {result.Checked} audio manifests line up with the sentences the app reads."
        : $"{result.Problems.Count} of {result.Checked} audio manifest(s) do not line up.";

    private static int CheckBook(BookFolder book, List<string> problems)
    {
        string audio = Path.Combine(book.Dir, AudioFolder);
        List<string> languages = Directory.Exists(audio)
            ? Directory.EnumerateDirectories(audio).Where(d => File.Exists(Path.Combine(d, PremadeAudio.ManifestFile))).Select(d => Path.GetFileName(d)).Order(StringComparer.Ordinal).ToList()
            : [];
        if (languages.Count == 0)
        {
            return 0;
        }

        Story? story = StoryOrNull(book.Stem, out string? storyProblem);
        foreach (string language in languages)
        {
            string? problem = story is null ? storyProblem : ProblemOf(book.Stem, story, language, Path.Combine(audio, language, PremadeAudio.ManifestFile));
            if (problem is not null)
            {
                problems.Add($"{book.Stem} {language}: {problem}");
            }
        }

        return languages.Count;
    }

    // A book with a manifest but no readable story (no story.md and not the known picture book) is one problem line per language, not a crash that hides the other books.
    private static Story? StoryOrNull(string stem, out string? problem)
    {
        try
        {
            problem = null;
            return SentenceDump.StoryOf(stem);
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or JsonException or KeyNotFoundException)
        {
            problem = $"the book cannot be read to compare with ({e.Message})";
            return null;
        }
    }

    private static string? ProblemOf(string stem, Story story, string language, string manifestPath)
    {
        if (AudioPacks.IsLfsPointer(manifestPath))
        {
            return "the manifest is a Git LFS pointer: run git lfs pull --include \"content/dino/*/audio/*/manifest.json\" --exclude \"\" first";
        }

        string json;
        try
        {
            json = File.ReadAllText(manifestPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return $"the manifest cannot be read ({e.Message})";
        }

        // Anything thrown below this line is a tool bug and reaches CheckBooks as a tool error.
        return ParseProblem(json) ?? FirstProblem(ReadEntries(json), SentenceDump.Build(stem, story, language).Sentences);
    }

    /// <summary>Null when the app's own <see cref="PremadeAudio.Parse"/> accepts the manifest; else why it rejects it (the app then reads that book with the device voice).</summary>
    public static string? ParseProblem(string json)
    {
        try
        {
            PremadeAudio.Parse(json);
            return null;
        }
        catch (PremadeAudioException e)
        {
            return $"the app rejects the manifest ({e.Message})";
        }
    }

    /// <summary>The entries of a version-3 manifest with an index, a hash and a clip file name (an entry without any of them has no clip, exactly as <see cref="PremadeAudio.Parse"/> skips it).</summary>
    public static IReadOnlyList<ManifestEntry> ReadEntries(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        var entries = new List<ManifestEntry>();
        foreach (JsonElement entry in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            if (entry.TryGetProperty("index", out JsonElement index) && index.ValueKind == JsonValueKind.Number && index.TryGetInt32(out int position)
                && entry.TryGetProperty("hash", out JsonElement hash) && hash.ValueKind == JsonValueKind.String && hash.GetString() is { Length: > 0 } text && HasClip(entry))
            {
                entries.Add(new ManifestEntry(position,text.ToLowerInvariant(), entry.TryGetProperty("text", out JsonElement said) ? said.GetString() : null));
            }
        }

        return entries;
    }

    private static bool HasClip(JsonElement entry) =>
        entry.TryGetProperty("clip", out JsonElement clip) && clip.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(clip.GetString());

    private static string Counts(IReadOnlyList<ManifestEntry> manifest, IReadOnlyList<SpokenSentence> dump) =>
        $"{manifest.Count} manifest entries, {dump.Count} pieces read";

    private static string Shorten(string? text) => text is null ? "?" : text.Length <= 40 ? text : text[..40] + "...";
}
