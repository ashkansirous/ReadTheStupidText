using DinoReader.Core;

namespace DinoReader.Tests;

// Slice 262, changed by Slice 360: the one rule for starting a run; no network rule is left (book audio goes on any network).
public sealed class ShelfAudioStartTests
{
    [Theory]
    [InlineData(false, false, ShelfAudioStartDecision.Start)]
    [InlineData(true, false, ShelfAudioStartDecision.Skip)]
    [InlineData(false, true, ShelfAudioStartDecision.Start)]
    [InlineData(true, true, ShelfAudioStartDecision.Start)]
    public void A_quiet_run_needs_no_cancel_and_a_visible_run_always_starts(bool cancelled, bool visible, ShelfAudioStartDecision expected) =>
        Assert.Equal(expected, ShelfAudioStart.Decide(cancelled, visible));
}
