namespace DinoReader;

/// <summary>The grown-up "⋯" over one gallery cover (Slice 189); the gallery opens the book's options sheet.</summary>
public partial class TileOverlay : ContentView
{
	public TileOverlay()
	{
		InitializeComponent();
		BindingContextChanged += (_, _) =>
		{
			if (BindingContext is StoryTile tile)
			{
				DesktopTips.Set(MoreButton, tile.MoreHint); // Slice 235 part B: Windows only
			}
		};
	}

	/// <summary>A tap on the dot or on the bare cover: the book's options sheet.</summary>
	public event EventHandler? OpenRequested;

	/// <summary>Raised when the cover has been pressed for <see cref="HoldMilliseconds"/> without moving (Slice 264): the gallery opens its reorder mode with this book lifted.</summary>
	public event EventHandler? HoldRequested;

	private const int HoldMilliseconds = 1000;
	private const double HoldSlop = 12; // dp the finger may drift before the press is a scroll or a swipe, not a hold
	private CancellationTokenSource? _hold;
	private Point? _holdStart;
	private bool _holdFired;

	private void OnBackdropTapped(object? sender, TappedEventArgs e)
	{
		if (_holdFired)
		{
			_holdFired = false; // the release after a hold must not also open the sheet
			return;
		}

		OpenRequested?.Invoke(this, e);
	}

	private async void OnPointerPressed(object? sender, PointerEventArgs e)
	{
		CancelHold();
		_holdFired = false;
		_holdStart = e.GetPosition(this);
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

		if (!source.IsCancellationRequested && _hold == source)
		{
			_hold = null;
			_holdFired = true;
			SetHoldCursor(DinoCursorShape.ClosedHand);
			HoldRequested?.Invoke(this, EventArgs.Empty);
			SetHoldCursor(DinoCursorShape.Default); // the release may go to the reorder layer, so the hand must not wait for it
		}

		source.Dispose();
	}

	private void OnPointerMoved(object? sender, PointerEventArgs e)
	{
		if (_hold is not null && _holdStart is { } start && e.GetPosition(this) is { } now && Math.Max(Math.Abs(now.X - start.X), Math.Abs(now.Y - start.Y)) > HoldSlop)
		{
			CancelHold();
		}
	}

	private void OnPointerEnded(object? sender, PointerEventArgs e) => CancelHold();

	private void CancelHold()
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
