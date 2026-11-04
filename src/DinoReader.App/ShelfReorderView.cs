using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The shelf's reorder mode (Slice 264, mock Batch 47 frames 173 to 176): a state of the gallery, drawn as a layer over it with its own ink bar (Cancel left, "Reorder", Done), a hint, and the books in a grid
/// that wiggle. A tap lifts a book; dragging one (a long press first on Android and iOS, as the system drag starts there; a plain mouse drag on Windows) shows the spot it would land in, lit, while the
/// others slide to make room. It works on an in-memory order and owns nothing stored: Done hands the order to the page, which is the only thing that saves; Cancel and closing save nothing.
/// Positions are computed by <see cref="ShelfOrderGrid"/> and the move by <see cref="ShelfOrder.MoveBefore"/>; the layout never mirrors.
/// </summary>
internal sealed class ShelfReorderView : Grid
{
	private const double HeaderHeight = 44;
	private const double TitleSpacing = 7;
	private const double EdgeBand = 72;
	private const double EdgeStep = 36;
	private const double BottomRoom = 24;
	private const double MinWidthChange = 1;
	private double _lastLayoutWidth;
	private const uint DropSettleMs = 150;
	private const int HideSourceDelayMs = 100;

	private readonly AbsoluteLayout _surface = new() { FlowDirection = FlowDirection.LeftToRight };
	private readonly ScrollView _scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Never };
	private readonly Label _cancelLabel = new() { AutomationId = "ReorderCancel" };
	private readonly Label _titleLabel = new();
	private readonly Label _doneLabel = new();
	private readonly Label _hint = new();
	private readonly List<ReorderEntry> _entries = [];
	private Border _lit = null!;
	private ShelfOrderGrid _grid;
	private int _columns;
	private double _sidePadding;
	private double _gap;
	private double _titleHeight;
	private ReorderEntry? _dragged;
	private bool _closing;
	private int _kbIndex = -1; // the spot the keyboard focus is on; -1 until a key is pressed
	private bool _keyboardHolding; // Space lifted the focused cover and the arrows carry it

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
		_hint.Padding = new Thickness(18, 12, 18, 8);
		_hint.AutomationId = "ReorderHint";
		this.Add(_hint, 0, 1);
		var drop = new DropGestureRecognizer { AllowDrop = true };
		drop.DragOver += OnDragOver;
		drop.Drop += (_, _) => EndDrag();
		_surface.GestureRecognizers.Add(drop);
		_scroll.Content = _surface;
		_scroll.SizeChanged += OnScrollSizeChanged;
		this.Add(_scroll, 0, 2);
	}

	/// <summary>Raised by Done with the new order of the book ids; the page saves it and closes the mode.</summary>
	public event Func<IReadOnlyList<string>, Task>? DoneRequested;

	/// <summary>Raised by Cancel: the page closes the mode and saves nothing.</summary>
	public event Action? CancelRequested;

	/// <summary>The book ids in the order shown now.</summary>
	public IReadOnlyList<string> Order => [.. _entries.OrderBy(e => e.Slot).Select(e => e.Id)];

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
		CastViews.AddTap(_cancelLabel, () => { CancelRequested?.Invoke(); return Task.CompletedTask; });
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
		CastViews.AddTap(done, () => DoneRequested is { } requested ? requested(Order) : Task.CompletedTask);
		bar.Add(_cancelLabel, 0, 0);
		bar.Add(_titleLabel, 1, 0);
		bar.Add(done, 2, 0);
		return bar;
	}

	/// <summary>Shows the mode over the gallery with <paramref name="tiles"/> in their shelf order; <paramref name="liftedId"/> starts lifted (the sheet's Reorder row).</summary>
	public void Open(IReadOnlyList<StoryTile> tiles, string? liftedId, int columns, double sidePadding, double gap, double titleHeight)
	{
		ReorderTrace.Step($"view open begin books={tiles.Count} lifted={liftedId is not null}");
		ClearEntries();
		_columns = columns;
		_sidePadding = sidePadding;
		_gap = gap;
		_titleHeight = titleHeight;
		_closing = false;
		_kbIndex = -1;
		_keyboardHolding = false;
		_cancelLabel.Text = AppResources.Common_Cancel;
		_titleLabel.Text = AppResources.Gallery_Reorder;
		_doneLabel.Text = AppResources.Common_Done;
		_hint.Text = AppResources.Gallery_ReorderHint;
		SemanticProperties.SetDescription(_cancelLabel, _cancelLabel.Text);
		_grid = ShelfOrderGrid.Fit(Math.Max(_scroll.Width, 1), columns, sidePadding, gap, gap, titleHeight, TitleSpacing);
		_lit = ShelfReorderTiles.LitSpot(_grid.TileWidth, _grid.TileHeight - TitleSpacing - titleHeight);
		_lit.IsVisible = false;
		_surface.Children.Add(_lit);
		AddEntries(tiles, liftedId);
		IsVisible = true;
		Dispatcher.Dispatch(Relayout);
		ReorderTrace.Step("view open end");
	}

	private void AddEntries(IReadOnlyList<StoryTile> tiles, string? liftedId)
	{
		bool outline = ReadTheStupidText.Mobile.Shared.Waiting.ReduceMotion.IsOn;
		for (int i = 0; i < tiles.Count; i++)
		{
			Grid view = ShelfReorderTiles.Build(tiles[i], _grid, _titleHeight, TitleSpacing, outline);
			var entry = new ReorderEntry(tiles[i], view, i) { Lifted = tiles[i].Story.Id == liftedId };
			_entries.Add(entry);
			AttachGestures(entry);
			_surface.Children.Add(view);
			ShelfReorderTiles.ApplyLift(entry);
			view.ZIndex = entry.Lifted ? 1 : 0;
			ShelfReorderTiles.StartWiggle(entry, i, Dispatcher);
		}
	}

	private void AttachGestures(ReorderEntry entry)
	{
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => Lift(entry);
		var drag = new DragGestureRecognizer { CanDrag = true };
		drag.DragStarting += (_, e) => StartDrag(entry, e);
		drag.DropCompleted += (_, _) => EndDrag();
		entry.View.GestureRecognizers.Add(tap);
		entry.View.GestureRecognizers.Add(drag);
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
	public void Reshape(int columns, double sidePadding, double gap)
	{
		if (!IsVisible || (columns == _columns && sidePadding == _sidePadding && gap == _gap))
		{
			return;
		}

		_columns = columns;
		_sidePadding = sidePadding;
		_gap = gap;
		Relayout();
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
		int target = ShelfOrderKeys.Target(_kbIndex, _entries.Count, _columns, arrow);
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
		_surface.Children.Clear();
		_dragged = null;
	}

	// Only a change of the scroll's width re-lays out (a height-only change, or a scrollbar flicker under 1 dp, must not feed back into the surface size: Windows threw a layout cycle).
	private void OnScrollSizeChanged(object? sender, EventArgs e)
	{
		if (Math.Abs(_scroll.Width - _lastLayoutWidth) >= MinWidthChange)
		{
			Relayout();
		}
	}

	// Sizes and places every tile from the scroll view's width; a rotation or the first show lands here, and nothing is animated.
	private void Relayout()
	{
		if (_closing || _entries.Count == 0 || _scroll.Width <= 0)
		{
			return;
		}


		_lastLayoutWidth = _scroll.Width;
		_grid = ShelfOrderGrid.Fit(_scroll.Width, _columns, _sidePadding, _gap, _gap, _titleHeight, TitleSpacing);
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
		Place(entry, animate: true);
		if (lifted)
		{
			ShelfReorderTiles.StopWiggle(entry);
		}
		else
		{
			ShelfReorderTiles.StartWiggle(entry, entry.Slot, Dispatcher);
		}
	}

	private void StartDrag(ReorderEntry entry, DragStartingEventArgs e)
	{
		e.Data.Text = entry.Id;
		_dragged = entry;
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
			_ = _scroll.ScrollToAsync(0, Math.Max(0, _scroll.ScrollY - EdgeStep), false);
		}
		else if (y > _scroll.Height - EdgeBand)
		{
			_ = _scroll.ScrollToAsync(0, _scroll.ScrollY + EdgeStep, false);
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
			Place(entry, animate: entry != _dragged);
		}
	}

	private void EndDrag()
	{
		if (_dragged is not { } entry)
		{
			return;
		}

		_dragged = null;
		_lit.IsVisible = false;
		entry.View.Opacity = 1;
		entry.View.AbortAnimation("TranslateTo");
		(double x, double y) = _grid.Origin(entry.Slot);
		(double homeX, double homeY) = _grid.Origin(entry.HomeIndex);
		_ = entry.View.TranslateToAsync(x - homeX, y - homeY, DropSettleMs, Easing.CubicOut);
		ShelfReorderTiles.StartWiggle(entry, entry.Slot, Dispatcher);
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
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Reorder] {line}");
#endif
	}
}
