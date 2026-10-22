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
/// overrides the guessed one, and the EDITED badge shows.
/// </summary>
public sealed record CastMember(string CharacterId, string Name, Gender Gender, string VoiceId, bool Edited = false);

/// <summary>One recognised line of a scanned page: its text and where it sits on the page image (0..1 across its width and height).</summary>
public sealed record PageLine(string Text, NormalizedRect Box);

/// <summary>One page: its text and, for a scanned or photographed page, the image file name inside the story folder
/// and the recognised lines with their boxes (Slice 64), which picture mode boxes while a sentence is spoken.</summary>
public sealed record StoryPage(string Text, string? ImageFile = null, IReadOnlyList<PageLine>? Lines = null);

/// <summary>A story on the device: its pages, its cover choice and the cast worked out at import.</summary>
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
    IReadOnlyDictionary<string, string>? CharacterMerges = null,
    bool TranslateToMyLanguage = false,
    string? CoverFile = null,
    int? CoverPageIndex = null)
{
    public const string PageSeparator = "\n\n";

    /// <summary>A gallery photo chosen as the cover is stored as <c>cover-&lt;short id&gt;.jpg</c> in the story folder.</summary>
    public const string CoverFilePrefix = "cover";
    public const string CoverFileExtension = ".jpg";

    /// <summary>The one shared "no merges" map.</summary>
    public static readonly IReadOnlyDictionary<string, string> NoMerges = new Dictionary<string, string>();

    /// <summary>Characters a grown-up merged (Slice 65): merged-away character id to the id it joined.
    /// Applied to the attribution every time the story is read, so the fix is remembered with the story.</summary>
    public IReadOnlyDictionary<string, string> Merges => CharacterMerges ?? NoMerges;

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

    /// <summary>Whether any page carries a picture (a scanned or photographed page).</summary>
    public bool HasPictures => Pages.Any(p => p.ImageFile is not null);

    /// <summary>The mode the reading screen opens in: the child's saved choice, else pictures when
    /// the story has them, else text.</summary>
    public ReadingMode Mode => PreferredMode ?? (HasPictures ? ReadingMode.Picture : ReadingMode.Text);

    public static string JoinPages(IEnumerable<StoryPage> pages) =>
        string.Join(PageSeparator, pages.Select(p => p.Text));
}
