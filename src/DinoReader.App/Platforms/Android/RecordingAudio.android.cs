using Android.Media;

namespace DinoReader;

// Android 9-11 (minSdk 28) record AAC in an MPEG-4 file through MediaRecorder; a recorder library only does AAC from Android 12.
internal sealed partial class MicRecorder
{
	private const int SampleRate = 44100;
	private const int BitRate = 64000;

	private MediaRecorder? _recorder;

	private partial Task PlatformStartAsync(string path)
	{
		MediaRecorder recorder = OperatingSystem.IsAndroidVersionAtLeast(31) ? new MediaRecorder(Android.App.Application.Context) : new MediaRecorder();
		try
		{
			recorder.SetAudioSource(AudioSource.Mic);
			recorder.SetOutputFormat(OutputFormat.Mpeg4);
			recorder.SetAudioEncoder(AudioEncoder.Aac);
			recorder.SetAudioChannels(1);
			recorder.SetAudioSamplingRate(SampleRate);
			recorder.SetAudioEncodingBitRate(BitRate);
			recorder.SetOutputFile(path);
			recorder.Prepare();
			recorder.Start();
		}
		catch
		{
			recorder.Release();
			recorder.Dispose();
			throw;
		}

		_recorder = recorder;
		return Task.CompletedTask;
	}

	private partial Task PlatformStopAsync()
	{
		MediaRecorder? recorder = _recorder;
		_recorder = null;
		if (recorder is null)
		{
			return Task.CompletedTask;
		}

		try
		{
			recorder.Stop();
		}
		finally
		{
			recorder.Release();
			recorder.Dispose();
		}

		return Task.CompletedTask;
	}
}

internal sealed partial class RecordingPlayer
{
	private MediaPlayer? _player;

	private partial void PlatformPlay(string path, Action ended, Action failed)
	{
		var player = new MediaPlayer();
		try
		{
			player.SetAudioAttributes(new AudioAttributes.Builder()!.SetContentType(AudioContentType.Speech)!.SetUsage(AudioUsageKind.Media)!.Build()!);
			player.SetDataSource(path);
			player.Prepare();
			player.Error += (_, e) =>
			{
				e.Handled = true;
				if (ReferenceEquals(_player, player))
				{
					ReleaseCurrent();
					failed();
				}
			};
			player.Completion += (_, _) =>
			{
				if (ReferenceEquals(_player, player))
				{
					ReleaseCurrent();
					ended();
				}
			};
			_player = player;
			player.Start();
		}
		catch
		{
			_player = null;
			player.Release();
			player.Dispose();
			throw;
		}
	}

	private partial void PlatformPause()
	{
		if (_player is { IsPlaying: true } player)
		{
			player.Pause();
		}
	}

	private partial void PlatformResume() => _player?.Start();

	private partial void PlatformStop() => ReleaseCurrent();

	// Frees the native player (its audio focus and file handle) once it has finished, failed or been stopped.
	private void ReleaseCurrent()
	{
		MediaPlayer? player = _player;
		_player = null;
		if (player is null)
		{
			return;
		}

		player.Release();
		player.Dispose();
	}
}
