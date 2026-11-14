using System.Text.RegularExpressions;
using DinoReader.Core;

namespace DinoReader.BookBuilder;

/// <summary>
/// Slice 277: what "complete audio" means for one book and language. There is no device-voice fallback (Slice 278), so a store book is published only when every sentence the app reads in every
/// language it is offered in has a clip: a pack must exist, no sentence may lack its clip, no clip may belong to text that is gone, and every clip file must be there and not empty.
/// The comparison is pure (a manifest, the sentences, and a function that says how big a clip file is), so it runs the same over a checkout and over a downloaded live pack.
/// </summary>
internal static partial class AudioCoverage
{
    /// <summary>How many offending sentences or files one message names.</summary>
    private const int NamedExamples = 3;

    private const int ShortenAt = 40;

    /// <summary>The languages a book is offered in: every language the app has (the listing offers the book in all of them).</summary>
    public static IReadOnlyList<string> OfferedLanguages => UiLanguage.Supported;

    /// <summary>
    /// The gaps between the manifest and the sentences the app reads, one message each kind: sentences with no clip, clips for text that no longer exists, clip files the manifest names that are not
    /// there, and clip files that are empty. <paramref name="clipSize"/> gives a clip file's size in bytes by its name, or null when the file is not there. Empty when the language is complete.
    /// </summary>
    public static IReadOnlyList<string> Gaps(IReadOnlyList<ManifestEntry> manifest, IReadOnlyList<SpokenSentence> dump, Func<string, long?> clipSize)
    {
        Dictionary<int, ManifestEntry> byIndex = manifest.GroupBy(e => e.Index).ToDictionary(g => g.Key, g => g.First());
        var uncovered = new List<SpokenSentence>();
        var covering = new List<ManifestEntry>();
        foreach (SpokenSentence piece in dump)
        {
            if (byIndex.TryGetValue(piece.Index, out ManifestEntry? entry) && entry.Hash == PremadeAudio.HashOf(piece.Text))
            {
                covering.Add(entry);
            }
            else
            {
                uncovered.Add(piece);
            }
        }

        HashSet<int> covered = covering.Select(e => e.Index).ToHashSet();
        List<ManifestEntry> stale = manifest.Where(e => !covered.Contains(e.Index)).ToList();
        var gaps = new List<string>();
        AddNoClip(gaps, uncovered, dump.Count);
        AddStale(gaps, stale);
        AddDuplicates(gaps, manifest);
        AddFileGaps(gaps, covering, clipSize);
        return gaps;
    }

    private static void AddNoClip(List<string> gaps, List<SpokenSentence> uncovered, int total)
    {
        if (uncovered.Count > 0)
        {
            gaps.Add($"{uncovered.Count} of {total} sentence(s) have no clip (first: {string.Join("; ", uncovered.Take(NamedExamples).Select(p => $"position {p.Index} \"{Shorten(p.Text)}\""))})");
        }
    }

    private static void AddStale(List<string> gaps, List<ManifestEntry> stale)
    {
        if (stale.Count > 0)
        {
            gaps.Add($"{stale.Count} clip(s) are for text that no longer exists (first: {string.Join("; ", stale.Take(NamedExamples).Select(e => $"position {e.Index} \"{Shorten(e.Text)}\""))})");
        }
    }

    private static void AddDuplicates(List<string> gaps, IReadOnlyList<ManifestEntry> manifest)
    {
        List<int> shared = manifest.GroupBy(e => e.Index).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (shared.Count > 0)
        {
            gaps.Add($"{shared.Count} position(s) have more than one manifest entry (first: {string.Join(", ", shared.Take(NamedExamples))}); an orphan or conflicting clip");
        }
    }

    private static void AddFileGaps(List<string> gaps, List<ManifestEntry> covering, Func<string, long?> clipSize)
    {
        var missing = new List<string>();
        var empty = new List<string>();
        foreach (string clip in covering.Select(e => e.Clip).Where(c => !string.IsNullOrEmpty(c)).Distinct(StringComparer.Ordinal).Cast<string>())
        {
            long? size = clipSize(clip);
            if (size is null)
            {
                missing.Add(clip);
            }
            else if (size == 0)
            {
                empty.Add(clip);
            }
        }

        if (covering.Any(e => string.IsNullOrEmpty(e.Clip)))
        {
            missing.Add("(an entry with no clip name)");
        }

        AddFiles(gaps, missing, "clip file(s) the manifest names are missing");
        AddFiles(gaps, empty, "clip file(s) are empty (zero length)");
    }

    private static void AddFiles(List<string> gaps, List<string> files, string what)
    {
        if (files.Count > 0)
        {
            gaps.Add($"{files.Count} {what} (first: {string.Join(", ", files.Take(NamedExamples))})");
        }
    }

    /// <summary>The size of a clip in a checkout: the file's length, or for a Git LFS pointer (the clip was not pulled) the size the pointer records; null when the file is not there.</summary>
    public static long? SizeInFolder(string folder, string clip)
    {
        string path = Path.Combine(folder, clip);
        if (!File.Exists(path))
        {
            return null;
        }

        return AudioPacks.IsLfsPointer(path) ? LfsPointerSize(path) : new FileInfo(path).Length;
    }

    /// <summary>The <c>size</c> line of a Git LFS pointer file; 0 when it has none (an unreadable pointer counts as an empty clip).</summary>
    internal static long LfsPointerSize(string path) => LfsPointerSizeOf(File.ReadAllText(path));

    internal static long LfsPointerSizeOf(string pointerText)
    {
        Match match = PointerSize().Match(pointerText);
        return match.Success && long.TryParse(match.Groups[1].Value, out long size) ? size : 0;
    }

    [GeneratedRegex(@"^size (\d+)\s*$", RegexOptions.Multiline)]
    private static partial Regex PointerSize();

    private static string Shorten(string? text) => text is null ? "?" : text.Length <= ShortenAt ? text : text[..ShortenAt] + "...";
}
