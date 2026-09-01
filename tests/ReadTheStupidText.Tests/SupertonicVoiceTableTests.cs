using ReadTheStupidText.Domain.Reading;

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

    [Theory]
    [InlineData("supertonic:F1", true)]
    [InlineData("supertonic:F5", true)]
    [InlineData("supertonic:M1", false)]
    [InlineData("supertonic:M5", false)]
    public void IsFemale_matches_the_sid_split(string id, bool expected)
    {
        Assert.Equal(expected, SupertonicVoiceTable.IsFemale(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("unknown-voice")]
    public void IsFemale_resolves_unknown_ids_through_the_default_male_voice(string? id)
    {
        Assert.False(SupertonicVoiceTable.IsFemale(id));
    }
}
