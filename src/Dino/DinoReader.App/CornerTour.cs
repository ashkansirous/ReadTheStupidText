using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using FallbackArea = Sirous.Reading.Application.Usage.FallbackArea;
using FallbackReason = Sirous.Reading.Application.Usage.FallbackReason;

namespace DinoReader;

/// <summary>
/// Step 6 of the first run, the grown-ups' corner and the child's shelf (Slice 362, mock N8a, N8b, N9, N10): a layer over the gallery. A welcome card over the Books tab, then each of the three tabs lifted out of the scrim with the amber
/// pulse and a tip ("1 of 3 · Next"), then the Child's shelf pill lifted with its tip, then the child's shelf itself under a lighter scrim with the Grown-ups pill lifted and the last card (Start reading). The tabs, the coral pill and the
/// Grown-ups pill are not moved: a copy of each is drawn over the scrim at the real one's place, so the corner and the shelf keep every rule they have. Under 600 wide the tabs are a bottom bar and the tips stand above them; from 600 the tabs are
/// a left rail and the tips stand beside it. <see cref="CornerTourFlow"/> holds the order. Android Back and Esc do nothing while it shows; reduce motion keeps the ring and drops the pulse.
/// </summary>
internal sealed class CornerTour : Grid
{
	private const string ScrimColour = "#9E140E28"; // rgba(20,14,40,.62)
	private const string LightScrimColour = "#73140E28"; // rgba(20,14,40,.45)
	private const string LiftFill = "#FFFFFF";
	private const string TipInk = "#3a3450";
	private const string TipLine = "#5a5470";
	private const double TipWidth = 300;
	private const double TipGap = 14;
	private const double TileCorner = 18;
	private const double PointerSize = 18;
	private const double CardCorner = 28;
	private const double EdgeMargin = 12;
	private const double LiftGrow = TourGeometry.LiftGrow;
	private const int CopyChildIndex = 1; // every stop with a target adds the scrim, then the lifted copy
	private const double ChildCardBottom = 40;
	private const double WelcomeTop = 190;

	private readonly Func<CornerTab, View> _tabView;
	private readonly Func<View> _childShelfPill;
	private readonly Func<View> _grownUpsPill;
	private readonly Action<CornerTab> _selectTab;
	private readonly Action _showShelf;
	private readonly Action _finish;
	private readonly BoxView _scrim = new() { Color = Color.FromArgb(ScrimColour) };
	private TourStop _stop = TourStop.Welcome;
	private int _showVersion;
	private int _placementMisses; // Debug flow check: stops drawn off their target or unreadable in this tour
	private int _fallbackReportedVersion = -1; // the one-sided screen read is reported once per drawn stop

	public CornerTour(Func<CornerTab, View> tabView, Func<View> childShelfPill, Func<View> grownUpsPill, Action<CornerTab> selectTab, Action showShelf, Action finish)
	{
		_tabView = tabView;
		_childShelfPill = childShelfPill;
		_grownUpsPill = grownUpsPill;
		_selectTab = selectTab;
		_showShelf = showShelf;
		_finish = finish;
		AutomationId = "CornerTour";
		ZIndex = 100;
		IsVisible = false;
		Taps.AddTap(_scrim, () => Task.CompletedTask); // no-tap-count: the scrim only swallows touches, so nothing under it can be tapped during the tour
		SizeChanged += (_, _) => RedrawForNewSize();
	}

	private bool _drawing; // a stop is chosen but not yet drawn: Next and Show me do nothing meanwhile, so a double tap cannot skip a stop
	private Size _drawnSize;

	/// <summary>True from <see cref="Begin"/> until the tour ends: Android Back and Esc do nothing meanwhile.</summary>
	public bool IsActive { get; private set; }

	/// <summary>Starts at the welcome card (N8a).</summary>
	public void Begin()
	{
		IsActive = true;
		IsVisible = true;
		_placementMisses = 0;
		Show(TourStop.Welcome);
	}

	/// <summary>Windows: Enter does what the card's main button does; every other key is left alone (Esc is swallowed by the gallery).</summary>
	public bool Advance()
	{
		if (!IsActive)
		{
			return false;
		}

		Next();
		return true;
	}

	private void Next()
	{
		if (_drawing)
		{
			return;
		}

		if (CornerTourFlow.Next(_stop) is { } next)
		{
			Show(next);
			return;
		}

		End();
	}

	private void End()
	{
		LogPlacementSummary();
		IsActive = false;
		IsVisible = false;
		_drawing = false;
		ClearLayer();
		_showShelf(); // every end leaves grown-ups mode, as the normal end does (a stop that cannot be drawn must not leave Settings and delete in a child's reach)
		_finish();
	}

	// Empties the layer and stops the rings first: a ring's repeating animation would otherwise keep running on a view that is no longer shown.
	private void ClearLayer()
	{
		StopWatchingPlacement();
		foreach (PulseRing ring in Children.OfType<PulseRing>())
		{
			ring.Stop();
		}

		Children.Clear();
	}

	// A rotation or a resize moves the real tabs and pills: the stop is drawn again at their new places (the layer fills the gallery, so its own size changing is the signal).
	private void RedrawForNewSize()
	{
		if (!IsActive || Width <= 0 || Height <= 0 || _drawnSize == new Size(Width, Height) || _drawnSize == default)
		{
			return;
		}

		int version = ++_showVersion;
		_drawing = true;
		TourStop stop = _stop;
		Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(RedrawDelayMilliseconds), () => Draw(stop, version, retries: 8)); // the gallery lays its tabs out for the new size first
	}

	private const int RedrawDelayMilliseconds = 250;

	// ---- one stop ----

	private void Show(TourStop stop)
	{
		_stop = stop;
		_drawing = true;
		int version = ++_showVersion;
		if (stop is TourStop.Books or TourStop.Store or TourStop.Settings)
		{
			_selectTab(stop == TourStop.Books ? CornerTab.Books : stop == TourStop.Store ? CornerTab.Store : CornerTab.Settings); // each step switches to its tab behind the scrim
		}
		else if (stop == TourStop.Welcome)
		{
			_selectTab(CornerTab.Books);
		}
		else if (stop == TourStop.ChildShelf)
		{
			_showShelf();
		}

		TourLog($"stop={stop}");
		Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(stop == TourStop.ChildShelf ? 450 : 120), () => Draw(stop, version, retries: 8)); // the tab or the shelf is laid out first, then its bounds are read
	}

	private void Draw(TourStop stop, int version, int retries)
	{
		if (version != _showVersion || !IsActive)
		{
			return;
		}

		Rect? bounds = NeedsBounds(stop) ? BoundsOf(stop, allowLayoutFallback: retries == 0) : null; // read once: the copy is drawn at the rect the guard accepted
		if (Width <= 0 || Height <= 0 || (NeedsBounds(stop) && bounds is null))
		{
			if (retries > 0)
			{
				Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(120), () => Draw(stop, version, retries - 1));
				return;
			}

			// The stop cannot be drawn (the control is not on screen): the tour ends here, saved as finished, and Back works again, rather than staying up with nothing to press.
			TourLog($"stop={stop} could not be drawn; ending");
			End();
			return;
		}

		_drawing = false;
		_drawnSize = new Size(Width, Height);
		ClearLayer();
		_scrim.Color = Color.FromArgb(stop == TourStop.ChildShelf ? LightScrimColour : ScrimColour);
		Children.Add(_scrim);
		switch (stop)
		{
			case TourStop.Welcome:
				DrawWelcome();
				break;
			case TourStop.ShelfPointer:
				DrawShelfPointer(bounds!.Value);
				break;
			case TourStop.ChildShelf:
				DrawChildShelf(bounds!.Value);
				break;
			default:
				DrawTab(stop, bounds!.Value);
				break;
		}

		if (NeedsBounds(stop) && ScreenPositionReadable) // Windows cannot read a screen position, so there is nothing to compare there
		{
			WatchPlacement(stop, version);
		}
	}

	// Slice 381, Debug only: where the drawn copy and its real target are on the screen, in dp, once the copy has been laid out. aligned=yes when the copy's centre is within 4 dp of the target's; the flow
	// onboarding-tour-position expects it for every stop, so a copy drawn a status bar too low fails it. The target is read again here, so a target that moved since the draw shows up too.
	private void LogPlacement(TourStop stop, int version, View copy)
	{
#if DEBUG
		if (version != _showVersion || !IsActive || BoundsTargetOf(stop) is not { } target)
		{
			return;
		}

		if (ScreenBounds(copy) is not { } copyBounds || ScreenBounds(target) is not { } targetBounds)
		{
			_placementMisses++;
			TourLog($"stop={stop} placement unreadable");
			return;
		}

		bool aligned = TourGeometry.IsAligned(copyBounds, targetBounds);
		_placementMisses += aligned ? 0 : 1;
		TourLog($"stop={stop} aligned={(aligned ? "yes" : "no")} copy={Format(copyBounds)} target={Format(targetBounds)}");
#endif
	}

	// Debug only: the placement is read as soon as the copy has been laid out (the first size change, or at once when it already is), not after a fixed delay, so a tap right after the card shows cannot drop it.
	private void WatchPlacement(TourStop stop, int version)
	{
#if DEBUG
		if (Children.Count <= CopyChildIndex || Children[CopyChildIndex] is not View copy)
		{
			_placementMisses++;
			TourLog($"stop={stop} placement unreadable");
			return;
		}

		if (copy.Width > 0 && copy.Height > 0)
		{
			Dispatcher.Dispatch(() => LogPlacement(stop, version, copy));
			return;
		}

		WatchUntilLaidOut(stop, version, copy);
#endif
	}

#if DEBUG
	private View? _watchedCopy; // the copy whose first layout is awaited, so the handler can be dropped when the stop ends
	private EventHandler? _watchHandler;
#endif

	private void WatchUntilLaidOut(TourStop stop, int version, View copy)
	{
#if DEBUG
		EventHandler onLaidOut = (_, _) =>
		{
			if (copy.Width <= 0 || copy.Height <= 0)
			{
				return;
			}

			StopWatchingPlacement();
			Dispatcher.Dispatch(() => LogPlacement(stop, version, copy));
		};
		_watchedCopy = copy;
		_watchHandler = onLaidOut;
		copy.SizeChanged += onLaidOut;
#endif
	}

	// Called whenever the layer is emptied (a new stop, a redraw, the end of the tour): a copy that never got laid out keeps no handler, and so no reference to the tour.
	private void StopWatchingPlacement()
	{
#if DEBUG
		if (_watchedCopy is not null && _watchHandler is not null)
		{
			_watchedCopy.SizeChanged -= _watchHandler;
		}

		_watchedCopy = null;
		_watchHandler = null;
#endif
	}

	// Debug only: one line when the tour ends, counting the stops that were drawn off their target or could not be read. The flow expects misaligned=0, so a misplaced draw that a later redraw corrected still fails it.
	private void LogPlacementSummary() => TourLog($"placement misaligned={_placementMisses}");

	private static string Format(TourRect rect) => FormattableString.Invariant($"{rect.X:0.#},{rect.Y:0.#},{rect.Width:0.#},{rect.Height:0.#}");

	private VisualElement? BoundsTargetOf(TourStop stop) => stop switch
	{
		TourStop.Books => _tabView(CornerTab.Books),
		TourStop.Store => _tabView(CornerTab.Store),
		TourStop.Settings => _tabView(CornerTab.Settings),
		TourStop.ShelfPointer => _childShelfPill(),
		TourStop.ChildShelf => _grownUpsPill(),
		_ => null,
	};

	private static bool NeedsBounds(TourStop stop) => stop != TourStop.Welcome;

	private Rect? BoundsOf(TourStop stop, bool allowLayoutFallback = true) => BoundsTargetOf(stop) is { } target ? BoundsIn(target, allowLayoutFallback) : null;

	private static Rect ToRect(TourRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

	private static TourRect ToTourRect(Rect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

	// Android and iOS can read a view's place on the screen; Windows cannot (and has no bar under the page), so it keeps the layout sum without a report.
#if ANDROID || IOS
	private static readonly bool ScreenPositionReadable = true;
#else
	private static readonly bool ScreenPositionReadable = false;
#endif

	// Where a view is, in the layer's own coordinates (Slice 381): both the view and the layer are read where they are on the screen, and the copy goes at the difference. The sum of layout offsets misses the inset
	// the gallery pads itself by under a status bar (it draws under it), which put every copy a status-bar height too low. Without a platform view (Windows, which has no bar under the page) the layout sum stands.
	// A view or the layer that is not attached to a window yet reads as unreadable (null) so Draw tries again on the next layout; on the last try the layout sum is used and the fallback is reported.
	private Rect? BoundsIn(VisualElement view, bool allowLayoutFallback)
	{
		if (view.Width <= 0 || view.Height <= 0 || view.Handler is null)
		{
			return null;
		}

		if (ScreenBounds(view) is { } onScreen && ScreenBounds(this) is { } layer)
		{
			return ToRect(TourGeometry.InLayer(onScreen, layer.X, layer.Y));
		}

		if (!ScreenPositionReadable)
		{
			return LayoutBoundsIn(view);
		}

		if (!allowLayoutFallback)
		{
			return null;
		}

		ReportLayoutFallback();
		return LayoutBoundsIn(view);
	}

	// Release gets the old status-bar offset again when only one of the two screen positions can be read: that is a fallback, reported once per drawn stop.
	private void ReportLayoutFallback()
	{
		if (_fallbackReportedVersion == _showVersion)
		{
			return;
		}

		_fallbackReportedVersion = _showVersion;
		DinoFallbacks.Report(FallbackArea.Ui, FallbackReason.LayoutFailed); // Slice 381: a tour copy is placed from layout offsets, so it can sit a status bar too low
	}

	// A view's rectangle on the screen in dp; null when its platform view is not there yet or the platform has no screen position to read.
	private static TourRect? ScreenBounds(VisualElement view)
	{
		if (view.Width <= 0 || view.Height <= 0 || ScreenOrigin(view) is not { } origin)
		{
			return null;
		}

		return new TourRect(origin.X, origin.Y, view.Width, view.Height);
	}

	private static Point? ScreenOrigin(VisualElement view)
	{
#if ANDROID
		return AndroidScreenOrigin(view);
#elif IOS
		return IosScreenOrigin(view);
#else
		return null;
#endif
	}

#if ANDROID
	private static Point? AndroidScreenOrigin(VisualElement view)
	{
		if (view.Handler?.PlatformView is not Android.Views.View native || !native.IsAttachedToWindow)
		{
			return null; // not in a window yet: GetLocationOnScreen would answer 0,0
		}

		int[] xy = new int[2];
		native.GetLocationOnScreen(xy);
		float density = native.Resources?.DisplayMetrics?.Density ?? 1f;
		return new Point(xy[0] / density, xy[1] / density);
	}
#elif IOS
	private static Point? IosScreenOrigin(VisualElement view)
	{
		if (view.Handler?.PlatformView is not UIKit.UIView native || native.Window is null)
		{
			return null; // not in a window yet: the conversion would land in the view's own root space
		}

		CoreGraphics.CGPoint origin = native.ConvertPointToView(CoreGraphics.CGPoint.Empty, null); // null: the window's own space, points
		return new Point(origin.X, origin.Y);
	}
#endif

	// The sum of a view's layout offsets up to the gallery: the old way, kept where the screen position cannot be read.
	private Rect? LayoutBoundsIn(VisualElement view)
	{
		double x = view.X;
		double y = view.Y;
		Element? parent = view.Parent;
		while (parent is VisualElement element && !ReferenceEquals(parent, Parent))
		{
			x += element.X;
			y += element.Y;
			parent = element.Parent;
		}

		return ReferenceEquals(parent, Parent) ? new Rect(x, y, view.Width, view.Height) : null;
	}

	private bool Rail => Width >= OnboardingLayout.SplitFromWidth;

	// ---- N8a, welcome ----

	private void DrawWelcome()
	{
		var disc = new Border
		{
			WidthRequest = 56, HeightRequest = 56, StrokeThickness = 0, StrokeShape = new Ellipse(), BackgroundColor = DinoTokens.Get(DinoTokens.Ink),
			Content = new IconView { Kind = IconKind.Lock, Color = Colors.White, Size = 26, StrokeWidth = 2.2, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		var head = new Grid { ColumnSpacing = 14, ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] };
		head.Add(disc, 0);
		head.Add(new VerticalStackLayout
		{
			Spacing = 2, VerticalOptions = LayoutOptions.Center,
			Children = { Heading(AppResources.Tour_WelcomeTitle, 22), Line(AppResources.Tour_WelcomeLine, 14, TipLine) },
		}, 1);
		var rows = new VerticalStackLayout { Spacing = 12 };
		rows.Children.Add(WelcomeRow(IconKind.TabStore, AppResources.Tour_StoreTitle, AppResources.Tour_StoreLine));
		rows.Children.Add(WelcomeRow(IconKind.TabBooks, AppResources.Tour_BooksTitle, AppResources.Tour_BooksLine));
		rows.Children.Add(WelcomeRow(IconKind.TabSettings, AppResources.Tour_SettingsTitle, AppResources.Tour_SettingsLine));
		TourButton go = TourButton.Coral(AppResources.Tour_ShowMeAround, "TourShowAround", Next);
		var content = new VerticalStackLayout { Spacing = 16, Children = { head, rows, go } };
		Border card = Card(content, new Thickness(20, 22, 20, 20));
		card.VerticalOptions = LayoutOptions.Start;
		card.HorizontalOptions = LayoutOptions.Center;
		card.MaximumWidthRequest = 460;
		card.Margin = new Thickness(20, Math.Min(WelcomeTop, Math.Max(24, Height * 0.12)), 20, 0);
		card.AutomationId = "TourWelcome";
		Children.Add(card);
	}

	private static View WelcomeRow(IconKind icon, string title, string line)
	{
		var tile = new Border
		{
			WidthRequest = 40, HeightRequest = 40, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 }, BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint), VerticalOptions = LayoutOptions.Start,
			Content = new IconView { Kind = icon, Color = DinoTokens.Get(DinoTokens.Brand), Size = 22, StrokeWidth = 2, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		var row = new Grid { ColumnSpacing = 12, ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] };
		row.Add(tile, 0);
		row.Add(new VerticalStackLayout { Spacing = 1, Children = { Heading(title, 15.5), Line(line, 13.5, TipLine) } }, 1);
		return row;
	}

	// ---- N8b, a tab ----

	private void DrawTab(TourStop stop, Rect tab)
	{
		View lift = TabReplica(stop, tab);
		Children.Add(lift);
		PulseRing ring = RingAround(tab, TileCorner);
		Children.Add(ring);
		ring.Start();
		string text = stop switch
		{
			TourStop.Books => AppResources.Tour_TipBooks,
			TourStop.Store => AppResources.Tour_TipStore,
			_ => AppResources.Tour_TipSettings,
		};
		string? title = stop == TourStop.Store ? AppResources.Tour_TipStoreTitle : null;
		string counter = AppResources.Format(AppResources.Intro_Counter, CornerTourFlow.CounterOf(stop), CornerTourFlow.CounterTotal);
		PlaceTip(BuildTip("TourTip", title, text, FootWithCounter(counter, AppResources.Wizard_Next, "TourNext")), tab);
	}

	// The lifted tab: a white tile at the tab's own place with its icon and name in the brand colour.
	private static View TabReplica(TourStop stop, Rect tab)
	{
		(IconKind icon, string name) = stop switch
		{
			TourStop.Books => (IconKind.TabBooks, AppResources.Corner_TabBooks),
			TourStop.Store => (IconKind.TabStore, AppResources.Store_Title),
			_ => (IconKind.TabSettings, AppResources.Settings_Title),
		};
		var column = new VerticalStackLayout
		{
			Spacing = 2, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new IconView { Kind = icon, Color = DinoTokens.Get(DinoTokens.Brand), Size = 24, StrokeWidth = 2, HorizontalOptions = LayoutOptions.Center },
				new Label { Text = name, FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Brand), HorizontalTextAlignment = TextAlignment.Center, FontFamily = DinoFonts.Ui },
			},
		};
		return Lifted(new Border { StrokeThickness = 0, BackgroundColor = Color.FromArgb(LiftFill), StrokeShape = new RoundRectangle { CornerRadius = TileCorner }, Content = column }, tab);
	}

	// ---- N9, the Child's shelf pointer ----

	private void DrawShelfPointer(Rect pill)
	{
		var content = new HorizontalStackLayout
		{
			Spacing = 6, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new IconView { Kind = IconKind.Play, Color = Colors.White, Filled = true, Size = 14, VerticalOptions = LayoutOptions.Center },
				new Label { Text = AppResources.Corner_ChildsShelf, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.NoWrap, FontFamily = DinoFonts.Ui },
			},
		};
		Border replica = new()
		{
			StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.Coral), StrokeShape = new RoundRectangle { CornerRadius = pill.Height / 2 }, Content = content,
			Shadow = new Shadow { Brush = new SolidColorBrush(DinoTokens.Get(DinoTokens.CoralEdge)), Offset = new Point(0, 3), Radius = 0, Opacity = 1 },
		};
		Taps.AddTap(replica, () =>
		{
			Next();
			return Task.CompletedTask;
		}, UiControls.OpenCorner); // the pill itself opens the child's shelf, like the button under the tip
		Children.Add(Lifted(replica, pill));
		PulseRing ring = RingAround(pill, pill.Height / 2);
		Children.Add(ring);
		ring.Start();
		View foot = FootWithCounter(AppResources.Tour_LastStep, AppResources.Tour_ShowMe, "TourShowMe");
		PlaceTip(BuildTip("TourShelfTip", AppResources.Tour_ShelfTitle, AppResources.Tour_ShelfLine, foot, markup: true), pill);
	}

	// ---- N10, the child's shelf ----

	private void DrawChildShelf(Rect pill)
	{
		var content = new HorizontalStackLayout
		{
			Spacing = 6, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new IconView { Kind = IconKind.Lock, Color = DinoTokens.Get(DinoTokens.Ink), StrokeWidth = 2.2, Size = 18, VerticalOptions = LayoutOptions.Center },
				new Label { Text = AppResources.Gallery_GrownUpsPill, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.NoWrap, FontFamily = DinoFonts.Ui },
			},
		};
		Children.Add(Lifted(new Border { StrokeThickness = 0, BackgroundColor = Color.FromArgb(LiftFill), StrokeShape = new RoundRectangle { CornerRadius = pill.Height / 2 }, Content = content }, pill));
		PulseRing ring = RingAround(pill, pill.Height / 2);
		Children.Add(ring);
		ring.Start();
		var hint = new Label { FontSize = 13.5, LineHeight = 1.4, TextColor = Color.FromArgb(TipInk), FontFamily = DinoFonts.Ui, VerticalOptions = LayoutOptions.Center };
		OnboardingText.ShowMarkup(hint, AppResources.Tour_ChildShelfBack);
		var hold = new Grid { ColumnSpacing = 10, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		hold.Add(hint, 0);
		hold.Add(new IconView { Kind = IconKind.Lock, Color = DinoTokens.Get(DinoTokens.Ink), Size = 20, StrokeWidth = 2, VerticalOptions = LayoutOptions.Center }, 1);
		var hintBox = new Border { StrokeThickness = 0, BackgroundColor = Colors.White, StrokeShape = new RoundRectangle { CornerRadius = 16 }, Padding = new Thickness(12), Content = hold };
		TourButton start = TourButton.Coral(AppResources.Tour_StartReading, "TourStartReading", Next, UiControls.Done);
		var content2 = new VerticalStackLayout
		{
			Spacing = 12,
			Children = { Heading(AppResources.Tour_ChildShelfTitle, 22), Line(AppResources.Tour_ChildShelfLine, 14.5, TipInk), hintBox, start },
		};
		Border card = Card(content2, new Thickness(20));
		card.VerticalOptions = LayoutOptions.End;
		card.HorizontalOptions = LayoutOptions.Center;
		card.MaximumWidthRequest = 460;
		card.Margin = new Thickness(16, 0, 16, ChildCardBottom);
		card.AutomationId = "TourChildShelf";
		Children.Add(card);
	}

	// ---- shared pieces ----

	private static Border Card(View content, Thickness padding) => new()
	{
		StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.Cream), StrokeShape = new RoundRectangle { CornerRadius = CardCorner }, Padding = padding, Content = content,
		Shadow = new Shadow { Brush = new SolidColorBrush(Color.FromArgb("#140E28")), Offset = new Point(0, 24), Radius = 60, Opacity = 0.45f },
	};

	private static Label Heading(string text, double size)
	{
		var label = new Label { Text = text, FontSize = size, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), LineHeight = 1.2, FontFamily = DinoFonts.Ui };
		SemanticProperties.SetHeadingLevel(label, SemanticHeadingLevel.Level2);
		return label;
	}

	private static Label Line(string text, double size, string colour) =>
		new() { Text = text, FontSize = size, LineHeight = 1.4, TextColor = Color.FromArgb(colour), FontFamily = DinoFonts.Ui };

	// The copy that stands over the scrim at the real control's place, a few dp larger so it reads as lifted.
	private static View Lifted(View copy, Rect at)
	{
		copy.HorizontalOptions = LayoutOptions.Start;
		copy.VerticalOptions = LayoutOptions.Start;
		TourRect lifted = TourGeometry.LiftedCopy(ToTourRect(at));
		copy.WidthRequest = lifted.Width;
		copy.HeightRequest = lifted.Height;
		copy.Margin = new Thickness(lifted.X, lifted.Y, 0, 0);
		return copy;
	}

	private static PulseRing RingAround(Rect at, double corner)
	{
		TourRect lifted = TourGeometry.LiftedCopy(ToTourRect(at));
		var ring = new PulseRing(corner + LiftGrow)
		{
			HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, WidthRequest = lifted.Width, HeightRequest = lifted.Height,
			Margin = new Thickness(lifted.X, lifted.Y, 0, 0), AutomationId = "TourRing",
		};
		return ring;
	}

	private View FootWithCounter(string counter, string button, string id)
	{
		var foot = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		foot.Add(new Label { Text = counter, FontSize = 12.5, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(TipLine), VerticalOptions = LayoutOptions.Center, FontFamily = DinoFonts.Ui }, 0);
		foot.Add(TourButton.Brand(button, id, Next), 1);
		return foot;
	}

	// The tip: width 300, 16 16 14 padding, a title (when it has one), the text and the foot, in a cream card with a pointer square on the edge towards the lifted control.
	private static Grid BuildTip(string id, string? title, string text, View foot, bool markup = false)
	{
		var body = new VerticalStackLayout { Spacing = 8 };
		if (title is not null)
		{
			body.Children.Add(Heading(title, 18));
		}

		var line = new Label { FontSize = 14, LineHeight = 1.45, TextColor = Color.FromArgb(TipInk), FontFamily = DinoFonts.Ui };
		if (markup)
		{
			OnboardingText.ShowMarkup(line, text);
		}
		else
		{
			line.Text = text;
		}

		body.Children.Add(line);
		body.Children.Add(foot);
		Border card = new()
		{
			StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.Cream), StrokeShape = new RoundRectangle { CornerRadius = 20 }, Padding = new Thickness(16, 16, 16, 14), Content = body,
			Shadow = new Shadow { Brush = new SolidColorBrush(Color.FromArgb("#140E28")), Offset = new Point(0, 16), Radius = 40, Opacity = 0.4f },
		};
		return new Grid { AutomationId = id, WidthRequest = TipWidth, Children = { card } };
	}

	// Under a bar the tip stands above the lifted tab with its pointer on the bottom edge; beside a rail (and for the pill in the header: below it) it stands beside or under the control, the pointer on the near edge.
	private void PlaceTip(Grid tip, Rect target)
	{
		var pointer = new Border
		{
			WidthRequest = PointerSize, HeightRequest = PointerSize, StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.Cream), Rotation = 45, StrokeShape = new RoundRectangle { CornerRadius = 3 },
			HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, InputTransparent = true,
		};
		tip.HorizontalOptions = LayoutOptions.Start;
		bool above = IsBottomBar(target);
		bool below = !above && !Rail;
		double tipLeft = Math.Clamp(target.Center.X - (TipWidth / 2), EdgeMargin, Math.Max(EdgeMargin, Width - TipWidth - EdgeMargin));
		if (above)
		{
			tip.VerticalOptions = LayoutOptions.End;
			tip.Margin = new Thickness(tipLeft, 0, 0, Height - target.Y + LiftGrow + TipGap);
			pointer.Margin = new Thickness(Math.Clamp(target.Center.X - tipLeft - (PointerSize / 2), 20, TipWidth - 40), 0, 0, 0);
			pointer.VerticalOptions = LayoutOptions.End;
			pointer.TranslationY = PointerSize / 2;
		}
		else if (below)
		{
			tipLeft = Math.Clamp(target.Right - TipWidth, EdgeMargin, Math.Max(EdgeMargin, Width - TipWidth - EdgeMargin)); // N9: the tip hangs from the pill, its right edge in line
			tip.VerticalOptions = LayoutOptions.Start;
			tip.Margin = new Thickness(tipLeft, target.Bottom + LiftGrow + TipGap, 0, 0);
			pointer.Margin = new Thickness(Math.Clamp(target.Center.X - tipLeft - (PointerSize / 2), 20, TipWidth - 40), 0, 0, 0);
			pointer.TranslationY = -PointerSize / 2;
		}
		else
		{
			tip.VerticalOptions = LayoutOptions.Start;
			tip.Margin = new Thickness(target.Right + LiftGrow + TipGap, Math.Max(EdgeMargin, target.Center.Y - 60), 0, 0);
			pointer.Margin = new Thickness(0, 44, 0, 0);
			pointer.TranslationX = -PointerSize / 2;
		}

		tip.Children.Add(pointer);
		tip.ZIndex = 2;
		Children.Add(tip);
	}

	// A tab is in the bottom bar when it sits in the lower third of a stacked window.
	private bool IsBottomBar(Rect target) => !Rail && target.Y > Height * 0.6;

	private static void TourLog(string line)
	{
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Onboarding] tour {line}");
#endif
		Debug.WriteLine($"Dino Reader onboarding tour {line}");
	}
}

/// <summary>A button of the corner tour (Slice 362): a coral or brand pill with a transparent focusable button over it for the screen reader and the keyboard, counted as a press.</summary>
internal sealed class TourButton : Grid
{
	private TourButton(string text, string id, Color fill, Color edge, double height, Action tapped, string control)
	{
		var face = new Border
		{
			HeightRequest = height, StrokeThickness = 0, BackgroundColor = fill, StrokeShape = new RoundRectangle { CornerRadius = height / 2 }, Padding = new Thickness(height > 44 ? 24 : 18, 0),
			Content = new Label { Text = text, FontSize = height > 44 ? 17 : 14.5, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, FontFamily = DinoFonts.Ui },
		};
		if (edge != Colors.Transparent)
		{
			face.Shadow = new Shadow { Brush = new SolidColorBrush(edge), Offset = new Point(0, 5), Radius = 0, Opacity = 1 };
		}

		var hit = new Button { AutomationId = id, Text = text, BackgroundColor = Colors.Transparent, TextColor = Colors.Transparent, BorderWidth = 0, Padding = 0 };
		UiUsage.Press(hit, control, tapped);
		Children.Add(face);
		Children.Add(hit);
		HorizontalOptions = height > 44 ? LayoutOptions.Fill : LayoutOptions.End;
		Margin = new Thickness(0, 0, 0, height > 44 ? 5 : 0);
	}

	/// <summary>The coral call to action, 56 high with its darker edge (welcome card, last card).</summary>
	public static TourButton Coral(string text, string id, Action tapped, string control = UiControls.Next) =>
		new(text, id, DinoTokens.Get(DinoTokens.Coral), DinoTokens.Get(DinoTokens.CoralEdge), 56, tapped, control);

	/// <summary>The brand "Next" of a tip, 40 high.</summary>
	public static TourButton Brand(string text, string id, Action tapped) =>
		new(text, id, DinoTokens.Get(DinoTokens.Brand), Colors.Transparent, 40, tapped, UiControls.Next);
}
