using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The shelf's reorder mode (Slice 264, mock Batch 47 frames 173 to 176): a state of the gallery, drawn as a layer over it with its own ink bar (Cancel left, "Reorder", Done), a hint, and the books in a grid
/// that wiggle. A tap lifts a book; dragging one (a plain touch drag on Android and iOS, no hold, Slice 382; a plain mouse drag on Windows) shows the spot it would land in, lit, while the
/// others slide to make room. It works on an in-memory order and owns nothing stored: Done hands the order to the page, which is the only thing that saves; Cancel and closing save nothing.
/// Positions are computed by <see cref="ShelfOrderGrid"/> and the move by <see cref="ShelfOrder.MoveBefore"/>; the layout never mirrors.
/// </summary>
internal sealed class ShelfReorderView : Grid
{
	private const double HeaderHeight = 44;
	private const double TitleSpacing = WindowShapeSeam.ReorderTitleSpacing; // the row gap in Core is computed against this spacing
	private const double EdgeBand = 72;
	private const double EdgeStep = 36;
	private const double BottomRoom = 24;
	private const double MinWidthChange = 1;
	private const double PictureReloadWidthChange = 16; // a tile this much wider or narrower than the one its picture was decoded for gets the picture loaded again
	private double _lastLayoutWidth;
	private double _decodedTileWidth;
	private bool _edgeScrolling;
	private const uint DropSettleMs = 150;
	private const int HideSourceDelayMs = 100;

	private readonly AbsoluteLayout _surface = new() { FlowDirection = FlowDirection.LeftToRight, VerticalOptions = LayoutOptions.Start }; // Start: with few books the surface (HeightRequest = the grid's height) is shorter than the viewport and the ScrollView centred it (Slice 321)
	private readonly ScrollView _scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Never };
	private readonly Label _cancelLabel = new() { AutomationId = "ReorderCancel" };
	private readonly Label _titleLabel = new();
	private readonly Label _doneLabel = new();
	private readonly Label _hint = new();
	private readonly List<ReorderEntry> _entries = [];
	private Border _lit = null!;
	private ShelfOrderGrid _grid;
	private WindowShapeSeam.ShelfGeometry _geometry; // copied from the shelf list (Slice 319): its margins and columns
	private double _titleHeight;
	private ReorderEntry? _dragged;
	private readonly ShelfDragSession _drag = new(); // the drag's end decisions (undo on cancel, keep a landed drop, either event order) live here in Core
	private bool _closing;
	private int _kbIndex = -1; // the spot the keyboard focus is on; -1 until a key is pressed
	private bool _keyboardHolding; // Space lifted the focused cover and the arrows carry it
	private (IReadOnlyList<StoryTile> Tiles, string? LiftedId)? _pending; // Slice 382: the covers wait here until the scroll has a width, so each picture loads at its real size
	private const int PanTickMs = 50;
	private bool _pointerDrag; // the drag is a touch pan (Android, iOS): the lifted cover itself follows the finger; the system drag (Windows) draws its own picture
	private double _panCoverX;
	private double _panCoverY;
	private double _panTouchX;
	private double _panTouchY;
	private double _panScrollY;
	private double _panDx;
	private double _panDy;
	private IDispatcherTimer? _panTimer;

	public ShelfReorderView()
	{
		IsVisible = false;
		BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		AutomationId = "ReorderView";
		RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)];
		FlowDirection = FlowDirection.LeftToRight;
		this.Add(Header(), 0, 0);
		_hint.FontSize = 13.5;
		_hint.TextColor = DinoTokens.Get(DinoTokens.Body);
		_hint.AutomationId = "ReorderHint";
		this.Add(_hint, 0, 1);
		var drop = new DropGestureRecognizer { AllowDrop = true };
		drop.DragOver += OnDragOver;
		drop.Drop += (_, _) => ApplyDragOutcome(_drag.Drop(Environment.TickCount64));
		_surface.GestureRecognizers.Add(drop);
		_scroll.Content = _surface;
		_scroll.SizeChanged += OnScrollSizeChanged;
		TraceSize("scroll", _scroll);
		TraceSize("surface", _surface);
		this.Add(_scroll, 0, 2);
	}

	/// <summary>Raised by Done with the new order of the book ids; the page saves it and closes the mode.</summary>
	public event Func<IReadOnlyList<string>, Task>? DoneRequested;

	/// <summary>Raised by Cancel: the page closes the mode and saves nothing.</summary>
	public event Action? CancelRequested;

	/// <summary>The book ids in the order shown now.</summary>
	public IReadOnlyList<string> Order => _pending is { } pending
		? [.. pending.Tiles.Select(t => t.Story.Id)] // the covers are not built yet (no width): nothing was moved, the order is the one the mode opened with, never an empty list
		: [.. _entries.OrderBy(e => e.Slot).Select(e => e.Id)];

	private Grid Header()
	{
		var bar = new Grid { BackgroundColor = DinoTokens.Get(DinoTokens.Ink), Padding = new Thickness(8, 10), ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		_cancelLabel.FontSize = 15;
		_cancelLabel.FontAttributes = FontAttributes.Bold;
		_cancelLabel.TextColor = Colors.White;
		_cancelLabel.VerticalOptions = LayoutOptions.Center;
		_cancelLabel.Padding = new Thickness(10, 0);
		_cancelLabel.MinimumHeightRequest = HeaderHeight;
		_cancelLabel.VerticalTextAlignment = TextAlignment.Center;
		Taps.AddTap(_cancelLabel, () => { CancelRequested?.Invoke(); return Task.CompletedTask; }, UiControls.Cancel);
		_titleLabel.FontSize = 19;
		_titleLabel.FontAttributes = FontAttributes.Bold;
		_titleLabel.TextColor = Colors.White;
		_titleLabel.HorizontalTextAlignment = TextAlignment.Center;
		_titleLabel.VerticalOptions = LayoutOptions.Center;
		_titleLabel.LineBreakMode = LineBreakMode.TailTruncation;
		_titleLabel.MaxLines = 1;
		SemanticProperties.SetHeadingLevel(_titleLabel, SemanticHeadingLevel.Level1);
		_doneLabel.FontSize = 15;
		_doneLabel.FontAttributes = FontAttributes.Bold;
		_doneLabel.TextColor = DinoTokens.Get(DinoTokens.Ink);
		_doneLabel.VerticalOptions = LayoutOptions.Center;
		var done = new Border
		{
			HeightRequest = HeaderHeight, BackgroundColor = Colors.White, StrokeThickness = 0, Padding = new Thickness(20, 0), AutomationId = "ReorderDone",
			StrokeShape = new RoundRectangle { CornerRadius = HeaderHeight / 2 }, Content = _doneLabel,
		};
		Taps.AddTap(done, () => DoneRequested is { } requested ? requested(Order) : Task.CompletedTask, UiControls.Done);
		bar.Add(_cancelLabel, 0, 0);
		bar.Add(_titleLabel, 1, 0);
		bar.Add(done, 2, 0);
		return bar;
	}

	/// <summary>Shows the mode over the gallery with <paramref name="tiles"/> in their shelf order; <paramref name="liftedId"/> starts lifted (the sheet's Reorder row).</summary>
	public void Open(IReadOnlyList<StoryTile> tiles, string? liftedId, WindowShapeSeam.ReorderLayerShape shape, double titleHeight)
	{
		ReorderTrace.Step($"view open begin books={tiles.Count} lifted={liftedId is not null}");
		ClearEntries();
		_lastLayoutWidth = 0; // a second open at the same width, or a first show at width 0, must still lay the tiles out
		_geometry = shape.Grid; // the mock's grid for this platform (Slice 321)
		ApplyHint(shape.Hint);
		_titleHeight = titleHeight;
		_closing = false;
		_kbIndex = -1;
		_keyboardHolding = false;
		_cancelLabel.Text = AppResources.Common_Cancel;
		_titleLabel.Text = AppResources.Gallery_Reorder;
		_doneLabel.Text = AppResources.Common_Done;
		_hint.Text = AppResources.Gallery_ReorderHint;
		SemanticProperties.SetDescription(_cancelLabel, _cancelLabel.Text);
		_grid = FitGrid(Math.Max(_scroll.Width, 1), titleHeight);
		_lit = ShelfReorderTiles.LitSpot(_grid.TileWidth, _grid.TileHeight - TitleSpacing - titleHeight);
		_lit.IsVisible = false;
		_surface.Children.Add(_lit);
		TraceSize("lit", _lit);
		BuildOrWait(tiles, liftedId);
		IsVisible = true;
		Dispatcher.Dispatch(Relayout);
		ReorderTrace.Step("view open end");
	}

	// Slice 382, the blurred covers on the first open: the scroll is hidden until the first Open, so its width is 0 and the tiles were built 1 dp wide; the cover pictures decode to the size of
	// the view they are first set on (the platform image loader downsamples to it) and were never decoded again when the grid was laid out at the real width. Build the tiles only once the scroll has a width.
	private void BuildOrWait(IReadOnlyList<StoryTile> tiles, string? liftedId)
	{
		if (_scroll.Width > 0)
		{
			AddEntries(tiles, liftedId);
			return;
		}

		_pending = (tiles, liftedId);
		DebugLog("covers wait for the scroll width");
	}

	private void AddEntries(IReadOnlyList<StoryTile> tiles, string? liftedId)
	{
		bool outline = Sirous.Reading.Mobile.Shared.Waiting.ReduceMotion.IsOn;
		_decodedTileWidth = _grid.TileWidth;
		for (int i = 0; i < tiles.Count; i++)
		{
			Grid view = ShelfReorderTiles.Build(tiles[i], _grid, _titleHeight, TitleSpacing, outline);
			var entry = new ReorderEntry(tiles[i], view, i) { Lifted = tiles[i].Story.Id == liftedId };
			_entries.Add(entry);
			AttachGestures(entry);
			_surface.Children.Add(view);
			TraceSize($"tile{i}", view);
			ShelfReorderTiles.ApplyLift(entry);
			view.ZIndex = entry.Lifted ? 1 : 0;
			ShelfReorderTiles.StartWiggle(entry, i, Dispatcher);
		}
	}

	private void AttachGestures(ReorderEntry entry)
	{
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) =>
		{
			UiUsage.Tap(UiControls.Reorder); // Slice 352: a tile lifted to move
			DebugLog($"tap lift {entry.Id}"); // the shelf-reorder flow taps a cover and expects this: the touch pan must not eat the tap
			Lift(entry);
		};
		entry.View.GestureRecognizers.Add(tap);
#if ANDROID || IOS
		ReorderTouchPan.Attach(entry.View, new ReorderPanCallbacks((x, y) => PanBegin(entry, x, y), PanMove, cancelled => PanEnd(entry, cancelled))); // Slice 382: a touch that moves drags at once, no hold
#else
		var drag = new DragGestureRecognizer { CanDrag = true };
		drag.DragStarting += (_, e) => StartDrag(entry, e);
		drag.DropCompleted += (_, _) => ApplyDragOutcome(_drag.Completed(Order, Environment.TickCount64));
		entry.View.GestureRecognizers.Add(drag);
#endif
#if WINDOWS
		entry.View.HandlerChanged += (_, _) => DinoCursor.Set(entry.View, DinoCursorShape.OpenHand); // an open hand over every cover (Windows)
#endif
	}

	/// <summary>
	/// Windows keyboard (mock Batch 47 frame 176): an arrow key moves the focus ring over the covers (the first press shows it on the first book); Space lifts the focused cover, the arrows then carry it,
	/// and Space drops it. Esc and Enter are Cancel and Done and are handled by the page. False for a key that is not ours.
	/// </summary>
	public bool HandleKey(DinoKeyAction action)
	{
		if (action == DinoKeyAction.ReorderToggle)
		{
			ToggleKeyboardLift();
			return true;
		}

		if (action is DinoKeyAction.ReorderLeft or DinoKeyAction.ReorderRight or DinoKeyAction.ReorderUp or DinoKeyAction.ReorderDown)
		{
			MoveByKey(action);
			return true;
		}

		return false;
	}

	private ReorderEntry? AtSlot(int slot) => _entries.FirstOrDefault(e => e.Slot == slot);

	/// <summary>Re-fits the open mode to a new window shape (a rotation or a resize); nothing when it is closed.</summary>
	public void Reshape(WindowShapeSeam.ReorderLayerShape shape)
	{
		WindowShapeSeam.ShelfGeometry geometry = shape.Grid;
		if (!IsVisible || geometry.Columns < 1)
		{
			return;
		}

		ApplyHint(shape.Hint);
		bool sameWholeDp = WholeDp(geometry) == WholeDp(_geometry);
		_geometry = geometry; // always stored, so a later width-triggered Relayout never fits with stale exact margins
		if (sameWholeDp)
		{
			return;
		}

		Relayout();
		_lastLayoutWidth = -1; // a rotation lands here before the scroll has its new width: the next width change, however small, must lay out again
	}

	// The grid floors its tile to whole dp, so a value that moves by a fraction (the Windows side margin above 1600 dp) is not a new layout: only the decision to lay out again compares
	// the WHOLE geometry (every value, columns included) in whole dp; the layout itself uses the exact values. Floor, as the shelf's own tile width does.
	private static WindowShapeSeam.ShelfGeometry WholeDp(WindowShapeSeam.ShelfGeometry g) =>
		new(Math.Floor(g.Left), Math.Floor(g.Top), Math.Floor(g.Right), g.Columns, Math.Floor(g.GapX), Math.Floor(g.GapY));

	// The mock's grid starts right under the hint (top 0 on phone and Windows); a tablet's geometry already carries the shelf's old top inset (Slice 321).
	private ShelfOrderGrid FitGrid(double width, double titleHeight) =>
		ShelfOrderGrid.Fit(width, _geometry.Columns, _geometry.Left, _geometry.Right, _geometry.Top, _geometry.GapX, _geometry.GapY, titleHeight, TitleSpacing);

	private void ApplyHint(WindowShapeSeam.ReorderHint hint)
	{
		_hint.FontSize = hint.FontSize;
		_hint.Padding = new Thickness(hint.Left, hint.Top, hint.Right, hint.Bottom);
	}

	private void ToggleKeyboardLift()
	{
		if (_kbIndex < 0)
		{
			_kbIndex = _entries.FirstOrDefault(e => e.Lifted)?.Slot ?? 0; // start on the book already lifted (the sheet's Reorder row, a tap)
		}

		if (AtSlot(_kbIndex) is not { } entry)
		{
			return;
		}

		_keyboardHolding = !_keyboardHolding;
		if (_keyboardHolding)
		{
			foreach (ReorderEntry other in _entries)
			{
				SetLifted(other, other == entry);
			}
		}
		else
		{
			SetLifted(entry, false); // covers lifted by other means are left as they are
		}

		ShowKeyboardFocus();
		DebugLog($"key {(_keyboardHolding ? "lift" : "drop")} {_kbIndex + 1}");
	}

	private void MoveByKey(DinoKeyAction arrow)
	{
		int from = _kbIndex;
		_kbIndex = from >= 0 ? from : _entries.FirstOrDefault(e => e.Lifted)?.Slot ?? 0; // the first key shows the ring on the book already lifted, as Space would start from
		int target = ShelfOrderKeys.Target(_kbIndex, _entries.Count, _geometry.Columns, arrow);
		if (from >= 0 && target != _kbIndex && _keyboardHolding && AtSlot(_kbIndex) is { } held)
		{
			ApplyOrder(ShelfOrder.MoveToSlot(Order, held.Id, target));
			DebugLog($"key move {_kbIndex + 1} to {target + 1}");
		}

		if (from >= 0)
		{
			_kbIndex = target;
		}

		ShowKeyboardFocus();
	}

	// The focus ring is the lit spot, on the focused slot; a lifted cover needs none.
	private void ShowKeyboardFocus()
	{
		_lit.IsVisible = _kbIndex >= 0 && !_keyboardHolding;
		PlaceLit(Math.Max(_kbIndex, 0), animate: true);
	}

	/// <summary>Hides the mode and forgets its state; nothing is saved.</summary>
	public void Close()
	{
		ReorderTrace.Step("view close begin");
		_closing = true;
		ClearEntries();
		_drag.Reset();
		IsVisible = false;
		ReorderTrace.Step("view close end");
	}

	private void ClearEntries()
	{
		foreach (ReorderEntry entry in _entries)
		{
			ShelfReorderTiles.StopWiggle(entry);
		}

		_entries.Clear();
		_pending = null;
		StopPanTimer();
		_pointerDrag = false;
		if (_lit is not null) // the first Open clears before it makes the lit spot (device run 2026-11-15: a null _lit threw and the reorder mode never opened)
		{
			_lit.IsVisible = false; // review #6: a drag torn down mid-flight leaves no lit spot behind
		}
		_surface.Children.Clear();
		_dragged = null; // one rule (review #4/#5): a drag in flight when its entries go away ends as cancelled, never a drop: nothing is applied, the order is the one the next Open brings, and a late Ended(cancelled) finds no drag; the ShelfDragSession is reset only in Close and Begin (Core CLAUDE.md), so a late Drop after a rebuild is still judged by it
	}

	// Only a change of the scroll's width re-lays out (a height-only change, or a scrollbar flicker under 1 dp, must not feed back into the surface size: Windows threw a layout cycle).
	private void OnScrollSizeChanged(object? sender, EventArgs e)
	{
		if (_pending is not null && !IsVisible)
		{
			_pending = null; // review #7: the layer was hidden before the scroll got a width; nothing waits for it
		}

		if (_pending is { } pending && _scroll.Width > 0)
		{
			_pending = null;
			_grid = FitGrid(_scroll.Width, _titleHeight);
			AddEntries(pending.Tiles, pending.LiftedId);
			DebugLog($"covers built at width {_scroll.Width:0}");
		}

		if (Math.Abs(_scroll.Width - _lastLayoutWidth) >= MinWidthChange)
		{
			Relayout();
		}
	}

	// Sizes and places every tile from the scroll view's width; a rotation or the first show lands here, and nothing is animated.
	// Slice 264 diagnosis: names the element and its size on every size change.
	[System.Diagnostics.Conditional("DEBUG"), System.Diagnostics.Conditional("DINO_TEST_BOOKS")] // compiled out of store builds: no handlers, no strings
	private static void TraceSize(string name, VisualElement element) =>
		element.SizeChanged += (_, _) => ReorderTrace.Step($"size {name} {element.Width:0.##}x{element.Height:0.##} req={element.WidthRequest:0.##}x{element.HeightRequest:0.##}");

	private void Relayout()
	{
		if (_closing || _entries.Count == 0 || _scroll.Width <= 0)
		{
			return;
		}


		_lastLayoutWidth = _scroll.Width;
		ReorderTrace.Step($"relayout scroll={_scroll.Width:0.##}x{_scroll.Height:0.##} surface={_surface.Width:0.##}x{_surface.Height:0.##}");
		_grid = FitGrid(_scroll.Width, _titleHeight);
		_surface.HeightRequest = _grid.ContentHeight(_entries.Count) + BottomRoom;
		double coverHeight = _grid.TileHeight - TitleSpacing - _titleHeight;
		_lit.WidthRequest = _grid.TileWidth;
		_lit.HeightRequest = coverHeight;
		AbsoluteLayout.SetLayoutBounds(_lit, new Rect(_grid.Origin(0).X, _grid.Origin(0).Y, _grid.TileWidth, coverHeight));
		foreach (ReorderEntry entry in _entries)
		{
			entry.View.WidthRequest = _grid.TileWidth;
			entry.View.HeightRequest = _grid.TileHeight;
			(double x, double y) = _grid.Origin(entry.HomeIndex);
			AbsoluteLayout.SetLayoutBounds(entry.View, new Rect(x, y, _grid.TileWidth, _grid.TileHeight));
			Place(entry, animate: false);
		}

		if (_dragged is not null || _kbIndex >= 0)
		{
			PlaceLit(_dragged?.Slot ?? _kbIndex, animate: false); // the lit spot and the keyboard ring are the same element and follow the new columns
		}

		ReloadPicturesIfResized();
	}

	// Slice 382: a picture decodes to the size of the cover it is set on, so a rotation or a window resize that changes the tile width needs the pictures loaded again at the new size, after this layout pass.
	private void ReloadPicturesIfResized()
	{
		if (Math.Abs(_grid.TileWidth - _decodedTileWidth) < PictureReloadWidthChange)
		{
			return;
		}

		_decodedTileWidth = _grid.TileWidth;
		Dispatcher.Dispatch(() =>
		{
			foreach (ReorderEntry entry in _entries)
			{
				ShelfReorderTiles.ReloadCover(entry);
			}
		});
	}

	// A view sits in its layout bounds at its home spot and moves to its slot with its translation; a lifted book rides 10 dp up.
	private void Place(ReorderEntry entry, bool animate)
	{
		(double x, double y) = _grid.Origin(entry.Slot);
		(double homeX, double homeY) = _grid.Origin(entry.HomeIndex);
		double tx = x - homeX;
		double ty = y - homeY - (entry.Lifted ? ShelfReorderTiles.LiftRise : 0);
		entry.View.AbortAnimation("TranslateTo");
		if (animate)
		{
			_ = entry.View.TranslateToAsync(tx, ty, ShelfReorderTiles.SlideMs, Easing.CubicOut);
			return;
		}

		entry.View.TranslationX = tx;
		entry.View.TranslationY = ty;
	}

	private void PlaceLit(int slot, bool animate)
	{
		(double x, double y) = _grid.Origin(slot);
		(double homeX, double homeY) = _grid.Origin(0);
		_lit.AbortAnimation("TranslateTo");
		if (animate)
		{
			_ = _lit.TranslateToAsync(x - homeX, y - homeY, ShelfReorderTiles.SlideMs, Easing.CubicOut);
			return;
		}

		_lit.TranslationX = x - homeX;
		_lit.TranslationY = y - homeY;
	}

	// A tap only lifts a book (and puts the one lifted before back); nothing opens.
	private void Lift(ReorderEntry tapped)
	{
		_kbIndex = -1; // a pointer lift takes over from the keyboard, as StartDrag does
		_keyboardHolding = false;
		foreach (ReorderEntry entry in _entries)
		{
			SetLifted(entry, entry == tapped);
		}
	}

	private void SetLifted(ReorderEntry entry, bool lifted)
	{
		if (entry.Lifted == lifted)
		{
			return;
		}

		entry.Lifted = lifted;
		entry.View.ZIndex = lifted ? 1 : 0;
		ShelfReorderTiles.ApplyLift(entry);
		Place(entry, animate: true); // a lifted book keeps wiggling (Slice 382)
	}

	private void StartDrag(ReorderEntry entry, DragStartingEventArgs e)
	{
		e.Data.Text = entry.Id;
		_dragged = entry;
		_drag.Begin(Order);
		_kbIndex = -1; // the mouse takes over from the keyboard
		_keyboardHolding = false;
		SetLifted(entry, false);
		_lit.IsVisible = true;
		PlaceLit(entry.Slot, animate: false);
		Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(HideSourceDelayMs), () =>
		{
			if (_dragged == entry)
			{
				entry.View.Opacity = 0; // the system's drag picture is the lifted book; its own spot is the lit one
			}
		});
		DebugLog($"drag start {entry.Id} from {entry.Slot + 1}");
	}

	// Slice 382, Android and iOS: the touch drag. The lifted cover itself follows the finger (scale 1.10, shadow, still wiggling); the lit spot and the sliding follow the finger's slot as with the system drag.
	private bool PanBegin(ReorderEntry entry, double touchX, double touchY)
	{
		if (_dragged is not null || _closing)
		{
			return false;
		}

		_dragged = entry;
		_pointerDrag = true;
		_drag.Begin(Order);
		_kbIndex = -1;
		_keyboardHolding = false;
		foreach (ReorderEntry other in _entries)
		{
			SetLifted(other, other == entry);
		}

		(_panCoverX, _panCoverY) = _grid.Origin(entry.Slot);
		(_panTouchX, _panTouchY) = (touchX, touchY);
		_panScrollY = _scroll.ScrollY;
		(_panDx, _panDy) = (0, 0);
		_lit.IsVisible = true;
		PlaceLit(entry.Slot, animate: false);
		StartPanTimer();
		DebugLog($"drag start {entry.Id} from {entry.Slot + 1} touch");
		DebugLog($"drag wiggle {entry.Id} running={ShelfReorderTiles.IsWiggling(entry)} reduceMotion={Sirous.Reading.Mobile.Shared.Waiting.ReduceMotion.IsOn} scale={entry.View.Scale:0.00}");
		PanUpdate(edgeScroll: false);
		return true;
	}

	private void PanMove(double dx, double dy)
	{
		(_panDx, _panDy) = (dx, dy);
		PanUpdate(edgeScroll: false); // only the timer scrolls at the edge, so the speed does not follow the touch event rate
	}

	private void PanUpdate(bool edgeScroll)
	{
		if (_dragged is not { } entry || !_pointerDrag)
		{
			return;
		}

		(double fx, double fy) = ReorderPan.FingerOnSurface(_panCoverX, _panCoverY, _panTouchX, _panTouchY, _panDx, _panDy, _scroll.ScrollY - _panScrollY);
		(double homeX, double homeY) = _grid.Origin(entry.HomeIndex);
		entry.View.AbortAnimation("TranslateTo");
		entry.View.TranslationX = fx - _panTouchX - homeX;
		entry.View.TranslationY = fy - _panTouchY - homeY;
		MoveTarget(_grid.SlotAt(fx, fy, _entries.Count));
		if (edgeScroll)
		{
			ScrollNearEdge(fy - _scroll.ScrollY);
		}
	}

	// Ended by the finger lifting (a drop) or by the system taking the touch (a cancel: the old order comes back). Nothing when no touch drag is running (a tap, or a pan that never began).
	private void PanEnd(ReorderEntry entry, bool cancelled)
	{
		if (_dragged != entry || !_pointerDrag) // an end from a torn-down entry's pan (review #5) never touches another cover's drag
		{
			return;
		}

		StopPanTimer();
		long now = Environment.TickCount64;
		ApplyDragOutcome(cancelled ? _drag.Completed(Order, now) : _drag.Drop(now));
		DebugLog($"drag end {(cancelled ? "cancelled" : "drop")}");
	}

	// A finger held still near an edge sends no moves, so a timer keeps the edge scroll going.
	private void StartPanTimer()
	{
		StopPanTimer();
		IDispatcherTimer timer = Dispatcher.CreateTimer();
		timer.Interval = TimeSpan.FromMilliseconds(PanTickMs);
		timer.Tick += (_, _) => PanUpdate(edgeScroll: true);
		_panTimer = timer;
		timer.Start();
	}

	private void StopPanTimer()
	{
		_panTimer?.Stop();
		_panTimer = null;
	}

	private void OnDragOver(object? sender, DragEventArgs e)
	{
		if (_dragged is null)
		{
			return;
		}

		if (e.GetPosition(_surface) is { } point)
		{
			MoveTarget(_grid.SlotAt(point.X, point.Y, _entries.Count));
		}

		if (e.GetPosition(_scroll) is { } inView)
		{
			ScrollNearEdge(inView.Y);
		}
	}

	// Dragging near the top or bottom edge scrolls the grid.
	private void ScrollNearEdge(double y)
	{
		if (y < EdgeBand)
		{
			StartEdgeScroll(Math.Max(0, _scroll.ScrollY - EdgeStep));
		}
		else if (y > _scroll.Height - EdgeBand)
		{
			StartEdgeScroll(_scroll.ScrollY + EdgeStep);
		}
	}

	// At most one edge scroll in flight: DragOver fires faster than a scroll finishes, and overlapping ones would pile up and speed the scroll with the event rate.
	private async void StartEdgeScroll(double y)
	{
		if (_edgeScrolling)
		{
			return;
		}

		_edgeScrolling = true;
		try
		{
			await _scroll.ScrollToAsync(0, y, false);
		}
		catch (Exception ex)
		{
			// no-fallback: the list simply does not scroll this tick; drag-reorder still works
			DebugLog($"edge scroll failed: {ex.GetType().Name}"); // an async void must never take the app down
		}
		finally
		{
			_edgeScrolling = false;
		}
	}

	private void MoveTarget(int slot)
	{
		if (_dragged is null || slot == _dragged.Slot)
		{
			return;
		}

		ApplyOrder(ShelfOrder.MoveToSlot(Order, _dragged.Id, slot));
		PlaceLit(_dragged.Slot, animate: true);
		DebugLog($"target {_dragged.Slot + 1}");
	}

	// Gives every book the slot of its id in the list; the others slide there, the dragged one jumps (it is not shown).
	private void ApplyOrder(IReadOnlyList<string> ids)
	{
		List<string> list = [.. ids];
		foreach (ReorderEntry entry in _entries)
		{
			int slot = list.IndexOf(entry.Id);
			if (slot < 0 || slot == entry.Slot)
			{
				continue;
			}

			entry.Slot = slot;
			if (entry == _dragged && _pointerDrag)
			{
				continue; // the finger carries it
			}

			Place(entry, animate: entry != _dragged);
		}
	}

	// Slice 298: the drop and drop-completed events only go to ShelfDragSession (Core, unit-tested, any event order); this applies what it decides.
	private void ApplyDragOutcome(ShelfDragOutcome outcome)
	{
		if (outcome.Order is { } order)
		{
			ApplyOrder(order);
		}

		if (outcome.EndVisual)
		{
			EndDrag();
		}
	}

	private void EndDrag()
	{
		if (_dragged is not { } entry)
		{
			return;
		}

		bool pointer = _pointerDrag;
		_pointerDrag = false;
		_dragged = null;
		_lit.IsVisible = false;
		entry.View.Opacity = 1;
		if (pointer)
		{
			SetLifted(entry, false); // scale and shadow back; the wiggle never stopped
		}

		entry.View.AbortAnimation("TranslateTo");
		(double x, double y) = _grid.Origin(entry.Slot);
		(double homeX, double homeY) = _grid.Origin(entry.HomeIndex);
		_ = entry.View.TranslateToAsync(x - homeX, y - homeY, DropSettleMs, Easing.CubicOut);
		if (!pointer && !ShelfReorderTiles.IsWiggling(entry))
		{
			ShelfReorderTiles.StartWiggle(entry, entry.Slot, Dispatcher); // the wiggle never stops during a drag (Slice 382): restarting a running one would jerk the dropped cover
		}

		DebugLog($"dropped at {entry.Slot + 1}");
	}

	/// <summary>Debug-only (the shelf-reorder flow): moves the book at 1-based position <paramref name="from"/> to position <paramref name="to"/>, by the rule a drop uses; returns the ids in the new order.</summary>
	internal string DebugMove(int from, int to)
	{
		List<string> ids = [.. Order];
		if (from < 1 || from > ids.Count || to < 1 || to > ids.Count)
		{
			return string.Empty;
		}

		ApplyOrder(ShelfOrder.MoveToSlot(ids, ids[from - 1], to - 1)); // the same rule a real drop and the arrow keys use
		return string.Join(",", Order);
	}

	// Debug builds only: the shelf-reorder flow reads these lines (RTST-TEST tag) to see which path ran.
	[System.Diagnostics.Conditional("DEBUG")]
	private static void DebugLog(string line)
	{
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Reorder] {line}");
#endif
	}
}
