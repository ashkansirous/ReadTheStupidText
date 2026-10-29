using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Tests;

// Slice 164 follow-up: speed chips shared by the tray panel and the phone settings, and the phone's empty-player rule.
public class SpeedPresetEmptyPlayerTests
{
    [Theory]
    [InlineData("1.25", 1.25)]
    [InlineData("2", 2.0)]
    [InlineData("9", 2.0)]
    public void Tag_parses_with_the_invariant_culture_and_snaps(string tag, double expected)
    {
        Assert.True(SpeedPreset.TryParse(tag, out PlaybackRate rate));
        Assert.Equal(expected, rate.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fast")]
    [InlineData("1,25x")]
    public void A_tag_that_is_not_a_number_does_not_parse(string? tag)
    {
        Assert.False(SpeedPreset.TryParse(tag, out _));
    }

    [Fact]
    public void Only_the_chip_matching_the_current_rate_is_current()
    {
        var current = new PlaybackRate(1.5);

        Assert.True(SpeedPreset.IsCurrent("1.5", current));
        Assert.False(SpeedPreset.IsCurrent("1.25", current));
        Assert.False(SpeedPreset.IsCurrent(null, current));
        Assert.False(SpeedPreset.IsCurrent("x", current));
    }

    [Theory]
    [InlineData(null, PlaybackState.Idle, true)]
    [InlineData("  ", PlaybackState.Paused, true)]
    [InlineData("  ", PlaybackState.Playing, false)]
    [InlineData("Hi", PlaybackState.Idle, false)]
    public void Empty_player_is_no_text_and_nothing_playing(string? text, PlaybackState state, bool expected)
    {
        Assert.Equal(expected, EmptyPlayer.Is(text, state));
    }
}
