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
		Loaded += (_, _) => Register();
		Unloaded += (_, _) =>
		{
			Unregister();
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
	// Slice 298: the overlays that are on screen, held weakly. A static event held every subscribed overlay (and its recycled cell) alive, and a cell recycled without Unloaded kept
	// its handler; here each overlay adds and removes only itself, and a collected one drops out on the next scroll.
	private static readonly List<WeakReference<TileOverlay>> Live = [];

	private void Register()
	{
		Unregister(); // Loaded can fire again without Unloaded: never register twice
		Live.Add(new WeakReference<TileOverlay>(this));
	}

	private void Unregister() => Live.RemoveAll(reference => !reference.TryGetTarget(out TileOverlay? overlay) || ReferenceEquals(overlay, this));

	/// <summary>The gallery's list started to scroll: every waiting hold is over.</summary>
	public static void NotifyScrollStarted()
	{
		foreach (WeakReference<TileOverlay> reference in Live.ToArray())
		{
			if (reference.TryGetTarget(out TileOverlay? overlay))
			{
				overlay.CancelHoldKeepingSwallow();
			}
		}

		Live.RemoveAll(reference => !reference.TryGetTarget(out _));
	}

	private void OnBackdropTapped(object? sender, TappedEventArgs e)
	{
		if (_gesture.ShouldSwallowTap(Environment.TickCount64))
		{
			return; // the release after a hold must not also open the sheet
		}

		OpenRequested?.Invoke(this, e); // no-tap-count: the gallery counts OpenRequested (OnOpenRequested)
	}

	private async void OnPointerPressed(object? sender, PointerEventArgs e)
	{
		try
		{
			await HoldAsync(e);
		}
		catch (Exception ex)
		{
			// no-fallback: the long-press does not start a reorder; the shelf is unchanged
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
			// no-fallback: the press ended before the hold time; that is the normal tap
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
		_gesture.Release(Environment.TickCount64); // decides whether the tap that follows (or just came) is swallowed
	}

	// Debug only, like the gallery's [Reorder] lines: a device check reads which path (fired, moved, released) a press took.
	private static void HoldLog(string line)
	{
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write(line);
#endif
	}

	// Hide, unload or a new binding: the hold is over and nothing is left to swallow (a leftover swallow would drop the first tap on the next book).
	private void CancelHold()
	{
		_gesture.Cancel();
		StopTimer();
	}

	// A scroll: a waiting hold is over, but a fired hold keeps its swallow until the release.
	private void CancelHoldKeepingSwallow()
	{
		_gesture.CancelKeepingSwallow();
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
		OpenRequested?.Invoke(this, e); // no-tap-count: the gallery counts OpenRequested (OnOpenRequested)
	}
}
