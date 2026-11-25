using DinoReader.Testing;
using Xunit;

namespace DinoReader.Tests;

/// <summary>The Debug scroll-shelf and turn-page hooks' pure part (Slice 433 item 6).</summary>
public class ShelfScrollPlanTests
{
    [Theory]
    [InlineData("0.6", "900", 0.6, 900)]
    [InlineData("-0.5", "", -0.5, ShelfScrollPlan.DefaultMilliseconds)]
    [InlineData("1", "100", 1.0, 100)]
    public void TryParse_AcceptsGoodArguments(string by, string ms, double fraction, int milliseconds)
    {
        Assert.True(ShelfScrollPlan.TryParse(by, ms, out double gotFraction, out int gotMs, out string error));
        Assert.Equal(fraction, gotFraction);
        Assert.Equal(milliseconds, gotMs);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("", "900")]
    [InlineData("abc", "")]
    [InlineData("NaN", "")]
    [InlineData("Infinity", "")]
    [InlineData("0", "")]
    [InlineData("6", "")]
    [InlineData("0.6", "50")]
    [InlineData("0.6", "20000")]
    [InlineData("0.6", "fast")]
    public void TryParse_RefusesBadArguments(string by, string ms)
    {
        Assert.False(ShelfScrollPlan.TryParse(by, ms, out _, out _, out string error));
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData(800, 900)]
    [InlineData(-300, 500)]
    [InlineData(5, 100)]
    public void FrameDeltas_AddUpToTheTotal(int total, int ms)
    {
        IReadOnlyList<int> deltas = ShelfScrollPlan.FrameDeltas(total, ms);
        Assert.Equal(total, deltas.Sum());
        Assert.Equal(Math.Max(1, ms / ShelfScrollPlan.FrameMilliseconds), deltas.Count);
    }

    [Fact]
    public void FrameDeltas_EaseInAndOut()
    {
        IReadOnlyList<int> deltas = ShelfScrollPlan.FrameDeltas(1000, 960);
        Assert.True(deltas[0] < deltas[deltas.Count / 2]);
        Assert.True(deltas[^1] < deltas[deltas.Count / 2]);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("next", true)]
    [InlineData("prev", false)]
    public void ParseDirection_ReadsNextAndPrev(string dir, bool next) => Assert.Equal(next, ShelfScrollPlan.ParseDirection(dir));

    [Fact]
    public void ParseDirection_RefusesATypo() => Assert.Null(ShelfScrollPlan.ParseDirection("left"));
}
