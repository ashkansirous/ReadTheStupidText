using System.Diagnostics;
using System.Globalization;
using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>
/// The grown-up gate: a bottom sheet, or from 600 wide a centred card (a short card under 540 high). Step 1: press and hold the lock for 3 seconds (a "Continue" button
/// when a screen reader is on). Step 2: answer a sum on a number pad; a wrong answer rolls a new
/// sum. <see cref="Result"/> completes true on a right answer and false on any way out.
/// </summary>
public partial class GatePage : ContentPage, IDinoKeyTarget
{
	private const int KeyRows = OnboardingLayout.KeyRows;
	private const double UnsetWidth = -1; // VisualElement.WidthRequest's "no request"

	// Slice 363, mock Missing States G1-G3: the card's sizes. Upright (G1, G2) padding 28 36 26; short (G3) padding 20 28 18.
	private const int CardUprightRows = 4;
	private const double CardRingSize = 150;
	private const double CardShortRing = 110;
	private const double CardUprightEyebrow = 14;
	private const double CardShortEyebrow = 13;
	private const double CardEyebrowTracking = 0.6;
	private const double CardUprightTitle = 28;
	private const double CardSumTitle = 32;
	private const double CardShortTitle = 24;
	private const double CardKeyHeight = 60;
	private const double CardKeyFont = 26;
	private const double CardKeyGap = 10;
	private const double CardShortKeyHeight = 56;
	private const double CardShortKeyWidth = 66;
	private const double CardShortKeyFont = 24;
	private const double CardShortKeyGap = 8;
	private const double KeyCapFont = 13;
	private const int CardKeyColumns = 3;
	private static readonly Color BarIdle = Color.FromArgb("#e6e0d4");
	private static readonly Thickness CardUprightPadding = new(36, 28, 36, 26);
	private static readonly Thickness CardShortPadding = new(28, 20, 28, 18);
	private static readonly Color CardScrim = Color.FromRgba(43, 37, 64, 158); // rgba(43,37,64,.62)
	private static readonly Color KeyCapFill = Colors.White;
	private const string BackspaceKey = GateKeypad.BackspaceKey;
	private const string OkKey = GateKeypad.OkKey;
	private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(33);

	private readonly GateChallenge _challenge = new();
	private readonly HoldTracker _hold = new(HoldTracker.GateHold);
	private readonly HoldRingDrawable _ring = new();
	private readonly CardRingDrawable _cardRing = new();
	private readonly Stopwatch _clock = new();
	private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly IDispatcherTimer _timer;
	private string _answer = string.Empty;
	private bool _closing;
	private bool _keyHolding; // Slice 235 part B: the hold was started by the Space bar, so only letting go of it ends it
	private Size _windowSize;
	private GateShape? _shape;
	private bool _sumStep;
	private bool _screenReaderOn;

	public GatePage(bool screenReaderOn)
	{
		InitializeComponent();
		// A modal page does not inherit the navigation page's direction: the sum and the number pad stay left to right.
		FlowDirection = UiFlow.Screen;
		HoldRing.Drawable = _ring;
		CardRing.Drawable = _cardRing;
		_timer = Dispatcher.CreateTimer();
		_timer.Interval = TickInterval;
		_timer.Tick += OnTick;
		_clock.Start();
		_screenReaderOn = screenReaderOn;
		ShowHoldAlternative(screenReaderOn);
		BuildKeyPad();
		ShowSum();
		AddDesktopCaptions();
	}

	// Slice 363 (mock G1, G2): the card's captions. The hold step says the Space bar works too (Windows); the sum step says to type it, then Enter, or click the answer (Windows).
	// The key word is drawn as a key cap inside the one translated string, never as a second string.
	private void ShowCardStep()
	{
		CardEyebrow.Text = AppResources.Gallery_ForGrownUps.ToUpper(CultureInfo.CurrentUICulture);
		// Letter-spacing pulls joined Arabic and Farsi letters apart, so those two scripts get none.
		CardEyebrow.CharacterSpacing = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? 0 : CardEyebrowTracking;
		CardTitle.Text = _sumStep ? AppResources.Format(AppResources.Gate_SumQuestion, _challenge.Left, _challenge.Right) : AppResources.Gate_HoldDescription;
		// The ring already carries the hold step's name, so the title says it once only: two elements with one name are one too many for a screen reader and for the VM's UIA lookup.
		AutomationProperties.SetIsInAccessibleTree(CardTitle, _sumStep);
		CardHoldHint.FormattedText = DesktopTips.IsWindows ? KeyCapCaption(AppResources.Gate_HoldCaptionWindows, AppResources.Key_Space) : new FormattedString { Spans = { new Span { Text = AppResources.Gate_KeepHolding } } };
		CardHoldHint.Opacity = DesktopTips.IsWindows ? 1 : 0;
		CardSumHint.FormattedText = KeyCapCaption(AppResources.Gate_TypeOrClickWindows, AppResources.Key_Enter);
		CardScroll.Orientation = _sumStep ? ScrollOrientation.Vertical : ScrollOrientation.Neither;
		CardTitle.IsVisible = _sumStep || !_screenReaderOn; // a screen reader's hold step is the Continue button alone
		CardRingBox.IsVisible = !_sumStep;
		CardRingHost.IsVisible = !_screenReaderOn;
		CardContinue.IsVisible = !_sumStep && _screenReaderOn;
		CardField.IsVisible = _sumStep;
		CardKeys.IsVisible = _sumStep;
		CardBarTwo.Color = _sumStep ? DinoTokens.Get(DinoTokens.Brand) : BarIdle;
		if (_shape is { Card: true })
		{
			PlaceCardBody();
		}
	}

	private static FormattedString KeyCapCaption(string caption, string keyName)
	{
		CaptionParts parts = CaptionParts.Split(caption, keyName);
		var text = new FormattedString();
		text.Spans.Add(new Span { Text = parts.Before });
		if (parts.Key.Length > 0)
		{
			text.Spans.Add(new Span { Text = $" {parts.Key} ", BackgroundColor = KeyCapFill, TextColor = DinoTokens.Get(DinoTokens.Ink), FontSize = KeyCapFont, FontAttributes = FontAttributes.Bold });
			text.Spans.Add(new Span { Text = parts.After });
		}

		return text;
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

	// Slice 363: the window size picks the bottom sheet, the card or the short card (OnboardingLayout.Gate); the card's height is capped to the window.
	// Rotating or resizing only re-places the views: the step shown, the sum and the typed answer live in fields and views that a size change never touches.
	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		(width, height) = WindowShapeSeam.Effective(DesktopTips.IsWindows, width, height); // Slice 235 part C: through the seam, like every page
		if (width <= 0 || height <= 0 || (width == _windowSize.Width && height == _windowSize.Height))
		{
			return;
		}

		_windowSize = new Size(width, height);
		ApplyShape();
	}

	// Slice 363: only the mode (sheet, card, short card) and the card's width come from the size. The size comes through WindowShapeSeam.Effective first, which can shrink the height on Windows (900 wide gives 599), so the rule sees that height.
	private void ApplyShape()
	{
		GateShape shape = OnboardingLayout.Gate(_windowSize.Width, _windowSize.Height);
		// No fixed height cap: CardRoot (Container safe area + 24 dp padding) hands the card only the room left after the notch, the bars and its padding, so a tall card scrolls in that room.
		if (shape != _shape)
		{
			_shape = shape;
			PlaceGate(shape);
		}
	}

	private void PlaceGate(GateShape shape)
	{
		CardRoot.IsVisible = shape.Card;
		Sheet.IsVisible = !shape.Card;
		if (shape.Card)
		{
			Card.WidthRequest = shape.CardWidth;
			ScrimLayer.BackgroundColor = CardScrim;
			PlaceCardBody();
		}
		else
		{
			ScrimLayer.BackgroundColor = DinoTokens.Get(DinoTokens.Scrim);
		}
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] gate mode={shape.Mode} step={(SumText.IsVisible ? "sum" : "hold")} cardWidth={shape.CardWidth}");
#endif
	}

	private void PlaceCardBody()
	{
		bool shortCard = _shape is { Short: true };
		bool sum = SumText.IsVisible;
		Card.Padding = shortCard ? CardShortPadding : CardUprightPadding;
		CardEyebrow.FontSize = shortCard ? CardShortEyebrow : CardUprightEyebrow;
		CardTitle.FontSize = shortCard ? CardShortTitle : (sum ? CardSumTitle : CardUprightTitle);
		TextAlignment align = shortCard ? TextAlignment.Start : TextAlignment.Center;
		CardEyebrow.HorizontalTextAlignment = align;
		CardTitle.HorizontalTextAlignment = align;
		CardRingHost.Scale = shortCard ? CardShortRing / CardRingSize : 1;
		CardRingHost.Margin = shortCard ? new Thickness(-(CardRingSize - CardShortRing) / 2) : new Thickness(0);
		bool room = !shortCard || DesktopTips.IsWindows; // the short card has no room for a caption, except the Windows one
		CardHoldHint.IsVisible = !sum && !_screenReaderOn && room;
		CardSumHint.IsVisible = sum && DesktopTips.IsWindows;
		double keyHeight = shortCard ? CardShortKeyHeight : CardKeyHeight;
		foreach (Button key in CardKeys.Children.OfType<Button>())
		{
			key.HeightRequest = keyHeight;
			key.FontSize = shortCard ? CardShortKeyFont : CardKeyFont;
			key.WidthRequest = shortCard ? CardShortKeyWidth : UnsetWidth;
		}

		CardKeys.RowDefinitions = [new RowDefinition(keyHeight), new RowDefinition(keyHeight)];
		if (shortCard)
		{
			PlaceShortBody(sum);
		}
		else
		{
			PlaceUprightBody();
		}
	}

	// G1 and G2: one column, centred: heading, then the ring (or the field and the six keys), then the caption.
	private void PlaceUprightBody()
	{
		CardBody.ColumnDefinitions = [new ColumnDefinition(GridLength.Star)];
		CardBody.RowDefinitions = [.. Enumerable.Range(0, CardUprightRows).Select(_ => new RowDefinition(GridLength.Auto))];
		Place(CardHead, 0, 0, 1, 1);
		Place(CardRingBox, 1, 0, 1, 1);
		Place(CardField, 1, 0, 1, 1);
		Place(CardKeys, 2, 0, 1, 1);
		Place(CardHoldHint, 3, 0, 1, 1);
		Place(CardSumHint, 3, 0, 1, 1);
		CardHead.VerticalOptions = LayoutOptions.Start;
		CardRingBox.HorizontalOptions = LayoutOptions.Center;
		CardField.HorizontalOptions = LayoutOptions.Center;
		CardKeys.VerticalOptions = LayoutOptions.Start;
		CardKeys.ColumnSpacing = CardKeyGap;
		CardKeys.RowSpacing = CardKeyGap;
	}

	// G3: the ring left of the heading on the hold step; on the sum step the question and field left and the 3 x 2 keys right, all inside the card.
	private void PlaceShortBody(bool sum)
	{
		CardBody.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];
		CardKeys.VerticalOptions = LayoutOptions.Center;
		CardKeys.ColumnSpacing = CardShortKeyGap;
		CardKeys.RowSpacing = CardShortKeyGap;
		if (sum)
		{
			CardBody.ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)];
			Place(CardHead, 0, 0, 1, 1);
			Place(CardField, 1, 0, 1, 1);
			Place(CardKeys, 0, 1, 2, 1);
			Place(CardSumHint, 2, 0, 1, 2);
			CardHead.VerticalOptions = LayoutOptions.End;
			CardField.HorizontalOptions = LayoutOptions.Start;
			return;
		}

		CardBody.ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)];
		Place(CardRingBox, 0, 0, 1, 1);
		Place(CardHead, 0, 1, 1, 1);
		Place(CardHoldHint, 1, 0, 1, 2);
		CardRingBox.HorizontalOptions = LayoutOptions.Start;
		CardHead.VerticalOptions = LayoutOptions.Center;
	}

	private static void Place(View view, int row, int column, int rowSpan, int columnSpan)
	{
		Grid.SetRow(view, row);
		Grid.SetColumn(view, column);
		Grid.SetRowSpan(view, rowSpan);
		Grid.SetColumnSpan(view, columnSpan);
	}

	public Task<bool> Result => _result.Task;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_showing = true;
		int shown = ++_showCount;
		Dispatcher.Dispatch(() => { if (_showing && shown == _showCount) { ClearStaleFocus(); } }); // after the window has given the new page its first focus
		_keyHandled = false;
		_focusMovedByUser = false;
		WatchUserFocus();
		Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(StaleFocusRecheckMilliseconds), () => { if (_showing && shown == _showCount) { ClearStaleFocusUnlessKeyed(); } }); // and once more: the window can hand the focus over after the first dispatch ran
	}

	private const int StaleFocusRecheckMilliseconds = 300;

	private bool _showing; // between OnAppearing and OnDisappearing: a delayed repair of the focus does nothing once the gate is gone
	private int _showCount; // one number per appearance, so a repair queued by an earlier appearance does nothing

	private bool _keyHandled; // a key reached the gate since it appeared: any focus on a button may then be the user's own choice
	private bool _focusMovedByUser; // the user pressed the mouse or touch on the gate, or moved the focus with the keyboard (Tab): the focus is theirs

	// The late recheck only repairs a focus the window handed over; once the user has pressed a key, clicked or tabbed the focus is theirs and is left alone.
	private void ClearStaleFocusUnlessKeyed()
	{
		if (!_keyHandled && !_focusMovedByUser && !_hold.IsHolding)
		{
			ClearStaleFocus();
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_showing = false;
		UnwatchUserFocus();
		ResetHold();
	}

#if WINDOWS
	private Microsoft.UI.Xaml.UIElement? _watchedPage;

	// A press anywhere on the page (even one a button handles) and a focus the user gave by keyboard or pointer cancel the late recheck. The window's own hand-over and our own Focus() are Programmatic and do not.
	private void WatchUserFocus()
	{
		Microsoft.UI.Xaml.Input.FocusManager.GettingFocus -= OnGettingFocus; // -= first: a second OnAppearing never leaves a duplicate
		Microsoft.UI.Xaml.Input.FocusManager.GettingFocus += OnGettingFocus;
		HandlerChanged -= OnHandlerChangedWatch;
		HandlerChanged += OnHandlerChangedWatch; // a page whose native side appears after OnAppearing is watched when it does
		WatchNativePointer();
	}

	private void OnHandlerChangedWatch(object? sender, EventArgs e) => WatchNativePointer();

	private void WatchNativePointer()
	{
		if (_showing && _watchedPage is null && Handler?.PlatformView is Microsoft.UI.Xaml.UIElement page)
		{
			_watchedPage = page;
			page.AddHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnNativePointerPressed), true);
		}
	}

	private void UnwatchUserFocus()
	{
		Microsoft.UI.Xaml.Input.FocusManager.GettingFocus -= OnGettingFocus;
		HandlerChanged -= OnHandlerChangedWatch;
		_watchedPage?.RemoveHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnNativePointerPressed));
		_watchedPage = null;
	}

	private void OnNativePointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
	{
		_focusMovedByUser = true;
	}

	private void OnGettingFocus(object? sender, Microsoft.UI.Xaml.Input.GettingFocusEventArgs e)
	{
		if (!_showing)
		{
			UnwatchUserFocus(); // the page went away without OnDisappearing: the static event must not keep it alive
			return;
		}

		if (e.FocusState is Microsoft.UI.Xaml.FocusState.Keyboard or Microsoft.UI.Xaml.FocusState.Pointer)
		{
			_focusMovedByUser = true;
		}
	}
#else
	private void WatchUserFocus()
	{
		// no-fallback: only the Windows head has a keyboard focus the window can hand over
	}

	private void UnwatchUserFocus()
	{
		// no-fallback: see WatchUserFocus
	}
#endif

	protected override bool OnBackButtonPressed()
	{
		_ = CloseAsync(false);
		return true;
	}

	private void ShowHoldAlternative(bool screenReaderOn)
	{
		RingBox.IsVisible = !screenReaderOn;
		HoldRingHost.IsVisible = !screenReaderOn;
		CardRingHost.IsVisible = !screenReaderOn;
		CardContinue.IsVisible = screenReaderOn;
		HoldHint.IsVisible = !screenReaderOn;
		ContinueButton.IsVisible = screenReaderOn;
		HoldBody.IsVisible = !screenReaderOn;
	}

	private void OnHoldStart(object? sender, TouchEventArgs e) => StartHold();

	private void StartHold()
	{
		_tickCount = 0;
		_hold.Start(_clock.Elapsed);
		HoldHint.Opacity = 1;
		CardHoldHint.Opacity = 1;
		_timer.Start();
	}

	private void OnHoldEnd(object? sender, TouchEventArgs e)
	{
		TraceHold("gate ring EndInteraction");
		ResetHold();
	}

	// Slice 325: the VM's gate check sometimes never gets past the hold; these lines show which of start, tick, end, cancel, reset or complete came last.
	private void TraceHold(string line)
	{
#if WINDOWS
		WindowTrace.Write(line);
#endif
	}

	DinoScreen IDinoKeyTarget.Screen => DinoScreen.Gate;

	// Slice 235 part B: the Windows window's keys. Each returns false when it does not apply right now (the sum step is not showing, the screen-reader button replaced the ring),
	// so the key goes on to the focused control as it did before.
	bool IDinoKeyTarget.Handle(DinoKeyAction action, DinoKey key)
	{
		_keyHandled = true;
		switch (action)
		{
			case DinoKeyAction.GateHoldStart:
				return StartKeyHold();
			case DinoKeyAction.GateHoldEnd:
				return EndKeyHold();
			case DinoKeyAction.GateDigit:
				return DinoKeyMap.DigitOf(key) is { } digit && PressSumKey(digit.ToString());
			case DinoKeyAction.GateErase:
				return SumKeyApplies() && PressSumKey(BackspaceKey);
			case DinoKeyAction.GateSubmit:
				return SumKeyApplies() && PressSumKey(OkKey);
			case DinoKeyAction.Back:
				_ = CloseAsync(false);
				return true;
			default:
				return false;
		}
	}

	// Enter and Backspace belong to the sum, except while the focus sits on Back to story or Continue (Tab got there): they then activate that button, so a keyboard user can always press it.
	// The window may hand the focus to one of those two when the gate opens, before anyone pressed Tab; ClearStaleFocus takes it away then, so the sum keys are not blocked by a focus nobody chose.
	private bool SumKeyApplies() => !AnyButtonFocused();

	private void ClearStaleFocus()
	{
		if (!DesktopTips.IsWindows)
		{
			return;
		}

		BackButton.Unfocus();
		ContinueButton.Unfocus();
		CardBack.Unfocus();
		CardContinue.Unfocus();
#if WINDOWS
		// Unfocus() does not reliably take the WinUI focus off a Button (Slice 325 review): when it is still there, move the focus to the step's own panel, which holds no key of its own.
		if (AnyButtonFocused())
		{
			bool card = _shape is { Card: true };
			View target = card ? (SumText.IsVisible ? CardField : CardRingHost) : (SumText.IsVisible ? SumText : HoldRingHost);
			if (target.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement element)
			{
				bool wasTabStop = element.IsTabStop;
				element.IsTabStop = true;
				element.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
				element.IsTabStop = wasTabStop; // the panel is a focus parking place only, never a Tab stop of its own
			}
		}
#endif
	}

	// True while the keyboard focus sits on Back to story or Continue: Enter and Backspace then belong to that button.
	// The pad keys are never keyboard tab stops on Windows (see MakeKey), so focus can only be on these two or nowhere, and a mouse click on a pad key never leaves the sum keys dead.
	private bool AnyButtonFocused() => BackButton.IsFocused || ContinueButton.IsFocused || CardBack.IsFocused || CardContinue.IsFocused;

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

	// The ring is up on the hold step, in the card or the sheet; a screen reader's Continue button replaces it.
	private bool HoldRingShown() => _shape is { Card: true } ? !_sumStep && CardRingHost.IsVisible : HoldRingHost.IsVisible && HoldAction.IsVisible;

	// A key repeat on a held Space arrives as more starts: only the first one starts the ring, and none can restart it.
	private bool StartKeyHold()
	{
		if (_closing || !HoldRingShown())
		{
			return false;
		}

		if (!_keyHolding)
		{
			_keyHolding = true;
			StartHold();
#if DEBUG
			Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write("[Keyboard] gate hold started by the Space bar");
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
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write("[Keyboard] gate hold ended by letting go of the Space bar");
#endif
		return true;
	}

	private void OnHoldCancel(object? sender, EventArgs e)
	{
		TraceHold("gate ring CancelInteraction");
		ResetHold();
	}

	private void ResetHold()
	{
		if (_hold.IsHolding)
		{
			TraceHold($"gate hold reset while holding (timer enabled {_timer.IsRunning}, page showing {_showing})");
		}

		_keyHolding = false; // any end of the hold (let go, page covered, window deactivated) also ends a Space hold, so no later key-up is needed
		_timer.Stop();
		_hold.Cancel();
		_ring.Progress = 0;
		_cardRing.Progress = 0;
		HoldHint.Opacity = 0;
		CardHoldHint.Opacity = DesktopTips.IsWindows ? 1 : 0; // the Windows caption is always there; the touch hint only while holding
		HoldRing.Invalidate();
		CardRing.Invalidate();
	}

	private void OnTick(object? sender, EventArgs e)
	{
		TimeSpan now = _clock.Elapsed;
		_ring.Progress = _hold.Progress(now);
		_cardRing.Progress = _ring.Progress;
		HoldRing.Invalidate();
		CardRing.Invalidate();
		TraceHoldTick();
		if (_hold.IsComplete(now))
		{
			TraceHold("gate hold complete, showing the sum");
			ResetHold();
			ShowSumStep();
		}
	}

	private int _tickCount; // ticks since the hold started, so the trace logs about one in thirty (roughly once a second)

	private void TraceHoldTick()
	{
		if (++_tickCount % TickTraceEvery == 1)
		{
			TraceHold($"gate hold tick {_tickCount}: progress {_ring.Progress:0.00}");
		}
	}

	private const int TickTraceEvery = 30;

	private void OnContinueClicked(object? sender, EventArgs e)
	{
		UiUsage.Tap(UiControls.Continue); // Slice 352
		ShowSumStep();
	}

	private void ShowSumStep()
	{
		_sumStep = true; // before anything reads it (ShowCardStep, the sheet views)
		HoldText.IsVisible = false;
		HoldAction.IsVisible = false;
		RingBox.IsVisible = false;
		SumText.IsVisible = true;
		KeyPad.IsVisible = true;
		StepBarTwo.Color = DinoTokens.Get(DinoTokens.Brand);
		ShowCardStep();
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] gate mode={_shape?.Mode} step=sum cardWidth={_shape?.CardWidth}");
#endif
		ClearStaleFocus(); // the hidden Continue button may still hold the focus from the hold step
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
		return UiUsage.Press(button, UiControls.GateKey, () => PressPadKey(button, key)); // Slice 352: that a key was pressed, never which one
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
		ShowAnswer();
	}

	private void ShowAnswer()
	{
		AnswerLabel.Text = _answer;
		CardAnswer.Text = _answer;
	}

	// Slice 363 (mock G2): a tap or click on one of the six answers is an answer, as typing it and pressing Enter is.
	private void PressAnswerKey(Button button, int value, int forAnswer)
	{
		if (DesktopTips.IsWindows)
		{
			button.Unfocus();
		}

		if (_closing || !_sumStep || forAnswer != _challenge.Answer) // a tap on a key made for an older sum is ignored
		{
			return;
		}

		_answer = value.ToString(CultureInfo.InvariantCulture);
		ShowAnswer();
		Submit();
	}

	// The six answer keys are refilled with every new sum, so they always hold the right answer once and five near misses.
	private void FillAnswerKeys()
	{
		CardKeys.Children.Clear();
		IReadOnlyList<int> choices = GateKeypad.AnswerChoices(_challenge.Answer, Random.Shared);
		for (int i = 0; i < choices.Count; i++)
		{
			CardKeys.Add(MakeAnswerKey(choices[i], _challenge.Answer), i % CardKeyColumns, i / CardKeyColumns);
		}

		bool shortCard = _shape is { Short: true };
		foreach (Button key in CardKeys.Children.OfType<Button>())
		{
			key.HeightRequest = shortCard ? CardShortKeyHeight : CardKeyHeight;
			key.FontSize = shortCard ? CardShortKeyFont : CardKeyFont;
			key.WidthRequest = shortCard ? CardShortKeyWidth : UnsetWidth;
		}
	}

	private Button MakeAnswerKey(int value, int forAnswer)
	{
		var button = new Button
		{
			Text = value.ToString(CultureInfo.InvariantCulture),
			FontSize = CardKeyFont,
			FontAttributes = FontAttributes.Bold,
			CornerRadius = 18,
			Padding = 0,
			BackgroundColor = Colors.White,
			TextColor = DinoTokens.Get(DinoTokens.Ink),
			HeightRequest = CardKeyHeight,
			Shadow = new Shadow { Brush = new SolidColorBrush(DinoTokens.Get(DinoTokens.Ink)), Offset = new Point(0, 3), Radius = 1, Opacity = 0.1f },
		};
#if WINDOWS
		button.HandlerChanged += (_, _) =>
		{
			if (button.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Control native)
			{
				native.IsTabStop = false; // as the pad: the keyboard types the answer
			}
		};
#endif
		return UiUsage.Press(button, UiControls.GateKey, () => PressAnswerKey(button, value, forAnswer));
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
		ShowAnswer();
		FillAnswerKeys();
		ShowCardStep();
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

	private void OnBackClicked(object? sender, EventArgs e)
	{
		UiUsage.Tap(UiControls.Back); // Slice 352
		_ = CloseAsync(false);
	}
}

/// <summary>The card's hold ring (mock G1): a 150 round ring, a 14 wide band of brand colour over a pale track that fills clockwise from the top; the ink disc and the lock are views on top of it.</summary>
internal sealed class CardRingDrawable : IDrawable
{
	private const float Diameter = 150;
	private const float Band = 14;
	private const float TopAngle = 90;
	private const float FullTurn = 360;

	private static readonly Color Track = Color.FromArgb("#ECE7FF");
	private static readonly Color Arc = Color.FromArgb("#5B57E8");

	/// <summary>0 to 1: how much of the hold has been done.</summary>
	public double Progress { get; set; }

	public void Draw(ICanvas canvas, RectF dirtyRect)
	{
		float cx = dirtyRect.Center.X;
		float cy = dirtyRect.Center.Y;
		float radius = (Diameter - Band) / 2;
		canvas.StrokeSize = Band;
		canvas.StrokeColor = Track;
		canvas.DrawCircle(cx, cy, radius);
		if (Progress > 0)
		{
			canvas.StrokeColor = Arc;
			canvas.StrokeLineCap = LineCap.Butt;
			float end = TopAngle - (float)(FullTurn * Math.Min(Progress, 0.9999));
			canvas.DrawArc(cx - radius, cy - radius, radius * 2, radius * 2, TopAngle, end, true, false);
		}
	}
}
