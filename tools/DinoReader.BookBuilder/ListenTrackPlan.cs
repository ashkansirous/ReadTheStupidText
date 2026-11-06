using DinoReader.Core;
using ReadTheStupidText.Application.Reading;

namespace DinoReader.BookBuilder;

/// <summary>One clip of a listening track: the highlight sentence index it speaks (the manifest's <c>index</c>), the story page that sentence sits on, and the clip file name in the pack's folder.</summary>
internal sealed record ListenClip(int Index, int Page, string File);

/// <summary>One step of the join: a clip to play, or (File null) that many milliseconds of silence.</summary>
internal sealed record ListenStep(string? File, int SilenceMs);

/// <summary>
/// Slice 281, the pure half of a listening track: which clips, in which order, with which gaps. The order is the app's own read order (the sentence listing <see cref="SentenceDump.Build"/> makes from
/// the same code the app reads with); a piece with no clip in the manifest fails the build, never a silent hole. The gap between two pieces is the app's own, <see cref="VoicedAudio.DefaultSentenceGap"/>
/// (150 ms; <c>PremadeAudio</c> and <c>PhraseSplitter.SentenceGapMs</c> use the same value). The app itself reads on across a page with that same gap (pages are one text joined by <see cref="Story.PageSeparator"/>
/// and a page turn is the picture changing), so a longer pause at a page turn is the track's own choice: <see cref="PageGapMs"/>, for a listener who has no picture to see the page change by.
/// </summary>
internal static class ListenTrackPlan
{
    public const int SentenceGapMs = 150;
    public const int PageGapMs = 900;

    /// <summary>The page each highlight sentence sits on: the last page that starts at or before the sentence's first character in the story's joined text.</summary>
    public static IReadOnlyList<int> PagesOfSentences(Story story, IReadOnlyList<SentenceSpan> spans)
    {
        int[] pageStarts = PageStarts(story.Pages);
        return spans.Select(span => PageAt(pageStarts, span.Start)).ToList();
    }

    private static int[] PageStarts(IReadOnlyList<StoryPage> pages)
    {
        var starts = new int[pages.Count];
        int offset = 0;
        for (int i = 0; i < pages.Count; i++)
        {
            starts[i] = offset;
            offset += pages[i].Text.Length + Story.PageSeparator.Length;
        }

        return starts;
    }

    private static int PageAt(int[] pageStarts, int position)
    {
        int page = 0;
        for (int i = 0; i < pageStarts.Length && pageStarts[i] <= position; i++)
        {
            page = i;
        }

        return page;
    }

    /// <summary>The clips in reading order. Fails when a spoken piece has no manifest entry (or its entry has no clip name), so a track is never made with a hole in it.</summary>
    public static IReadOnlyList<ListenClip> Clips(SentenceListing listing, IReadOnlyList<int> pageOfSentence, IReadOnlyDictionary<int, string> clipByIndex)
    {
        if (listing.Sentences.Count == 0)
        {
            throw new InvalidOperationException($"{listing.Book} [{listing.Language}] has no spoken sentence; there is nothing to join.");
        }

        var clips = new List<ListenClip>();
        foreach (SpokenSentence piece in listing.Sentences)
        {
            if (!clipByIndex.TryGetValue(piece.Index, out string? file))
            {
                throw new InvalidOperationException($"{listing.Book} [{listing.Language}]: piece {piece.Index} (\"{Shorten(piece.Text)}\") has no clip in the manifest; the listening track is not built.");
            }

            clips.Add(new ListenClip(piece.Index, pageOfSentence[Math.Clamp(piece.Sentence, 0, pageOfSentence.Count - 1)], file));
        }

        return clips;
    }

    private static string Shorten(string text) => text.Length <= 40 ? text : text[..40] + "...";

    /// <summary>The join: each clip, with silence before every clip but the first (the page gap where the page changes from the previous clip's, else the sentence gap).</summary>
    public static IReadOnlyList<ListenStep> Steps(IReadOnlyList<ListenClip> clips)
    {
        var steps = new List<ListenStep>();
        for (int i = 0; i < clips.Count; i++)
        {
            if (i > 0)
            {
                steps.Add(new ListenStep(null, clips[i].Page != clips[i - 1].Page ? PageGapMs : SentenceGapMs));
            }

            steps.Add(new ListenStep(clips[i].File, 0));
        }

        return steps;
    }

    public static string FileName(string language, int version) => $"listen-{language}-v{version}.m4a";

    /// <summary>The distinct gap lengths the steps use, one silence file each (a book has two: the sentence gap and the page gap).</summary>
    public static IReadOnlyList<int> SilenceLengths(IReadOnlyList<ListenStep> steps) =>
        steps.Where(s => s.File is null).Select(s => s.SilenceMs).Distinct().Order().ToList();

    public static string SilenceName(int milliseconds) => $"silence-{milliseconds}.m4a";

    /// <summary>
    /// The ffconcat list for ffmpeg's concat demuxer: one <c>file</c> line per step, a clip by its absolute path in <paramref name="clipsFolder"/>, a gap by its silence file in <paramref name="silenceFolder"/>.
    /// Paths use forward slashes and single quotes (a quote inside a name is written <c>'\''</c>), read with <c>-safe 0</c>. The list has one line per step, so a long book is a long file, never a long command line.
    /// </summary>
    public static string ConcatList(IReadOnlyList<ListenStep> steps, string clipsFolder, string silenceFolder)
    {
        var list = new System.Text.StringBuilder("ffconcat version 1.0\n");
        foreach (ListenStep step in steps)
        {
            string path = step.File is null ? Path.Combine(silenceFolder, SilenceName(step.SilenceMs)) : Path.Combine(clipsFolder, step.File);
            list.Append("file '").Append(Path.GetFullPath(path).Replace('\\', '/').Replace("'", "'\\''")).Append("'\n");
        }

        return list.ToString();
    }
}
