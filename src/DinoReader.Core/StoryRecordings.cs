using System.Text.RegularExpressions;

namespace DinoReader.Core;

/// <summary>A parent's recording of one page (Slice 111, Decision 115): the language it was read in, which decides the text shown with it, and its length.</summary>
public sealed record PageRecording(string Language, int Seconds, string? File = null);

/// <summary>
/// Pure rules for the per-page parent recordings of a story (Slice 111): the file each page's recording lives in, which pages play one,
/// and which clips a new one replaces. A recording belongs to a page by its index and is a file named <c>rec-&lt;page number&gt;.m4a</c> in the
/// story folder; it is never part of a store upload and rides along in <c>.dino</c> packages and backups.
/// </summary>
public static class StoryRecordings
{
    public const string FilePrefix = "rec";
    public const string FileExtension = ".m4a";

    /// <summary>The prefix of a take being recorded (Slice 279): it lives in the cache folder until Next, Done, Back or Finish keeps it, so an app killed mid-take leaves the old clip and a stray take file.</summary>
    public const string TakePrefix = "take";

    private static readonly Regex FileNamePattern = new(@"^rec-[1-9][0-9]{0,3}(-[1-9][0-9]{0,3}-[1-9][0-9]{0,3})?(-[0-9a-f]{8})?\.m4a$", RegexOptions.CultureInvariant);
    private static readonly Regex TakeNamePattern = new(@"^take-[0-9a-f]{32}\.m4a$", RegexOptions.CultureInvariant);

    /// <summary>The file name of page <paramref name="page"/>'s whole-page recording (0-based index, the number in the name is 1-based).</summary>
    public static string FileNameOf(int page) => $"{FilePrefix}-{page + 1}{FileExtension}";

    /// <summary>The file name of a clip (Slice 279): <c>rec-&lt;page&gt;.m4a</c> for a whole-page one, else <c>rec-&lt;page&gt;-&lt;first&gt;-&lt;last&gt;.m4a</c>, every number counted from 1.</summary>
    public static string FileNameOf(StoryClipKey key) =>
        key.IsWholePage ? FileNameOf(key.Page) : $"{FilePrefix}-{key.Page + 1}-{key.First + 1}-{key.Last + 1}{FileExtension}";

    /// <summary>Whether <paramref name="fileName"/> is a name <see cref="FileNameOf(int)"/> or <see cref="FileNameOf(StoryClipKey)"/> makes, the only recording files the library may store or delete.</summary>
    public static bool IsRecordingFileName(string? fileName) => fileName is not null && FileNamePattern.IsMatch(fileName);

    /// <summary>A fresh file name for a clip being committed (Slice 279): the clip's usual name plus eight hex digits, so a commit never overwrites a file; the story names the file per clip (<see cref="PageRecording.File"/>).</summary>
    public static string NewFileName(StoryClipKey key) => $"{Path.GetFileNameWithoutExtension(FileNameOf(key))}-{Guid.NewGuid().ToString("N")[..8]}{FileExtension}";

    /// <summary>Whether <paramref name="name"/> is a name <see cref="NewFileName"/> makes for <paramref name="key"/>: a story only names a clip's file so, never another clip's file.</summary>
    public static bool IsFreshFileNameOf(string name, StoryClipKey key)
    {
        string stem = Path.GetFileNameWithoutExtension(FileNameOf(key));
        return IsRecordingFileName(name) && name.StartsWith(stem + "-", StringComparison.Ordinal) && name.Length == stem.Length + 1 + 8 + FileExtension.Length;
    }

    /// <summary>The file the clip <paramref name="key"/> of <paramref name="story"/> lives in: the name the story stores for it, else the clip's usual name (older books).</summary>
    public static string FileOf(Story story, StoryClipKey key) => FileOf(ClipOf(story, key), key);

    /// <summary>As <see cref="FileOf(Story, StoryClipKey)"/> for a recording already at hand.</summary>
    public static string FileOf(PageRecording? recording, StoryClipKey key) =>
        recording?.File is { } name && IsRecordingFileName(name) ? name : FileNameOf(key);

    /// <summary>As <see cref="FileOf(Story, StoryClipKey)"/> for a place that reads the file: the stored name only when <paramref name="exists"/> says that file is in the book's folder, else the clip's usual name
    /// (a story whose stored name and clip disagree after a merge keeps pointing at the file it has).</summary>
    public static string FileOf(Story story, StoryClipKey key, Func<string, bool> exists) =>
        ClipOf(story, key)?.File is { } name && IsRecordingFileName(name) && exists(name) ? name : FileNameOf(key);

    /// <summary>The files of every clip that shares a sentence with <paramref name="key"/>, a clip with exactly that key included: the files a new clip of <paramref name="key"/> replaces.</summary>
    public static IReadOnlyList<string> ReplacedFiles(Story story, StoryClipKey key) =>
        Clips(story).Where(c => c.Key.Overlaps(key)).Select(c => FileOf(c.Value, c.Key)).ToList();

    /// <summary>Whether a clip that <paramref name="key"/> replaces also covers sentences outside it (a wider whole-page or differently cut clip): they lose their recording too, which the Record screen says.</summary>
    public static bool ReplacesBeyond(Story story, StoryClipKey key) =>
        Clips(story).Any(c => c.Key.Overlaps(key) && (c.Key.First < key.First || c.Key.Last > key.Last));

    /// <summary>A fresh name for a take in the cache folder (Slice 279).</summary>
    public static string NewTakeFileName() => $"{TakePrefix}-{Guid.NewGuid():N}{FileExtension}";

    /// <summary>Whether <paramref name="fileName"/> is a take file left in the cache folder; the Record screen deletes these when it opens.</summary>
    public static bool IsTakeFileName(string? fileName) => fileName is not null && TakeNamePattern.IsMatch(fileName);

    /// <summary>Every clip the story holds, whole-page ones first within a page, in page order: (key, recording).</summary>
    public static IEnumerable<KeyValuePair<StoryClipKey, PageRecording>> Clips(Story story)
    {
        IEnumerable<KeyValuePair<StoryClipKey, PageRecording>> whole = RecordedPages(story)
            .Select(p => new KeyValuePair<StoryClipKey, PageRecording>(StoryClipKey.OfPage(p), story.Recordings![p]));
        IEnumerable<KeyValuePair<StoryClipKey, PageRecording>> pieces = (story.PieceRecordings ?? NoPieces).Where(e => e.Key.IsValid && e.Key.Page < story.Pages.Count);
        return whole.Concat(pieces).OrderBy(c => c.Key.Page).ThenBy(c => c.Key.First);
    }

    /// <summary>The recording that covers exactly <paramref name="key"/>, or null.</summary>
    public static PageRecording? ClipOf(Story story, StoryClipKey key) =>
        key.IsWholePage ? Of(story, key.Page) : story.PieceRecordings is { } all && all.TryGetValue(key, out PageRecording? recording) && key.IsValid && key.Page < story.Pages.Count ? recording : null;

    /// <summary>The clips already in the story that share a sentence with <paramref name="key"/>, other than a clip with exactly that key.</summary>
    public static IReadOnlyList<StoryClipKey> Overlapping(Story story, StoryClipKey key) =>
        Clips(story).Select(c => c.Key).Where(k => k != key && k.Overlaps(key)).ToList();

    /// <summary>The story with <paramref name="recording"/> for <paramref name="key"/>. Any other clip that shares a sentence with it goes (a page cannot be read from two clips at once); the first clip of a book turns "Read with my voice" on.</summary>
    public static Story WithClip(Story story, StoryClipKey key, PageRecording recording)
    {
        bool first = Count(story) == 0;
        Story cleared = Overlapping(story, key).Aggregate(story, WithoutClip);
        if (key.IsWholePage)
        {
            Story withPage = With(cleared, key.Page, recording);
            return withPage with { UseRecordings = story.UseRecordings || first };
        }

        var pieces = new Dictionary<StoryClipKey, PageRecording>(cleared.PieceRecordings ?? NoPieces) { [key] = recording };
        return cleared with { PieceRecordings = pieces, UseRecordings = story.UseRecordings || first };
    }

    /// <summary>The story without the clip with exactly <paramref name="key"/>; the last clip removed turns the switch off and leaves no field.</summary>
    public static Story WithoutClip(Story story, StoryClipKey key)
    {
        if (key.IsWholePage)
        {
            return Without(story, key.Page);
        }

        if (story.PieceRecordings is not { } existing || !existing.ContainsKey(key))
        {
            return story;
        }

        var pieces = existing.Where(e => e.Key != key).ToDictionary(e => e.Key, e => e.Value);
        Story updated = story with { PieceRecordings = pieces.Count == 0 ? null : pieces };
        return Count(updated) == 0 ? updated with { UseRecordings = false } : updated;
    }

    /// <summary>The clip of page <paramref name="page"/> that covers sentence <paramref name="sentence"/> (counted on the page), or null.</summary>
    public static StoryClipKey? ClipCovering(Story story, int page, int sentence) =>
        Clips(story).Select(c => c.Key).Where(k => k.Page == page && k.First <= sentence && sentence <= k.Last).Select(k => (StoryClipKey?)k).FirstOrDefault();

    /// <summary>Whether the reader plays the clip <paramref name="key"/>: the book's "Read with my voice" is on, the clip exists, and it was made in the story's current language.</summary>
    public static bool PlaysClip(Story story, StoryClipKey key) =>
        story.UseRecordings && ClipOf(story, key) is { } recording && UiLanguage.Resolve(recording.Language) == UiLanguage.Resolve(story.Language);

    /// <summary>Every clip the reader plays, in page order.</summary>
    public static IReadOnlyList<StoryClipKey> PlayingClips(Story story) =>
        Clips(story).Select(c => c.Key).Where(k => PlaysClip(story, k)).ToList();

    /// <summary>The recording of <paramref name="page"/>, or null.</summary>
    public static PageRecording? Of(Story story, int page) =>
        story.Recordings is { } all && page >= 0 && page < story.Pages.Count && all.TryGetValue(page, out PageRecording? recording) ? recording : null;

    /// <summary>How many clips the book holds (whole-page ones and pieces).</summary>
    public static int Count(Story story) => Clips(story).Count();

    /// <summary>The indexes of the pages that have a whole-page recording (made before Slice 279 or in whole-page mode), in order. A page recorded in pieces is not listed: see <see cref="Clips"/>.</summary>
    public static IEnumerable<int> RecordedPages(Story story) =>
        (story.Recordings?.Keys ?? Enumerable.Empty<int>()).Where(p => p >= 0 && p < story.Pages.Count).Order();

    /// <summary>The recording files the story names, in page order.</summary>
    public static IEnumerable<string> Files(Story story) => Clips(story).Select(c => FileOf(c.Value, c.Key));

    /// <summary>Whether the reader plays page <paramref name="page"/>'s recording: the book's "Read with my voice" is on, the page has one, and it was
    /// made in the story's current language (a recording keeps the language it was made in, so a story whose language was edited reads that page generated again).</summary>
    public static bool Plays(Story story, int page) =>
        story.UseRecordings && Of(story, page) is { } recording && UiLanguage.Resolve(recording.Language) == UiLanguage.Resolve(story.Language);

    /// <summary>The pages whose every sentence is covered by clips the reader plays: they show the source text even while the rest of the book shows a translation (Decision 115).
    /// A page with only some of its sentences recorded (one character's line, one part) is not listed: it reads translated, with the pre-made audio (Slice 279).
    /// <paramref name="sentencesPerPage"/> is how many sentences each page holds.</summary>
    public static IReadOnlySet<int> PagesShownInOwnLanguage(Story story, IReadOnlyList<int> sentencesPerPage)
    {
        IReadOnlyList<StoryClipKey> playing = PlayingClips(story);
        return playing.Select(k => k.Page).Distinct()
            .Where(page => page < sentencesPerPage.Count && CoversPage(playing, page, sentencesPerPage[page]))
            .ToHashSet();
    }

    // Only clips that fit the page count, the same rule the reader uses to play one (a clip naming sentences the page lacks plays nothing).
    private static bool CoversPage(IReadOnlyList<StoryClipKey> playing, int page, int sentenceCount)
    {
        List<StoryClipKey> fitting = playing.Where(k => k.Page == page && k.FitsPage(sentenceCount)).ToList();
        return fitting.Any(k => k.IsWholePage) // a whole-page clip covers its page even when the page holds no sentence
            || (sentenceCount > 0 && Enumerable.Range(0, sentenceCount).All(s => fitting.Any(k => k.First <= s && s <= k.Last)));
    }

    /// <summary>How many sentences each page of <paramref name="pages"/> holds, a sentence belonging to the page its first character is on.</summary>
    public static IReadOnlyList<int> SentencesPerPage(IReadOnlyList<StoryPage> pages, IReadOnlyList<SentenceSpan> sentences)
    {
        IReadOnlyList<int> starts = PageStarts(pages);
        var counts = new int[pages.Count];
        foreach (SentenceSpan sentence in sentences.Where(_ => pages.Count > 0))
        {
            counts[PageOfPosition(starts, sentence.Start)]++;
        }

        return counts;
    }

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
        Story updated = story with { Recordings = all.Count == 0 ? null : all };
        return Count(updated) == 0 ? updated with { UseRecordings = false } : updated;
    }

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
            .Where(p => p < phone.Pages.Count && string.Equals(phone.Pages[p].Text, backed.Pages[p].Text, StringComparison.Ordinal) && hasAudio(FileOf(backed, StoryClipKey.OfPage(p))))
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

    /// <summary>As <see cref="MergeablePages"/> for every clip of <paramref name="backed"/>, pieces included (Slice 279): the page is still there with the same text, <paramref name="hasAudio"/> says the backup
    /// carries the clip's file, and no different clip of the phone shares a sentence with it (a merge never drops what the phone recorded).</summary>
    public static IReadOnlyList<StoryClipKey> MergeableClips(Story phone, Story backed, Func<string, bool> hasAudio) =>
        Clips(backed).Select(c => c.Key)
            .Where(k => k.Page < phone.Pages.Count && string.Equals(phone.Pages[k.Page].Text, backed.Pages[k.Page].Text, StringComparison.Ordinal)
                && hasAudio(FileOf(backed, k)) && Overlapping(phone, k).Count == 0)
            .ToList();

    /// <summary>As <see cref="WithMerged"/> for clips: <paramref name="phone"/> with the backup's recording for each of <paramref name="keys"/>.</summary>
    public static Story WithMergedClips(Story phone, Story backed, IReadOnlyList<StoryClipKey> keys) =>
        keys.Aggregate(phone, (story, key) => WithClip(story, key, ClipOf(backed, key)!));

    private static readonly IReadOnlyDictionary<int, PageRecording> NoRecordings = new Dictionary<int, PageRecording>();
    private static readonly IReadOnlyDictionary<StoryClipKey, PageRecording> NoPieces = new Dictionary<StoryClipKey, PageRecording>();
}
