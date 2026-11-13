using ReadTheStupidText.Application.Reading;
using Xunit;

namespace ReadTheStupidText.Tests;

public sealed class PhraseSplitterTests
{
    [Fact]
    public void SoundWordGluedToASentenceStandsAlone()
    {
        IReadOnlyList<Phrase> phrases = PhraseSplitter.Split("بچه خوک فرار کرد. تق!");
        Assert.Equal(["بچه خوک فرار کرد.", "تق!"], phrases.Select(p => p.Text));
    }

    [Fact]
    public void CommaGapIsShorterThanASentenceGap()
    {
        IReadOnlyList<Phrase> phrases = PhraseSplitter.Split("Pip, ran away!");
        Assert.Equal([PhraseSplitter.CommaGapMs, PhraseSplitter.SentenceGapMs], phrases.Select(p => p.GapAfterMs));
    }

    [Fact]
    public void RepeatedMarksStayWithTheirWord() =>
        Assert.Equal(["ووش…!", "ووش!"], PhraseSplitter.Split("ووش…! ووش!").Select(p => p.Text));

    [Fact]
    public void DecimalPointDoesNotSplit() =>
        Assert.Single(PhraseSplitter.Split("It costs 3.5 coins"));

    [Fact]
    public void MarkOnlyPieceJoinsItsNeighbour() =>
        Assert.Equal(["Oh ... !"], PhraseSplitter.Split("Oh ... !").Select(p => p.Text));

    [Fact]
    public void TextWithoutLettersIsKeptWhole() =>
        Assert.Equal(["..."], PhraseSplitter.Split("...").Select(p => p.Text));

    [Fact]
    public void TwoLongClausesStayOneUtterance() =>
        Assert.Single(PhraseSplitter.Split("The little pig ran very far away, and nobody saw him again."));

    [Fact]
    public void ShortClauseAfterACommaStandsAlone() =>
        Assert.Equal(["The little pig ran very far away,", "تق!"], PhraseSplitter.Split("The little pig ran very far away, تق!").Select(p => p.Text));

    [Fact]
    public void PointBeforeAClosingQuoteOrGluedWordEndsTheSentence()
    {
        Assert.Equal(2, PhraseSplitter.Split("رفت.تق!").Count);
        Assert.Equal(2, PhraseSplitter.Split("He said go.» تق!").Count);
    }
}
