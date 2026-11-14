using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Storage;

namespace DinoReader;

// The microphone is a packaged-app capability (Package.appxmanifest); the first InitializeAsync asks the user, and a refusal throws.
internal sealed partial class MicRecorder
{
	private MediaCapture? _capture;

	private partial Task PlatformStartAsync(string path) => StartCoreAsync(path);

	private async Task StartCoreAsync(string path)
	{
		var capture = new MediaCapture();
		try
		{
			await capture.InitializeAsync(new MediaCaptureInitializationSettings { StreamingCaptureMode = StreamingCaptureMode.Audio });
			await File.WriteAllBytesAsync(path, []);
			StorageFile file = await StorageFile.GetFileFromPathAsync(path);
			await capture.StartRecordToStorageFileAsync(MediaEncodingProfile.CreateM4a(AudioEncodingQuality.Medium), file);
		}
		catch
		{
			// no-fallback: releases the capture and rethrows
			capture.Dispose();
			throw;
		}

		_capture = capture;
	}

	private partial Task PlatformStopAsync() => StopCoreAsync();

	private async Task StopCoreAsync()
	{
		MediaCapture? capture = _capture;
		_capture = null;
		if (capture is null)
		{
			return;
		}

		try
		{
			await capture.StopRecordAsync();
		}
		finally
		{
			capture.Dispose();
		}
	}
}

internal sealed partial class RecordingPlayer
{
	private MediaPlayer? _player;

	private partial void PlatformPlay(string path, Action ended, Action failed, double rate)
	{
		var player = new MediaPlayer { Source = MediaSource.CreateFromUri(new Uri(path)) };
		player.MediaEnded += (_, _) => OnUiThread(player, ended);
		player.MediaFailed += (_, _) => OnUiThread(player, failed);
		_player = player;
		player.PlaybackSession.PlaybackRate = rate; // MediaPlayer's own rate property is the deprecated spelling of the session's; the pitch is kept by default
		player.Play();
	}

	// MediaEnded and MediaFailed come on a worker thread. Slice 270: they are moved to the UI dispatcher here, at the source, before they reach the hand-off, so the hand-off (no lock)
	// only ever runs on the UI thread with Play, Stop, Pause and Resume. With no dispatcher or a refused Dispatch (the app is shutting down) the event is logged and dropped, never run on this thread.
	private void OnUiThread(MediaPlayer player, Action native)
	{
		if (!Post(() =>
		{
			if (ReferenceEquals(_player, player))
			{
				native();
			}
		}))
		{
			AudioPackLog.Write("player event dropped: no UI dispatcher");
		}
	}

	// IDispatcher.Dispatch always queues (checked with context7, dotnet/maui), so the hand-off's Run is a later turn than the call that posted it.
	private partial void PlatformPost(Action action)
	{
		if (!Post(action))
		{
			AudioPackLog.Write("player hand-off dropped: no UI dispatcher");
		}
	}

	private static bool Post(Action action)
	{
		var dispatcher = Microsoft.Maui.Controls.Application.Current?.Dispatcher;
		return dispatcher is not null && dispatcher.Dispatch(action);
	}

	private partial void PlatformPause() => _player?.Pause();

	private partial void PlatformResume() => _player?.Play();

	private partial void PlatformStop()
	{
		MediaPlayer? player = _player;
		_player = null;
		player?.Pause();
		player?.Dispose();
	}
}
