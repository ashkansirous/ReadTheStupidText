using DinoReader.BookBuilder;
using Xunit;

namespace DinoReader.Tests;

/// <summary>Slice 256: <c>dump-sentences</c> works for a picture book whose pages come from <c>lines.json</c> (Good Enough).</summary>
public sealed class PictureBookSentenceDumpTests
{
    private const string Stem = "good-enough";

    [Theory]
    [InlineData("en")]
    [InlineData("fa")]
    public void Good_Enough_dumps_in_order_with_its_four_speakers(string language)
    {
        SentenceListing listing = SentenceDump.Build(Stem, SentenceDump.StoryOf(Stem), language);

        Assert.NotEmpty(listing.Sentences);
        Assert.Equal(Enumerable.Range(0, listing.Sentences.Count), listing.Sentences.Select(s => s.Index));
        Assert.Equal(["Carrot", "Crowd", "Hazelnut", "Referee"], listing.Sentences.Select(s => s.Speaker).OfType<string>().Distinct().Order());
        Assert.All(listing.Sentences, s => Assert.Equal(s.Speaker is null, s.Narrator));
    }

    [Fact]
    public void Good_Enough_pieces_follow_the_page_lines_in_the_story_language()
    {
        SentenceListing listing = SentenceDump.Build(Stem, SentenceDump.StoryOf(Stem), "en");

        Assert.Equal("Carrot and Hazelnut are best friends.", listing.Sentences[0].Text);
        Assert.Equal(0, listing.Sentences[0].Sentence);
    }

    // The dialogue version (2026-11-13): Carrot, Hazelnut, the Crowd and the Referee speak their own lines.
    [Fact]
    public void Good_Enough_has_57_pieces_35_narrator_and_its_speakers_lines()
    {
        SentenceListing listing = SentenceDump.Build(Stem, SentenceDump.StoryOf(Stem), "en");

        Assert.Equal(57, listing.Sentences.Count);
        Assert.Equal(35, listing.Sentences.Count(s => s.Narrator));
        Assert.Equal(8, listing.Sentences.Count(s => s.Speaker == "Carrot"));
        Assert.Equal(10, listing.Sentences.Count(s => s.Speaker == "Hazelnut"));
        Assert.Equal(2, listing.Sentences.Count(s => s.Speaker == "Crowd"));
        Assert.Equal(2, listing.Sentences.Count(s => s.Speaker == "Referee"));
    }

    [Fact]
    public void Good_Enough_fa_dump_is_the_translation_not_the_english()
    {
        SentenceListing en = SentenceDump.Build(Stem, SentenceDump.StoryOf(Stem), "en");
        SentenceListing fa = SentenceDump.Build(Stem, SentenceDump.StoryOf(Stem), "fa");

        Assert.Equal(en.Sentences.Count, fa.Sentences.Count);
        Assert.All(fa.Sentences, s => Assert.Contains(s.Text, c => c is >= '؀' and <= 'ۿ'));
        Assert.All(en.Sentences.Zip(fa.Sentences), pair => Assert.NotEqual(pair.First.Text, pair.Second.Text));
    }

    [Fact]
    public void A_book_with_no_story_md_that_is_not_Good_Enough_fails_clearly()
    {
        Exception error = Assert.Throws<InvalidOperationException>(() => SentenceDump.SourceOf("another-picture-book", hasStoryMd: false));

        Assert.Contains("another-picture-book", error.Message);
        Assert.Contains("no story.md", error.Message);
    }

    [Fact]
    public void The_source_is_story_md_when_there_is_one_and_Good_Enough_otherwise()
    {
        Assert.Equal(SentenceDump.DumpSource.StoryMd, SentenceDump.SourceOf("three-little-pigs", hasStoryMd: true));
        Assert.Equal(SentenceDump.DumpSource.GoodEnoughPictureBook, SentenceDump.SourceOf(Stem, hasStoryMd: false));
    }
}
