using DinoReader.Core;

namespace DinoReader;

/// <summary>Raised with a cover's hold; the handler sets <see cref="Opened"/> when it opened the reorder layer.</summary>
public sealed class HoldRequestedEventArgs : EventArgs
{
	public bool Opened { get; set; }
}

/// <summary>The grown-up "⋯" over one gallery cover (Slice 189); the gallery opens the book's options sheet.</summary>
public partial class TileOverlay : ContentView
{
	public TileOverlay()
	{
		InitializeComponent();
		BindingContextChanged += (_, _) =>
		{
			CancelHold(); // a recycled cell must not fire the previous book's hold
			if (BindingContext is StoryTile tile)
			{
				DesktopTips.Set(MoreButton, tile.MoreHint); // Slice 235 part B: Windows only
			}
		};
		Loaded += (_, _) => ScrollStarted += CancelHold;
		Unloaded += (_, _) =>
		{
			ScrollStarted -= CancelHold;
			CancelHold();
		};
		PropertyChanged += (_, args) =>
		{
			if (args.PropertyName == IsVisibleProperty.PropertyName && !IsVisible)
			{
				CancelHold();
			}
		};
	}

	/// <summary>A tap on the dot or on the bare cover: the book's options sheet.</summary>
	public event EventHandler? OpenRequested;

	/// <summary>Raised when the cover has been pressed for <see cref="HoldMilliseconds"/> without moving (Slice 264): the gallery opens its reorder mode with this book lifted.</summary>
	public event EventHandler<HoldRequestedEventArgs>? HoldRequested;

	private const int HoldMilliseconds = 1000;
	private CancellationTokenSource? _hold;
	private readonly HoldGesture _gesture = new(); // all hold decisions live here (Core, unit-tested); this view feeds it events and a timer

	// The platform scroller takes a touch without telling the overlay (no move, no release), so the gallery raises this when its list starts to scroll.
	private static event Action? ScrollStarted;

	/// <summary>The gallery's list started to scroll: every waiting hold is over.</summary>
	public static void NotifyScrollStarted() => ScrollStarted?.Invoke();

	private void OnBackdropTapped(object? sender, TappedEventArgs e)
	{
		if (_gesture.ShouldSwallowTap())
		{
			return; // the release after a hold must not also open the sheet
		}

		OpenRequested?.Invoke(this, e);
	}

	private async void OnPointerPressed(object? sender, PointerEventArgs e)
	{
		try
		{
			await HoldAsync(e);
		}
		catch (Exception ex)
		{
			HoldLog($"[Reorder] hold failed: {ex.GetType().Name}"); // an async void must never take the app down
		}
	}

	private async Task HoldAsync(PointerEventArgs e)
	{
		CancelHold();
		Point start = e.GetPosition(this) ?? Point.Zero;
		_gesture.Press(start.X, start.Y);
		SetHoldCursor(DinoCursorShape.OpenHand);
		var source = new CancellationTokenSource();
		_hold = source;
		try
		{
			await Task.Delay(HoldMilliseconds, source.Token);
		}
		catch (OperationCanceledException)
		{
			source.Dispose();
			return;
		}

		if (!source.IsCancellationRequested && _hold == source && _gesture.CanFire())
		{
			_hold = null;
			HoldLog("[Reorder] hold fired");
			SetHoldCursor(DinoCursorShape.ClosedHand);
			var fired = new HoldRequestedEventArgs();
			HoldRequested?.Invoke(this, fired);
			_gesture.Fired(fired.Opened); // when the reorder layer opened, the release goes to it, not to this cover, so nothing is left to swallow
			SetHoldCursor(DinoCursorShape.Default); // the release may go to the reorder layer, so the hand must not wait for it
		}

		source.Dispose();
	}

	private void OnPointerMoved(object? sender, PointerEventArgs e)
	{
		if (_hold is not null && e.GetPosition(this) is { } now && _gesture.Move(now.X, now.Y))
		{
			HoldLog("[Reorder] hold cancelled: moved");
			StopTimer();
		}
	}

	private void OnPointerEnded(object? sender, PointerEventArgs e)
	{
		if (_hold is not null)
		{
			HoldLog("[Reorder] hold cancelled: released");
		}

		StopTimer();
		_gesture.Release(); // decides whether the tap that follows (or just came) is swallowed
	}

	// Debug only, like the gallery's [Reorder] lines: a device check reads which path (fired, moved, released) a press took.
	private static void HoldLog(string line)
	{
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write(line);
#endif
	}

	// Hide, unload, a new binding or a scroll: the hold is over and nothing is left to swallow.
	private void CancelHold()
	{
		_gesture.Cancel();
		StopTimer();
	}

	private void StopTimer()
	{
		_hold?.Cancel(); // the waiting press disposes its own source once the cancelled delay unwinds
		_hold = null;
		SetHoldCursor(DinoCursorShape.Default);
	}

	// Windows only: an open hand while the mouse button is held on the cover, a closed hand once the hold has fired (mock Batch 47 frame 176).
	private void SetHoldCursor(DinoCursorShape shape)
	{
#if WINDOWS
		DinoCursor.Set(Backdrop, shape);
#endif
	}

	private void OnMoreTapped(object? sender, TappedEventArgs e)
	{
		CancelHold();
		OpenRequested?.Invoke(this, e);
	}
}
