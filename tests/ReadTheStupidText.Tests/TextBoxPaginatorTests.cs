using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class TextBoxPaginatorTests
{
    // A simple stand-in for real text measurement: fits iff the candidate page is
    // at most MaxLen characters.
    private const int MaxLen = 40;

    private static bool Fits(string candidate) => candidate.Length <= MaxLen;

    [Fact]
    public void Empty_text_produces_no_pages()
    {
        Assert.Empty(TextBoxPaginator.Paginate(string.Empty, Fits));
    }

    [Fact]
    public void Text_that_fits_a_single_page_stays_one_page()
    {
        const string text = "One short sentence.";

        IReadOnlyList<TextPage> pages = TextBoxPaginator.Paginate(text, Fits);

        TextPage page = Assert.Single(pages);
        Assert.Equal(text, page.Text);
        Assert.Equal(0, page.SourceStart);
        Assert.Equal(text.Length, page.SourceEnd);
    }

    [Fact]
    public void Long_text_splits_into_multiple_pages_on_sentence_boundaries()
    {
        string text = "Alpha sentence one. Beta sentence two. Gamma sentence three. " +
            "Delta sentence four. Epsilon sentence five.";

        IReadOnlyList<TextPage> pages = TextBoxPaginator.Paginate(text, Fits);

        Assert.True(pages.Count > 1, "long text should split into multiple pages");
        Assert.All(pages, p => Assert.True(p.Text.Length <= MaxLen, $"page over limit: '{p.Text}'"));
        // No page should ever start or end mid-sentence.
        Assert.All(pages, p => Assert.False(p.Text.StartsWith(' ') || p.Text.EndsWith(' ')));
    }

    [Fact]
    public void A_single_sentence_longer_than_the_limit_still_gets_its_own_page()
    {
        string longSentence = string.Concat(Enumerable.Repeat("word ", 20)).Trim() + ".";
        Assert.True(longSentence.Length > MaxLen, "test setup: sentence should exceed the limit");

        IReadOnlyList<TextPage> pages = TextBoxPaginator.Paginate(longSentence, Fits);

        TextPage page = Assert.Single(pages);
        Assert.Equal(longSentence, page.Text);
    }

    [Fact]
    public void Pages_cover_the_source_text_contiguously_without_gaps_or_overlap()
    {
        string text = "Alpha sentence one. Beta sentence two. Gamma sentence three. " +
            "Delta sentence four. Epsilon sentence five.";

        IReadOnlyList<TextPage> pages = TextBoxPaginator.Paginate(text, Fits);

        for (int i = 1; i < pages.Count; i++)
        {
            Assert.True(pages[i].SourceStart >= pages[i - 1].SourceEnd);
        }
    }

    [Fact]
    public void PageIndexContaining_finds_the_page_spanning_a_source_position()
    {
        string text = "Alpha sentence one. Beta sentence two. Gamma sentence three. " +
            "Delta sentence four. Epsilon sentence five.";
        IReadOnlyList<TextPage> pages = TextBoxPaginator.Paginate(text, Fits);
        Assert.True(pages.Count > 1);

        int middleOfSecondPage = pages[1].SourceStart + 1;

        Assert.Equal(1, TextBoxPaginator.PageIndexContaining(pages, middleOfSecondPage));
    }

    [Fact]
    public void PageIndexContaining_clamps_a_past_the_end_position_to_the_last_page()
    {
        string text = "One short sentence.";
        IReadOnlyList<TextPage> pages = TextBoxPaginator.Paginate(text, Fits);

        Assert.Equal(0, TextBoxPaginator.PageIndexContaining(pages, text.Length));
    }

    [Fact]
    public void PageIndexContaining_of_no_pages_is_negative_one()
    {
        Assert.Equal(-1, TextBoxPaginator.PageIndexContaining(Array.Empty<TextPage>(), 0));
    }
}
