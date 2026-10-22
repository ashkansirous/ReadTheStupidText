using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class VoiceAvailabilityTests
{
    [Fact]
    public void PhoneReadsOnlyItsOwnLanguages()
    {
        var availability = new VoiceAvailability(["en", "es"], []);

        Assert.True(availability.CanPhoneRead("es"));
        Assert.False(availability.CanPhoneRead("fa"));
    }

    [Theory]
    [InlineData("en-GB")]
    [InlineData("EN")]
    [InlineData("en_US")]
    public void RegionAndCaseAreIgnored(string code)
    {
        var availability = new VoiceAvailability(["en-GB"], []);

        Assert.True(availability.CanPhoneRead(code));
    }

    [Fact]
    public void NaturalPackMakesALanguageReadableWithoutAPhoneVoice()
    {
        var availability = new VoiceAvailability(["en"], ["fa"]);

        Assert.False(availability.CanPhoneRead("fa"));
        Assert.True(availability.CanNaturalRead("fa"));
        Assert.True(availability.CanRead("fa"));
    }

    [Fact]
    public void LanguageNoVoiceCoversIsNotReadable()
    {
        var availability = new VoiceAvailability(["en"], ["en"]);

        Assert.False(availability.CanRead("fa"));
    }

    [Fact]
    public void BlankCodesAreIgnored()
    {
        var availability = new VoiceAvailability(["", " "], [""]);

        Assert.False(availability.CanRead(""));
    }

    [Fact]
    public void UnknownPhoneNeverHidesOrBlocks()
    {
        var availability = new VoiceAvailability(null, []);

        Assert.False(availability.PhoneKnown);
        Assert.True(availability.CanPhoneRead("fa"));
    }

    [Fact]
    public void KnownEmptyPhoneReadsNothing()
    {
        var availability = new VoiceAvailability([], []);

        Assert.True(availability.PhoneKnown);
        Assert.False(availability.CanPhoneRead("en"));
    }
}
