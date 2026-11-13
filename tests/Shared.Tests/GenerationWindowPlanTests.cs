using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class GenerationWindowPlanTests
{
    private const long Gb = 1024L * 1024 * 1024;

    [Theory]
    [InlineData(2 * Gb, GenerationWindowPlan.SmallMemoryWindow)]
    [InlineData(3 * Gb - 1, GenerationWindowPlan.SmallMemoryWindow)]
    [InlineData(3 * Gb, GenerationWindowPlan.DefaultWindow)]
    [InlineData(8 * Gb, GenerationWindowPlan.DefaultWindow)]
    [InlineData(0, GenerationWindowPlan.DefaultWindow)]
    [InlineData(-1, GenerationWindowPlan.DefaultWindow)]
    public void The_window_follows_physical_memory_and_unknown_memory_takes_the_full_window(long bytes, int expected)
    {
        Assert.Equal(expected, GenerationWindowPlan.WindowFor(bytes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void The_first_window_of_chunks_start_at_once(int offset)
    {
        Assert.Null(GenerationWindowPlan.WaitsFor(offset, 8));
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(9, 1)]
    [InlineData(109, 101)]
    public void A_later_chunk_waits_for_the_one_a_window_before_it(int offset, int expected)
    {
        Assert.Equal(expected, GenerationWindowPlan.WaitsFor(offset, 8));
    }

    [Fact]
    public void At_most_the_window_is_in_flight_at_once()
    {
        const int window = 3;
        for (int offset = 0; offset < 50; offset++)
        {
            int? behind = GenerationWindowPlan.WaitsFor(offset, window);
            Assert.True(behind is null || offset - behind.Value == window);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_window_below_one_still_lets_the_chain_move(int window)
    {
        Assert.Null(GenerationWindowPlan.WaitsFor(0, window));
        Assert.Equal(0, GenerationWindowPlan.WaitsFor(1, window));
        Assert.Equal(4, GenerationWindowPlan.WaitsFor(5, window));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    public void The_chunk_exactly_a_window_in_waits_for_the_first_and_the_one_before_does_not_wait(int window)
    {
        Assert.Equal(0, GenerationWindowPlan.WaitsFor(window, window));
        Assert.Null(GenerationWindowPlan.WaitsFor(window - 1, window));
    }
}
