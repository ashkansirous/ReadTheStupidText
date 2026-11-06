using DinoReader.BookBuilder;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary><c>dump-sentences</c> (Slice 246 A2): the pieces the app reads aloud, as the pre-made audio generator takes them.</summary>
public sealed class SentenceDumpTests
{
    private const string PigsStem = "three-little-pigs";
    private const string Farsi = "fa";
    private const string Pim = "Pim";

    private static SentenceListing PigsInFarsi() => SentenceDump.Build(PigsStem, StoreBuilder.Build(Books.SpecOf(PigsStem)), Farsi);

    [Fact]
    public void ThePieces_AreTheOnesTheAppReads_InOrder_WithTheirHighlightSentence()
    {
        Story story = StoreBuilder.Build(Books.SpecOf(PigsStem));
        SentenceListing listing = PigsInFarsi();

        Assert.Equal(PigsStem, listing.Book);
        Assert.Equal(Farsi, listing.Language);
        Assert.Equal(Enumerable.Range(0, listing.Sentences.Count), listing.Sentences.Select(s => s.Index));
        Assert.Equal(listing.Sentences.Select(s => s.Sentence).Order(), listing.Sentences.Select(s => s.Sentence));
        Assert.Equal(StoryPieces.Of(story).Count(p => BundledTranslations.Lookup(story, Farsi, p.Segment.Text) is { } t && t.Any(char.IsLetterOrDigit)), listing.Sentences.Count(s => !s.Sound));
    }

    [Fact]
    public void TheAnimalSounds_AreNotListed_TheAppPlaysARecordingForThem()
    {
        Assert.DoesNotContain(PigsInFarsi().Sentences, s => s.Sound);
        Assert.DoesNotContain(SentenceDump.Build(PigsStem, StoreBuilder.Build(Books.SpecOf(PigsStem)), "en").Sentences, s => s.Sound);
    }

    [Fact]
    public void TheHighlight_IsTheSentenceOfTheSegmentsSourceStart()
    {
        Story story = StoreBuilder.Build(Books.SpecOf(PigsStem));
        IReadOnlyList<SentenceSpan> spans = StorySentences.Build(story.Text, LanguageRules.For(UiLanguage.Resolve(story.Language)), story.BreakMap());
        IReadOnlyList<int> expected = StoryPieces.Of(story)
            .Where(p => BundledTranslations.Lookup(story, Farsi, p.Segment.Text) is { } t && t.Any(char.IsLetterOrDigit))
            .Select(p => StorySentences.IndexAt(spans, p.Segment.SourceStart)).ToList();

        Assert.Equal(expected, PigsInFarsi().Sentences.Where(s => !s.Sound).Select(s => s.Sentence));
    }

    [Fact]
    public void TheText_IsTheDisplayedTranslation_NotTheHowToReadTable()
    {
        Story story = StoreBuilder.Build(Books.SpecOf(PigsStem));
        SpokenSentence line = PigsInFarsi().Sentences.First(s => s.Speaker == "Pip");

        Assert.Equal(BundledTranslations.Lookup(story, Farsi, line.Source), line.Text);
        Assert.NotEqual(line.Source, line.Text);
    }

    [Fact]
    public void ADialogueLine_NamesItsSpeaker_AndNarrationIsTheNarrator()
    {
        IReadOnlyList<SpokenSentence> sentences = PigsInFarsi().Sentences;

        Assert.All(sentences.Where(s => s.Speaker is not null), s => Assert.False(s.Narrator));
        Assert.All(sentences.Where(s => s.Speaker is null), s => Assert.True(s.Narrator));
        Assert.Contains(sentences, s => s.Speaker == "Wolf");
        Assert.Contains(sentences, s => s.Narrator);
    }

    [Fact]
    public void Farsi_ReadsTheTagBeforeTheWholeSplitQuote_AndTheSourceTextsFollowTheNewOrder()
    {
        IReadOnlyList<SpokenSentence> sentences = PigsInFarsi().Sentences;
        int tag = sentences.ToList().FindIndex(s => s.Source == "said Pim." && s.Text == BundledTranslations.Lookup(StoreBuilder.Build(Books.SpecOf(PigsStem)), Farsi, "said Pim."));

        Assert.True(tag >= 0, "the tag comes first with the text it was translated as, its colon is display only (Slice 280)");
        Assert.Equal("A strong house takes time,", sentences[tag + 1].Source);
        Assert.Equal("I'll play when it's done.", sentences[tag + 2].Source);
        Assert.Equal(Pim, sentences[tag + 1].Speaker);
        Assert.Equal(sentences[tag + 1].Speaker, sentences[tag + 2].Speaker);
        Assert.True(sentences[tag].Narrator);
        Assert.Equal(sentences.Select(s => s.Sentence).Order(), sentences.Select(s => s.Sentence));
    }

    [Fact]
    public void ALanguageWithoutATable_SpeaksTheSourceText()
    {
        Story story = StoreBuilder.Build(Books.SpecOf(PigsStem));
        SentenceListing english = SentenceDump.Build(PigsStem, story, "en");

        Assert.All(english.Sentences, s => Assert.Equal(s.Source, s.Text));
    }
}
