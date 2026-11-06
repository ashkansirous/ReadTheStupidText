namespace DinoReader.Core;

/// <summary>
/// Which words a parent's recording covers (Slice 279): a page and a run of that page's sentences, counted from 0 on the page (the sentence the page's text starts with is 0).
/// A recording made before Slice 279 covers its whole page; it keeps the key <see cref="OfPage"/> and its file name <c>rec-&lt;page&gt;.m4a</c>.
/// </summary>
public readonly record struct StoryClipKey(int Page, int First, int Last)
{
    /// <summary>The <see cref="Last"/> of a clip that covers its whole page, whatever the page's sentence count.</summary>
    public const int WholePage = int.MaxValue;

    /// <summary>The key of a recording that covers the whole of <paramref name="page"/> (today's one-clip-per-page recording).</summary>
    public static StoryClipKey OfPage(int page) => new(page, 0, WholePage);

    /// <summary>Whether this is a whole-page clip made before pieces existed (its file is <c>rec-&lt;page&gt;.m4a</c>).</summary>
    public bool IsWholePage => First == 0 && Last == WholePage;

    /// <summary>Whether the two clips share a sentence, so only one of them can stand.</summary>
    public bool Overlaps(StoryClipKey other) => Page == other.Page && First <= other.Last && other.First <= Last;

    /// <summary>Whether the key is a usable range (not negative, first not after last).</summary>
    public bool IsValid => Page >= 0 && First >= 0 && Last >= First;

    /// <summary>Whether the clip's sentences all exist on a page that now has <paramref name="sentenceCount"/> of them; a whole-page clip always fits.
    /// A clip that does not was recorded for other words: it is left in the story but neither plays nor counts as recorded.</summary>
    public bool FitsPage(int sentenceCount) => IsWholePage || Last < sentenceCount;
}
