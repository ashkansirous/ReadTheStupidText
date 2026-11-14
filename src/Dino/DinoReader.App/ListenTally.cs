namespace DinoReader;

/// <summary>
/// Slice 288: adds up the time audio was really heard in one reading of a book, from a counted <c>read_started</c> to its <c>read_stopped</c>.
/// It runs only between <see cref="Resume"/> and <see cref="Pause"/>, which the reading session's audio output drives (never the usage verbs): not while a piece loads, a menu holds the read,
/// or a silent piece sits behind the lock screen. A reading is a listen once it has played <see cref="ListenSeconds"/>. Pure (the clock is passed in), so a test drives it;
/// <see cref="UsageCounter"/> owns the one instance and the timer.
/// </summary>
public sealed class ListenTally
{
	/// <summary>A reading counts as a listen at this much played time (the user's rule, 2026-11-07).</summary>
	public const int ListenSeconds = 30;

	private const long MillisecondsPerSecond = 1000;
	private const double MillisecondsPerSecondExact = 1000.0;

	private readonly Func<long> _nowMilliseconds;
	private string _reading = string.Empty;
	private bool _active;
	private bool _playing;
	private bool _audible;
	private bool _listened;
	private long _segmentStart;
	private long _playedBefore;
	private long _reportedMilliseconds;

	public ListenTally(Func<long> nowMilliseconds)
	{
		_nowMilliseconds = nowMilliseconds;
	}

	/// <summary>True between <see cref="Start"/> and <see cref="Stop"/>.</summary>
	public bool Active => _active;

	/// <summary>True when this reading has already reached <see cref="ListenSeconds"/> (the listen was counted).</summary>
	public bool Listened => _listened;

	/// <summary>True when the audio output was last reported as heard (<see cref="SetAudible"/>); a "heard" report with no reading open is not kept.</summary>
	public bool Audible => _audible;

	/// <summary>
	/// The audio output's word on whether audio is heard (the only thing that runs or pauses the tally). "Heard" is remembered only while a reading is open or on its way out (false is always
	/// remembered), so a reading that starts while its own audio is still heard begins running at once; true with no reading open does nothing.
	/// </summary>
	public void SetAudible(bool audible)
	{
		if (audible && !_active)
		{
			return; // audio with no counted reading open (the intro's demo read, a page that has left) is nobody's listening time
		}

		_audible = audible;
		if (audible)
		{
			Resume();
			return;
		}

		Pause();
	}

	/// <summary>
	/// A new reading of the story <paramref name="reading"/> begins, with nothing heard yet (it runs at once when audio is already heard); returns true then. Starting the same story while a reading
	/// is open changes nothing (false): the reading ends at <see cref="Stop"/> only, so its time is never thrown away or counted twice. Starting a different story ends the open reading first:
	/// its unsent seconds are dropped, never billed to the new story. One reading is one story.
	/// </summary>
	public bool Start(string reading)
	{
		if (_active && reading == _reading)
		{
			return false;
		}

		Stop();
		_reading = reading;
		_active = true;
		_listened = false;
		_playedBefore = 0;
		_reportedMilliseconds = 0;
		_playing = false;
		if (_audible)
		{
			Resume();
		}

		return true;
	}

	/// <summary>Audio stopped being heard (paused, loading, covered); ignored when it was not playing.</summary>
	public void Pause()
	{
		EndSegment();
	}

	/// <summary>Audio is heard again; ignored when no reading is active or it is already playing.</summary>
	public void Resume()
	{
		if (_active && !_playing)
		{
			_segmentStart = _nowMilliseconds();
			_playing = true;
		}
	}

	/// <summary>The reading ended: whatever was playing is added up and the reading is over.</summary>
	public void Stop()
	{
		EndSegment();
		_active = false;
	}

	/// <summary>
	/// The whole seconds heard since the last call (or the start), or null when no reading is active. Milliseconds are kept, so what is rounded off now is carried into the next call and
	/// the rows of one reading add up to its true total.
	/// </summary>
	public int? TakeSeconds()
	{
		if (!_active)
		{
			return null;
		}

		long seconds = (long)Math.Round((PlayedMilliseconds() - _reportedMilliseconds) / MillisecondsPerSecondExact, MidpointRounding.AwayFromZero);
		seconds = Math.Max(0, seconds);
		_reportedMilliseconds += seconds * MillisecondsPerSecond;
		return (int)seconds;
	}

	/// <summary>Milliseconds of playing left until this reading is a listen, or null when it is not playing, was not started or already counted. Never negative.</summary>
	public long? MillisecondsUntilListen()
	{
		if (!_active || !_playing || _listened)
		{
			return null;
		}

		return Math.Max(0, ListenSeconds * MillisecondsPerSecond - PlayedMilliseconds());
	}

	/// <summary>True exactly once per reading: when playing has reached <see cref="ListenSeconds"/>.</summary>
	public bool TryMarkListened()
	{
		if (!_active || _listened || PlayedMilliseconds() < ListenSeconds * MillisecondsPerSecond)
		{
			return false;
		}

		_listened = true;
		return true;
	}

	private long PlayedMilliseconds()
	{
		return _playedBefore + (_playing ? _nowMilliseconds() - _segmentStart : 0);
	}

	private void EndSegment()
	{
		if (!_active || !_playing)
		{
			return;
		}

		_playedBefore += Math.Max(0, _nowMilliseconds() - _segmentStart);
		_playing = false;
	}
}
