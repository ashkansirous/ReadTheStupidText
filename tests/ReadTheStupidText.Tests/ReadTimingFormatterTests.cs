using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Tests;

public class ReadTimingFormatterTests
{
    [Fact]
    public void Zero_elapsed_and_unknown_total_renders_dashes_for_total()
    {
        var timing = new ReadTiming(TimeSpan.Zero, null);

        Assert.Equal("00:00/--:--", ReadTimingFormatter.Format(timing));
    }

    [Fact]
    public void Known_elapsed_and_total_render_as_mm_ss_slash_mm_ss()
    {
        var timing = new ReadTiming(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(143));

        Assert.Equal("00:03/02:23", ReadTimingFormatter.Format(timing));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(3, "0:03")]
    [InlineData(143, "2:23")]
    [InlineData(3900, "65:00")]
    public void FormatClock_leaves_minutes_unpadded_and_pads_seconds(int seconds, string expected)
    {
        Assert.Equal(expected, ReadTimingFormatter.FormatClock(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void FormatClock_renders_dashes_when_unknown()
    {
        Assert.Equal("--:--", ReadTimingFormatter.FormatClock(null));
    }

    [Fact]
    public void ClampToTotal_caps_elapsed_at_a_known_total()
    {
        var clamped = ReadTimingFormatter.ClampToTotal(new ReadTiming(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(10)));

        Assert.Equal(TimeSpan.FromSeconds(10), clamped.Elapsed);
        Assert.Equal("00:10/00:10", ReadTimingFormatter.Format(new ReadTiming(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(10))));
    }

    [Fact]
    public void ClampToTotal_leaves_elapsed_alone_when_below_or_total_unknown()
    {
        var below = new ReadTiming(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10));
        var unknown = new ReadTiming(TimeSpan.FromSeconds(30), null);

        Assert.Equal(below, ReadTimingFormatter.ClampToTotal(below));
        Assert.Equal(unknown, ReadTimingFormatter.ClampToTotal(unknown));
    }

    [Fact]
    public void Idle_clock_is_zero_elapsed_and_unknown_total()
    {
        Assert.Equal("0:00", ReadTimingFormatter.FormatClock(ReadTimingFormatter.Idle.Elapsed));
        Assert.Equal("--:--", ReadTimingFormatter.FormatClock(ReadTimingFormatter.Idle.Total));
    }

    [Fact]
    public void Minutes_grow_past_two_digits_without_wrapping()
    {
        var timing = new ReadTiming(TimeSpan.FromMinutes(125) + TimeSpan.FromSeconds(33), null);

        Assert.Equal("125:33/--:--", ReadTimingFormatter.Format(timing));
    }
}
