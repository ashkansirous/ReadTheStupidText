using ReadTheStupidText.Infrastructure.Reading;

namespace ReadTheStupidText.Tests;

public class SupertonicVoiceTableTests
{
    [Fact]
    public void Ten_voices_in_sid_order_five_female_then_five_male()
    {
        Assert.Equal(10, SupertonicVoiceTable.Voices.Count);
        // Default is M1 = "Momonga" at sid 5.
        Assert.Equal("Momonga", SupertonicVoiceTable.Default.DisplayName);
    }

    [Fact]
    public void Default_speaker_id_is_five_the_first_male()
    {
        Assert.Equal(5, SupertonicVoiceTable.DefaultSpeakerId);
    }

    [Theory]
    [InlineData("supertonic:F1", 0)]
    [InlineData("supertonic:F5", 4)]
    [InlineData("supertonic:M1", 5)]
    [InlineData("supertonic:M5", 9)]
    public void SpeakerIdFor_maps_known_ids_to_their_sid(string id, int expectedSid)
    {
        Assert.Equal(expectedSid, SupertonicVoiceTable.SpeakerIdFor(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("supertonic:Momonga")]   // display name is not an id
    [InlineData("unknown-voice")]
    public void SpeakerIdFor_falls_back_to_default_for_unknown_ids(string? id)
    {
        Assert.Equal(SupertonicVoiceTable.DefaultSpeakerId, SupertonicVoiceTable.SpeakerIdFor(id));
    }

    [Fact]
    public void Voice_ids_are_prefixed_and_stable_for_persistence()
    {
        Assert.All(SupertonicVoiceTable.Voices, v => Assert.StartsWith("supertonic:", v.Id));
    }
}
