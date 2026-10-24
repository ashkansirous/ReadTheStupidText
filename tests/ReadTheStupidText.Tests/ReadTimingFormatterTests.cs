using ReadTheStupidText.Application.Reading;

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
    public void Minutes_grow_past_two_digits_without_wrapping()
    {
        var timing = new ReadTiming(TimeSpan.FromMinutes(125) + TimeSpan.FromSeconds(33), null);

        Assert.Equal("125:33/--:--", ReadTimingFormatter.Format(timing));
    }
}
