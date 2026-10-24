using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>
/// The grown-up gate sheet. Step 1: press and hold the lock for 3 seconds (a "Continue" button
/// when a screen reader is on). Step 2: answer a sum on a number pad; a wrong answer rolls a new
/// sum. <see cref="Result"/> completes true on a right answer and false on any way out.
/// </summary>
public partial class GatePage : ContentPage
{
	private const int MaxAnswerDigits = 2;
	private const int KeyRows = 4;
	private const string BackspaceKey = "⌫";
	private const string OkKey = "OK";
	private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(33);

	private readonly GateChallenge _challenge = new();
	private readonly HoldTracker _hold = new(HoldTracker.GateHold);
	private readonly HoldRingDrawable _ring = new();
	private readonly Stopwatch _clock = new();
	private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly IDispatcherTimer _timer;
	private string _answer = string.Empty;
	private bool _closing;

	public GatePage(bool screenReaderOn)
	{
		InitializeComponent();
		// A modal page does not inherit the navigation page's direction: the sum and the number pad stay left to right.
		FlowDirection = UiFlow.Screen;
		HoldRing.Drawable = _ring;
		_timer = Dispatcher.CreateTimer();
		_timer.Interval = TickInterval;
		_timer.Tick += OnTick;
		_clock.Start();
		ShowHoldAlternative(screenReaderOn);
		BuildKeyPad();
		ShowSum();
	}

	public Task<bool> Result => _result.Task;

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		ResetHold();
	}

	protected override bool OnBackButtonPressed()
	{
		_ = CloseAsync(false);
		return true;
	}

	private void ShowHoldAlternative(bool screenReaderOn)
	{
		HoldRingHost.IsVisible = !screenReaderOn;
		HoldHint.IsVisible = !screenReaderOn;
		ContinueButton.IsVisible = screenReaderOn;
		HoldBody.IsVisible = !screenReaderOn;
	}

	private void OnHoldStart(object? sender, TouchEventArgs e)
	{
		_hold.Start(_clock.Elapsed);
		HoldHint.Opacity = 1;
		_timer.Start();
	}

	private void OnHoldEnd(object? sender, TouchEventArgs e) => ResetHold();

	private void OnHoldCancel(object? sender, EventArgs e) => ResetHold();

	private void ResetHold()
	{
		_timer.Stop();
		_hold.Cancel();
		_ring.Progress = 0;
		HoldHint.Opacity = 0;
		HoldRing.Invalidate();
	}

	private void OnTick(object? sender, EventArgs e)
	{
		TimeSpan now = _clock.Elapsed;
		_ring.Progress = _hold.Progress(now);
		HoldRing.Invalidate();
		if (_hold.IsComplete(now))
		{
			ResetHold();
			ShowSumStep();
		}
	}

	private void OnContinueClicked(object? sender, EventArgs e) => ShowSumStep();

	private void ShowSumStep()
	{
		HoldStep.IsVisible = false;
		SumStep.IsVisible = true;
		StepBarTwo.Color = DinoTokens.Get(DinoTokens.Brand);
	}

	private void BuildKeyPad()
	{
		for (int row = 0; row < KeyRows; row++)
		{
			KeyPad.AddRowDefinition(new RowDefinition(new GridLength(52)));
		}

		string[] keys = ["1", "2", "3", "4", "5", "6", "7", "8", "9", BackspaceKey, "0", OkKey];
		for (int i = 0; i < keys.Length; i++)
		{
			KeyPad.Add(MakeKey(keys[i]), i % 3, i / 3);
		}
	}

	private Button MakeKey(string key)
	{
		bool isOk = key == OkKey;
		var button = new Button
		{
			Text = isOk ? AppResources.Common_Ok : key,
			FontSize = 22,
			CornerRadius = 14,
			Padding = 0,
			BackgroundColor = DinoTokens.Get(isOk ? DinoTokens.Brand : DinoTokens.Sand),
			TextColor = isOk ? Colors.White : DinoTokens.Get(DinoTokens.Ink),
		};
		SemanticProperties.SetDescription(button, key == BackspaceKey ? AppResources.Gate_Backspace : isOk ? AppResources.Common_Ok : key);
		button.Clicked += (_, _) => OnKey(key);
		return button;
	}

	private void OnKey(string key)
	{
		switch (key)
		{
			case OkKey:
				Submit();
				return;
			case BackspaceKey:
				_answer = _answer.Length > 0 ? _answer[..^1] : _answer;
				break;
			default:
				_answer = _answer.Length < MaxAnswerDigits ? _answer + key : _answer;
				break;
		}

		AnswerLabel.Text = _answer;
	}

	private void Submit()
	{
		if (!int.TryParse(_answer, out int value))
		{
			return;
		}

		if (_challenge.Submit(value) == GateOutcome.Passed)
		{
			_ = CloseAsync(true);
			return;
		}

		_answer = string.Empty;
		ShowSum();
	}

	private void ShowSum()
	{
		SumLabel.Text = $"{_challenge.Left} + {_challenge.Right} =";
		AnswerLabel.Text = _answer;
		SemanticProperties.SetDescription(SumLabel, AppResources.Format(AppResources.Gate_SumQuestion, _challenge.Left, _challenge.Right));
	}

	/// <summary>Closes the gate as a "no": the app left the foreground, so the gate is needed again.</summary>
	public Task DismissAsync() => CloseAsync(false);

	// Pops first, then completes, so the caller's next push never runs under a still-open modal.
	// The result is set even if the pop fails, so the caller is never left waiting.
	private async Task CloseAsync(bool passed)
	{
		if (_closing)
		{
			return;
		}

		_closing = true;
		ResetHold();
		try
		{
			await Navigation.PopModalAsync();
		}
		finally
		{
			_result.TrySetResult(passed);
		}
	}

	private void OnBackClicked(object? sender, EventArgs e) => _ = CloseAsync(false);
}
