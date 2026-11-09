using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The three tabs of the grown-ups' corner (Slice 325 part A, grown-up mock frames P1-P4, L1, T1, W1): a white bar along the bottom under 600 dp wide, or three rows in the ink rail from 600.
/// One instance serves both; <see cref="Apply"/> restyles it and the page moves it between the page and the rail. The tab names are the strings Books, Store and Settings; the AutomationIds keep the old
/// ones (Store is <c>GalleryStore</c>, Settings <c>GallerySettings</c>) so no flow changes its taps. Layout never mirrors (Slice 96).
/// </summary>
internal sealed class CornerTabs : Grid
{
	private static readonly CornerTab[] Tabs = [CornerTab.Books, CornerTab.Store, CornerTab.Settings];
	private static readonly string[] Ids = ["CornerTabBooks", "GalleryStore", "GallerySettings"];
	private static readonly IconKind[] Icons = [IconKind.TabBooks, IconKind.TabStore, IconKind.TabSettings];
	private static readonly string[] Hints = ["Ctrl+1", "Ctrl+2", "Ctrl+3"];
	private static readonly Color Amber = Color.FromArgb("#FFC24D");
	private static readonly Color RailText = Color.FromArgb("#cfc6e6");
	private static readonly Color RailHint = Color.FromArgb("#8e86a8");
	private static readonly Color RailSelected = Color.FromRgba(1, 1, 1, 0.14);
	private static readonly Color RailHover = Color.FromRgba(1, 1, 1, 0.07);
	private static readonly Color BarLine = Color.FromArgb("#e8e1d3");
	private const double BarLabelGap = 2;

	private readonly Border[] _roots = new Border[3];
	private readonly Button[] _hits = new Button[3];
	private CornerTab _selected = CornerTab.Books;
	private bool _rail;
	private CornerSizes _sizes = CornerLayout.SizesOf(CornerLayout.ChromeOf(393, 852, false));
	private int _hovered = -1;
	private bool _wantRail; // what the page asked for; it is built by RebuildIfNeeded, only while this Grid has its native side
	private CornerSizes _wantSizes;
	private bool _textsDirty;

	public CornerTabs()
	{
		_wantRail = _rail;
		_wantSizes = _sizes;
		FlowDirection = FlowDirection.LeftToRight; // never mirrored (Slice 96)
		for (int i = 0; i < Tabs.Length; i++)
		{
			int index = i;
			_hits[i] = new Button { BackgroundColor = Colors.Transparent, TextColor = Colors.Transparent, BorderWidth = 0, Padding = 0, AutomationId = Ids[i] };
			_hits[i].Clicked += (_, _) => TabRequested?.Invoke(Tabs[index]);
			_roots[i] = new Border { StrokeThickness = 0, BackgroundColor = Colors.Transparent };
		}

		Rebuild();
	}

	/// <summary>A tab was tapped (or clicked, or Enter on it).</summary>
	public event Action<CornerTab>? TabRequested;

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
		Grid items = this;
		if (_rail)
		{
			RowSpacing = _sizes.RailGap;
			RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];
		}
		else
		{
			RowSpacing = 0;
			RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];
			this.Add(new BoxView { Color = BarLine, HeightRequest = 1 }, 0, 0);
			items = new Grid
			{
				ColumnSpacing = 0,
				Padding = new Thickness(_sizes.TabBar.Left, 0, _sizes.TabBar.Right, 0),
				ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)],
			};
			this.Add(items, 0, 1);
		}

		for (int i = 0; i < Tabs.Length; i++)
		{
			if (_roots[i].Parent is Layout previous)
			{
				previous.Remove(_roots[i]);
			}

			BuildItem(i);
			if (_rail)
			{
				items.Add(_roots[i], 0, i);
			}
			else
			{
				items.Add(_roots[i], i, 0);
			}
		}

		Restyle();
	}

	private void BuildItem(int i)
	{
		Border root = _roots[i];
		if (_hits[i].Parent is Layout oldParent)
		{
			oldParent.Remove(_hits[i]); // the previous build's content is dropped; its hit button is reused
		}

		string name = NameOf(Tabs[i]);
		_hits[i].Text = name;
		SemanticProperties.SetDescription(_hits[i], name);
		root.GestureRecognizers.Clear();
		root.Content = _rail ? RailContent(i, name) : BarContent(i, name);
		root.HeightRequest = _rail ? _sizes.TabHeight : _sizes.TabRow;
		root.StrokeShape = new RoundRectangle { CornerRadius = _rail ? _sizes.TabRadius : 0 };
		root.Padding = _rail ? new Thickness(_sizes.TabPadding, 0) : new Thickness(0, _sizes.TabBar.Top, 0, _sizes.TabBar.Bottom);
		if (_rail && DesktopTips.IsWindows)
		{
			WireHover(root, i);
		}
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
		var label = new Label { Text = name, FontSize = _sizes.TabLabel, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
		var column = new VerticalStackLayout { Spacing = BarLabelGap, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Children = { pill, label } };
		return new Grid { Children = { column, _hits[i] } };
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

	// Frame W1: the pointer lightens a tab that is not selected (white at 7%) and turns its words white.
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

		for (int i = 0; i < Tabs.Length; i++)
		{
			bool selected = Tabs[i] == _selected;
			string name = NameOf(Tabs[i]);
			SemanticProperties.SetDescription(_hits[i], selected ? AppResources.Format(AppResources.Store_ChipSelected, name) : name); // the current tab is told to a screen reader, not only shown by colour
			if (_rail)
			{
				RestyleRail(i, selected, hovered: i == _hovered && !selected);
			}
			else
			{
				RestyleBar(i, selected);
			}
		}
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

	private void RestyleRail(int i, bool selected, bool hovered)
	{
		_roots[i].BackgroundColor = selected ? RailSelected : hovered ? RailHover : Colors.Transparent;
		var row = (Grid)_roots[i].Content!;
		((IconView)row.Children[0]).Color = selected ? Amber : hovered ? Colors.White : RailText;
		((Label)row.Children[1]).TextColor = selected ? Colors.White : hovered ? Colors.White : RailText;
	}
}
