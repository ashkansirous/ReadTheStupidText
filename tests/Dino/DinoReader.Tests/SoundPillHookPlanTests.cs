using DinoReader.Testing;

namespace DinoReader.Tests;

public sealed class SoundPillHookPlanTests
{
	[Theory]
	[InlineData("getting", SoundStatusKind.Getting)]
	[InlineData("wifi", SoundStatusKind.WaitingForInternet)]
	[InlineData("preparing", SoundStatusKind.Preparing)]
	[InlineData("nosound", SoundStatusKind.NoSound)]
	public void Each_state_word_names_its_pill(string word, SoundStatusKind expected)
	{
		Assert.Equal(expected, SoundPillHookPlan.KindOf(word));
	}

	[Theory]
	[InlineData("off")]
	[InlineData("Getting")]
	[InlineData("")]
	public void Off_and_unknown_words_name_no_pill(string word)
	{
		Assert.Null(SoundPillHookPlan.KindOf(word));
	}

	[Fact]
	public void Only_the_word_off_releases_the_hold()
	{
		Assert.True(SoundPillHookPlan.IsOff("off"));
		Assert.False(SoundPillHookPlan.IsOff("getting"));
	}

	[Theory]
	[InlineData("", true, SoundPillHookPlan.DefaultPercent)]
	[InlineData("0", true, 0)]
	[InlineData("100", true, 100)]
	[InlineData("101", false, SoundPillHookPlan.DefaultPercent)]
	[InlineData("-1", false, SoundPillHookPlan.DefaultPercent)]
	[InlineData("abc", false, SoundPillHookPlan.DefaultPercent)]
	[InlineData(" 40", false, SoundPillHookPlan.DefaultPercent)]
	[InlineData("+5", false, SoundPillHookPlan.DefaultPercent)]
	public void The_percent_is_a_whole_number_from_0_to_100_or_the_default(string text, bool ok, int expected)
	{
		bool parsed = SoundPillHookPlan.TryParsePercent(text, out int value, out string error);

		Assert.Equal(ok, parsed);
		Assert.Equal(ok, error.Length == 0);
		if (ok)
		{
			Assert.Equal(expected, value);
		}
	}

	[Fact]
	public void The_hook_reads_only_launch_keys_the_iOS_entry_point_passes_on()
	{
		Assert.Contains("state", SoundPillHookPlan.LaunchKeys);
		Assert.Contains("percent", SoundPillHookPlan.LaunchKeys);
	}

	[Fact]
	public void Only_getting_carries_the_count()
	{
		SoundStatusPillState getting = SoundPillHookPlan.StateFor(SoundStatusKind.Getting, 40);

		Assert.True(getting.HasCount);
		Assert.Equal(0.4, getting.RingProgress, 3);
		Assert.False(SoundPillHookPlan.StateFor(SoundStatusKind.Preparing, 40).HasCount);
	}
}
