using DinoReader.BookBuilder;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary><c>dump-sentences</c> (Slice 246 A2): the pieces the app reads aloud, as the pre-made audio generator takes them.</summary>
public sealed class SentenceDumpTests
{
    private const string PigsStem = "three-little-pigs";
    private const string Farsi = "fa";

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
    public void TheAnimalSounds_TheAppInserts_AreListedInOrder_InTheirCharactersVoice()
    {
        IReadOnlyList<SpokenSentence> sentences = PigsInFarsi().Sentences;
        IReadOnlyList<SpokenSentence> sounds = sentences.Where(s => s.Sound).ToList();

        Assert.NotEmpty(sounds);
        Assert.All(sounds, s => Assert.False(string.IsNullOrEmpty(s.Speaker)));
        Assert.All(sounds, s => Assert.Equal(s.Text, s.Source));
        Assert.All(sounds, s => Assert.Equal(sentences[s.Index - 1].Sentence, s.Sentence));
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
    public void ALanguageWithoutATable_SpeaksTheSourceText()
    {
        Story story = StoreBuilder.Build(Books.SpecOf(PigsStem));
        SentenceListing english = SentenceDump.Build(PigsStem, story, "en");

        Assert.All(english.Sentences, s => Assert.Equal(s.Source, s.Text));
    }
}
