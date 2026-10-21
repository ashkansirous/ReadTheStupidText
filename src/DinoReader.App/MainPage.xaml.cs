using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile;

namespace DinoReader;

public partial class MainPage : ContentPage
{
	// How long the page waits for the first audio after SpeakAsync returns before it stops
	// showing the loading state (same grace as the Read The Stupid Text reader page).
	private static readonly TimeSpan FirstAudioGrace = TimeSpan.FromSeconds(8);

	private readonly ISpeechReader _reader;
	private readonly VoicePackInstaller _installer;
	private readonly IVoiceModelService _model;
	private int _readSequence;
	private int _pendingReadId;
	private int _returnedReadId;

	public MainPage(ISpeechReader reader, VoicePackInstaller installer, IVoiceModelService model)
	{
		InitializeComponent();
		_reader = reader;
		_installer = installer;
		_model = model;
		ShowPackStatus();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_reader.StateChanged += OnReaderStateChanged;
		_installer.Changed += OnInstallerChanged;
		_model.ReadyChanged += OnInstallerChanged;
		ShowPackStatus();
		RenderPlayButton();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_reader.StateChanged -= OnReaderStateChanged;
		_installer.Changed -= OnInstallerChanged;
		_model.ReadyChanged -= OnInstallerChanged;
	}

	private async void OnPlayClicked(object? sender, EventArgs e)
	{
		// Stop works at every stage, including while the audio is still being generated.
		if (_pendingReadId != 0 || _reader.State != PlaybackState.Idle)
		{
			_reader.Stop();
			ClearPendingRead();
			return;
		}

		if (!string.IsNullOrWhiteSpace(TextEditor.Text))
		{
			await SpeakAsync(TextEditor.Text);
		}
	}

	// Each read gets an incrementing id. A superseded or stopped read never alerts and never
	// touches the current read's loading state.
	private async Task SpeakAsync(string text)
	{
		int id = ++_readSequence;
		_pendingReadId = id;
		RenderPlayButton();
		try
		{
			await _reader.SpeakAsync(text);
		}
		catch (OperationCanceledException)
		{
			ClearPendingReadIfCurrent(id);
			return;
		}
		catch (Exception)
		{
			bool wasCurrent = id == _pendingReadId;
			ClearPendingReadIfCurrent(id);
			if (wasCurrent)
			{
				await DisplayAlertAsync("Couldn't read that text", "Something went wrong while reading.", "OK");
			}

			return;
		}

		await WaitForFirstAudioAsync(id);
	}

	// SpeakAsync returned but nothing plays yet: keep "Loading..." until Playing, an Idle after
	// the return (ApplyPlaybackState), or the grace expires.
	private async Task WaitForFirstAudioAsync(int id)
	{
		if (id != _pendingReadId || _reader.State == PlaybackState.Playing)
		{
			ClearPendingReadIfCurrent(id);
			return;
		}

		_returnedReadId = id;
		await Task.Delay(FirstAudioGrace);
		ClearPendingReadIfCurrent(id);
	}

	private void ClearPendingReadIfCurrent(int id)
	{
		if (id == _pendingReadId && _reader.State != PlaybackState.Playing)
		{
			ClearPendingRead();
		}
	}

	private void ClearPendingRead()
	{
		_pendingReadId = 0;
		_returnedReadId = 0;
		RenderPlayButton();
	}

	private void OnReaderStateChanged(object? sender, PlaybackState state)
	{
		MainThread.BeginInvokeOnMainThread(() => ApplyPlaybackState(state));
	}

	private void ApplyPlaybackState(PlaybackState state)
	{
		bool idleAfterReturn = state == PlaybackState.Idle
			&& _pendingReadId != 0 && _pendingReadId == _returnedReadId;
		if (state == PlaybackState.Playing || idleAfterReturn)
		{
			ClearPendingRead();
			return;
		}

		RenderPlayButton();
	}

	private void RenderPlayButton()
	{
		if (_pendingReadId != 0 && _reader.State != PlaybackState.Playing)
		{
			PlayButton.Text = "Loading… (tap to stop)";
			return;
		}

		PlayButton.Text = _reader.State == PlaybackState.Idle ? "Play" : "Stop";
	}

	private void OnInstallerChanged(object? sender, EventArgs e)
	{
		MainThread.BeginInvokeOnMainThread(ShowPackStatus);
	}

	private void OnPackStatusTapped(object? sender, TappedEventArgs e)
	{
		if (_installer.CanRetry)
		{
			_ = _installer.StartAsync();
		}
	}

	// Every non-ready pack state, Failed included, has copy (with its retry hint) in
	// PackStatusText; tapping the label retries when the installer allows it.
	private void ShowPackStatus()
	{
		string? status = PackStatusText.Compose(_installer.State, _installer.Progress);
		StatusLabel.Text = status;
		StatusLabel.IsVisible = status is not null;
	}
}
