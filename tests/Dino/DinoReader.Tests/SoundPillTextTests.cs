namespace DinoReader.Tests;

public sealed class SoundPillTextTests
{
	[Fact]
	public void The_name_becomes_its_own_run_between_the_words()
	{
		IReadOnlyList<SoundPillRun> runs = SoundPillText.Split("Getting فارسی sound for your books · 3 of 15", "فارسی");

		Assert.Equal(["Getting ", "فارسی", " sound for your books · 3 of 15"], runs.Select(r => r.Text));
		Assert.Equal([false, true, false], runs.Select(r => r.IsName));
	}

	[Fact]
	public void A_name_at_the_start_or_end_leaves_no_empty_run()
	{
		Assert.Equal(2, SoundPillText.Split("فارسی sound", "فارسی").Count);
		Assert.Equal(2, SoundPillText.Split("sound فارسی", "فارسی").Count);
	}

	[Theory]
	[InlineData("Waiting for Wi-Fi…", "فارسی")]
	[InlineData("Waiting for Wi-Fi…", null)]
	[InlineData("Waiting for Wi-Fi…", "")]
	public void Without_the_name_in_the_text_it_is_one_plain_run(string text, string? name)
	{
		IReadOnlyList<SoundPillRun> runs = SoundPillText.Split(text, name);

		Assert.Single(runs);
		Assert.Equal(text, runs[0].Text);
		Assert.False(runs[0].IsName);
	}

	[Fact]
	public void Only_a_right_to_left_name_run_gets_the_name_font()
	{
		IReadOnlyList<SoundPillRun> runs = SoundPillText.Split("Getting فارسی sound", "فارسی");

		Assert.Equal(["Line", "Name", "Line"], runs.Select(r => SoundPillText.FontFor(r, "Line", "Name")));
	}

	[Fact]
	public void A_left_to_right_name_keeps_the_line_font()
	{
		IReadOnlyList<SoundPillRun> runs = SoundPillText.Split("Getting Deutsch sound", "Deutsch");

		Assert.All(runs, r => Assert.Equal("Line", SoundPillText.FontFor(r, "Line", "Name")));
	}

	[Fact]
	public void A_name_inside_another_word_is_not_marked_when_the_whole_word_comes_later()
	{
		IReadOnlyList<SoundPillRun> inside = SoundPillText.Split("Getting Svenskan and Svensk sound", "Svensk");
		Assert.Equal(["Getting Svenskan and ", "Svensk", " sound"], inside.Select(r => r.Text));
	}

	[Fact]
	public void A_combining_mark_after_the_name_makes_it_part_of_a_longer_word()
	{
		const string name = "فارس";
		string text = $"Getting {name}َ and {name} sound"; // the first has a vowel sign after it, the second stands alone

		IReadOnlyList<SoundPillRun> runs = SoundPillText.Split(text, name);

		Assert.Equal([$"Getting {name}َ and ", name, " sound"], runs.Select(r => r.Text));
	}

	[Fact]
	public void With_no_whole_word_the_first_occurrence_is_marked()
	{
		IReadOnlyList<SoundPillRun> runs = SoundPillText.Split("xفارسیx", "فارسی");

		Assert.Equal([false, true, false], runs.Select(r => r.IsName));
	}

	[Fact]
	public void The_runs_join_back_to_the_text()
	{
		const string text = "Hole فارسی-Audio für deine Bücher";

		Assert.Equal(text, string.Concat(SoundPillText.Split(text, "فارسی").Select(r => r.Text)));
	}
}
