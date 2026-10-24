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

	private partial void PlatformPlay(string path, Action ended, Action failed)
	{
		var player = new MediaPlayer { Source = MediaSource.CreateFromUri(new Uri(path)) };
		player.MediaEnded += (_, _) =>
		{
			if (ReferenceEquals(_player, player))
			{
				ended();
			}
		};
		player.MediaFailed += (_, _) =>
		{
			if (ReferenceEquals(_player, player))
			{
				failed();
			}
		};
		_player = player;
		player.Play();
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
