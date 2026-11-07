namespace DinoReader.Core;

/// <summary>
/// Which words a parent's recording covers (Slice 279): a page and a run of that page's sentences, counted from 0 on the page (the sentence the page's text starts with is 0).
/// A recording made before Slice 279 covers its whole page; it keeps the key <see cref="OfPage"/> and its file name <c>rec-&lt;page&gt;.m4a</c>.
/// Since Slice 286 a clip also belongs to one story language (<see cref="Language"/>, a short code): the same sentences recorded in two languages are two clips.
/// </summary>
public readonly record struct StoryClipKey(int Page, int First, int Last, string Language)
{
    /// <summary>The story language the clip was recorded in; empty only for a key that names no language (a whole-page lookup that matches any).</summary>
    public string Language { get => _language ?? string.Empty; init => _language = value ?? string.Empty; }

    // A default(StoryClipKey) or an array element never ran the constructor: its field is null and reads as "" (and equals a key built with "").
    private readonly string? _language = Language ?? string.Empty;

    /// <summary>Equal when page, range and language match; a null and an empty language are the same.</summary>
    public bool Equals(StoryClipKey other) => Page == other.Page && First == other.First && Last == other.Last && Language == other.Language;

    public override int GetHashCode() => HashCode.Combine(Page, First, Last, Language);

    /// <summary>This key for <paramref name="language"/> (resolved to the app's short code).</summary>
    public StoryClipKey In(string language) => this with { Language = UiLanguage.Resolve(language) };

    /// <summary>The language of a key that names none (the pre-Slice-286 file names, a lookup that matches any language); said out loud so no caller forgets to give a language.</summary>
    public const string NoLanguage = "";

    /// <summary>The <see cref="Last"/> of a clip that covers its whole page, whatever the page's sentence count.</summary>
    public const int WholePage = int.MaxValue;

    /// <summary>The key of a recording that covers the whole of <paramref name="page"/> (today's one-clip-per-page recording).</summary>
    public static StoryClipKey OfPage(int page, string language) => new(page, 0, WholePage, language);

    /// <summary>Whether this is a whole-page clip made before pieces existed (its file is <c>rec-&lt;page&gt;.m4a</c>).</summary>
    public bool IsWholePage => First == 0 && Last == WholePage;

    /// <summary>Whether the two clips of the same language share a sentence, so only one of them can stand (clips of different languages never overlap).</summary>
    public bool Overlaps(StoryClipKey other) => Page == other.Page && Language == other.Language && First <= other.Last && other.First <= Last;

    /// <summary>Whether the key is a usable range (not negative, first not after last).</summary>
    public bool IsValid => Page >= 0 && First >= 0 && Last >= First;

    /// <summary>Whether the clip's sentences all exist on a page that now has <paramref name="sentenceCount"/> of them; a whole-page clip always fits.
    /// A clip that does not was recorded for other words: it is left in the story but neither plays nor counts as recorded.</summary>
    public bool FitsPage(int sentenceCount) => IsWholePage || Last < sentenceCount;
}
