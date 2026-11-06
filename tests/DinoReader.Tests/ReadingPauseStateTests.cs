using DinoReader.BookBuilder;

namespace DinoReader.Tests;

// Slice 271 item 1: a parent's recording that ends while the read is paused waits for Resume (Slice 270).
public sealed class ReadingPauseStateTests
{
	private const int ReadId = 7;
	private const int Page = 3;

	[Fact]
	public void A_recording_that_ends_while_paused_is_held_for_Resume()
	{
		var state = new ReadingPauseState();

		RecordingEndAction action = state.OnRecordingEnded(ReadId, Page, ReadId, recordingActive: true, paused: true);

		Assert.Equal(RecordingEndAction.HoldForResume, action);
		Assert.Equal((ReadId, Page), state.TakeHeld());
	}

	[Fact]
	public void Resume_takes_the_held_end_only_once()
	{
		var state = new ReadingPauseState();
		state.OnRecordingEnded(ReadId, Page, ReadId, recordingActive: true, paused: true);

		Assert.NotNull(state.TakeHeld());
		Assert.Null(state.TakeHeld());
		Assert.Null(state.TakeHeld());
	}

	[Fact]
	public void A_recording_that_ends_while_playing_advances_and_holds_nothing()
	{
		var state = new ReadingPauseState();

		RecordingEndAction action = state.OnRecordingEnded(ReadId, Page, ReadId, recordingActive: true, paused: false);

		Assert.Equal(RecordingEndAction.Advance, action);
		Assert.Null(state.TakeHeld());
	}

	[Fact]
	public void A_stale_read_id_is_ignored_even_while_paused()
	{
		var state = new ReadingPauseState();

		RecordingEndAction action = state.OnRecordingEnded(ReadId - 1, Page, ReadId, recordingActive: true, paused: true);

		Assert.Equal(RecordingEndAction.Ignore, action);
		Assert.Null(state.TakeHeld());
	}

	[Fact]
	public void An_end_with_no_recording_playing_is_ignored()
	{
		var state = new ReadingPauseState();

		RecordingEndAction action = state.OnRecordingEnded(ReadId, Page, ReadId, recordingActive: false, paused: false);

		Assert.Equal(RecordingEndAction.Ignore, action);
		Assert.Null(state.TakeHeld());
	}

	[Fact]
	public void A_stop_or_new_play_clears_a_held_end_so_a_later_Resume_does_not_fire_it()
	{
		var state = new ReadingPauseState();
		state.OnRecordingEnded(ReadId, Page, ReadId, recordingActive: true, paused: true);

		state.Clear();

		Assert.Null(state.TakeHeld());
	}

	// ReadingSession needs the MAUI main thread, so the wiring that held the Slice 270 bug is pinned on its source (the pattern of AnimalSoundClipTests): Stop and a new play clear the state, Resume takes it.
	[Fact]
	public void The_session_clears_the_state_on_stop_and_on_a_new_play_and_Resume_takes_it()
	{
		string session = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "src", "DinoReader.App", "ReadingSession.cs"));

		Assert.Contains("_pauseState.Clear(); // a stale flag must not fire on a later Resume", session);
		Assert.Contains("_recorded = RecordedPlay.Playing;\r\n\t\t_pauseState.Clear();", session.ReplaceLineEndings("\r\n"));
		Assert.Contains("if (_pauseState.TakeHeld() is { } ended)", session);
		Assert.Contains("ApplyRecordingEnded(ended.Id, ended.Page);", session);
		Assert.Contains("_pauseState.OnRecordingEnded(id, page, activeReadId: _activeReadId, recordingActive:", session);
	}
}
