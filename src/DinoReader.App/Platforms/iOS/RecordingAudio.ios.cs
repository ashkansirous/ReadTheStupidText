using AudioToolbox;
using AVFoundation;
using Foundation;
using ReadTheStupidText.Mobile.Shared;

namespace DinoReader;

internal sealed partial class MicRecorder
{
	private const int SampleRate = 44100;
	private const int BitRate = 64000;

	private AVAudioRecorder? _recorder;

	private partial Task PlatformStartAsync(string path)
	{
		AVAudioSession session = AVAudioSession.SharedInstance();
#pragma warning disable CS0618
		session.SetCategory(AVAudioSession.CategoryPlayAndRecord, AVAudioSessionCategoryOptions.DefaultToSpeaker, out NSError? categoryError);
#pragma warning restore CS0618
		session.SetActive(true, out NSError? activeError);
		if (categoryError is not null || activeError is not null)
		{
			throw new IOException("The audio session could not be opened for recording.");
		}

		var settings = new AudioSettings
		{
			Format = AudioFormatType.MPEG4AAC,
			SampleRate = SampleRate,
			NumberChannels = 1,
			EncoderBitRate = BitRate,
		};
		AVAudioRecorder? recorder = AVAudioRecorder.Create(NSUrl.FromFilename(path), settings, out NSError? error);
		if (recorder is null || error is not null || !recorder.PrepareToRecord() || !recorder.Record())
		{
			recorder?.Dispose();
			ReleaseSession();
			throw new IOException("The microphone could not be opened.");
		}

		_recorder = recorder;
		return Task.CompletedTask;
	}

	// Lets go of the microphone session (so other audio comes back) and puts back the category every reader in the app uses.
	private static void ReleaseSession()
	{
		AVAudioSession session = AVAudioSession.SharedInstance();
		session.SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out NSError? _);
#pragma warning disable CS0618
		session.SetCategory(AVAudioSession.CategoryPlayback, out NSError? _);
#pragma warning restore CS0618
	}

	private partial Task PlatformStopAsync()
	{
		AVAudioRecorder? recorder = _recorder;
		_recorder = null;
		recorder?.Stop();
		recorder?.Dispose();
		ReleaseSession();
		return Task.CompletedTask;
	}
}

internal sealed partial class RecordingPlayer
{
	private AVAudioPlayer? _player;

	private partial void PlatformPlay(string path, Action ended, Action failed)
	{
		IosAudioSession.Activate(this);
		AVAudioPlayer? player = AVAudioPlayer.FromUrl(NSUrl.FromFilename(path), out NSError? error);
		if (player is null || error is not null)
		{
			IosAudioSession.Deactivate(this);
			throw new IOException("The recording could not be opened.");
		}

		player.FinishedPlaying += (_, e) =>
		{
			if (!ReferenceEquals(_player, player))
			{
				return;
			}

			IosAudioSession.Deactivate(this);
			if (e.Status)
			{
				ended();
			}
			else
			{
				failed(); // an interrupted or undecodable play did not reach its end: the page reads in the generated voice
			}
		};
		player.DecoderError += (_, _) =>
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

	private partial void PlatformResume()
	{
		if (_player is { } player)
		{
			IosAudioSession.Activate(this);
			player.Play();
		}
	}

	private partial void PlatformStop()
	{
		AVAudioPlayer? player = _player;
		_player = null;
		player?.Stop();
		player?.Dispose();
		IosAudioSession.Deactivate(this);
	}
}
