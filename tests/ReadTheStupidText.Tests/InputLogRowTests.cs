using ReadTheStupidText.Domain.Activity;
using ReadTheStupidText.Infrastructure.Logging;

namespace ReadTheStupidText.Tests;

public class InputLogRowTests
{
    private static readonly DateTimeOffset At = new(2026, 6, 29, 14, 5, 9, 123, TimeSpan.Zero);

    private static ActivityEntry Entry()
    {
        var e = new ActivityEntry(7, At, ActivityTrigger.Hotkey, new WindowSource("Chrome", "Inbox — Gmail"), "hello world");
        e.State = ActivityState.Reading;
        return e;
    }

    [Fact]
    public void Header_has_nine_tab_separated_columns()
    {
        Assert.Equal(9, InputLogRow.Header.Split('\t').Length);
        Assert.StartsWith("timestamp\tid\ttrigger", InputLogRow.Header);
    }

    [Fact]
    public void Format_emits_all_columns_in_order()
    {
        string[] cols = InputLogRow.Format(At, Entry()).Split('\t');

        Assert.Equal(9, cols.Length);
        Assert.Equal("2026-06-29 14:05:09.123", cols[0]);
        Assert.Equal("7", cols[1]);
        Assert.Equal("Hotkey", cols[2]);
        Assert.Equal("Reading", cols[3]);
        Assert.Equal("None", cols[4]);
        Assert.Equal("Chrome | Inbox — Gmail", cols[5]);
        Assert.Equal("hello world", cols[8]);
    }

    [Fact]
    public void Timings_render_as_milliseconds_or_blank()
    {
        ActivityEntry e = Entry();
        e.TimeToFirstAudio = TimeSpan.FromMilliseconds(840);
        e.SynthesisDuration = TimeSpan.FromMilliseconds(610);

        string[] cols = InputLogRow.Format(At, e).Split('\t');
        Assert.Equal("840", cols[6]);
        Assert.Equal("610", cols[7]);

        string[] blank = InputLogRow.Format(At, Entry()).Split('\t');
        Assert.Equal(string.Empty, blank[6]);
        Assert.Equal(string.Empty, blank[7]);
    }

    [Fact]
    public void Null_window_renders_a_dash()
    {
        var e = new ActivityEntry(3, At, ActivityTrigger.AutoRead, null, "x");
        Assert.Equal("-", InputLogRow.Format(At, e).Split('\t')[5]);
    }

    [Fact]
    public void Multiline_text_is_flattened_to_one_line()
    {
        var e = new ActivityEntry(1, At, ActivityTrigger.Manual, null, "line one\nline\ttwo");
        string row = InputLogRow.Format(At, e);

        Assert.DoesNotContain("\n", row);
        // The text column (last) has its newline and tab turned into spaces.
        Assert.Equal("line one line two", row.Split('\t')[8]);
    }
}
