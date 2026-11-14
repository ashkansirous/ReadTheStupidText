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
			// no-fallback: releases the recorder and rethrows
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
	private double _rate = NormalRate;

	private partial void PlatformPlay(string path, Action ended, Action failed, double rate)
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
					failed(); // Slice 270: only queues the hand-off; the player is released after this call has returned
				}
			};
			player.Completion += (_, _) =>
			{
				if (ReferenceEquals(_player, player))
				{
					ended();
				}
			};
			_player = player;
			_rate = rate;
			ApplyRate(player, rate); // on a prepared player this already starts it, at this speed, so no clip begins at 1.0x; Start then changes nothing
			player.Start();
		}
		catch
		{
			// no-fallback: releases the player and rethrows
			_player = null;
			player.Release();
			player.Dispose();
			throw;
		}
	}

	// PlaybackParams.SetSpeed (API 23+, minSdk is 28) keeps the pitch. It is set after Start, so a player that is already running takes it and a paused one is never started by it.
	// A rate the platform refuses leaves the clip at normal speed.
	private static void ApplyRate(MediaPlayer player, double rate)
	{
		if (Math.Abs(rate - NormalRate) < 0.001)
		{
			return;
		}

		try
		{
			player.PlaybackParams = new PlaybackParams().SetSpeed((float)rate)!;
		}
		catch (Exception ex) when (ex is Java.Lang.IllegalArgumentException or Java.Lang.IllegalStateException)
		{
			DinoFallbacks.Report(Sirous.Reading.Application.Usage.FallbackArea.PremadeAudio, Sirous.Reading.Application.Usage.FallbackReason.ClipSpeedUnset); // Slice 273 part D: the clip plays at its own speed
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not set the clip speed {rate}: {ex.Message}");
		}
	}

	private partial void PlatformPause()
	{
		if (_player is { IsPlaying: true } player)
		{
			player.Pause();
		}
	}

	private partial void PlatformResume()
	{
		if (_player is { } player)
		{
			player.Start();
			ApplyRate(player, _rate); // a resumed player is given its speed again, so a pause never drops it
		}
	}

	private partial void PlatformStop() => ReleaseCurrent();

	private partial void PlatformPost(Action action) => new Android.OS.Handler(Android.OS.Looper.MainLooper!).Post(action);

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
