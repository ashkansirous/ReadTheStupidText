namespace DinoReader.Core;

/// <summary>Which cover a story shows in the gallery (mock screen 8b: "First page" or "Colour").</summary>
public enum CoverStyle
{
    FirstPage,
    Colour,
}

/// <summary>A character's stored voice: the cast is saved with its story (Slice 61).</summary>
public sealed record CastMember(string CharacterId, string Name, Gender Gender, string VoiceId);

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
    string NarratorVoiceId)
{
    public const string PageSeparator = "\n\n";

    /// <summary>All page texts as one string, a blank line between pages: what the reader speaks.</summary>
    public string Text => JoinPages(Pages);

    /// <summary>The first page's image file when the cover is "First page" and that page has one; else null (a colour cover).</summary>
    public string? CoverImageFile =>
        CoverStyle == CoverStyle.FirstPage ? Pages.FirstOrDefault()?.ImageFile : null;

    public static string JoinPages(IEnumerable<StoryPage> pages) =>
        string.Join(PageSeparator, pages.Select(p => p.Text));
}
