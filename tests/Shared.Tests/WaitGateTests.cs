using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class WaitGateTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Nothing_shows_before_500_ms()
    {
        var gate = new WaitGate();
        gate.Begin(WaitStage.PreparingVoice, Start);

        Assert.False(gate.IsVisible(Start));
        Assert.False(gate.IsVisible(Start.AddMilliseconds(499)));
    }

    [Fact]
    public void Shows_from_500_ms_until_the_wait_ends()
    {
        var gate = new WaitGate();
        gate.Begin(WaitStage.PreparingVoice, Start);

        Assert.True(gate.IsVisible(Start.AddMilliseconds(500)));
        Assert.True(gate.IsVisible(Start.AddSeconds(30)));
        gate.End(Start.AddSeconds(31));
        Assert.False(gate.IsVisible(Start.AddSeconds(32)));
    }

    [Fact]
    public void A_short_wait_ends_without_ever_showing()
    {
        var gate = new WaitGate();
        gate.Begin(WaitStage.Translating, Start);

        Assert.False(gate.IsVisible(Start.AddMilliseconds(300)));
        Assert.Null(gate.End(Start.AddMilliseconds(400)));
        Assert.False(gate.IsActive);
    }

    [Fact]
    public void End_reports_how_long_the_overlay_showed()
    {
        var gate = new WaitGate();
        gate.Begin(WaitStage.ReadingPictures, Start);
        gate.IsVisible(Start.AddMilliseconds(500));

        Assert.Equal(TimeSpan.FromSeconds(2), gate.End(Start.AddMilliseconds(2500)));
    }

    [Fact]
    public void End_reports_the_shown_time_even_when_nobody_polled_IsVisible()
    {
        var gate = new WaitGate();
        gate.Begin(WaitStage.ReadingPictures, Start);

        Assert.Equal(TimeSpan.FromSeconds(2), gate.End(Start.AddMilliseconds(2500)));
        gate.Begin(WaitStage.ReadingPictures, Start);
        Assert.Null(gate.End(Start.AddMilliseconds(400)));
    }

    [Fact]
    public void A_second_Begin_changes_the_stage_and_keeps_the_clock()
    {
        var gate = new WaitGate();
        gate.Begin(WaitStage.DownloadingModel, Start);
        gate.Begin(WaitStage.Translating, Start.AddMilliseconds(400));

        Assert.Equal(WaitStage.Translating, gate.Stage);
        Assert.True(gate.IsVisible(Start.AddMilliseconds(500)));
    }

    [Fact]
    public void TimeUntilVisible_counts_down_and_stops_at_zero()
    {
        var gate = new WaitGate();
        Assert.Equal(TimeSpan.Zero, gate.TimeUntilVisible(Start));

        gate.Begin(WaitStage.Saving, Start);

        Assert.Equal(TimeSpan.FromMilliseconds(200), gate.TimeUntilVisible(Start.AddMilliseconds(300)));
        Assert.Equal(TimeSpan.Zero, gate.TimeUntilVisible(Start.AddSeconds(1)));
    }

    [Fact]
    public void A_new_wait_starts_a_new_clock()
    {
        var gate = new WaitGate();
        gate.Begin(WaitStage.Saving, Start);
        gate.End(Start.AddSeconds(5));
        gate.Begin(WaitStage.Saving, Start.AddSeconds(10));

        Assert.False(gate.IsVisible(Start.AddSeconds(10).AddMilliseconds(100)));
    }

    [Fact]
    public void End_without_a_wait_is_harmless()
    {
        Assert.Null(new WaitGate().End(Start));
    }
}
