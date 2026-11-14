using Sirous.Reading.Mobile.Shared.Usage;

namespace DinoReader.Tests;

// Slice 352: a full usage queue drops taps, screens and setting changes before any other row.
public sealed class UsageQueueTrimTests
{
    private static bool IsNoise(string row) => row.StartsWith("tap", StringComparison.Ordinal) || row.StartsWith("screen", StringComparison.Ordinal) || row.StartsWith("setting", StringComparison.Ordinal);

    [Fact]
    public void A_queue_within_the_cap_is_untouched()
    {
        List<string> queue = ["first_run", "tap1"];

        UsageQueueTrim.ToCap(queue, 5, IsNoise);

        Assert.Equal(["first_run", "tap1"], queue);
    }

    [Fact]
    public void The_oldest_noise_goes_before_any_other_row_even_an_older_one()
    {
        List<string> queue = ["first_run", "tap1", "intro", "screen1", "tap2", "book_opened", "tap3"];

        UsageQueueTrim.ToCap(queue, 5, IsNoise);

        Assert.Equal(["first_run", "intro", "tap2", "book_opened", "tap3"], queue);
    }

    [Fact]
    public void When_no_noise_is_left_the_oldest_row_goes()
    {
        List<string> queue = ["first_run", "tap1", "intro", "book_opened"];

        UsageQueueTrim.ToCap(queue, 2, IsNoise);

        Assert.Equal(["intro", "book_opened"], queue);
    }

    [Fact]
    public void With_no_noise_rule_the_oldest_rows_go()
    {
        List<string> queue = ["a", "tap1", "b", "c"];

        UsageQueueTrim.ToCap(queue, 3, null);

        Assert.Equal(["tap1", "b", "c"], queue);
    }

    [Fact]
    public void An_offline_stretch_of_taps_never_pushes_out_the_rows_that_matter()
    {
        List<string> queue = ["first_run", "intro", "book_opened"];
        for (int i = 0; i < 500; i++)
        {
            queue.Add("tap" + i);
            UsageQueueTrim.ToCap(queue, 200, IsNoise);
        }

        Assert.Equal(200, queue.Count);
        Assert.Equal(["first_run", "intro", "book_opened"], queue.Take(3));
    }
}
