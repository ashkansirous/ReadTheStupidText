namespace DinoReader.Core;

/// <summary>Which cover a story shows in the gallery (mock screen 8b: "First page" or "Colour").</summary>
public enum CoverStyle
{
    FirstPage,
    Colour,
}

/// <summary>
/// A character's stored voice: the cast is saved with its story (Slice 61). <see cref="Edited"/> is set
/// once a grown-up changed the character on the cast screen (Slice 65): the stored gender then also
/// overrides the guessed one, and the EDITED badge shows.
/// </summary>
public sealed record CastMember(string CharacterId, string Name, Gender Gender, string VoiceId, bool Edited = false);

/// <summary>One page: its text and, for a scanned or photographed page, the image file name inside the story folder.</summary>
public sealed record StoryPage(string Text, string? ImageFile = null);

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
    bool TranslateToMyLanguage = false)
{
    public const string PageSeparator = "\n\n";

    /// <summary>The one shared "no merges" map.</summary>
    public static readonly IReadOnlyDictionary<string, string> NoMerges = new Dictionary<string, string>();

    /// <summary>Characters a grown-up merged (Slice 65): merged-away character id to the id it joined.
    /// Applied to the attribution every time the story is read, so the fix is remembered with the story.</summary>
    public IReadOnlyDictionary<string, string> Merges => CharacterMerges ?? NoMerges;

    /// <summary>All page texts as one string, a blank line between pages: what the reader speaks.</summary>
    public string Text => JoinPages(Pages);

    /// <summary>The first page's image file when the cover is "First page" and that page has one; else null (a colour cover).</summary>
    public string? CoverImageFile =>
        CoverStyle == CoverStyle.FirstPage ? Pages.FirstOrDefault()?.ImageFile : null;

    /// <summary>Whether any page carries a picture (a scanned or photographed page).</summary>
    public bool HasPictures => Pages.Any(p => p.ImageFile is not null);

    /// <summary>The mode the reading screen opens in: the child's saved choice, else pictures when
    /// the story has them, else text.</summary>
    public ReadingMode Mode => PreferredMode ?? (HasPictures ? ReadingMode.Picture : ReadingMode.Text);

    public static string JoinPages(IEnumerable<StoryPage> pages) =>
        string.Join(PageSeparator, pages.Select(p => p.Text));
}
