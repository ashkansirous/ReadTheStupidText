using Sirous.Reading.Application.Usage;

namespace ReadTheStupidText.Tests;

/// <summary>Slice 273 part D: the new areas and reasons are on the closed lists, and a window or font fallback is counted once per session like every other kind.</summary>
public sealed class FallbackPartDTests
{
    [Fact]
    public void Every_part_D_area_and_reason_is_on_the_closed_lists()
    {
        string[] areas = [FallbackArea.Window, FallbackArea.Font,FallbackArea.Library];
        string[] reasons =
        [
            FallbackReason.StorytellerPackMissing, FallbackReason.AnimalClipUnplayable, FallbackReason.ClipUnplayable, FallbackReason.ClipSpeedUnset, FallbackReason.InAppUpdateFailed,
            FallbackReason.WindowFrameFailed, FallbackReason.DisplaysUnreadable, FallbackReason.SavedPositionMoved, FallbackReason.DpiDefaulted,            FallbackReason.FontFallback,FallbackReason.ShelfOrderUnreadable, FallbackReason.StoryUnreadable, FallbackReason.LanguageDetectFailed,
            FallbackReason.PageReadFailed, FallbackReason.IntroStateReset,
        ];

        Assert.All(areas, area => Assert.Contains(area, FallbackArea.All));
        Assert.All(reasons, reason => Assert.Contains(reason, FallbackReason.All));
        Assert.Equal(FallbackReason.All.Count, FallbackReason.All.Distinct().Count());
        Assert.Equal(FallbackArea.All.Count, FallbackArea.All.Distinct().Count());
    }

    [Fact]
    public void A_window_fallback_is_queued_once_and_repeats_are_counted()
    {
        var recorder = new CountingRecorder();
        var reporter = new FallbackReporter(recorder);

        reporter.Report(FallbackArea.Window, FallbackReason.DpiDefaulted);
        reporter.Report(FallbackArea.Window, FallbackReason.DpiDefaulted);
        reporter.Report(FallbackArea.Font, FallbackReason.FontFallback);

        Assert.Equal(2, recorder.Recorded);
    }

    private sealed class CountingRecorder : IUsageRecorder
    {
        public int Recorded { get; private set; }

        public bool Enabled { get; set; } = true;

        public void Record(Func<UsageContext, UsageEvent?> make) => Recorded++;
    }
}
