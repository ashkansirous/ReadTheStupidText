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

    // rec-[<lang>-]<page>[-<first>-<last>][-<8 hex>].m4a: the language part (Slice 286) is a short lower-case code and is never a number, so the older names still match.
    private static readonly Regex FileNamePattern = new(@"^rec-([a-z]{2,3}-)?[1-9][0-9]{0,3}(-[1-9][0-9]{0,3}-[1-9][0-9]{0,3})?(-[0-9a-f]{8})?\.m4a$", RegexOptions.CultureInvariant);
    private static readonly Regex TakeNamePattern = new(@"^take-[0-9a-f]{32}\.m4a$", RegexOptions.CultureInvariant);

    /// <summary>The file name of page <paramref name="page"/>'s whole-page recording (0-based index, the number in the name is 1-based).</summary>
    public static string FileNameOf(int page) => $"{FilePrefix}-{page + 1}{FileExtension}";

    /// <summary>The file name of a clip (Slice 279): <c>rec-&lt;lang&gt;-&lt;page&gt;.m4a</c> for a whole-page one, else <c>rec-&lt;lang&gt;-&lt;page&gt;-&lt;first&gt;-&lt;last&gt;.m4a</c> (Slice 286: the language leads;
    /// a key with no language keeps the older <c>rec-&lt;page&gt;.m4a</c> and <c>rec-&lt;page&gt;-&lt;first&gt;-&lt;last&gt;.m4a</c>), every number counted from 1. Two languages never share a name.</summary>
    public static string FileNameOf(StoryClipKey key) =>
        key.IsWholePage ? $"{FilePrefix}-{LanguagePart(key)}{key.Page + 1}{FileExtension}" : $"{FilePrefix}-{LanguagePart(key)}{key.Page + 1}-{key.First + 1}-{key.Last + 1}{FileExtension}";

    /// <summary>The file a clip lives in when its story stores no name for it: a piece's usual name, but a whole-page clip's pre-Slice-286 name (<c>rec-&lt;page&gt;.m4a</c>), the file the older books
    /// recorded into. A whole-page clip made since always stores its own fresh name.</summary>
    public static string DefaultFileNameOf(StoryClipKey key) => key.IsWholePage ? FileNameOf(key.Page) : FileNameOf(key);

    /// <summary>The name a clip made before Slice 286 had for <paramref name="key"/> (no language in it): migrated clips keep pointing at it.</summary>
    public static string LegacyFileNameOf(StoryClipKey key) => FileNameOf(key with { Language = string.Empty });

    private static string LanguagePart(StoryClipKey key) => key.Language.Length == 0 ? string.Empty : key.Language + "-";

    /// <summary>Whether <paramref name="name"/> is the name a clip of <paramref name="key"/> had before Slice 286 (usual or with eight hex digits): a migrated clip may keep it.</summary>
    public static bool IsLegacyFileNameOf(string name, StoryClipKey key)
    {
        string stem = Path.GetFileNameWithoutExtension(LegacyFileNameOf(key));
        return IsRecordingFileName(name) && (name == stem + FileExtension || (name.StartsWith(stem + "-", StringComparison.Ordinal) && name.Length == stem.Length + 1 + 8 + FileExtension.Length));
    }

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
        recording?.File is { } name && IsRecordingFileName(name) ? name : DefaultFileNameOf(key);

    /// <summary>As <see cref="FileOf(Story, StoryClipKey)"/> for a place that reads the file: the stored name only when <paramref name="exists"/> says that file is in the book's folder, else the clip's usual name
    /// (a story whose stored name and clip disagree after a merge keeps pointing at the file it has).</summary>
    public static string FileOf(Story story, StoryClipKey key, Func<string, bool> exists) =>
        ClipOf(story, key)?.File is { } name && IsRecordingFileName(name) && exists(name) ? name : DefaultFileNameOf(key);

    /// <summary>The files of every clip that shares a sentence with <paramref name="key"/>, a clip with exactly that key included: the files a new clip of <paramref name="key"/> replaces.
    /// A file that a clip which stays (any language) also names is never listed: no recording file is deleted while another clip still references it.</summary>
    public static IReadOnlyList<string> ReplacedFiles(Story story, StoryClipKey key)
    {
        HashSet<string> stays = Clips(story).Where(c => !c.Key.Overlaps(key)).Select(c => FileOf(c.Value, c.Key)).ToHashSet(StringComparer.Ordinal);
        return Clips(story).Where(c => c.Key.Overlaps(key)).Select(c => FileOf(c.Value, c.Key)).Where(f => !stays.Contains(f)).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>Whether any clip of <paramref name="story"/> (any language) names <paramref name="fileName"/>: a file that still is referenced is never deleted.</summary>
    public static bool IsReferenced(Story story, string fileName) => Files(story).Contains(fileName, StringComparer.Ordinal);

    /// <summary>Whether a clip is still waiting for the main language to be known (<see cref="StoryJson.PendingLanguage"/>): the book is read but not yet migrated.</summary>
    public static bool HasPendingClips(Story story) => Clips(story).Any(c => c.Key.Language.Length == 0);

    /// <summary>The story with every clip that names a file an earlier clip (page order, whole-page first) already names removed: a hand edit or a restore can name one file twice, and a re-record or
    /// delete of one clip would then destroy the other's audio.</summary>
    public static Story WithoutSharedFiles(Story story)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        IEnumerable<StoryClipKey> shared = Clips(story).Where(c => !seen.Add(FileOf(c.Value, c.Key))).Select(c => c.Key).ToList();
        return shared.Aggregate(story, (s, key) => WithoutClip(s, key));
    }

    /// <summary>Whether a clip that <paramref name="key"/> replaces also covers sentences outside it (a wider whole-page or differently cut clip): they lose their recording too, which the Record screen says.</summary>
    public static bool ReplacesBeyond(Story story, StoryClipKey key) =>
        Clips(story).Any(c => c.Key.Overlaps(key) && (c.Key.First < key.First || c.Key.Last > key.Last));

    /// <summary>A fresh name for a take in the cache folder (Slice 279).</summary>
    public static string NewTakeFileName() => $"{TakePrefix}-{Guid.NewGuid():N}{FileExtension}";

    /// <summary>Whether <paramref name="fileName"/> is a take file left in the cache folder; the Record screen deletes these when it opens.</summary>
    public static bool IsTakeFileName(string? fileName) => fileName is not null && TakeNamePattern.IsMatch(fileName);

    /// <summary>Every clip the story holds (every language), whole-page ones first within a page, in page order: (key, recording). A whole-page clip's key carries its recording's language.</summary>
    public static IEnumerable<KeyValuePair<StoryClipKey, PageRecording>> Clips(Story story)
    {
        IEnumerable<KeyValuePair<StoryClipKey, PageRecording>> whole = RecordedPages(story)
            .Select(p => new KeyValuePair<StoryClipKey, PageRecording>(StoryClipKey.OfPage(p, ClipLanguageOf(story.Recordings![p])), story.Recordings![p]));
        IEnumerable<KeyValuePair<StoryClipKey, PageRecording>> pieces = (story.PieceRecordings ?? NoPieces).Where(e => e.Key.IsValid && e.Key.Page < story.Pages.Count);
        return whole.Concat(pieces).OrderBy(c => c.Key.Page).ThenBy(c => c.Key.First).ThenBy(c => c.Key.Language, StringComparer.Ordinal);
    }

    // A recording that waits for its language (StoryJson.PendingLanguage, empty) stays empty; every other one is its resolved code.
    private static string ClipLanguageOf(PageRecording recording) =>
        recording.Language.Length == 0 ? StoryJson.PendingLanguage : UiLanguage.Resolve(recording.Language);

    /// <summary>The clips of one story language (Slice 286): what the Record screen lists, counts and plays back for the language picked there.</summary>
    public static IEnumerable<KeyValuePair<StoryClipKey, PageRecording>> Clips(Story story, string language) =>
        Clips(story).Where(c => c.Key.Language == UiLanguage.Resolve(language));

    /// <summary>The recording that covers exactly <paramref name="key"/>, or null. A whole-page key with no language matches the page's recording whatever its language.</summary>
    public static PageRecording? ClipOf(Story story, StoryClipKey key)
    {
        if (key.IsWholePage && Of(story, key.Page) is { } whole && (key.Language.Length == 0 || ClipLanguageOf(whole) == key.Language))
        {
            return whole;
        }

        // A whole-page clip whose page slot holds another language's clip is kept with the pieces (Slice 286: whole-page clips are keyed by language too).
        return story.PieceRecordings is { } all && key.IsValid && key.Page < story.Pages.Count && all.TryGetValue(key, out PageRecording? recording) ? recording : null;
    }

    /// <summary>The clips already in the story that share a sentence with <paramref name="key"/>, other than a clip with exactly that key.</summary>
    public static IReadOnlyList<StoryClipKey> Overlapping(Story story, StoryClipKey key) =>
        Clips(story).Select(c => c.Key).Where(k => k != key && k.Overlaps(key)).ToList();

    /// <summary>The story with <paramref name="recording"/> for <paramref name="key"/>. Any other clip that shares a sentence with it goes (a page cannot be read from two clips at once); the first clip of a book turns "Read with my voice" on.</summary>
    public static Story WithClip(Story story, StoryClipKey key, PageRecording recording)
    {
        string language = key.Language.Length == 0 ? UiLanguage.Resolve(recording.Language) : key.Language;
        key = key.In(language); // the key and the recording always name the same language (Slice 286)
        recording = recording with { Language = language };
        bool first = Count(story) == 0;
        Story cleared = Overlapping(story, key).Aggregate(story, WithoutClip);
        if (key.IsWholePage && (Of(cleared, key.Page) is not { } slot || ClipLanguageOf(slot) == key.Language))
        {
            Story withPage = With(WithoutClip(cleared, key), key.Page, recording);
            return withPage with { UseRecordings = story.UseRecordings || first };
        }

        if (key.IsWholePage && recording.File is null)
        {
            recording = recording with { File = FileNameOf(key) }; // never the page slot's pre-language name, which another language's clip may hold
        }

        var pieces = new Dictionary<StoryClipKey, PageRecording>(cleared.PieceRecordings ?? NoPieces) { [key] = recording };
        return cleared with { PieceRecordings = pieces, UseRecordings = story.UseRecordings || first };
    }

    /// <summary>The story without the clip with exactly <paramref name="key"/>; the last clip removed turns the switch off and leaves no field.</summary>
    public static Story WithoutClip(Story story, StoryClipKey key)
    {
        if (key.IsWholePage && Of(story, key.Page) is { } slot && (key.Language.Length == 0 || ClipLanguageOf(slot) == key.Language))
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

    /// <summary>The clip in <paramref name="language"/> of page <paramref name="page"/> that covers sentence <paramref name="sentence"/> (counted on the page), or null.</summary>
    public static StoryClipKey? ClipCovering(Story story, int page, int sentence, string language) =>
        Clips(story, language).Select(c => c.Key).Where(k => k.Page == page && k.First <= sentence && sentence <= k.Last).Select(k => (StoryClipKey?)k).FirstOrDefault();

    /// <summary>Whether the reader plays the clip <paramref name="key"/> while reading in <paramref name="readingLanguage"/>: the book's "Read with my voice" is on, the clip exists, and it was made in that language (Slice 286).</summary>
    public static bool PlaysClip(Story story, StoryClipKey key, string readingLanguage) =>
        story.UseRecordings && ClipOf(story, key) is not null && key.Language == UiLanguage.Resolve(readingLanguage);

    /// <summary>Every clip the reader plays while reading in <paramref name="readingLanguage"/>, in page order.</summary>
    public static IReadOnlyList<StoryClipKey> PlayingClips(Story story, string readingLanguage) =>
        Clips(story).Select(c => c.Key).Where(k => PlaysClip(story, k, readingLanguage)).ToList();

    /// <summary>The recording of <paramref name="page"/>, or null.</summary>
    public static PageRecording? Of(Story story, int page) =>
        story.Recordings is { } all && page >= 0 && page < story.Pages.Count && all.TryGetValue(page, out PageRecording? recording) ? recording : null;

    /// <summary>How many clips the book holds (whole-page ones and pieces, every language).</summary>
    public static int Count(Story story) => Clips(story).Count();

    /// <summary>How many clips the book holds in <paramref name="language"/> (Slice 286).</summary>
    public static int Count(Story story, string language) => Clips(story, language).Count();

    /// <summary>The number of distinct pages that have a clip in <paramref name="language"/> (the Record intro's "8 of 12 pages").</summary>
    public static int PagesRecorded(Story story, string language) => Clips(story, language).Select(c => c.Key.Page).Distinct().Count();

    /// <summary>The indexes of the pages that have a whole-page recording (made before Slice 279 or in whole-page mode), in order. A page recorded in pieces is not listed: see <see cref="Clips"/>.</summary>
    public static IEnumerable<int> RecordedPages(Story story) =>
        (story.Recordings?.Keys ?? Enumerable.Empty<int>()).Where(p => p >= 0 && p < story.Pages.Count).Order();

    /// <summary>The recording files the story names, in page order.</summary>
    public static IEnumerable<string> Files(Story story) => Clips(story).Select(c => FileOf(c.Value, c.Key));

    // Slice 286: a clip is read in its own language and the reading screen shows that language's text, so no page keeps the source text for a recording any more.

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
            .Where(p => p < phone.Pages.Count && string.Equals(phone.Pages[p].Text, backed.Pages[p].Text, StringComparison.Ordinal) && hasAudio(FileOf(backed, StoryClipKey.OfPage(p, StoryClipKey.NoLanguage))))
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
                && hasAudio(FileOf(backed, k)) && Overlapping(phone, k).Count == 0 && !ClashesWithOtherLanguage(phone, backed, k))
            .ToList();

    // Slice 286: a backup clip never lands on the file, or the whole-page slot, of a phone clip in another language (a migrated clip keeps its pre-language file name, so two
    // languages can name one file; placing the backup's audio there would destroy the phone's take).
    private static bool ClashesWithOtherLanguage(Story phone, Story backed, StoryClipKey key)
    {
        string file = FileOf(backed, key);
        bool sharesFile = Clips(phone).Any(c => c.Key != key && FileOf(c.Value, c.Key) == file);
        bool takesOtherWholePage = key.IsWholePage && Of(phone, key.Page) is { } whole && UiLanguage.Resolve(whole.Language) != key.Language;
        return sharesFile || takesOtherWholePage;
    }

    /// <summary>As <see cref="WithMerged"/> for clips: <paramref name="phone"/> with the backup's recording for each of <paramref name="keys"/>.</summary>
    public static Story WithMergedClips(Story phone, Story backed, IReadOnlyList<StoryClipKey> keys) =>
        keys.Aggregate(phone, (story, key) => WithClip(story, key, ClipOf(backed, key)!));

    private static readonly IReadOnlyDictionary<int, PageRecording> NoRecordings = new Dictionary<int, PageRecording>();
    private static readonly IReadOnlyDictionary<StoryClipKey, PageRecording> NoPieces = new Dictionary<StoryClipKey, PageRecording>();
}
