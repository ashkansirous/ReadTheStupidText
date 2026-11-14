namespace DinoReader.Tests;

public class AttributeFallbackTests
{
	[Fact]
	public void A_found_font_is_used()
	{
		Assert.Equal(FontSource.Named, AttributeFallback.SourceFor(true));
	}

	[Fact]
	public void A_missing_font_falls_back_to_the_system_font()
	{
		Assert.Equal(FontSource.System, AttributeFallback.SourceFor(false));
	}

	[Theory]
	[InlineData(22, 22)]
	[InlineData(0.5, 0.5)]
	[InlineData(0, AttributeFallback.DefaultSize)]
	[InlineData(-3, AttributeFallback.DefaultSize)]
	[InlineData(double.NaN, AttributeFallback.DefaultSize)]
	[InlineData(double.PositiveInfinity, AttributeFallback.DefaultSize)]
	public void A_size_that_is_not_usable_becomes_the_default(double requested, double expected)
	{
		Assert.Equal(expected, AttributeFallback.SizeFor(requested));
	}
}
