using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;
using Sirous.Reading.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>The mouse cursors of the shelf's reorder mode (Slice 264, mock Batch 47 frame 176): an open hand while a cover is held or can be dragged, a closed hand once it is lifted. Only Windows draws them (<c>Platforms/Windows/DinoCursor.cs</c>).</summary>
internal enum DinoCursorShape
{
	Default,
	OpenHand,
	ClosedHand,
}

/// <summary>One book in the reorder mode (Slice 264): the tile, the spot it was laid out at and the spot it is in now.</summary>
internal sealed class ReorderEntry(StoryTile tile, Grid view, int homeIndex)
{
	public StoryTile Tile { get; } = tile;

	public Grid View { get; } = view;

	/// <summary>The grid spot the view's layout bounds sit at; the view moves from there with its translation.</summary>
	public int HomeIndex { get; } = homeIndex;

	/// <summary>The grid spot the book is in now (its place in the working order).</summary>
	public int Slot { get; set; } = homeIndex;

	/// <summary>Lifted by a tap, or by the sheet's Reorder row: bigger, with a shadow, still, title hidden.</summary>
	public bool Lifted { get; set; }

	/// <summary>Bumped by every StartWiggle and StopWiggle: a delayed start whose number is no longer current has been cancelled.</summary>
	public int WiggleGeneration { get; set; }

	/// <summary>The timer of the one wiggle start still waiting for its stagger; stopped by the next start, by StopWiggle and so by Close.</summary>
	public IDispatcherTimer? WiggleDelay { get; set; }

	public string Id => Tile.Story.Id;
}

/// <summary>
/// Release-visible trace of the reorder mode (Slice 264): on Windows a line in window-trace.log ("Reorder trace: step", step names only, never a title or text), nothing elsewhere.
/// Added because a Release build showed no [Reorder] log and the app died opening the mode on the VM.
/// </summary>
internal static class ReorderTrace
{
	public const string Prefix = "Reorder trace: ";

	[System.Diagnostics.Conditional("DEBUG"), System.Diagnostics.Conditional("DINO_TEST_BOOKS")] // the device-test switch: calls and their arguments are compiled out of a store build
	public static void Step(string step)
	{
#if WINDOWS
		WindowTrace.Write(Prefix + step);
#endif
	}
}

/// <summary>The views and motion of the reorder mode's tiles (Slice 264, mock Batch 47 frames 173 and 174).</summary>
internal static class ShelfReorderTiles
{
	public const double WiggleDegrees = 1.6;
	public const uint WigglePeriod = 520; // 0.26 s each way
	public const int WiggleStaggerMs = 70;
	public const double LiftScale = 1.10;
	public const double LiftRise = 10;
	public const uint SlideMs = 350;
	public const string WiggleAnimation = "reorder-wiggle";
	private const double CoverCorner = 16;
	private const double OutlineStroke = 2;
	private const double TitleSize = 12.5;
	private const double GripDot = 3.5;
	private const double GripGap = 3;
	private const string LiftShadowId = "ReorderLiftShadow";
	private const float ShadowAlpha = 0.32f;
	private const double ShadowOffset = 14;
	private static readonly Color ShadowInk = Color.FromArgb("#2b2540");

	/// <summary>A tile: the cover (as on the shelf), its title under it, and, with reduced motion, a dashed outline in place of the wiggle.</summary>
	public static Grid Build(StoryTile tile, ShelfOrderGrid grid, double titleHeight, double titleSpacing, bool outline)
	{
		var cover = new StoryCover { BindingContext = tile, InputTransparent = true, AutomationId = "ReorderCover" };
		var title = new Label
		{
			Text = tile.Title, FontFamily = tile.TitleFont, FontSize = TitleSize, FontAttributes = FontAttributes.Bold, LineHeight = 1.25, MaxLines = 2,
			LineBreakMode = LineBreakMode.TailTruncation, HeightRequest = titleHeight, InputTransparent = true, AutomationId = "ReorderTileTitle",
		};
		var view = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)], RowSpacing = titleSpacing, AutomationId = "ReorderTile", FlowDirection = FlowDirection.LeftToRight };
		view.Add(LiftShadow(), 0, 0); // first, so it draws behind the cover
		view.Add(cover, 0, 0);
		view.Add(title, 0, 1);
		if (outline)
		{
			view.Add(DashedOutline(Colors.Transparent, OutlineStroke), 0, 0);
		}

		SemanticProperties.SetDescription(view, tile.Title);
		view.WidthRequest = grid.TileWidth;
		view.HeightRequest = grid.TileHeight;
		return view;
	}

	/// <summary>Loads the tile's cover picture again at the size the cover has now.</summary>
	public static void ReloadCover(ReorderEntry entry) => entry.View.Children.OfType<StoryCover>().FirstOrDefault()?.ReloadPicture();

	/// <summary>The lit target spot: Brand at 14% with a 3 dp dashed Brand ring, the size of a cover.</summary>
	public static Border LitSpot(double width, double height) => DashedOutline(Color.FromRgba(DinoTokens.Get(DinoTokens.Brand).Red, DinoTokens.Get(DinoTokens.Brand).Green, DinoTokens.Get(DinoTokens.Brand).Blue, 0.14), 3, width, height);

	private static Border DashedOutline(Color fill, double stroke, double width = -1, double height = -1) => new()
	{
		Stroke = DinoTokens.Get(DinoTokens.Brand),
		StrokeThickness = stroke,
		StrokeDashArray = [4, 3],
		BackgroundColor = fill,
		StrokeShape = new RoundRectangle { CornerRadius = CoverCorner },
		InputTransparent = true,
		WidthRequest = width,
		HeightRequest = height,
		VerticalOptions = height > 0 ? LayoutOptions.Start : LayoutOptions.Fill,
	};

	/// <summary>The six-dot grip of the sheet's Reorder row (two columns of three dots).</summary>
	public static View GripDots(Color colour)
	{
		var grid = new Grid { ColumnSpacing = GripGap, RowSpacing = GripGap, VerticalOptions = LayoutOptions.Center, InputTransparent = true };
		for (int i = 0; i < 6; i++)
		{
			var dot = new BoxView { Color = colour, WidthRequest = GripDot, HeightRequest = GripDot, CornerRadius = GripDot / 2 };
			grid.Add(dot, i % 2, i / 2);
		}

		return grid;
	}

	/// <summary>Starts the endless +-1.6 degree rotation, each cover 0.07 s after the one before; nothing under reduced motion. A lifted or dragged book wiggles too (Slice 382, the user's decision of 2026-11-14).</summary>
	public static void StartWiggle(ReorderEntry entry, int order, IDispatcher dispatcher)
	{
		if (ReduceMotion.IsOn)
		{
			return;
		}

		View view = entry.View;
		int generation = ++entry.WiggleGeneration;
		entry.WiggleDelay?.Stop(); // one waiting start per cover: rapid lift and drop replace it instead of queuing closures that outlive Close
		IDispatcherTimer timer = dispatcher.CreateTimer();
		timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, WiggleStaggerMs * (order % 8)));
		timer.IsRepeating = false;
		timer.Tick += (_, _) =>
		{
			timer.Stop();
			if (generation == entry.WiggleGeneration && view.IsVisible)
			{
				new Animation(t => view.Rotation = WiggleDegrees * Math.Sin(2 * Math.PI * t)).Commit(view, WiggleAnimation, 16, WigglePeriod, Easing.Linear, repeat: () => true);
			}
		};
		entry.WiggleDelay = timer;
		timer.Start();
	}

	public static void StopWiggle(ReorderEntry entry)
	{
		entry.WiggleGeneration++; // cancels a start still waiting for its stagger
		entry.WiggleDelay?.Stop();
		entry.WiggleDelay = null;
		entry.View.AbortAnimation(WiggleAnimation);
		entry.View.Rotation = 0;
	}

	/// <summary>Whether the cover's wiggle animation is running now (the shelf-reorder flow's log line reads it).</summary>
	public static bool IsWiggling(ReorderEntry entry) => entry.View.AnimationIsRunning(WiggleAnimation);

	/// <summary>The lifted look (scale 1.10, shadow, title hidden, 10 dp up) or the resting one.</summary>
	public static void ApplyLift(ReorderEntry entry)
	{
		ReorderTrace.Step($"lift apply before lifted={entry.Lifted}");
		entry.View.Scale = entry.Lifted ? LiftScale : 1;
		ReorderTrace.Step("lift scale set");
		foreach (IView child in entry.View.Children)
		{
			ApplyLiftToChild(child, entry.Lifted);
		}

		ReorderTrace.Step("lift apply after");
	}

	// The shadow is a plain element behind the cover (no MAUI Shadow: it faults on Windows composition and would look different per platform); the title hides while lifted.
	private static void ApplyLiftToChild(IView child, bool lifted)
	{
		if (child is Border { AutomationId: LiftShadowId } shadow)
		{
			shadow.IsVisible = lifted;
		}
		else if (child is Label title)
		{
			title.Opacity = lifted ? 0 : 1;
		}
	}

	private static Border LiftShadow() => new()
	{
		AutomationId = LiftShadowId,
		BackgroundColor = ShadowInk.WithAlpha(ShadowAlpha),
		StrokeThickness = 0,
		StrokeShape = new RoundRectangle { CornerRadius = CoverCorner },
		TranslationY = ShadowOffset,
		InputTransparent = true,
		IsVisible = false,
	};
}

/// <summary>What a touch drag on a cover tells the reorder view (Slice 382). Positions are in dp. Began: where inside the cover the finger is, answers whether the drag was accepted (false: the touch is left alone); Moved: how far the finger has moved since Began; Ended: true when the system cancelled it.</summary>
internal sealed record ReorderPanCallbacks(Func<double, double, bool> Began, Action<double, double> Moved, Action<bool> Ended);

#if ANDROID || IOS
/// <summary>
/// The touch drag of the reorder mode on Android and iOS (Slice 382): in edit mode a touch on a cover that moves past <see cref="ReorderPan.ThresholdDp"/> is a drag at once, with no hold (MAUI's DragGestureRecognizer is the
/// system drag-and-drop there, which needs a long press). It is the platform's own touch, not a PanGestureRecognizer: that one reports totals relative to the cover, which moves under the finger, and loses the touch to the
/// scroll view. A touch on a cover never scrolls the grid (the drag owns it; the grid still scrolls from between covers and by the edge scroll of a drag). A tap still reaches the tap recognizer.
/// </summary>
internal static class ReorderTouchPan
{
	public static void Attach(View view, ReorderPanCallbacks callbacks)
	{
		IDisposable? attached = null; // the pan on the current native view; HandlerChanged fires on every connect and disconnect, so one is removed before the next is added
		view.HandlerChanged += (_, _) =>
		{
			attached?.Dispose();
			attached = Connect(view, callbacks);
		};
	}

	private static IDisposable? Connect(View view, ReorderPanCallbacks callbacks)
	{
#if ANDROID
		if (view.Handler?.PlatformView is Android.Views.View native)
		{
			native.Clickable = true; // the view takes the touch-down itself, so the moves come to it whatever the other listeners answer
			return new AndroidPan(native, callbacks);
		}
#else
		if (view.Handler?.PlatformView is UIKit.UIView native)
		{
			return new IosPan(native, callbacks);
		}
#endif
		return null;
	}

#if ANDROID
	private sealed class AndroidPan : IDisposable
	{
		private const int NoPointer = -1;
		private readonly Android.Views.View _native;
		private readonly ReorderPanCallbacks _callbacks;
		private readonly float _density;
		private float _startX;
		private float _startY;
		private bool _dragging;
		private readonly double _thresholdDp;
		private int _pointerId = NoPointer;

		public AndroidPan(Android.Views.View native, ReorderPanCallbacks callbacks)
		{
			_native = native;
			_callbacks = callbacks;
			_density = native.Context?.Resources?.DisplayMetrics?.Density ?? 1;
			_thresholdDp = ReorderPan.ThresholdFor(TouchSlopDp(native.Context, _density));
			native.Touch += OnTouch;
		}

		// Review check #1/medium #2: the drag starts only past the platform's touch slop. MAUI's tap recognizer on Android is a GestureDetector (TapAndPanGestureDetector), which stops reporting a single tap once the finger leaves the slop, so a touch that stays under it is still a tap; a drag's own Up is swallowed (Handled), so no synthetic Cancel is needed.
		private static double TouchSlopDp(Android.Content.Context? context, float density) =>
			context is null ? 0 : Android.Views.ViewConfiguration.Get(context)?.ScaledTouchSlop / (double)density ?? 0;

		public void Dispose()
		{
			_native.Touch -= OnTouch;
			End(cancelled: true); // the view went away mid-drag: the view puts the old order back
		}

		// Handled only while a drag runs: a touch that has not moved yet must still reach MAUI's tap recognizer (a tap lifts a cover), as LongPressBehavior.Android does.
		private void OnTouch(object? sender, Android.Views.View.TouchEventArgs e)
		{
			bool wasDragging = _dragging;
			if (e.Event is { } ev)
			{
				Handle(ev);
			}

			e.Handled = wasDragging || _dragging;
		}

		private void Handle(Android.Views.MotionEvent ev)
		{
			switch (ev.ActionMasked)
			{
				case Android.Views.MotionEventActions.Down:
					End(cancelled: true); // a drag that never got its Up or Cancel (a rebuild, a detached view) ends here, so the order is put back
					_pointerId = ev.GetPointerId(0);
					(_startX, _startY) = Raw(ev, 0);
					_native.Parent?.RequestDisallowInterceptTouchEvent(true); // the scroll view must not take the touch from the cover before the threshold
					break;
				case Android.Views.MotionEventActions.Move:
					Move(ev);
					break;
				case Android.Views.MotionEventActions.PointerUp when ev.GetPointerId(ev.ActionIndex) == _pointerId:
				case Android.Views.MotionEventActions.Up:
					End(cancelled: false);
					break;
				case Android.Views.MotionEventActions.Cancel:
					End(cancelled: true);
					break;
			}
		}

		// The position of the tracked finger on the screen. API 29+ has GetRawX(index); below it RawX/RawY read pointer 0 only, so the other fingers are offset from it.
		private static (float X, float Y) Raw(Android.Views.MotionEvent ev, int index) =>
			OperatingSystem.IsAndroidVersionAtLeast(29)
				? (ev.GetRawX(index), ev.GetRawY(index))
				: (ev.GetX(index) + ev.RawX - ev.GetX(0), ev.GetY(index) + ev.RawY - ev.GetY(0));

		private void Move(Android.Views.MotionEvent ev)
		{
			int index = ev.FindPointerIndex(_pointerId);
			if (index < 0)
			{
				return;
			}

			(float rawX, float rawY) = Raw(ev, index);
			double dx = (rawX - _startX) / _density;
			double dy = (rawY - _startY) / _density;
			if (!_dragging)
			{
				if (!ReorderPan.StartsDrag(dx, dy, _thresholdDp))
				{
					return;
				}

				_dragging = _callbacks.Began(ev.GetX(index) / _density, ev.GetY(index) / _density); // a refused begin (another drag runs) leaves the touch unhandled
				_startX = rawX; // the moves are measured from where the drag began
				_startY = rawY;
				return;
			}

			_callbacks.Moved(dx, dy);
		}

		private void End(bool cancelled)
		{
			_pointerId = NoPointer;
			if (_dragging)
			{
				_dragging = false;
				_callbacks.Ended(cancelled);
			}
		}
	}
#else
	// A pan recognizer on the cover. The scroll view's own pan waits for it to fail, so a touch on a cover never scrolls the grid under the finger before the drag begins (the grid still scrolls from between covers).
	private sealed class IosPan : IDisposable
	{
		private readonly UIKit.UIPanGestureRecognizer _pan;
		private readonly UIKit.UIView _native;
		private readonly ReorderPanCallbacks _callbacks;
		private CoreGraphics.CGPoint _start;
		private bool _began;
		private UIKit.UIScrollView? _disabledScroll; // the scroll view this pan switched off at Began: the same one is switched back, never looked up again through Superview (null after a detach)

		public IosPan(UIKit.UIView native, ReorderPanCallbacks callbacks)
		{
			_native = native;
			_callbacks = callbacks;
			_pan = new UIKit.UIPanGestureRecognizer(OnPan)
			{
				MaximumNumberOfTouches = 1,
				ShouldBeRequiredToFailBy = (_, other) => other.View is UIKit.UIScrollView,
			};
			native.AddGestureRecognizer(_pan);
		}

		public void Dispose()
		{
			_native.RemoveGestureRecognizer(_pan);
			_pan.Dispose();
			End(cancelled: true);
		}

		private void OnPan(UIKit.UIPanGestureRecognizer pan)
		{
			CoreGraphics.CGPoint inWindow = pan.LocationInView(null); // the window: it does not move with the cover
			switch (pan.State)
			{
				case UIKit.UIGestureRecognizerState.Began:
					_start = inWindow;
					CoreGraphics.CGPoint inCover = pan.LocationInView(_native);
					if (_callbacks.Began(inCover.X, inCover.Y))
					{
						_began = true;
						_disabledScroll = FindScroll(_native);
						if (_disabledScroll is not null)
						{
							_disabledScroll.ScrollEnabled = false; // the scroll view's own pan must let go for the drag
						}
					}

					break;
				case UIKit.UIGestureRecognizerState.Changed when _began: // a refused begin (another cover is already dragged) moves nothing
					_callbacks.Moved(inWindow.X - _start.X, inWindow.Y - _start.Y);
					break;
				case UIKit.UIGestureRecognizerState.Ended:
				case UIKit.UIGestureRecognizerState.Cancelled:
					End(pan.State == UIKit.UIGestureRecognizerState.Cancelled);
					break;
			}
		}

		// Failed (a tap that never moved) is not an end: no drag began, so nothing is told and nothing is switched back.
		private void End(bool cancelled)
		{
			if (!_began)
			{
				return;
			}

			_began = false;
			if (_disabledScroll is not null)
			{
				_disabledScroll.ScrollEnabled = true;
				_disabledScroll = null;
			}

			_callbacks.Ended(cancelled);
		}

		private static UIKit.UIScrollView? FindScroll(UIKit.UIView from)
		{
			for (UIKit.UIView? v = from.Superview; v is not null; v = v.Superview)
			{
				if (v is UIKit.UIScrollView scroll)
				{
					return scroll;
				}
			}

			return null;
		}
	}
#endif
}
#endif
