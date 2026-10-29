using DinoReader.Core;
using ReadTheStupidText.Domain.Reading;
using Xunit;

namespace DinoReader.Tests;

public class CastRowLookupTests
{
    private static CastRow RowOf(string id, string voice, VoiceFlavour flavour) =>
        new(new CastMember(id, id, Gender.Male, voice), 10, false, false, flavour);

    [Fact]
    public void Current_ReturnsTheRowAsItIsNowNotAsItWasTapped()
    {
        CastRow tapped = RowOf("fox", "v1", VoiceFlavour.Normal);
        CastRow now = RowOf("fox", "v2", VoiceFlavour.Storyteller);

        Assert.Same(now, CastRowLookup.Current([RowOf("owl", "v3", VoiceFlavour.Normal), now], tapped));
    }

    [Fact]
    public void Current_KeepsTheTappedRowWhenTheCharacterIsGone()
    {
        CastRow tapped = RowOf("fox", "v1", VoiceFlavour.Normal);

        Assert.Same(tapped, CastRowLookup.Current([], tapped));
    }
}
