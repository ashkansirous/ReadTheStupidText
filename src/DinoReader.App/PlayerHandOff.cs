namespace DinoReader;

/// <summary>Slice 270: a native player's "finished" or "failed" call must return before the app starts, stops or disposes any player (AVAudioPlayer throws ObjectDisposedException
/// when its player is disposed inside its own delegate call, and the next clip's Play stops the old one). This hands <c>ended</c> and <c>failed</c> to a later turn of the platform's
/// loop (<paramref name="post"/>) and drops them when a <see cref="Stop"/> or a newer <see cref="Begin"/> came in meanwhile.
/// Single-threaded by construction: every member runs on the UI thread. Each platform delivers the native ended/failed call to the UI thread before it reaches <see cref="Defer"/>
/// (Android and iOS already do; Windows marshals MediaEnded and MediaFailed to the dispatcher), so there is no lock and no native call under one.
///</summary>
internal sealed class PlayerHandOff
{
	private readonly Action<Exception>? _logError;
	private int _generation;

	public PlayerHandOff(Action<Exception>? logError = null) => _logError = logError;

	/// <summary>True from the moment the platform reported the play finished or failed until a new play began: Pause and Resume must not touch the player then.</summary>
	public bool IsFinishing { get; private set; }

	/// <summary>Starts a new play: every callback handed out before it is now stale.</summary>
	public int Begin()
	{
		IsFinishing = false;
		return ++_generation;
	}

	/// <summary>Stops the current play: its callbacks are stale.</summary>
	public void Stop()
	{
		IsFinishing = false;
		_generation++;
	}

	/// <summary>Runs <paramref name="action"/> (a native Pause or Resume) unless the clip already finished.</summary>
	public void RunUnlessFinishing(Action action)
	{
		if (!IsFinishing)
		{
			action();
		}
	}

	/// <summary>Wraps <paramref name="callback"/> so it runs via <paramref name="post"/>, after the native call returned, and only while <paramref name="generation"/> is still current.
	/// <paramref name="release"/> frees the native player first (also off the native call), so an ended clip never leaks its player.</summary>
	public Action Defer(int generation, Action callback, Action<Action> post, Action release)
	{
		return () =>
		{
			if (_generation == generation)
			{
				IsFinishing = true; // synchronous, in the native call: a Resume before the queued turn must not restart the finished clip
			}

			post(() => Run(generation, callback, release));
		};
	}

	private void Run(int generation, Action callback, Action release)
	{
		// Claims the play: a second callback of the same play (iOS can report a decoder error and then a failed finish) is stale too.
		if (_generation != generation)
		{
			return;
		}

		_generation++;
		try
		{
			release();
		}
		catch (Exception ex)
		{
			_logError?.Invoke(ex); // a failed release must not skip the callback (the read would hang) or escape the platform loop
		}

		callback();
	}
}
