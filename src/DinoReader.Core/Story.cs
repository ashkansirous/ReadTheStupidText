using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>Which cover a story shows in the gallery (mock screen 8b: "First page" or "Colour"); Picture is a chosen
/// page (<see cref="Story.CoverPageIndex"/>) or a gallery photo (<see cref="Story.CoverFile"/>), Slice 77.</summary>
public enum CoverStyle
{
    FirstPage,
    Colour,
    Picture,
}

/// <summary>
/// A character's stored voice: the cast is saved with its story (Slice 61). <see cref="Edited"/> is set
/// once a grown-up changed the character on the cast screen (Slice 65): the stored gender then also
/// overrides the guessed one, and the EDITED badge shows. <see cref="Flavour"/> (Slice 113) is how the voice is shaped: the guess made
/// when the cast was built, or the grown-up's pick (pinned with <see cref="Edited"/>); null (an older story) means guess from the name.
/// <see cref="Animal"/> (Slice 125) is the animal the character is, decided by the book's builder or the grown-up; <see cref="NotAnimal"/> is the grown-up's (or builder's)
/// "not an animal". With neither set the animal is guessed from the name, then the story text (<see cref="AnimalSounds.Resolve"/>).
/// </summary>
public sealed record CastMember(
    string CharacterId, string Name, Gender Gender, string VoiceId, bool Edited = false, VoiceFlavour? Flavour = null,
    AnimalKind? Animal = null, bool NotAnimal = false, string? SuggestedVoice = null, CharacterTraits? Traits = null)
{
    /// <summary>Whether the animal was decided (an animal or "not an animal") rather than left to the guess.</summary>
    public bool HasAnimalSetting => Animal is not null || NotAnimal;

    /// <summary>Whether the book's author chose this character's voice (Slice 156): a suggested roster id or traits came with the book.</summary>
    public bool HasAuthoredVoice => !string.IsNullOrEmpty(SuggestedVoice) || Traits is not null;

    /// <summary>Who chose the voice (Slice 156): a grown-up's pick (<see cref="Edited"/>, which is what an older package's flag means), else the book's suggestion, else the app's picker.</summary>
    public VoiceSource Source => Edited ? VoiceSource.Grownup : HasAuthoredVoice ? VoiceSource.Book : VoiceSource.App;
}

/// <summary>Who chose a character's voice (Slice 156, Decision 161). The runtime picker never recasts a <see cref="Book"/> voice (while its roster id exists in the language); a <see cref="Grownup"/>'s change always wins.</summary>
public enum VoiceSource
{
    App,
    Book,
    Grownup,
}

/// <summary>One recognised line of a scanned page: its text and where it sits on the page image (0..1 across its width and height).</summary>
public sealed record PageLine(string Text, NormalizedRect Box);

/// <summary>
/// A block of a scanned page's lines (Slice 79): the box that holds it, the indexes of its lines in the page's
/// <see cref="StoryPage.Lines"/>, whether the heuristic called it background (a sign, a page number), and the grown-up's
/// override. <see cref="Included"/> null follows the heuristic.
/// </summary>
public sealed record PageRegion(NormalizedRect Box, IReadOnlyList<int> LineIndexes, bool Background, bool? Included = null)
{
    /// <summary>Whether the region's lines are part of the page text.</summary>
    public bool IsIncluded => Included ?? !Background;

    /// <summary>The region with its state flipped (an explicit choice, kept even if it matches the heuristic).</summary>
    public PageRegion Toggled() => this with { Included = !IsIncluded };
}

/// <summary>One page: its text and, for a scanned or photographed page, the image file name inside the story folder
/// and the recognised lines with their boxes (Slice 64), which picture mode boxes while a sentence is spoken. The text is
/// composed from the lines of the included regions (Slice 79); null regions (an older story) include every line.</summary>
public sealed record StoryPage(string Text, string? ImageFile = null, IReadOnlyList<PageLine>? Lines = null, IReadOnlyList<PageRegion>? Regions = null)
{
    /// <summary>The lines whose region is included: the ones the page text is made of.</summary>
    public IReadOnlyList<PageLine> IncludedLines => ImportedText.IncludedLines(Lines ?? [], Regions);

    /// <summary>Where each included line sits in <see cref="Lines"/>.</summary>
    public IReadOnlyList<int> IncludedLineIndexes => ImportedText.IncludedIndexes(Lines?.Count ?? 0, Regions);
}

/// <summary>A story on the device: its pages, its cover choice and the cast worked out at import. <see cref="NarratorVoiceId"/> is only what
/// an older story or package carried: the narrator is one per language now (Slice 109, Decision 114), so it is read but never used (it is still written, filled with the language's narrator, for older builds: <see cref="StoryJson.LegacyNarratorOf"/>).</summary>
public sealed record Story(
    string Id,
    string Title,
    string Language,
    DateTimeOffset Created,
    IReadOnlyList<StoryPage> Pages,
    IReadOnlyList<CastMember> Cast,
    CoverStyle CoverStyle,
    int CoverColourIndex,
    string NarratorVoiceId,
    ReadingMode? PreferredMode = null,
    bool TranslateToMyLanguage = false,
    string? CoverFile = null,
    int? CoverPageIndex = null,
    string? SourceId = null,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? Translations = null,
    IReadOnlyDictionary<string, IReadOnlyList<CastMember>>? LanguageCasts = null,
    bool ShowOriginalText = false,
    IReadOnlyDictionary<int, PageRecording>? Recordings = null,
    bool UseRecordings = false,
    bool AnimalSoundsOn = true)
{
    public const string PageSeparator = "\n\n";

    /// <summary>A gallery photo chosen as the cover is stored as <c>cover-&lt;short id&gt;.jpg</c> in the story folder.</summary>
    public const string CoverFilePrefix = "cover";
    public const string CoverFileExtension = ".jpg";

    /// <summary>A new own book (Slice 107): empty, in the grown-up's language, with the translate switch on so it reads and shows in "my language" wherever a translator exists.</summary>
    public static Story NewOwnBook(string id, string language, DateTimeOffset created, int coverColourIndex) =>
        new(id, string.Empty, language, created, [], [], CoverStyle.FirstPage, coverColourIndex, string.Empty, TranslateToMyLanguage: true);

    /// <summary>The cast of the story's own language (<see cref="Cast"/>) or of another language it has been read in (Slice 108, Decision 113):
    /// each language has its own voices and pins. Empty when that language has no cast yet.</summary>
    public IReadOnlyList<CastMember> CastIn(string language) =>
        IsOwnLanguage(language) ? Cast : OtherCast(language) ?? [];

    /// <summary>Whether <paramref name="language"/> has a stored cast (the story's own language always does). An empty stored cast
    /// (made while the language's voices were missing) does not count: it is made again on the next look or read.</summary>
    public bool HasCastIn(string language) => IsOwnLanguage(language) || OtherCast(language) is { Count: > 0 };

    /// <summary>The story with <paramref name="cast"/> as the cast of <paramref name="language"/>; every other language's cast is left as it is.</summary>
    public Story WithCastIn(string language, IReadOnlyList<CastMember> cast)
    {
        if (IsOwnLanguage(language))
        {
            return this with { Cast = cast };
        }

        var casts = new Dictionary<string, IReadOnlyList<CastMember>>(LanguageCasts ?? NoLanguageCasts)
        {
            [BundledTranslations.Key(language)] = cast,
        };
        return this with { LanguageCasts = casts };
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<CastMember>> NoLanguageCasts = new Dictionary<string, IReadOnlyList<CastMember>>();

    // Compared as the screens resolve a language (an unsupported code reads as English), so the reading language and the story's own agree.
    private bool IsOwnLanguage(string language) => UiLanguage.Resolve(language) == UiLanguage.Resolve(Language);

    private IReadOnlyList<CastMember>? OtherCast(string language) =>
        LanguageCasts?.FirstOrDefault(c => BundledTranslations.Key(c.Key) == BundledTranslations.Key(language)).Value;

    /// <summary>All page texts as one string, a blank line between pages: what the reader speaks.</summary>
    public string Text => JoinPages(Pages);

    /// <summary>The cover's image file name inside the story folder: the first page's picture ("First page"), the chosen
    /// page's picture or the chosen photo ("Picture"); null for a colour cover or when that picture is missing.</summary>
    public string? CoverImageFile => CoverStyle switch
    {
        CoverStyle.FirstPage => Pages.FirstOrDefault()?.ImageFile,
        CoverStyle.Picture => CoverPageIndex is { } index
            ? (index >= 0 && index < Pages.Count ? Pages[index].ImageFile : null)
            : CoverFile,
        _ => null,
    };

    /// <summary>Every picture file the story names (page pictures and the chosen cover photo), without blanks or repeats.</summary>
    public IEnumerable<string> PictureFiles() =>
        Pages.Select(p => p.ImageFile).Append(CoverFile)
            .Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).Distinct(StringComparer.Ordinal);

    /// <summary>Whether any page carries a picture (a scanned or photographed page).</summary>
    public bool HasPictures => Pages.Any(p => p.ImageFile is not null);

    /// <summary>The mode the reading screen opens in (Slice 151): a book without pictures has only text; a book with pictures opens in
    /// the child's saved choice, else picture and text.</summary>
    public ReadingMode Mode => HasPictures ? PreferredMode ?? ReadingMode.PictureAndText : ReadingMode.Text;

    public static string JoinPages(IEnumerable<StoryPage> pages) =>
        string.Join(PageSeparator, pages.Select(p => p.Text));

    /// <summary>Where <see cref="Text"/>'s soft line wraps are (Slice 79): a scanned page whose text still is its
    /// recognised lines uses their boxes (the geometry rule), every other page the plain-text rule.</summary>
    public LineBreakMap BreakMap() => BreakMaps.GetValue(this, s => BreakMapOf(s.Pages));

    // Worked out once per story instance (keyed by reference, so a `with` copy with other pages gets its own) and not on every read call.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Story, LineBreakMap> BreakMaps = new();

    /// <summary>As <see cref="BreakMap"/> for the joined text of <paramref name="pages"/>.</summary>
    public static LineBreakMap BreakMapOf(IReadOnlyList<StoryPage> pages)
    {
        var boxes = new List<OcrBox?>();
        for (int i = 0; i < pages.Count; i++)
        {
            if (i > 0)
            {
                boxes.Add(null); // the blank line between pages
            }

            boxes.AddRange(BoxesOf(pages[i]));
        }

        return LineReflow.BreakMap(JoinPages(pages), boxes);
    }

    // A line compared by its words: trailing spaces and runs of spaces do not count as an edit, a changed word does.
    private static string SameWords(string line) => WhitespaceRun.Replace(line.Trim(), " ");

    private static readonly System.Text.RegularExpressions.Regex WhitespaceRun = new(@"\s+");

    // One entry per line of the page text; all null unless the lines are exactly the text's lines.
    private static IEnumerable<OcrBox?> BoxesOf(StoryPage page)
    {
        string[] textLines = page.Text.Split('\n');
        IReadOnlyList<PageLine> lines = page.IncludedLines;
        bool matches = lines.Count > 0 && lines.Count == textLines.Length
            && lines.Select(l => SameWords(l.Text)).SequenceEqual(textLines.Select(SameWords));
        if (!matches)
        {
            return textLines.Select(_ => (OcrBox?)null);
        }

        return lines.Select(l => (OcrBox?)new OcrBox((float)l.Box.X, (float)l.Box.Y, (float)l.Box.Right, (float)l.Box.Bottom));
    }
}
