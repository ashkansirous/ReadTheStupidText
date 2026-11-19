using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The one shelf's menu (Slice 418, mock L1 to L6, WL1, WL2): Books, Store, Settings and the lock, the lock last. A white bar along the bottom under 600 dp wide (the lock at the right), or rows in the cream rail from 600 with the lock as a
/// plain row at its foot. While locked Store and Settings are faded to 38 % but still answer a tap (the page shows its hint and pulses the lock); the lock is a closed ink lock labelled "Locked", or a bigger open brand-purple lock
/// labelled "Unlocked". One instance serves both shapes; <see cref="Apply"/> restyles it and the page moves it between the page and the rail. The AutomationIds keep the old ones (Store is <c>GalleryStore</c>, Settings
/// <c>GallerySettings</c>), the lock is <c>GalleryLock</c>. Layout never mirrors (Slice 96).
/// </summary>
internal sealed class CornerTabs : Grid
{
	private const int TabCount = 3;
	private const int LockIndex = 3;
	private static readonly CornerTab[] Tabs = [CornerTab.Books, CornerTab.Store, CornerTab.Settings];
	private static readonly string[] TabControls = [UiControls.TabBooks, UiControls.TabStore, UiControls.TabSettings];
	private static readonly string[] Ids = ["CornerTabBooks", "GalleryStore", "GallerySettings", "GalleryLock"];
	private static readonly IconKind[] Icons = [IconKind.TabBooks, IconKind.TabStore, IconKind.TabSettings];
	private static readonly string[] Hints = ["Ctrl+1", "Ctrl+2", "Ctrl+3"];
	private static readonly Color SelectedInk = Color.FromArgb("#3f3bc4");
	private static readonly Color RailInk = Color.FromArgb("#3a3450");
	private static readonly Color RailHint = Color.FromArgb("#6f6884");
	private static readonly Color RailHover = Color.FromRgba(43 / 255.0, 37 / 255.0, 64 / 255.0, 0.06);
	private static readonly Color RailLine = Color.FromArgb("#f0d9b5");
	private static readonly Color BarLine = Color.FromArgb("#e8e1d3");
	private const double BarLabelGap = 2;
	private const double OpenBarLabelGap = 1;
	private const double FadedOpacity = 0.38; // Store and Settings while locked (decision 9)
	private const double LockedBarGlyph = 24;
	private const double OpenBarGlyph = 30;
	private const double RailGlyph = 26;
	private const double RailLockHeight = 52;
	private const double RailTipSize = 13;
	private const double RailLockSize = 16;

	private readonly Border[] _roots = new Border[LockIndex + 1];
	private readonly Button[] _hits = new Button[LockIndex + 1];
	private CornerTab _selected = CornerTab.Books;
	private bool _rail;
	private CornerSizes _sizes = CornerLayout.SizesOf(CornerLayout.ChromeOf(393, 852, false));
	private int _hovered = -1;
	private bool _wantRail; // what the page asked for; it is built by RebuildIfNeeded, only while this Grid has its native side
	private CornerSizes _wantSizes;
	private bool _textsDirty;
	private bool _locked = true;
	private int _seconds;
	private LockGlyph? _lockGlyph;
	private Label? _lockLabel;
	private Label? _tip;
	private Label? _lockHint;
	private Border? _lockBox;

	public CornerTabs()
	{
		_wantRail = _rail;
		_wantSizes = _sizes;
		FlowDirection = FlowDirection.LeftToRight; // never mirrored (Slice 96)
		for (int i = 0; i < TabCount; i++)
		{
			int index = i;
			_hits[i] = UiUsage.Press(NewHit(Ids[i]), TabControls[i], () => TabRequested?.Invoke(Tabs[index])); // Slice 352: counted before the tab changes
		}

		_hits[LockIndex] = UiUsage.Press(NewHit(Ids[LockIndex]), UiControls.OpenCorner, () => LockRequested?.Invoke()); // the one control name the Worker's closed list has for it: the lock opens or closes the grown-ups' places
		for (int i = 0; i <= LockIndex; i++)
		{
			_roots[i] = new Border { StrokeThickness = 0, BackgroundColor = Colors.Transparent };
		}

		Rebuild();
	}

	private static Button NewHit(string automationId) =>
		new() { BackgroundColor = Colors.Transparent, TextColor = Colors.Transparent, BorderWidth = 0, Padding = 0, AutomationId = automationId };

	/// <summary>A tab was tapped (or clicked, or Enter on it); the page decides what a faded one does.</summary>
	public event Action<CornerTab>? TabRequested;

	/// <summary>The lock was tapped.</summary>
	public event Action? LockRequested;

	/// <summary>The lock item's view, which the pulse ring and the hint point at.</summary>
	public View LockView => _roots[LockIndex];

	/// <summary>The amber ring that pulses around the lock's glyph when a faded tab is tapped (decision 9). It lives in the glyph's box, in both shapes.</summary>
	public LockPulse Pulse { get; } = new();

	/// <summary>Pulses the ring around the lock for 2 seconds (nothing with reduce motion).</summary>
	public void PulseLock() => Pulse.Run();

	// The glyph and its pulse ring share the box: the ring is taken from the previous build's grid first (a view has one parent).
	private Grid GlyphWithRing(LockGlyph glyph)
	{
		if (Pulse.Ring.Parent is Layout old)
		{
			old.Remove(Pulse.Ring);
		}

		return new Grid { Children = { glyph, Pulse.Ring } };
	}

	public CornerTab Selected
	{
		get => _selected;
		set
		{
			if (_selected != value)
			{
				_selected = value;
				Restyle();
			}
		}
	}

	/// <summary>True while the shelf is locked: Store and Settings fade and the lock is the closed one.</summary>
	public bool Locked
	{
		get => _locked;
		set
		{
			if (_locked != value)
			{
				_locked = value;
				Restyle();
			}
		}
	}

	/// <summary>The seconds on the rail's "Locks itself in {0}s" line.</summary>
	public void SetSeconds(int seconds)
	{
		_seconds = seconds;
		if (_tip is not null)
		{
			_tip.Text = AppResources.Format(AppResources.Lock_TapToLock, seconds);
		}
	}

	/// <summary>Asks for the tabs as the bottom bar (<paramref name="rail"/> false) or as the rail's rows, with <paramref name="sizes"/>. Only records the wish: the Grid is changed by <see cref="RebuildIfNeeded"/>.</summary>
	public void Apply(bool rail, CornerSizes sizes)
	{
		_wantRail = rail;
		_wantSizes = sizes;
	}

	/// <summary>The app language changed: the labels and the screen-reader names follow it at the next <see cref="RebuildIfNeeded"/>.</summary>
	public void ApplyTexts() => _textsDirty = true;

	/// <summary>The shape or the words asked for are not built yet.</summary>
	public bool NeedsRebuild => _textsDirty || _wantRail != _rail || _wantSizes != _sizes;

	/// <summary>Builds what was asked for. The page calls it only while this Grid's Handler is attached (a Grid with no native side is not mutated).</summary>
	public void RebuildIfNeeded()
	{
		if (!NeedsRebuild)
		{
			return;
		}

		_rail = _wantRail;
		_sizes = _wantSizes;
		_textsDirty = true; // stays set if Rebuild throws, so the half-built Grid is built again
		_building = true;
		try
		{
			Rebuild();
			_textsDirty = false; // the build is committed only after it finished
		}
		finally
		{
			_building = false;
		}
	}

	private bool _building; // Rebuild runs: its own final Restyle is the only one allowed while the Grid may be half-built

	private static string NameOf(CornerTab tab) => tab switch
	{
		CornerTab.Books => AppResources.Corner_TabBooks,
		CornerTab.Store => AppResources.Store_Title,
		_ => AppResources.Settings_Title,
	};

	private void Rebuild()
	{
		Children.Clear();
		RowDefinitions = [];
		ColumnDefinitions = [];
		BackgroundColor = _rail ? Colors.Transparent : Colors.White;
		SafeAreaEdges = _rail ? SafeAreaEdges.None : new SafeAreaEdges(SafeAreaRegions.Container);
		Padding = new Thickness(0);
		if (_rail)
		{
			RebuildRail();
		}
		else
		{
			RebuildBar();
		}

		Restyle();
	}

	private void RebuildBar()
	{
		_tip = null;
		_lockHint = null;
		RowSpacing = 0;
		RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];
		this.Add(new BoxView { Color = BarLine, HeightRequest = 1 }, 0, 0);
		var items = new Grid
		{
			ColumnSpacing = 0,
			Padding = new Thickness(_sizes.TabBar.Left, 0, _sizes.TabBar.Right, 0),
			ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)],
		};
		this.Add(items, 0, 1);
		for (int i = 0; i <= LockIndex; i++)
		{
			DetachRoot(i);
			BuildBarItem(i);
			items.Add(_roots[i], i, 0);
		}
	}

	private void RebuildRail()
	{
		RowSpacing = _sizes.RailGap;
		RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)];
		for (int i = 0; i < TabCount; i++)
		{
			DetachRoot(i);
			BuildRailItem(i);
			this.Add(_roots[i], 0, i);
		}

		DetachRoot(LockIndex);
		BuildRailLock();
		var foot = new VerticalStackLayout { Spacing = 4, Children = { new BoxView { Color = RailLine, HeightRequest = 1, Margin = new Thickness(0, 0, 0, 4) }, _roots[LockIndex] }, VerticalOptions = LayoutOptions.End };
		_tip = new Label { FontSize = RailTipSize, TextColor = DinoTokens.Get(DinoTokens.Body), Padding = new Thickness(14, 0), IsVisible = false, AutomationId = "LockTip" };
		foot.Children.Add(_tip);
		this.Add(foot, 0, 4);
	}

	private void DetachRoot(int i)
	{
		if (_roots[i].Parent is Layout previous)
		{
			previous.Remove(_roots[i]);
		}

		if (_hits[i].Parent is Layout oldParent)
		{
			oldParent.Remove(_hits[i]); // the previous build's content is dropped; its hit button is reused
		}
	}

	private void BuildBarItem(int i)
	{
		Border root = _roots[i];
		root.GestureRecognizers.Clear();
		string name = i == LockIndex ? AppResources.Lock_Locked : NameOf(Tabs[i]);
		_hits[i].Text = name;
		root.Content = i == LockIndex ? BuildBarLock() : BarContent(i, name);
		root.HeightRequest = -1; // a bar tab grows with its label (font scale, Arabic and Farsi); only a least height is set
		root.MinimumHeightRequest = CornerLayout.TabBarMinHeight(_sizes, BarLabelGap);
		root.StrokeShape = new RoundRectangle { CornerRadius = 0 };
		root.Padding = new Thickness(0, _sizes.TabBar.Top, 0, _sizes.TabBar.Bottom);
	}

	private View BarContent(int i, string name)
	{
		var icon = new IconView { Kind = Icons[i], Size = _sizes.TabIcon, StrokeWidth = 2.2, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		var pill = new Border
		{
			WidthRequest = _sizes.TabPillWidth,
			HeightRequest = _sizes.TabPillHeight,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = _sizes.TabPillHeight / 2 },
			HorizontalOptions = LayoutOptions.Center,
			Content = icon,
		};
		var label = new Label { Text = name, FontSize = _sizes.TabLabel, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, LineHeight = CornerLayout.TabLabelLineHeight, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
		var column = new VerticalStackLayout { Spacing = BarLabelGap, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Children = { pill, label } };
		return new Grid { Children = { column, _hits[i] } };
	}

	// Mock L1 and L4: the glyph sits in its own box (30 x 28 closed, 34 x 30 open) with no background, over its label; the page lays the amber pulse ring into the box.
	private View BuildBarLock()
	{
		_lockGlyph = new LockGlyph(open: false, DinoTokens.Get(DinoTokens.Ink), LockedBarGlyph);
		_lockBox = new Border { StrokeThickness = 0, BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.Center, StrokeShape = new RoundRectangle { CornerRadius = 15 }, Content = GlyphWithRing(_lockGlyph) };
		_lockLabel = new Label { FontSize = _sizes.TabLabel, HorizontalTextAlignment = TextAlignment.Center, LineHeight = CornerLayout.TabLabelLineHeight, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
		var column = new VerticalStackLayout { Spacing = BarLabelGap, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Children = { _lockBox, _lockLabel } };
		return new Grid { Children = { column, _hits[LockIndex] } };
	}

	private void BuildRailItem(int i)
	{
		Border root = _roots[i];
		root.GestureRecognizers.Clear();
		string name = NameOf(Tabs[i]);
		_hits[i].Text = name;
		root.Content = RailContent(i, name);
		root.HeightRequest = _sizes.TabHeight;
		root.MinimumHeightRequest = -1;
		root.StrokeShape = new RoundRectangle { CornerRadius = _sizes.TabRadius };
		root.Padding = new Thickness(_sizes.TabPadding, 0);
		if (DesktopTips.IsWindows)
		{
			WireHover(root, i);
		}
	}

	private View RailContent(int i, string name)
	{
		var icon = new IconView { Kind = Icons[i], Size = _sizes.TabIcon, StrokeWidth = 2.2, VerticalOptions = LayoutOptions.Center };
		var label = new Label { Text = name, FontSize = _sizes.TabLabelSize, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
		var row = new Grid { ColumnSpacing = _sizes.TabIconGap, ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		row.Add(icon, 0);
		row.Add(label, 1);
		if (_sizes.TabHints)
		{
			row.Add(new Label { Text = Hints[i], FontSize = 12, FontAttributes = FontAttributes.None, TextColor = RailHint, VerticalOptions = LayoutOptions.Center }, 2);
		}

		row.Add(_hits[i], 0);
		SetColumnSpan((BindableObject)_hits[i], 3);
		return row;
	}

	// Mock WL1 and WL2: a plain row at the rail's foot (icon 26, the word, "Ctrl+L" on Windows), 52 high, under a thin line.
	private void BuildRailLock()
	{
		Border root = _roots[LockIndex];
		root.GestureRecognizers.Clear();
		_hits[LockIndex].Text = AppResources.Lock_Locked;
		_lockGlyph = new LockGlyph(open: false, DinoTokens.Get(DinoTokens.Ink), RailGlyph);
		_lockLabel = new Label { FontSize = RailLockSize, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
		var row = new Grid { ColumnSpacing = _sizes.TabIconGap, ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		_lockBox = new Border { StrokeThickness = 0, BackgroundColor = Colors.Transparent, StrokeShape = new RoundRectangle { CornerRadius = 15 }, VerticalOptions = LayoutOptions.Center, Content = GlyphWithRing(_lockGlyph) };
		row.Add(_lockBox, 0);
		row.Add(_lockLabel, 1);
		_lockHint = _sizes.TabHints ? new Label { Text = DinoKeyHint, FontSize = 12, TextColor = RailHint, VerticalOptions = LayoutOptions.Center } : null;
		if (_lockHint is not null)
		{
			row.Add(_lockHint, 2);
		}

		row.Add(_hits[LockIndex], 0);
		SetColumnSpan((BindableObject)_hits[LockIndex], 3);
		root.Content = row;
		root.HeightRequest = Math.Max(_sizes.TabHeight, RailLockHeight);
		root.MinimumHeightRequest = -1;
		root.StrokeShape = new RoundRectangle { CornerRadius = _sizes.TabRadius };
		root.Padding = new Thickness(_sizes.TabPadding, 0);
		if (DesktopTips.IsWindows)
		{
			WireHover(root, LockIndex);
		}
	}

	private const string DinoKeyHint = "Ctrl+L"; // the key's own name, never translated (mock WL1, WL2)

	// Frame WL1: the pointer shades a row that is not selected.
	private void WireHover(Border root, int i)
	{
		var pointer = new PointerGestureRecognizer();
		pointer.PointerEntered += (_, _) => SetHover(i);
		pointer.PointerExited += (_, _) => SetHover(-1);
		root.GestureRecognizers.Add(pointer);
	}

	private void SetHover(int index)
	{
		_hovered = index;
		Restyle();
	}

	private void Restyle()
	{
		if (_textsDirty && !_building)
		{
			return; // a build is pending or failed half-way: the Grid may not match the mode, and the next build restyles it
		}

		for (int i = 0; i < TabCount; i++)
		{
			bool selected = Tabs[i] == _selected;
			string name = NameOf(Tabs[i]);
			SemanticProperties.SetDescription(_hits[i], selected ? AppResources.Format(AppResources.Store_ChipSelected, name) : name); // the current tab is told to a screen reader, not only shown by colour
			_roots[i].Opacity = _locked && Tabs[i] != CornerTab.Books ? FadedOpacity : 1;
			if (_rail)
			{
				RestyleRail(i, selected, hovered: i == _hovered && !selected);
			}
			else
			{
				RestyleBar(i, selected);
			}
		}

		RestyleLock();
	}

	private void RestyleBar(int i, bool selected)
	{
		Color ink = selected ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.Body);
		var column = (VerticalStackLayout)((Grid)_roots[i].Content!).Children[0];
		var pill = (Border)column.Children[0];
		pill.BackgroundColor = selected ? DinoTokens.Get(DinoTokens.BrandTint) : Colors.Transparent;
		((IconView)pill.Content!).Color = ink;
		((Label)column.Children[1]).TextColor = ink;
	}

	// Mock WL1: the selected row is the light purple one with purple words; a plain row is ink 3a3450.
	private void RestyleRail(int i, bool selected, bool hovered)
	{
		_roots[i].BackgroundColor = selected ? DinoTokens.Get(DinoTokens.BrandTint) : hovered ? RailHover : Colors.Transparent;
		var row = (Grid)_roots[i].Content!;
		Color ink = selected ? SelectedInk : RailInk;
		((IconView)row.Children[0]).Color = ink;
		((Label)row.Children[1]).TextColor = ink;
	}

	// The lock: closed and ink ("Locked") or open and brand purple ("Unlocked", bold, a bigger glyph, mock L4); screen readers hear "Locked. Tap to unlock." or "Unlocked. Tap to lock.".
	private void RestyleLock()
	{
		if (_lockGlyph is null || _lockLabel is null)
		{
			return;
		}

		bool open = !_locked;
		Color colour = open ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.Ink);
		RestyleLockGlyph(open, colour);
		RestyleLockText(open, colour);
		RestyleLockLayout(open);
	}

	private void RestyleLockGlyph(bool open, Color colour)
	{
		double size = _rail ? RailGlyph : open ? OpenBarGlyph : LockedBarGlyph;
		_lockGlyph!.Set(open, colour, size);
		if (_lockBox is not null && !_rail)
		{
			_lockBox.WidthRequest = open ? 34 : 30;
			_lockBox.HeightRequest = open ? 30 : 28;
		}
	}

	private void RestyleLockText(bool open, Color colour)
	{
		_lockLabel!.Text = open ? AppResources.Lock_Unlocked : AppResources.Lock_Locked;
		_lockLabel.FontAttributes = open ? FontAttributes.Bold : FontAttributes.None;
		_lockLabel.TextColor = colour;
		_hits[LockIndex].Text = _lockLabel.Text;
		SemanticProperties.SetDescription(_hits[LockIndex], open ? AppResources.Lock_UnlockedA11y : AppResources.Lock_LockedA11y);
	}

	private void RestyleLockLayout(bool open)
	{
		if (_rail)
		{
			_roots[LockIndex].BackgroundColor = _hovered == LockIndex ? RailHover : Colors.Transparent;
			if (_tip is not null)
			{
				_tip.IsVisible = open;
				_tip.Text = AppResources.Format(AppResources.Lock_TapToLock, _seconds);
			}
		}
		else if (_lockLabel!.Parent is VerticalStackLayout column)
		{
			column.Spacing = open ? OpenBarLabelGap : BarLabelGap;
		}
	}
}
