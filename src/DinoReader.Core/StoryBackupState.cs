using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DinoReader.Core;

/// <summary>
/// What a schema 2 backup keeps of one book (Slice 346, Decision 5): a stub story (no text, pictures, cast or translations; the pages that carry a recording hold only a hash of their
/// text, so a reworded page never gets the wrong voice), its recordings, its reading view. Pure, so the round trip is unit-tested.
/// </summary>
internal static class StoryBackupState
{
    public const string BooksFolder = "books/";

    public static string StateFile(string id) => $"{BooksFolder}{id}.json";

    public static string RecordingEntry(string id, string fileName) => $"{BooksFolder}{id}/{fileName}";

    /// <summary>The id a book is known by in a backup: its store id, else its own.</summary>
    public static string IdOf(Story story) => story.SourceId ?? story.Id;

    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>The stub saved for <paramref name="story"/>: page count and recordings kept, every page's text replaced by its hash (recorded pages) or nothing.</summary>
    public static Story Stub(Story story)
    {
        HashSet<int> recorded = StoryRecordings.Clips(story).Select(c => c.Key.Page).ToHashSet();
        return story with
        {
            Pages = [.. story.Pages.Select((page, index) => new StoryPage(recorded.Contains(index) ? Hash(page.Text) : string.Empty))],
            Cast = [],
            Translations = null,
            LanguageCasts = null,
            CoverFile = null,
            CoverPageIndex = null,
            SourceId = null,
            BackCover = null,
            TextInPictures = false,
        };
    }

    /// <summary>
    /// <paramref name="phone"/> carrying only the backup's recordings that still fit it: the page is there and its text hashes to what the backup recorded. This is the "backed up" story the
    /// recording merge compares with, so it never offers a clip for a page that was reworded.
    /// </summary>
    public static Story Backed(Story phone, Story stub)
    {
        bool Fits(int page) => page >= 0 && page < phone.Pages.Count && page < stub.Pages.Count && stub.Pages[page].Text == Hash(phone.Pages[page].Text);
        Dictionary<int, PageRecording>? pages = stub.Recordings?.Where(e => Fits(e.Key)).ToDictionary(e => e.Key, e => e.Value);
        Dictionary<StoryClipKey, PageRecording>? pieces = stub.PieceRecordings?.Where(e => Fits(e.Key.Page)).ToDictionary(e => e.Key, e => e.Value);
        return phone with
        {
            Recordings = pages is { Count: > 0 } ? pages : null,
            PieceRecordings = pieces is { Count: > 0 } ? pieces : null,
            UseRecordings = stub.UseRecordings,
        };
    }
}

/// <summary>
/// The few app settings a backup carries (Slice 346): a closed list of keys, each with the check its value must pass, so a hand-edited backup can set nothing else. The app reads and writes them
/// through its preferences; this holds only the rules.
/// </summary>
public static class BackupSettings
{
    public const string ReadingSpeedKey = "readingSpeed";
    public const string AnimalSoundsKey = "animalSounds";

    /// <summary>The keys a backup may carry.</summary>
    public static IReadOnlyList<string> Keys { get; } = [ReadingSpeedKey, AnimalSoundsKey];

    /// <summary>Only the allowed keys whose value is valid: a speed that parses (clamped later by <see cref="ReadingSpeed"/>), a switch that is exactly <c>true</c> or <c>false</c>.</summary>
    public static IReadOnlyDictionary<string, string> Sanitize(IReadOnlyDictionary<string, string>? raw)
    {
        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in Keys)
        {
            if (raw is not null && raw.TryGetValue(key, out string? value) && IsValid(key, value))
            {
                kept[key] = value;
            }
        }

        return kept;
    }

    public static string SpeedText(double speed) => speed.ToString("R", CultureInfo.InvariantCulture);

    public static bool TryParseSpeed(string? text, out double speed) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out speed) && double.IsFinite(speed);

    private static bool IsValid(string key, string value) => key switch
    {
        ReadingSpeedKey => TryParseSpeed(value, out _),
        AnimalSoundsKey => value is "true" or "false",
        _ => false,
    };
}
