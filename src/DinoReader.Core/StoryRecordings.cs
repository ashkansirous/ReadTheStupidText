using System.Text.RegularExpressions;

namespace DinoReader.Core;

/// <summary>A parent's recording of one page (Slice 111, Decision 115): the language it was read in, which decides the text shown with it, and its length.</summary>
public sealed record PageRecording(string Language, int Seconds);

/// <summary>
/// Pure rules for the per-page parent recordings of a story (Slice 111): the file each page's recording lives in, which pages play one,
/// and how an edit of the pages keeps or drops them. A recording belongs to a page by its index and is a file named <c>rec-&lt;page number&gt;.m4a</c> in the
/// story folder; it is never part of a store upload and rides along in <c>.dino</c> packages and backups.
/// </summary>
public static class StoryRecordings
{
    public const string FilePrefix = "rec";
    public const string FileExtension = ".m4a";

    private static readonly Regex FileNamePattern = new(@"^rec-[1-9][0-9]{0,3}\.m4a$", RegexOptions.CultureInvariant);

    /// <summary>The file name of page <paramref name="page"/>'s recording (0-based index, the number in the name is 1-based).</summary>
    public static string FileNameOf(int page) => $"{FilePrefix}-{page + 1}{FileExtension}";

    /// <summary>Whether <paramref name="fileName"/> is a name <see cref="FileNameOf"/> makes, the only recording files the library may store or delete.</summary>
    public static bool IsRecordingFileName(string? fileName) => fileName is not null && FileNamePattern.IsMatch(fileName);

    /// <summary>The recording of <paramref name="page"/>, or null.</summary>
    public static PageRecording? Of(Story story, int page) =>
        story.Recordings is { } all && page >= 0 && page < story.Pages.Count && all.TryGetValue(page, out PageRecording? recording) ? recording : null;

    /// <summary>How many pages have a recording.</summary>
    public static int Count(Story story) => RecordedPages(story).Count();

    /// <summary>The indexes of the pages that have a recording, in order.</summary>
    public static IEnumerable<int> RecordedPages(Story story) =>
        (story.Recordings?.Keys ?? Enumerable.Empty<int>()).Where(p => p >= 0 && p < story.Pages.Count).Order();

    /// <summary>The recording files the story names, in page order.</summary>
    public static IEnumerable<string> Files(Story story) => RecordedPages(story).Select(FileNameOf);

    /// <summary>Whether the reader plays page <paramref name="page"/>'s recording: the book's "Read with my voice" is on, the page has one, and it was
    /// made in the story's current language (a recording keeps the language it was made in, so a story whose language was edited reads that page generated again).</summary>
    public static bool Plays(Story story, int page) =>
        story.UseRecordings && Of(story, page) is { } recording && UiLanguage.Resolve(recording.Language) == UiLanguage.Resolve(story.Language);

    /// <summary>The pages that play a recording read in the story's own language: they show the source text even while the rest of the book shows a translation (Decision 115).</summary>
    public static IReadOnlySet<int> PagesShownInOwnLanguage(Story story) =>
        RecordedPages(story).Where(p => Plays(story, p)).ToHashSet();

    /// <summary>The story with <paramref name="recording"/> for <paramref name="page"/>. The first recording of a book turns "Read with my voice" on.</summary>
    public static Story With(Story story, int page, PageRecording recording)
    {
        var all = new Dictionary<int, PageRecording>(story.Recordings ?? NoRecordings) { [page] = recording };
        return story with { Recordings = all, UseRecordings = story.UseRecordings || Count(story) == 0 };
    }

    /// <summary>The story without the recording of <paramref name="page"/>; the last one removed turns the switch off and leaves no field.</summary>
    public static Story Without(Story story, int page)
    {
        if (story.Recordings is not { } existing || !existing.ContainsKey(page))
        {
            return story;
        }

        var all = existing.Where(e => e.Key != page).ToDictionary(e => e.Key, e => e.Value);
        return all.Count == 0 ? story with { Recordings = null, UseRecordings = false } : story with { Recordings = all };
    }

    /// <summary>The recordings that still match after the pages were edited to <paramref name="pages"/>: a page keeps its recording only when
    /// it is still there with the same text (a page moved, removed or reworded would otherwise read the wrong words); null when none is left.</summary>
    public static IReadOnlyDictionary<int, PageRecording>? Kept(Story original, IReadOnlyList<StoryPage> pages)
    {
        var kept = new Dictionary<int, PageRecording>();
        foreach (int page in RecordedPages(original))
        {
            if (page < pages.Count && string.Equals(original.Pages[page].Text, pages[page].Text, StringComparison.Ordinal))
            {
                kept[page] = original.Recordings![page];
            }
        }

        return kept.Count > 0 ? kept : null;
    }

    /// <summary>The recording files <paramref name="original"/> names that <paramref name="kept"/> no longer does: the ones to delete after an edit.</summary>
    public static IEnumerable<string> Dropped(Story original, IReadOnlyDictionary<int, PageRecording>? kept) =>
        RecordedPages(original).Where(p => kept is null || !kept.ContainsKey(p)).Select(FileNameOf);

    /// <summary>A recording's length as the Record screen writes it: minutes and two-digit seconds, <c>0:14</c>.</summary>
    public static string Clock(TimeSpan length) => $"{(int)length.TotalMinutes}:{length.Seconds:D2}";

    /// <summary>The length to store for a recording that ran <paramref name="length"/>: whole seconds, never below one.</summary>
    public static int WholeSeconds(TimeSpan length) => Math.Max(1, (int)Math.Round(length.TotalSeconds));

    /// <summary>Where each page starts in the story's joined text (<see cref="Story.Text"/>).</summary>
    public static IReadOnlyList<int> PageStarts(IReadOnlyList<StoryPage> pages)
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

    /// <summary>The page holding story index <paramref name="position"/>: the last page that starts at or before it (0 when there are no pages).</summary>
    public static int PageOfPosition(IReadOnlyList<int> starts, int position)
    {
        int page = 0;
        for (int i = 0; i < starts.Count && starts[i] <= position; i++)
        {
            page = i;
        }

        return page;
    }

    /// <summary>The pages of <paramref name="backed"/> (a story read from a backup) whose recording can be merged into <paramref name="phone"/> (Slice 230): the page is still
    /// there with the same text (a reworded page would read the wrong words, as in <see cref="Kept"/>) and <paramref name="hasAudio"/> says the backup carries the page's file.
    /// A page number the phone's story does not have is dropped.</summary>
    public static IReadOnlyList<int> MergeablePages(Story phone, Story backed, Func<string, bool> hasAudio) =>
        RecordedPages(backed)
            .Where(p => p < phone.Pages.Count && string.Equals(phone.Pages[p].Text, backed.Pages[p].Text, StringComparison.Ordinal) && hasAudio(FileNameOf(p)))
            .ToList();

    /// <summary><paramref name="phone"/> with the backup's recording for each of <paramref name="pages"/> (the backup's wins on a page both have). Pages, pictures, cast and every other
    /// field stay as they are. "Read with my voice" turns on when the phone had no recording yet (as <see cref="With"/> does for a first recording); otherwise the phone's choice stays.</summary>
    public static Story WithMerged(Story phone, Story backed, IReadOnlyList<int> pages)
    {
        if (pages.Count == 0)
        {
            return phone;
        }

        var all = new Dictionary<int, PageRecording>(phone.Recordings ?? NoRecordings);
        foreach (int page in pages)
        {
            all[page] = backed.Recordings![page];
        }

        return phone with { Recordings = all, UseRecordings = Count(phone) == 0 || phone.UseRecordings };
    }

    private static readonly IReadOnlyDictionary<int, PageRecording> NoRecordings = new Dictionary<int, PageRecording>();
}
