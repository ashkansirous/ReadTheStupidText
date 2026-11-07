using DinoReader.Core;

namespace DinoReader.Tests;

public sealed class SoundStatusPillStateTests
{
	[Fact]
	public void A_book_no_run_covers_is_no_sound_whatever_the_phase()
	{
		var state = new ShelfAudioState(ShelfAudioPhase.Running, Done: 2, Total: 15);

		SoundStatusPillState pill = SoundStatusPillState.From(false, state);

		Assert.Equal(SoundStatusKind.NoSound, pill.Kind);
		Assert.False(pill.HasCount);
	}

	[Theory]
	[InlineData(ShelfAudioPhase.Preparing, SoundStatusKind.Preparing)]
	[InlineData(ShelfAudioPhase.WaitingForInternet, SoundStatusKind.WaitingForInternet)]
	[InlineData(ShelfAudioPhase.WaitingForWifi, SoundStatusKind.WaitingForWifi)]
	[InlineData(ShelfAudioPhase.Idle, SoundStatusKind.NoSound)]
	[InlineData(ShelfAudioPhase.Cancelled, SoundStatusKind.NoSound)]
	public void A_covered_book_maps_each_phase_to_its_pill(ShelfAudioPhase phase, SoundStatusKind expected)
	{
		Assert.Equal(expected, SoundStatusPillState.From(true, new ShelfAudioState(phase)).Kind);
	}

	[Fact]
	public void A_running_fetch_carries_its_count_and_ring_share()
	{
		SoundStatusPillState pill = SoundStatusPillState.From(true, new ShelfAudioState(ShelfAudioPhase.Running, Done: 2, Total: 15));

		Assert.Equal(SoundStatusKind.Getting, pill.Kind);
		Assert.True(pill.HasCount);
		Assert.Equal((3, 15), (pill.Position, pill.Total));
		Assert.Equal(0.2, pill.RingProgress, 3); // the mock's 3 of 15
		Assert.True(pill.IsBrandPill);
	}

	[Fact]
	public void A_running_fetch_with_no_total_has_no_count_and_an_empty_ring()
	{
		SoundStatusPillState pill = SoundStatusPillState.From(true, new ShelfAudioState(ShelfAudioPhase.Running));

		Assert.Equal(SoundStatusKind.Getting, pill.Kind);
		Assert.False(pill.HasCount);
		Assert.Equal(0, pill.RingProgress);
	}

	[Fact]
	public void The_count_separator_is_the_mocks_middle_dot_with_spaces()
	{
		Assert.Equal(" · ", SoundStatusPillState.CountSeparator);
	}

	[Theory]
	[InlineData(0, false)]
	[InlineData(1, true)]
	public void A_running_fetch_has_a_count_only_from_a_total_of_one(int total, bool expected)
	{
		Assert.Equal(expected, new SoundStatusPillState(SoundStatusKind.Getting, 1, total).HasCount);
	}

	[Fact]
	public void Only_a_running_fetch_has_a_count_even_with_a_total()
	{
		Assert.False(new SoundStatusPillState(SoundStatusKind.Preparing, 3, 15).HasCount);
	}

	[Theory]
	[InlineData(20, 15, 1.0)]
	[InlineData(-2, 15, 0.0)]
	[InlineData(15, 15, 1.0)]
	public void The_ring_share_is_clamped_to_0_and_1(int position, int total, double expected)
	{
		Assert.Equal(expected, new SoundStatusPillState(SoundStatusKind.Getting, position, total).RingProgress);
	}

	[Fact]
	public void A_failed_book_still_counts_toward_the_position()
	{
		// Failed books count as done, so the ring can reach 100% while the last book still downloads (pinned, not endorsed).
		SoundStatusPillState pill = SoundStatusPillState.From(true, new ShelfAudioState(ShelfAudioPhase.Running, Done: 14, Total: 15, Failed: 1));

		Assert.Equal(15, pill.Position);
		Assert.Equal(1.0, pill.RingProgress);
	}

	[Theory]
	[InlineData(SoundStatusKind.WaitingForWifi)]
	[InlineData(SoundStatusKind.WaitingForInternet)]
	[InlineData(SoundStatusKind.Preparing)]
	[InlineData(SoundStatusKind.NoSound)]
	public void Only_a_moving_fetch_is_the_brand_pill(SoundStatusKind kind)
	{
		Assert.False(new SoundStatusPillState(kind).IsBrandPill);
	}
}
