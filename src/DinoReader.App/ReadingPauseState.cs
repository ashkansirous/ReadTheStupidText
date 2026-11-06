namespace DinoReader;

/// <summary>What a recorded page's "ended" call means for the read (Slice 271).</summary>
internal enum RecordingEndAction
{
	/// <summary>A stale call (another read is active, or no recording is playing): nothing happens.</summary>
	Ignore,

	/// <summary>The user paused after the clip ended: the read holds here and Resume goes on from it.</summary>
	HoldForResume,

	/// <summary>The clip ended while playing: the read goes on with the sentence after the page.</summary>
	Advance,
}

/// <summary>
/// Slice 271 (from Slice 270): the pure part of the recorded-page pause state, apart from <c>ReadingSession</c> (which needs the MAUI main thread) so a unit test can drive it.
/// A parent's recording that ends while the read is paused is remembered here until Resume takes it; a stop, a new play or a newer read clears it, so a stale entry never fires on a later Resume.
/// </summary>
internal sealed class ReadingPauseState
{
	private (int Id, int Page)? _endedWhilePaused;

	/// <summary>Decides what the ended call of page <paramref name="page"/> in read <paramref name="id"/> does, and remembers it when the read must wait for Resume.</summary>
	public RecordingEndAction OnRecordingEnded(int id, int page, int activeReadId, bool recordingActive, bool paused)
	{
		if (id != activeReadId || !recordingActive)
		{
			return RecordingEndAction.Ignore;
		}

		if (paused)
		{
			_endedWhilePaused = (id, page);
			return RecordingEndAction.HoldForResume;
		}

		return RecordingEndAction.Advance;
	}

	/// <summary>The recorded page that ended during the pause (removed from the state), or null when none did.</summary>
	public (int Id, int Page)? TakeHeld()
	{
		(int Id, int Page)? held = _endedWhilePaused;
		_endedWhilePaused = null;
		return held;
	}

	/// <summary>Forgets a held end: a stop or a new play came in.</summary>
	public void Clear() => _endedWhilePaused = null;
}
