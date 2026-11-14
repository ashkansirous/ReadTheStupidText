using System.Text.Json;
using DinoReader.Core;

namespace DinoReader.BookBuilder;

/// <summary>One manifest entry as the check needs it: the piece position, the text hash the app compares and the text (for the message).</summary>
internal sealed record ManifestEntry(int Index, string Hash, string? Text, string? Clip = null);

/// <summary>
/// The verdict on one book (Slice 277): its stem and store id, the gaps found (one line each) and the tool error if the check itself failed. A book is complete only with no gap and no tool error;
/// the publish job uploads a book only when it finds a complete verdict for it.
/// </summary>
internal sealed record BookVerdict(string Stem, string Id, IReadOnlyList<string> Gaps, string? ToolError = null)
{
    public bool Complete => ToolError is null && Gaps.Count == 0;
}

/// <summary>What a check-audio run found: manifests checked, manifest problems (exit 1), tool errors (exit 2) and one verdict per book checked.</summary>
internal sealed record AudioCheckResult(int Checked, List<string> Problems, List<string> ToolErrors, IReadOnlyList<BookVerdict>? Verdicts = null)
{
    public IReadOnlyList<BookVerdict> BookVerdicts => Verdicts ?? [];
}

/// <summary>
/// <c>check-audio [&lt;stem&gt;...]</c> (Slice 272): every book x language that has an <c>audio/&lt;lang&gt;/manifest.json</c> is compared with the sentences the current app reads (the same listing
/// <c>dump-sentences</c> prints). The app finds a clip by the piece's position and the text hash at that position (<see cref="PremadeAudio.ClipAt"/>), so a manifest whose entry at position N
/// does not hash the app's piece N makes the device voice read that piece. Slice 255 changed the count (animal sounds left out) and eight Farsi packs silently stopped fitting; this fails
/// until a pack is re-aligned. Manifests are Git LFS: the CI step fetches only them (<c>git lfs pull --include "content/dino/*/audio/*/manifest.json" --exclude ""</c>).
/// </summary>
internal static class AudioAlignment
{
    private const string AudioFolder = "audio";

    /// <summary>
    /// Checks the named books (all with an audio folder when none is named). Prints one line per problem, naming the book and language, and returns 1 when there is any.
    /// <paramref name="verdictPath"/> gets the structured verdict file (<see cref="VerdictJson"/>) for the publish job: an empty one is written before anything runs, so a crash leaves a file that publishes nothing.
    /// </summary>
    public static int Run(IReadOnlyList<string> stems, bool live = false, string? verdictPath = null)
    {
        WriteVerdicts(verdictPath, []);
        AudioCheckResult result = live
            ? LiveAudioCheck.RunAsync(stems, LiveAudioCheck.StoriesUrl, Path.Combine(Path.GetTempPath(), "dino-live-audio")).GetAwaiter().GetResult()
            : CheckBooks(stems.Count == 0 ? Books.All : stems.Select(Books.Folder), CheckBook);
        WriteVerdicts(verdictPath, result.BookVerdicts);
        result.Problems.ForEach(Console.Error.WriteLine);
        result.ToolErrors.ForEach(Console.Error.WriteLine);
        Console.WriteLine(SummaryOf(result));
        return ExitCodeOf(result);
    }

    /// <summary>The verdict file the publish job reads: <c>{"books":[{"stem","id","complete","gaps":[...],"toolError"}]}</c>, one entry per book checked. A book absent from it is not published.</summary>
    internal static string VerdictJson(IReadOnlyList<BookVerdict> verdicts) =>
        JsonSerializer.Serialize(new { books = verdicts.Select(v => new { stem = v.Stem, id = v.Id, complete = v.Complete, gaps = v.Gaps, toolError = v.ToolError }) });

    private static void WriteVerdicts(string? path, IReadOnlyList<BookVerdict> verdicts)
    {
        if (path is not null)
        {
            File.WriteAllText(path, VerdictJson(verdicts));
        }
    }

    /// <summary>Runs <paramref name="check"/> on each book, which adds that book's gap lines to the list it is given. Each book gets a verdict; any exception the check throws is a tool bug, recorded as that book's tool error (no verdict of "complete") so the other books are still checked.</summary>
    internal static AudioCheckResult CheckBooks(IEnumerable<BookFolder> books, Func<BookFolder, List<string>, int> check)
    {
        var problems = new List<string>();
        var toolErrors = new List<string>();
        var verdicts = new List<BookVerdict>();
        int checkedManifests = 0;
        foreach (BookFolder book in books)
        {
            var own = new List<string>();
            try
            {
                checkedManifests += check(book, own);
                problems.AddRange(own);
                verdicts.Add(new BookVerdict(book.Stem, book.Info.Id, own));
            }
            catch (Exception e)
            {
                string error = $"TOOL ERROR {book.Stem}: check-audio itself failed ({e.GetType().Name}: {e.Message})";
                toolErrors.Add(error);
                verdicts.Add(new BookVerdict(book.Stem, book.Info.Id, [], error));
            }
        }

        return new AudioCheckResult(checkedManifests, problems, toolErrors, verdicts);
    }

    /// <summary>2 when the tool itself failed, 1 for a manifest problem or when no manifest was found at all (nothing was checked), else 0.</summary>
    internal static int ExitCodeOf(AudioCheckResult result) =>
        result.ToolErrors.Count > 0 ? 2 : result.Problems.Count > 0 || result.Checked == 0 ? 1 : 0;

    private static string SummaryOf(AudioCheckResult result) =>
        result.ToolErrors.Count > 0 ? $"check-audio failed on {result.ToolErrors.Count} book(s): a tool error, not a manifest problem."
        : result.Checked == 0 ? "No audio manifest found to check (a mistyped book name, or the manifests are not pulled from Git LFS)."
        : result.Problems.Count == 0 ? $"All {result.Checked} audio manifests line up with the sentences the app reads."
        : $"{result.Problems.Count} of {result.Checked} audio manifest(s) do not line up.";

    /// <summary>Slice 277: every language the book is offered in must have a complete pack. A language with no manifest is a problem (it used to be skipped), as is any gap <see cref="AudioCoverage"/> finds. Returns the languages checked.</summary>
    private static int CheckBook(BookFolder book, List<string> problems)
    {
        problems.AddRange(ProblemsOf(book));
        return AudioCoverage.OfferedLanguages.Count;
    }

    /// <summary>Every audio gap of one book as <c>&lt;stem&gt; &lt;language&gt;: &lt;gap&gt;</c> lines (empty when the book's audio is complete in every offered language). A tool bug throws.</summary>
    internal static List<string> ProblemsOf(BookFolder book)
    {
        var lines = new List<string>();
        Story? story = StoryOrNull(book.Stem, out string? storyProblem);
        IReadOnlyDictionary<string, AudioPackRecord> records = AudioPacks.RecordsIn(book.Dir);
        foreach (string language in AudioCoverage.OfferedLanguages)
        {
            string folder = Path.Combine(book.Dir, AudioFolder, language);
            IEnumerable<string> found = story is null ? [storyProblem!] : LanguageProblems(book.Stem, story, language, folder);
            lines.AddRange(found.Concat(RecordProblem(records, language, folder)).Select(problem => $"{book.Stem} {language}: {problem}"));
        }

        return lines;
    }

    /// <summary>Slice 277: clips and a manifest are not enough; the pack the store serves comes from the language's <c>audioPacks</c> record in audio-cast.json (<c>build-audio</c> writes it), so a language without one ships no audio. Not asked when the manifest is missing (that is already a gap).</summary>
    private static IEnumerable<string> RecordProblem(IReadOnlyDictionary<string, AudioPackRecord> records, string language, string folder) =>
        !File.Exists(Path.Combine(folder, PremadeAudio.ManifestFile)) || records.Keys.Any(key => string.Equals(key, language, StringComparison.OrdinalIgnoreCase))
            ? []
            : [$"{AudioPacks.CastFile} has no {AudioPacks.RecordsKey} record for this language: run build-audio"];

    internal static IEnumerable<string> LanguageProblems(string stem, Story story, string language, string folder)
    {
        string manifestPath = Path.Combine(folder, PremadeAudio.ManifestFile);
        return File.Exists(manifestPath)
            ? ProblemsOfPack(stem, story, language, folder, manifestPath)
            : [$"no audio pack: audio/{language}/{PremadeAudio.ManifestFile} is missing"];
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

    private static IReadOnlyList<string> ProblemsOfPack(string stem, Story story, string language, string folder, string manifestPath)
    {
        if (AudioPacks.IsLfsPointer(manifestPath))
        {
            return ["the manifest is a Git LFS pointer: run git lfs pull --include \"content/dino/*/audio/*/manifest.json\" --exclude \"\" first"];
        }

        string json;
        try
        {
            json = File.ReadAllText(manifestPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [$"the manifest cannot be read ({e.Message})"];
        }

        // Anything thrown below this line is a tool bug and reaches CheckBooks as a tool error.
        return ParseProblem(json, language) is { } rejected
            ? [rejected]
            : AudioCoverage.Gaps(ReadEntries(json), SentenceDump.Build(stem, story, language).Sentences, clip => AudioCoverage.SizeInFolder(folder, clip));
    }

    /// <summary>Null when the app's own <see cref="PremadeAudio.Parse"/> accepts the manifest and it was made for <paramref name="language"/>; else why not (the app then reads that book with the device voice).</summary>
    public static string? ParseProblem(string json, string language)
    {
        try
        {
            string made = PremadeAudio.Parse(json).Language;
            return string.Equals(made, language, StringComparison.OrdinalIgnoreCase) ? null : $"the manifest is for language \"{made}\", not \"{language}\"";
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
                entries.Add(new ManifestEntry(position, text.ToLowerInvariant(), entry.TryGetProperty("text", out JsonElement said) ? said.GetString() : null, entry.GetProperty("clip").GetString()));
            }
        }

        return entries;
    }

    private static bool HasClip(JsonElement entry) =>
        entry.TryGetProperty("clip", out JsonElement clip) && clip.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(clip.GetString());
}
