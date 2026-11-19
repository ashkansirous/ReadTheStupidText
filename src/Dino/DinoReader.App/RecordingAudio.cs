using System.Diagnostics;

namespace DinoReader;

/// <summary>Records the microphone to one AAC/M4A file (Slice 111, Decision 115). One recording at a time; each platform has its own
/// recorder (Android 9-11 have no AAC in a recorder library, so none is used) in <c>Platforms/&lt;OS&gt;/RecordingAudio.*.cs</c>.</summary>
public interface IMicRecorder
{
	bool IsRecording { get; }

	/// <summary>Starts recording into <paramref name="path"/>; throws when the microphone cannot be opened.</summary>
	Task StartAsync(string path);

	/// <summary>Stops and returns how long the recording ran.</summary>
	Task<TimeSpan> StopAsync();

	/// <summary>Stops and drops what was recorded; never throws.</summary>
	Task CancelAsync();
}

/// <summary>Plays one recorded page (Slice 111). <see cref="Play(string, Action, Action)"/> calls <c>ended</c> once when the file played to its end, or <c>failed</c> once when it cannot be played (a corrupt or empty file), never after <see cref="Stop"/> or a newer <see cref="Play(string, Action, Action)"/>; both run after the native player was released, on the main loop (UI dispatcher on Windows), never inside the native finished call.</summary>
public interface IRecordingPlayer
{
	void Play(string path, Action ended, Action failed);

	/// <summary>Plays at <paramref name="rate"/> times the normal speed (Slice 246 C2: a pre-made clip follows the reading speed); the pitch is kept. The rate is for this play only.</summary>
	void Play(string path, Action ended, Action failed, double rate);

	void Pause();

	void Resume();

	void Stop();
}

/// <summary>The shared half of the recorder: the length is the wall-clock time between the platform start and stop.</summary>
internal sealed partial class MicRecorder : IMicRecorder, IDisposable
{
	private readonly Stopwatch _clock = new();
	private IDisposable? _lockHold; // Slice 418 decision 16: a recording in progress holds the shelf's 30-second lock, the same on Android, iOS and Windows (this half is shared)

	public bool IsRecording { get; private set; }

	public async Task StartAsync(string path)
	{
		if (IsRecording)
		{
			throw new InvalidOperationException("A recording is already running.");
		}

		ReleaseLockHold(); // a hold left by an abandoned earlier recording never stacks under this one
		await PlatformStartAsync(path);
		_clock.Restart();
		IsRecording = true;
		_lockHold = LockHolds.Begin("recording");
	}

	/// <summary>An abandoned recorder (the page closed, an error, the app tearing down) gives the shelf's clock back.</summary>
	public void Dispose() => ReleaseLockHold();

	private void ReleaseLockHold()
	{
		_lockHold?.Dispose();
		_lockHold = null;
	}

	public async Task<TimeSpan> StopAsync()
	{
		if (!IsRecording)
		{
			return TimeSpan.Zero;
		}

		IsRecording = false;
		_clock.Stop();
		ReleaseLockHold();
		await PlatformStopAsync();
		return _clock.Elapsed;
	}

	public async Task CancelAsync()
	{
		try
		{
			await StopAsync();
		}
		catch (Exception ex)
		{
			// no-fallback: a cancel discards the take anyway; nothing the person hears or sees changes
			Debug.WriteLine($"Dino Reader could not stop a recording cleanly: {ex.Message}");
		}
	}

	private partial Task PlatformStartAsync(string path);

	private partial Task PlatformStopAsync();
}

/// <summary>The shared half of the page player; the platform half owns the native player.</summary>
internal sealed partial class RecordingPlayer : IRecordingPlayer
{
	public const double NormalRate = 1.0;

	public void Play(string path, Action ended, Action failed) => Play(path, ended, failed, NormalRate);

	private readonly PlayerHandOff _handOff = new(ex => AudioPackLog.Write($"player release failed: {ex.GetType().Name}"));

	public void Play(string path, Action ended, Action failed, double rate)
	{
		Stop();
		int generation = _handOff.Begin();
		// Slice 270: the platform's finished call only queues these; the native player is released and the callback runs after that call has returned.
		PlatformPlay(path, _handOff.Defer(generation, ended, PlatformPost, PlatformStop), _handOff.Defer(generation, failed, PlatformPost, PlatformStop), rate);
	}

	// A clip that already reported its end or failure is finished: a Pause or Resume before the queued hand-off runs must not touch (restart) it.
	public void Pause() => _handOff.RunUnlessFinishing(PlatformPause);

	public void Resume() => _handOff.RunUnlessFinishing(PlatformResume);

	public void Stop()
	{
		_handOff.Stop();
		PlatformStop();
	}

	/// <summary>Runs <paramref name="action"/> on a later turn of the platform's loop, never inside the native call that raised it.</summary>
	private partial void PlatformPost(Action action);

	private partial void PlatformPlay(string path, Action ended, Action failed, double rate);

	private partial void PlatformPause();

	private partial void PlatformResume();

	private partial void PlatformStop();
}
