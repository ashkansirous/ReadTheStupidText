using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

/// <summary>Slice 273 part B: a book's suggested voice that the language's roster does not hold at all is found by plain membership, nothing about taken voices or gender.</summary>
public class TraitFallbackTests
{
    private static readonly string[] Roster = ["duckling2", "bear1"];

    [Fact]
    public void A_suggested_voice_in_the_roster_is_not_missing_even_when_another_character_took_it() =>
        Assert.Empty(FallbackUsage.MissingFromRoster(["duckling2", "duckling2"], Roster));

    [Fact]
    public void A_suggested_voice_absent_from_the_roster_is_missing() =>
        Assert.Equal(["no-such-voice"], FallbackUsage.MissingFromRoster(["bear1", "no-such-voice"], Roster));

    [Fact]
    public void No_suggestion_is_not_missing() =>
        Assert.Empty(FallbackUsage.MissingFromRoster([null, string.Empty], Roster));
}
