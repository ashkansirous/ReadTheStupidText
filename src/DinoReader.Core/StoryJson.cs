using System.Text.Json;
using System.Text.Json.Serialization;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>Raised when a story file is not valid story JSON or comes from a newer schema.</summary>
public sealed class StoryFormatException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// The versioned <c>story.json</c> format. Reading ignores fields it does not know and fills
/// missing optional ones with defaults, so a later app version can add fields without breaking
/// this one; a file with a newer <see cref="CurrentVersion"/> is refused rather than half-read.
/// </summary>
public static class StoryJson
{
    public const int CurrentVersion = 1;

    // How far a stored line box may overshoot the page edge and still be pulled back onto it (rounding only).
    private const double BoxRoundingTolerance = 0.001;
    private const string NoAnimal = "none";

    /// <summary>The narrator written as <c>NarratorVoiceId</c> for a story in the given language, so older app builds (which require the field)
    /// still open new packages and backups. The app sets it to the language's current narrator (Slice 109); reading never uses it.</summary>
    public static Func<string, string> LegacyNarratorOf { get; set; } = _ => SupertonicVoiceTable.Default.Id;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(Story story) => JsonSerializer.Serialize(ToFile(story), Options);

    public static Story Deserialize(string json)
    {
        try
        {
            StoryFile file = JsonSerializer.Deserialize<StoryFile>(json, Options)
                ?? throw new StoryFormatException("The story file is empty.");
            return FromFile(file);
        }
        catch (JsonException ex)
        {
            throw new StoryFormatException("The story file is not valid.", ex);
        }
    }

    private static StoryFile ToFile(Story s) => new()
    {
        Version = CurrentVersion,
        Id = s.Id,
        Title = s.Title,
        Language = s.Language,
        Created = s.Created,
        CoverStyle = s.CoverStyle,
        CoverColourIndex = s.CoverColourIndex,
        CoverFile = s.CoverFile,
        CoverPageIndex = s.CoverPageIndex,
        NarratorVoiceId = LegacyNarratorOf(s.Language),
        PreferredMode = s.PreferredMode is { } mode ? JsonSerializer.SerializeToElement(mode.ToString()) : null,
        Pages = s.Pages.Select(p => new PageFile { Text = p.Text, ImageFile = p.ImageFile, Lines = ToLineFiles(p.Lines), Regions = ToRegionFiles(p.Regions) }).ToList(),
        Cast = ToCastFiles(s.Cast),
        Translate = s.TranslateToMyLanguage,
        ShowOriginal = s.ShowOriginalText ? true : null,
        Recordings = ToRecordingFiles(s.Recordings, s.Pages.Count),
        UseRecordings = s.UseRecordings && s.Recordings is { Count: > 0 } ? true : null,
        AnimalSounds = s.AnimalSoundsOn ? null : false,
        SourceId = s.SourceId,
        Translations = ToTranslationFiles(s.Translations),
        LanguageCasts = ToLanguageCastFiles(s.LanguageCasts),
    };

    private static Story FromFile(StoryFile f)
    {
        if (f.Version > CurrentVersion || !StoryIds.IsValid(f.Id))
        {
            throw new StoryFormatException("The story file is from a newer version or has no valid id.");
        }

        RequireFields(f);
        return new Story(
            f.Id, f.Title, f.Language, f.Created,
            f.Pages.Select(p => ToPage(p)).ToList(),
            f.Cast.Select(ToMember).ToList(),
            f.CoverStyle, f.CoverColourIndex, f.NarratorVoiceId ?? string.Empty, ParseMode(f.PreferredMode, f.WordsOverPictures), f.Translate,
            f.CoverFile, f.CoverPageIndex, StoryIds.IsValid(f.SourceId) ? f.SourceId : null, CleanTranslations(f.Translations),
            CleanLanguageCasts(f.LanguageCasts, f.Language), f.ShowOriginal == true,
            CleanRecordings(f.Recordings, f.Pages.Count, f.Language), f.UseRecordings == true, f.AnimalSounds != false);
    }

    private static List<RecordingFile>? ToRecordingFiles(IReadOnlyDictionary<int, PageRecording>? recordings, int pageCount)
    {
        List<RecordingFile> files = (recordings ?? new Dictionary<int, PageRecording>())
            .Where(r => r.Key >= 0 && r.Key < pageCount).OrderBy(r => r.Key)
            .Select(r => new RecordingFile { Page = r.Key, Language = r.Value.Language, Seconds = r.Value.Seconds }).ToList();
        return files.Count == 0 ? null : files;
    }

    // A recording for a page the story does not have, or a second one for a page, is dropped (a hand edit); a missing language reads as the story's own,
    // a negative length as zero. No recording left reads as none, and "Read with my voice" with none is off.
    private static IReadOnlyDictionary<int, PageRecording>? CleanRecordings(List<RecordingFile>? recordings, int pageCount, string storyLanguage)
    {
        var kept = new Dictionary<int, PageRecording>();
        foreach (RecordingFile? r in recordings ?? [])
        {
            if (r is not null && r.Page >= 0 && r.Page < pageCount && !kept.ContainsKey(r.Page))
            {
                kept[r.Page] = new PageRecording(string.IsNullOrWhiteSpace(r.Language) ? storyLanguage : r.Language, Math.Max(0, r.Seconds));
            }
        }

        return kept.Count > 0 ? kept : null;
    }

    private static List<CastFile> ToCastFiles(IReadOnlyList<CastMember> cast) =>
        cast.Select(c => new CastFile
        {
            CharacterId = c.CharacterId, Name = c.Name, Gender = c.Gender, VoiceId = c.VoiceId, Edited = c.Edited,
            Flavour = c.Flavour?.ToString(), Animal = AnimalText(c),
        }).ToList();

    // The animal is a kind's name, or "none" for "not an animal"; absent means guess. A value this build does not know (a newer app's) reads as absent.
    private static string? AnimalText(CastMember member) => member.NotAnimal ? NoAnimal : member.Animal?.ToString();

    private static (AnimalKind? Animal, bool NotAnimal) ParseAnimal(string? text)
    {
        if (string.Equals(text, NoAnimal, StringComparison.OrdinalIgnoreCase))
        {
            return (null, true);
        }

        return Enum.TryParse(text, ignoreCase: true, out AnimalKind kind) && Enum.IsDefined(kind) ? (kind, false) : (null, false);
    }

    // The flavour is a name in the file; one this build does not know (a newer app's) reads as no pick, so the guess applies.
    private static CastMember ToMember(CastFile c)
    {
        (AnimalKind? animal, bool notAnimal) = ParseAnimal(c.Animal);
        return new CastMember(c.CharacterId, c.Name, c.Gender, c.VoiceId, c.Edited, ParseFlavour(c.Flavour), animal, notAnimal);
    }

    private static VoiceFlavour? ParseFlavour(string? name) =>
        Enum.TryParse(name, ignoreCase: true, out VoiceFlavour flavour) && VoiceFlavours.Assignable.Contains(flavour) ? flavour : null;

    private static Dictionary<string, List<CastFile>>? ToLanguageCastFiles(IReadOnlyDictionary<string, IReadOnlyList<CastMember>>? casts) =>
        casts is null || casts.Count == 0 ? null : casts.ToDictionary(c => c.Key, c => ToCastFiles(c.Value));

    // A language with a damaged member (a null field, a hand edit) is dropped, not the whole story; so is one that names the story's own
    // language (its cast is the main one). Two keys naming one language ("fa" and "fa-IR") keep the first.
    private static IReadOnlyDictionary<string, IReadOnlyList<CastMember>>? CleanLanguageCasts(Dictionary<string, List<CastFile>>? casts, string ownLanguage)
    {
        var kept = new Dictionary<string, IReadOnlyList<CastMember>>();
        foreach (var (language, members) in casts ?? [])
        {
            string key = BundledTranslations.Key(language);
            bool usable = !string.IsNullOrWhiteSpace(language) && UiLanguage.Resolve(language) != UiLanguage.Resolve(ownLanguage) && !kept.ContainsKey(key)
                && members is not null && members.All(c => c is { CharacterId: not null, Name: not null, VoiceId: not null });
            if (usable)
            {
                kept[key] = members!.Select(ToMember).ToList();
            }
        }

        return kept.Count > 0 ? kept : null;
    }

    private static StoryPage ToPage(PageFile p)
    {
        List<PageLine>? lines = FromLineFiles(p.Lines, out int[] newIndexOf);
        return new StoryPage(p.Text, p.ImageFile, lines, FromRegionFiles(p.Regions, newIndexOf));
    }

    private static List<RegionFile>? ToRegionFiles(IReadOnlyList<PageRegion>? regions) =>
        regions is null || regions.Count == 0
            ? null
            : regions.Select(r => new RegionFile
            {
                X = r.Box.X, Y = r.Box.Y, Width = r.Box.Width, Height = r.Box.Height,
                LineIndexes = r.LineIndexes.ToList(), Background = r.Background, Included = r.Included,
            }).ToList();

    // A damaged region (no lines, a line index the file never had, a box off the page) is dropped, and the page then
    // follows the heuristic for what is left; null when no region survives (an older story has none). The indexes are the
    // file's own; newIndexOf maps each to the line's place once damaged lines were dropped (-1 for a dropped line, which leaves
    // the region), so a region keeps naming the lines it named.
    private static List<PageRegion>? FromRegionFiles(List<RegionFile>? regions, int[] newIndexOf)
    {
        var kept = new List<PageRegion>();
        foreach (RegionFile? r in regions ?? [])
        {
            if (r is { LineIndexes: { Count: > 0 } indexes } && indexes.All(i => i >= 0 && i < newIndexOf.Length)
                && ClampToPage(r.X, r.Y, r.Width, r.Height) is { } box
                && indexes.Select(i => newIndexOf[i]).Where(i => i >= 0).ToList() is { Count: > 0 } mapped)
            {
                kept.Add(new PageRegion(box, mapped, r.Background, r.Included));
            }
        }

        return kept.Count > 0 ? kept : null;
    }

    private static List<LineFile>? ToLineFiles(IReadOnlyList<PageLine>? lines) =>
        lines is null || lines.Count == 0
            ? null
            : lines.Select(l => new LineFile
            {
                Text = l.Text, X = l.Box.X, Y = l.Box.Y, Width = l.Box.Width, Height = l.Box.Height,
            }).ToList();

    // A hand-edited or damaged line (no text, a box truly off the page) is dropped, not the whole story and not
    // the page's picture; a box that overshoots the page edge by rounding only is clamped onto it.
    private static List<PageLine>? FromLineFiles(List<LineFile>? lines, out int[] newIndexOf)
    {
        var kept = new List<PageLine>();
        newIndexOf = new int[lines?.Count ?? 0];
        for (int i = 0; i < newIndexOf.Length; i++)
        {
            LineFile? l = lines![i];
            newIndexOf[i] = -1;
            if (l is { Text: not null } && ClampToPage(l.X, l.Y, l.Width, l.Height) is { } box)
            {
                newIndexOf[i] = kept.Count;
                kept.Add(new PageLine(l.Text, box));
            }
        }

        return kept.Count > 0 ? kept : null;
    }

    private static NormalizedRect? ClampToPage(double x0, double y0, double w0, double h0)
    {
        bool sane = w0 > 0 && h0 > 0 && x0 >= -BoxRoundingTolerance && y0 >= -BoxRoundingTolerance
            && x0 + w0 <= 1 + BoxRoundingTolerance && y0 + h0 <= 1 + BoxRoundingTolerance;
        if (!sane)
        {
            return null;
        }

        double x = Math.Max(0, x0);
        double y = Math.Max(0, y0);
        double width = Math.Min(w0, 1 - x);
        double height = Math.Min(h0, 1 - y);
        return width > 0 && height > 0 ? new NormalizedRect(x, y, width, height) : null;
    }

    // The name an older file saved for the picture view, which showed the words too (Slice 151): it reads as picture and text.
    private const string LegacyPictureMode = "Picture";

    // A reading mode this app does not know (a newer version, a hand edit, a number) must not make
    // the whole story unreadable: it reads as "no choice" and the default applies.
    private static ReadingMode? ParseMode(JsonElement? value, bool? wordsOverPictures)
    {
        ReadingMode? mode = ParseModeName(value);
        bool wordsWereOff = wordsOverPictures == false;
        return wordsWereOff && mode is null or ReadingMode.PictureAndText ? ReadingMode.PictureOnly : mode;
    }

    private static ReadingMode? ParseModeName(JsonElement? value)
    {
        if (value is not { ValueKind: JsonValueKind.String } element)
        {
            return null;
        }

        if (string.Equals(element.GetString(), LegacyPictureMode, StringComparison.OrdinalIgnoreCase))
        {
            return ReadingMode.PictureAndText;
        }

        bool known = Enum.TryParse(element.GetString(), ignoreCase: true, out ReadingMode mode)
            && Enum.IsDefined(mode)
            && !int.TryParse(element.GetString(), out _);
        return known ? mode : null;
    }

    private static Dictionary<string, Dictionary<string, string>>? ToTranslationFiles(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? translations) =>
        translations is null || translations.Count == 0
            ? null
            : translations.ToDictionary(t => t.Key, t => t.Value.ToDictionary(e => e.Key, e => e.Value));

    // A language or entry with a null or empty side (a hand edit) is dropped; no table left reads as no translations.
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? CleanTranslations(Dictionary<string, Dictionary<string, string>>? translations)
    {
        var kept = new Dictionary<string, Dictionary<string, string>>();
        foreach (var (language, table) in translations ?? [])
        {
            Dictionary<string, string> entries = (table ?? []).Where(e => !string.IsNullOrEmpty(e.Key) && !string.IsNullOrEmpty(e.Value)).ToDictionary(e => e.Key, e => e.Value);
            if (!string.IsNullOrWhiteSpace(language) && entries.Count > 0)
            {
                MergeInto(kept, BundledTranslations.Key(language), entries);
            }
        }

        return kept.Count > 0 ? kept.ToDictionary(k => k.Key, k => (IReadOnlyDictionary<string, string>)k.Value) : null;
    }

    // Two files keys that name one language ("fa" and "fa-IR") become one table; the first entry for a piece wins.
    private static void MergeInto(Dictionary<string, Dictionary<string, string>> kept, string key, Dictionary<string, string> entries)
    {
        if (!kept.TryGetValue(key, out var existing))
        {
            kept[key] = entries;
            return;
        }

        foreach (var (piece, text) in entries)
        {
            existing.TryAdd(piece, text);
        }
    }

    // JSON null overwrites the property defaults, so a hand-edited or corrupt file can still
    // carry nulls; refuse it as a format error rather than failing later with a NullReference.
    private static void RequireFields(StoryFile f)
    {
        bool complete = f.Title is not null && f.Language is not null
            && f.Pages is not null && f.Pages.All(p => p is { Text: not null })
            && f.Cast is not null
            && f.Cast.All(c => c is { CharacterId: not null, Name: not null, VoiceId: not null });
        if (!complete)
        {
            throw new StoryFormatException("The story file is missing required fields.");
        }
    }

    private sealed class StoryFile
    {
        public int Version { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Language { get; set; } = "en";
        public DateTimeOffset Created { get; set; }
        public CoverStyle CoverStyle { get; set; } = CoverStyle.Colour;
        public int CoverColourIndex { get; set; }
        public string? CoverFile { get; set; }
        public int? CoverPageIndex { get; set; }
        /// <summary>Written for older builds only (<see cref="LegacyNarratorOf"/>); the narrator is one per language now (Slice 109), so reading ignores it.</summary>
        public string? NarratorVoiceId { get; set; }
        public JsonElement? PreferredMode { get; set; }

        /// <summary>Read only, for files older than Slice 151: false meant the words were off, which now reads as the picture-only mode. Never written.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? WordsOverPictures { get; set; }
        public List<PageFile> Pages { get; set; } = [];
        public List<CastFile> Cast { get; set; } = [];
        public bool Translate { get; set; }

        /// <summary>The grown-up's per-story "Show original text" switch (Slice 110); absent when off.</summary>
        public bool? ShowOriginal { get; set; }

        /// <summary>The parent's page recordings (Slice 111): the file of each is <see cref="StoryRecordings.FileNameOf"/>; absent when there are none.</summary>
        public List<RecordingFile>? Recordings { get; set; }

        /// <summary>The per-book "Read with my voice" switch (Slice 111); absent when off.</summary>
        public bool? UseRecordings { get; set; }

        /// <summary>The per-story "Animal sounds" switch (Slice 117); absent when on (the default).</summary>
        public bool? AnimalSounds { get; set; }

        public string? SourceId { get; set; }
        public Dictionary<string, Dictionary<string, string>>? Translations { get; set; }

        /// <summary>The casts of the other languages the story was read in (Slice 108); <see cref="Cast"/> is the story language's own.</summary>
        public Dictionary<string, List<CastFile>>? LanguageCasts { get; set; }
    }

    private sealed class RecordingFile
    {
        public int Page { get; set; }
        public string? Language { get; set; }
        public int Seconds { get; set; }
    }

    private sealed class PageFile
    {
        public string Text { get; set; } = string.Empty;
        public string? ImageFile { get; set; }
        public List<LineFile>? Lines { get; set; }
        public List<RegionFile>? Regions { get; set; }
    }

    private sealed class RegionFile
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public List<int>? LineIndexes { get; set; }
        public bool Background { get; set; }
        public bool? Included { get; set; }
    }

    private sealed class CastFile
    {
        public string CharacterId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Gender Gender { get; set; }
        public string VoiceId { get; set; } = string.Empty;
        public bool Edited { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Flavour { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Animal { get; set; }
    }

    private sealed class LineFile
    {
        public string Text { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}
