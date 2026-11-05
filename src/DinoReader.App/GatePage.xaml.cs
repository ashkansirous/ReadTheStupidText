using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The grown-up gate sheet. Step 1: press and hold the lock for 3 seconds (a "Continue" button
/// when a screen reader is on). Step 2: answer a sum on a number pad; a wrong answer rolls a new
/// sum. <see cref="Result"/> completes true on a right answer and false on any way out.
/// </summary>
public partial class GatePage : ContentPage, IDinoKeyTarget
{
	private const int KeyRows = OnboardingLayout.KeyRows;
	private const double SplitAnswersWidth = 330;
	private const double UnsetWidth = -1; // VisualElement.WidthRequest's "no request"
	private const double CardPadding = 40;
	private const double CardCorner = 32;
	private const double HoldRingTopMargin = 4;
	private const double CardKeyFontSize = 30;
	private const double PhoneKeyFontSize = 22;
	private const double CardSumFontSize = 44;
	private const double PhoneSumFontSize = 40;
	private const double HoldCaptionHeight = 20;
	private const double CardBackHeight = 52;
	private const double PhoneBackHeight = 48;
	private const string BackspaceKey = GateKeypad.BackspaceKey;
	private const string OkKey = GateKeypad.OkKey;
	private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(33);

	private readonly GateChallenge _challenge = new();
	private readonly HoldTracker _hold = new(HoldTracker.GateHold);
	private readonly HoldRingDrawable _ring = new();
	private readonly Stopwatch _clock = new();
	private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly IDispatcherTimer _timer;
	private string _answer = string.Empty;
	private bool _closing;
	private bool _keyHolding; // Slice 235 part B: the hold was started by the Space bar, so only letting go of it ends it
	private Size _windowSize;
	private GateShape? _shape;
	private ScrollView? _cardScroll;

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
		AddDesktopCaptions();
	}

	// Slice 235 part B (mock Batch 46 frames 158a and 158b): at a desktop window the hold step says the Space bar works too and the sum step says to type it, then Enter.
	// The on-screen pad stays, and typed digits feed the same answer as a pad press, so a second text field would only show the answer twice.
	private void AddDesktopCaptions()
	{
		if (!DesktopTips.IsWindows)
		{
			return;
		}

		HoldText.Children.Add(new Label
		{
			Text = AppResources.Gate_HoldCaptionWindows,
			IsVisible = HoldBody.IsVisible, // hidden with the hold instruction when a screen reader replaces the ring
			FontSize = 13,
			TextColor = DinoTokens.Get(DinoTokens.Muted),
			HorizontalTextAlignment = TextAlignment.Center,
		});
		SumText.Children.Add(new Label
		{
			Text = AppResources.Gate_TypeEnterWindows,
			FontSize = 13,
			TextColor = DinoTokens.Get(DinoTokens.Muted),
			HorizontalTextAlignment = TextAlignment.Center,
		});
	}

	// Slice 232 part E (mock Batch 44 frame 122): held sideways the gate splits (question left, answers or the hold ring right, 64 dp keys); upright it is the bottom sheet.
	// Rotating only re-places the views in SheetGrid: the step shown, the sum and the typed answer live in fields and views that a size change never touches.
	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		(width, height) = WindowShapeSeam.Effective(DesktopTips.IsWindows, width, height);
		if (width <= 0 || height <= 0 || (width == _windowSize.Width && height == _windowSize.Height))
		{
			return;
		}

		_windowSize = new Size(width, height);
		ApplyShape();
	}

	// Only the split flag matters: sideways the four key rows are star-sized and the keys fill them (40..64 dp), so the layout shares the height itself.
	private void ApplyShape()
	{
		GateShape shape = OnboardingLayout.Gate(_windowSize.Width, _windowSize.Height);
		if (shape != _shape)
		{
			_shape = shape;
			PlaceSheet(shape);
		}
	}

	private void PlaceSheet(GateShape shape)
	{
		Sheet.HorizontalOptions = LayoutOptions.Fill;
		Sheet.WidthRequest = UnsetWidth;
		if (shape.Split)
		{
			PlaceSplit();
		}
		else
		{
			PlaceStacked();
		}

		if (shape.Card)
		{
			PlaceCard(shape);
		}

		GridLength keyRow = shape.Split ? GridLength.Star : new GridLength(shape.KeyHeight);
		foreach (RowDefinition row in KeyPad.RowDefinitions)
		{
			row.Height = keyRow;
		}

		SizeForCard(shape);
		HostSheetGrid(shape);
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] gate split={shape.Split} step={(SumText.IsVisible ? "sum" : "hold")} card={shape.Card} cardWidth={shape.CardWidth} key={shape.KeyHeight} ring={shape.RingSize}");
#endif
	}

	// A tablet held upright (Slice 234 part B, mock Batch 45 frame 137): the sheet becomes a centred white card, 520 dp wide at most, floating over the dimmed page.
	private void PlaceCard(GateShape shape)
	{
		Sheet.VerticalOptions = LayoutOptions.Center;
		Sheet.HorizontalOptions = LayoutOptions.Center;
		Sheet.WidthRequest = shape.CardWidth;
		Sheet.Padding = new Thickness(CardPadding);
		Sheet.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(CardCorner) };
	}

	// The card never grows past the window less its margins and scrolls inside (a short window, a square split screen); the other shapes hold the grid directly,
	// because their star-sized rows need a bounded height. Only the grid's parent changes, so the step, the sum and the typed answer stay.
	// The hold ring, its caption and Back stay outside the scroll (a hold inside a ScrollView is taken as a drag and resets); only the title, text and number pad scroll,
	// and the ScrollView is always vertical, capped to the room the card has left (TabletCardLayout does the arithmetic).
	private void HostSheetGrid(GateShape shape)
	{
		Sheet.MaximumHeightRequest = shape.Card ? shape.CardMaxHeight : double.PositiveInfinity;
		if (shape.Card && _cardScroll is null)
		{
			EnterCardHosting();
		}
		else if (!shape.Card && _cardScroll is not null)
		{
			LeaveCardHosting();
		}

		UpdateCardScroll();
	}

	private void EnterCardHosting()
	{
		HoldAction.Children.Remove(RingBox);
		SheetGrid.Children.Remove(BackButton);
		Sheet.Content = null;
		_cardScroll = new ScrollView { Content = SheetGrid, Orientation = ScrollOrientation.Vertical };
		Sheet.Content = new VerticalStackLayout { Spacing = SheetGrid.RowSpacing, Children = { _cardScroll, RingBox, BackButton } };
	}

	private void LeaveCardHosting()
	{
		if (Sheet.Content is VerticalStackLayout column)
		{
			column.Children.Clear();
		}

		_cardScroll!.Content = null;
		_cardScroll = null;
		HoldAction.Children.Insert(0, RingBox);
		SheetGrid.Children.Add(BackButton);
		Sheet.Content = SheetGrid;
	}

	private void UpdateCardScroll()
	{
		if (_cardScroll is null || _shape is not { Card: true } shape)
		{
			return;
		}

		bool ringShown = RingBox.IsVisible && HoldAction.IsVisible;
		double below = TabletCardLayout.GateFixedBelow(ringShown, shape.RingSize, HoldRingTopMargin, HoldCaptionHeight, CardBackHeight, SheetGrid.RowSpacing);
		double room = TabletCardLayout.ScrollAreaHeight(shape.CardMaxHeight, 2 * CardPadding, below);
		_cardScroll.MaximumHeightRequest = room;
	}

	// The answers (and so the sum's type) are bigger on the card; the hold ring is the same drawn ring, scaled, with room made for it. Back on the phone's sizes elsewhere.
	private void SizeForCard(GateShape shape)
	{
		double scale = shape.RingSize / OnboardingLayout.PhoneHoldRing;
		double extra = (shape.RingSize - OnboardingLayout.PhoneHoldRing) / 2;
		HoldRingHost.Scale = scale;
		HoldRingHost.Margin = new Thickness(0, HoldRingTopMargin + extra, 0, extra);
		double keyMax = shape.Card ? shape.KeyHeight : OnboardingLayout.SidewaysKeyHeight;
		foreach (Button key in KeyPad.Children.OfType<Button>())
		{
			key.MaximumHeightRequest = keyMax;
			key.FontSize = shape.Card ? CardKeyFontSize : PhoneKeyFontSize;
		}

		SumLabel.FontSize = shape.Card ? CardSumFontSize : PhoneSumFontSize;
		AnswerLabel.FontSize = SumLabel.FontSize;
		BackButton.HeightRequest = shape.Card ? CardBackHeight : PhoneBackHeight;
	}

	private void PlaceStacked()
	{
		Sheet.VerticalOptions = LayoutOptions.End;
		Sheet.BackgroundColor = Colors.White;
		Sheet.Padding = new Thickness(22, 24, 22, 28);
		Sheet.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28, 28, 0, 0) };
		SheetGrid.ColumnDefinitions = [];
		SheetGrid.RowDefinitions = [.. Enumerable.Range(0, KeyRows).Select(_ => new RowDefinition(GridLength.Auto))];
		SheetGrid.VerticalOptions = LayoutOptions.Fill;
		Place(StepBars, 0, 0, 1, 1);
		Place(HoldText, 1, 0, 1, 1);
		Place(SumText, 1, 0, 1, 1);
		Place(HoldAction, 2, 0, 1, 1);
		Place(KeyPad, 2, 0, 1, 1);
		Place(BackButton, 3, 0, 1, 1);
		BackButton.HorizontalOptions = LayoutOptions.Center;
		StepBars.HorizontalOptions = LayoutOptions.Center;
		HoldAction.VerticalOptions = LayoutOptions.Start;
		KeyPad.VerticalOptions = LayoutOptions.Start;
	}

	private void PlaceSplit()
	{
		Sheet.VerticalOptions = LayoutOptions.Fill;
		Sheet.BackgroundColor = DinoTokens.Get(DinoTokens.Cream);
		// No cut-out inset of our own here: Sheet's SafeAreaEdges="Container" already keeps clear of a notch.
		Sheet.Padding = new Thickness(26, 16, 26, 16);
		Sheet.StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(0) };
		SheetGrid.ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(SplitAnswersWidth))];
		SheetGrid.ColumnSpacing = 24;
		// The third row takes the height left over, so the right column (spanning all four rows) is as tall as the sheet and the star-sized key rows share it.
		SheetGrid.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)];
		SheetGrid.VerticalOptions = LayoutOptions.Fill;
		Place(StepBars, 0, 0, 1, 1);
		Place(HoldText, 1, 0, 1, 1);
		Place(SumText, 1, 0, 1, 1);
		Place(BackButton, 3, 0, 1, 1);
		Place(HoldAction, 0, 1, 4, 1);
		Place(KeyPad, 0, 1, 4, 1);
		StepBars.HorizontalOptions = LayoutOptions.Start;
		BackButton.HorizontalOptions = LayoutOptions.Start;
		HoldAction.VerticalOptions = LayoutOptions.Center;
		KeyPad.VerticalOptions = LayoutOptions.Fill;
	}

	private static void Place(View view, int row, int column, int rowSpan, int columnSpan)
	{
		Grid.SetRow(view, row);
		Grid.SetColumn(view, column);
		Grid.SetRowSpan(view, rowSpan);
		Grid.SetColumnSpan(view, columnSpan);
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
		RingBox.IsVisible = !screenReaderOn;
		HoldRingHost.IsVisible = !screenReaderOn;
		HoldHint.IsVisible = !screenReaderOn;
		ContinueButton.IsVisible = screenReaderOn;
		HoldBody.IsVisible = !screenReaderOn;
	}

	private void OnHoldStart(object? sender, TouchEventArgs e) => StartHold();

	private void StartHold()
	{
		_hold.Start(_clock.Elapsed);
		HoldHint.Opacity = 1;
		_timer.Start();
	}

	private void OnHoldEnd(object? sender, TouchEventArgs e) => ResetHold();

	DinoScreen IDinoKeyTarget.Screen => DinoScreen.Gate;

	// Slice 235 part B: the Windows window's keys. Each returns false when it does not apply right now (the sum step is not showing, the screen-reader button replaced the ring),
	// so the key goes on to the focused control as it did before.
	bool IDinoKeyTarget.Handle(DinoKeyAction action, DinoKey key)
	{
		switch (action)
		{
			case DinoKeyAction.GateHoldStart:
				return StartKeyHold();
			case DinoKeyAction.GateHoldEnd:
				return EndKeyHold();
			case DinoKeyAction.GateDigit:
				return DinoKeyMap.DigitOf(key) is { } digit && PressSumKey(digit.ToString());
			case DinoKeyAction.GateErase:
				return !AnyButtonFocused() && PressSumKey(BackspaceKey);
			case DinoKeyAction.GateSubmit:
				return !AnyButtonFocused() && PressSumKey(OkKey); // Enter on a focused button (Tab got there) activates that button instead
			case DinoKeyAction.Back:
				_ = CloseAsync(false);
				return true;
			default:
				return false;
		}
	}

	// True while the keyboard focus sits on Back to story or Continue: Enter and Backspace then belong to that button.
	// The pad keys are never keyboard tab stops on Windows (see MakeKey), so focus can only be on these two or nowhere, and a mouse click on a pad key never leaves the sum keys dead.
	private bool AnyButtonFocused() => BackButton.IsFocused || ContinueButton.IsFocused;

	// Types a key on the sum step the way a pad press does (digits add, backspace removes one, Enter checks); false while the hold step shows.
	private bool PressSumKey(string key)
	{
		if (!SumText.IsVisible || _closing)
		{
			return false;
		}

		OnKey(key);
		return true;
	}

	// A key repeat on a held Space arrives as more starts: only the first one starts the ring, and none can restart it.
	private bool StartKeyHold()
	{
		if (_closing || !HoldRingHost.IsVisible || !HoldAction.IsVisible)
		{
			return false;
		}

		if (!_keyHolding)
		{
			_keyHolding = true;
			StartHold();
#if DEBUG
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write("[Keyboard] gate hold started by the Space bar");
#endif
		}

		return true;
	}

	private bool EndKeyHold()
	{
		if (!_keyHolding)
		{
			return false;
		}

		_keyHolding = false;
		ResetHold();
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write("[Keyboard] gate hold ended by letting go of the Space bar");
#endif
		return true;
	}

	private void OnHoldCancel(object? sender, EventArgs e) => ResetHold();

	private void ResetHold()
	{
		_keyHolding = false; // any end of the hold (let go, page covered, window deactivated) also ends a Space hold, so no later key-up is needed
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
		HoldText.IsVisible = false;
		HoldAction.IsVisible = false;
		RingBox.IsVisible = false;
		SumText.IsVisible = true;
		KeyPad.IsVisible = true;
		StepBarTwo.Color = DinoTokens.Get(DinoTokens.Brand);
		UpdateCardScroll();
	}

	private void BuildKeyPad()
	{
		for (int row = 0; row < KeyRows; row++)
		{
			KeyPad.AddRowDefinition(new RowDefinition(new GridLength(OnboardingLayout.UprightKeyHeight)));
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
			VerticalOptions = LayoutOptions.Fill,
			MinimumHeightRequest = OnboardingLayout.MinSidewaysKeyHeight,
			MaximumHeightRequest = OnboardingLayout.SidewaysKeyHeight,
			BackgroundColor = DinoTokens.Get(isOk ? DinoTokens.Brand : DinoTokens.Sand),
			TextColor = isOk ? Colors.White : DinoTokens.Get(DinoTokens.Ink),
		};
		SemanticProperties.SetDescription(button, key == BackspaceKey ? AppResources.Gate_Backspace : isOk ? AppResources.Common_Ok : key);
#if WINDOWS
		// Slice 235 part B: the keyboard types the digits, so Tab skips the pad; it stays a mouse and touch target. MAUI has no IsTabStop, so this is the native WinUI control's.
		button.HandlerChanged += (_, _) =>
		{
			if (button.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Control native)
			{
				native.IsTabStop = false;
			}
		};
#endif
		button.Clicked += (_, _) => PressPadKey(button, key);
		return button;
	}

	private void PressPadKey(Button button, string key)
	{
		if (DesktopTips.IsWindows)
		{
			button.Unfocus(); // a mouse click must not leave focus on the pad key
		}

		OnKey(key);
	}

	private void OnKey(string key)
	{
		if (GateKeypad.IsSubmit(key))
		{
			Submit();
			return;
		}

		_answer = GateKeypad.Press(_answer, key);
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
