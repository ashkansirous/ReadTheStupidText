using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class SentenceUnitsTests
{
    [Fact]
    public void A_chunk_splits_into_its_sentences()
    {
        IReadOnlyList<string> units = SentenceUnits.Split("The first sentence is here. The second one follows it.");

        Assert.Equal(["The first sentence is here.", "The second one follows it."], units);
    }

    [Fact]
    public void An_abbreviation_stub_joins_the_next_sentence()
    {
        IReadOnlyList<string> units = SentenceUnits.Split("Mr. Smith went to the market today. It was a sunny day.");

        Assert.Equal(["Mr. Smith went to the market today.", "It was a sunny day."], units);
    }

    [Fact]
    public void A_short_tail_joins_the_previous_unit()
    {
        IReadOnlyList<string> units = SentenceUnits.Split("The first sentence is here. Yes.");

        Assert.Equal(["The first sentence is here. Yes."], units);
    }

    [Fact]
    public void A_short_chunk_stays_one_unit()
    {
        Assert.Equal(["Hi. Yes."], SentenceUnits.Split("Hi. Yes."));
    }

    [Fact]
    public void Text_without_a_terminator_is_one_unit()
    {
        Assert.Equal(["no terminator in this fragment at all"], SentenceUnits.Split("no terminator in this fragment at all"));
    }

    [Fact]
    public void Units_rejoin_to_the_chunk_text()
    {
        const string chunk = "Dr. Who arrived late to the party. Everyone cheered loudly. Ok.";

        Assert.Equal(chunk, string.Join(' ', SentenceUnits.Split(chunk)));
    }

    [Fact]
    public void Unspaced_chinese_splits_at_fullwidth_marks()
    {
        IReadOnlyList<string> units = SentenceUnits.Split("今天天气非常好，我们一起去公园玩吧。明天可能会下雨，所以要带上雨伞。");

        Assert.Equal(2, units.Count);
    }
}
