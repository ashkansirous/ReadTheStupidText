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

/// <summary>Plays one recorded page (Slice 111). <see cref="Play"/> calls <c>ended</c> once when the file played to its end, or <c>failed</c> once when it cannot be played (a corrupt or empty file), never after <see cref="Stop"/> or a newer <see cref="Play"/>; either may be called off the main thread.</summary>
public interface IRecordingPlayer
{
	void Play(string path, Action ended, Action failed);

	void Pause();

	void Resume();

	void Stop();
}

/// <summary>The shared half of the recorder: the length is the wall-clock time between the platform start and stop.</summary>
internal sealed partial class MicRecorder : IMicRecorder
{
	private readonly Stopwatch _clock = new();

	public bool IsRecording { get; private set; }

	public async Task StartAsync(string path)
	{
		if (IsRecording)
		{
			throw new InvalidOperationException("A recording is already running.");
		}

		await PlatformStartAsync(path);
		_clock.Restart();
		IsRecording = true;
	}

	public async Task<TimeSpan> StopAsync()
	{
		if (!IsRecording)
		{
			return TimeSpan.Zero;
		}

		IsRecording = false;
		_clock.Stop();
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
			Debug.WriteLine($"Dino Reader could not stop a recording cleanly: {ex.Message}");
		}
	}

	private partial Task PlatformStartAsync(string path);

	private partial Task PlatformStopAsync();
}

/// <summary>The shared half of the page player; the platform half owns the native player.</summary>
internal sealed partial class RecordingPlayer : IRecordingPlayer
{
	public void Play(string path, Action ended, Action failed)
	{
		Stop();
		PlatformPlay(path, ended, failed);
	}

	public void Pause() => PlatformPause();

	public void Resume() => PlatformResume();

	public void Stop() => PlatformStop();

	private partial void PlatformPlay(string path, Action ended, Action failed);

	private partial void PlatformPause();

	private partial void PlatformResume();

	private partial void PlatformStop();
}
