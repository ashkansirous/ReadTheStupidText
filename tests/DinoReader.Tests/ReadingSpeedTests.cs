using DinoReader.Core;

namespace DinoReader.Tests;

public class ReadingSpeedTests
{
    [Fact]
    public void The_default_is_eight_tenths()
    {
        Assert.Equal(0.8, ReadingSpeed.DefaultRate.Value);
    }

    [Theory]
    [InlineData(0.1, 0.5)]
    [InlineData(0.5, 0.5)]
    [InlineData(0.8, 0.8)]
    [InlineData(1.2, 1.2)]
    [InlineData(1.5, 1.2)]
    [InlineData(2.0, 1.2)]
    [InlineData(0.83, 0.85)]
    public void Clamp_stays_in_range_and_on_a_step(double input, double expected)
    {
        Assert.Equal(expected, ReadingSpeed.Clamp(input).Value);
    }

    [Fact]
    public void Not_a_number_gives_the_default()
    {
        Assert.Equal(ReadingSpeed.Default, ReadingSpeed.Clamp(double.NaN).Value);
    }

    [Theory]
    [InlineData(double.PositiveInfinity, 1.2)]
    [InlineData(double.NegativeInfinity, 0.5)]
    [InlineData(-3.0, 0.5)]
    [InlineData(0.0, 0.5)]
    public void Extreme_stored_values_are_clamped(double input, double expected)
    {
        Assert.Equal(expected, ReadingSpeed.Clamp(input).Value);
    }

    [Fact]
    public void Every_step_from_min_to_max_is_kept_as_is()
    {
        int steps = (int)Math.Round((ReadingSpeed.Max - ReadingSpeed.Min) / ReadingSpeed.Step);
        for (int i = 0; i <= steps; i++)
        {
            double v = Math.Round(ReadingSpeed.Min + i * ReadingSpeed.Step, 2);
            Assert.Equal(v, ReadingSpeed.Clamp(v).Value);
        }
    }
}
