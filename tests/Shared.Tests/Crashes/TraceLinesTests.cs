using ReadTheStupidText.Application.Crashes;
using ReadTheStupidText.Mobile.Shared.Crashes;
using Xunit;

namespace ReadTheStupidText.Tests.Crashes;

// Slice 243 item 5 (built in Slice 249): the rolling log's trace lines use the same words on Android and iOS, in both apps, and never carry text.
public class TraceLinesTests
{
    [Fact]
    public void The_lines_use_the_agreed_words()
    {
        Assert.Equal("page shown ReadingPage", TraceLines.PageShown("ReadingPage"));
        Assert.Equal("page left GalleryPage", TraceLines.PageLeft("GalleryPage"));
        Assert.Equal("page popped SettingsPage", TraceLines.PagePopped("SettingsPage"));
        Assert.Equal("back pressed", TraceLines.BackPressed);
        Assert.Equal("app background", TraceLines.AppBackground);
        Assert.Equal("app foreground", TraceLines.AppForeground);
        Assert.Equal("memory warning RunningCritical", TraceLines.MemoryWarning("RunningCritical"));
        Assert.Equal("synthesis queue 3", TraceLines.SynthesisQueue(3));
    }

    [Fact]
    public void A_reading_line_names_what_happened_and_why()
    {
        Assert.Equal("reading started", TraceLines.Reading(TraceLines.ReadingEvent.Started));
        Assert.Equal("reading stopped (page left)", TraceLines.Reading(TraceLines.ReadingEvent.Stopped, TraceLines.ReadingReason.PageLeft));
        Assert.Equal("reading paused (user)", TraceLines.Reading(TraceLines.ReadingEvent.Paused, TraceLines.ReadingReason.User));
        Assert.Equal("reading detached (page left)", TraceLines.Reading(TraceLines.ReadingEvent.Detached, TraceLines.ReadingReason.PageLeft));
        Assert.Equal("reading stalled", TraceLines.Reading(TraceLines.ReadingEvent.Stalled, null));
    }

    [Fact]
    public void A_queue_of_one_chunk_writes_nothing_and_a_build_up_writes_its_peak_and_its_end()
    {
        var trace = new SynthesisQueueTrace();
        Assert.Null(trace.Entered(1));
        Assert.Null(trace.Left(0));
        Assert.Null(trace.Entered(1));
        Assert.Equal("synthesis queue 2", trace.Entered(2));
        Assert.Null(trace.Entered(2));
        Assert.Equal("synthesis queue 3", trace.Entered(3));
        Assert.Null(trace.Left(1));
        Assert.Equal("synthesis queue 0", trace.Left(0));
        Assert.Null(trace.Entered(1));
        Assert.Null(trace.Left(0));
    }

    [Fact]
    public void A_missing_type_name_or_a_negative_queue_does_not_break_a_line()
    {
        Assert.Equal("page shown unknown", TraceLines.PageShown(""));
        Assert.Equal("synthesis queue 0", TraceLines.SynthesisQueue(-1));
    }

    [Fact]
    public void Every_fixed_word_passes_the_scrub_that_removes_quoted_text()
    {
        string[] lines =
        [
            TraceLines.PageShown("ReadingPage"), TraceLines.BackPressed, TraceLines.AppBackground, TraceLines.AppForeground, TraceLines.SynthesisQueue(2),
            TraceLines.Reading(TraceLines.ReadingEvent.Stopped, TraceLines.ReadingReason.LanguageChange),
            TraceLines.Reading(TraceLines.ReadingEvent.Stopped, TraceLines.ReadingReason.Superseded),
            TraceLines.Reading(TraceLines.ReadingEvent.Stopped, TraceLines.ReadingReason.Finished),
        ];

        Assert.All(lines, line => Assert.Equal(line, LogLineFormatter.ScrubMessage(line)));
    }
}
