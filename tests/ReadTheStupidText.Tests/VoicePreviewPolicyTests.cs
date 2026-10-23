using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using Xunit;

namespace ReadTheStupidText.Tests;

public class VoicePreviewPolicyTests
{
    [Fact]
    public void Previews_when_nothing_is_being_read() =>
        Assert.True(VoicePreviewPolicy.ShouldPreview(PlaybackState.Idle));

    [Theory]
    [InlineData(PlaybackState.Playing)]
    [InlineData(PlaybackState.Paused)]
    public void Does_not_preview_over_a_read_in_progress(PlaybackState state) =>
        Assert.False(VoicePreviewPolicy.ShouldPreview(state));
}
