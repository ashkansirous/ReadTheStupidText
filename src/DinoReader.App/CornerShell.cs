using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The pill both places share (Slice 325, grown-up mock frames P1, L1, T1, K1, KL1): the coral "Child's shelf" in the corner, the quiet "Grown-ups" on the shelf. Same height, same spot, so going in and coming out is
/// the same tap. A transparent button over it gives screen readers the role, the keyboard focus and Enter/Space (the pattern of <see cref="GrownUpHeader"/>). The label can be dropped, never the icon.
/// </summary>
internal sealed class PillButton : Border
{
	private static readonly Color CoralHover = Color.FromArgb("#FF8A6B");
	private static readonly Color ShelfText = Color.FromArgb("#3a3450");
	private static readonly Color ShelfFill = Color.FromRgba(43 / 255.0, 37 / 255.0, 64 / 255.0, 0.07);
	private static readonly Color ShelfHover = Color.FromRgba(43 / 255.0, 37 / 255.0, 64 / 255.0, 0.13);
	private const double CoralShadowOffset = 3;
	private const double TallCoralShadowOffset = 4;
	private const double TallPill = 52;

	private readonly bool _coral;
	private readonly IconView _icon;
	private readonly Label _label;
	private readonly Button _hit = new() { BackgroundColor = Colors.Transparent, TextColor = Colors.Transparent, BorderWidth = 0, Padding = 0 };
	private readonly HorizontalStackLayout _row;

	public PillButton(string automationId, bool coral)
	{
		_coral = coral;
		_hit.AutomationId = automationId;
		_hit.Clicked += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
		StrokeThickness = 0;
		VerticalOptions = LayoutOptions.Center;
		BackgroundColor = coral ? DinoTokens.Get(DinoTokens.Coral) : ShelfFill;
		_icon = coral
			? new IconView { Kind = IconKind.Play, Color = Colors.White, Filled = true, VerticalOptions = LayoutOptions.Center }
			: new IconView { Kind = IconKind.Lock, Color = DinoTokens.Get(DinoTokens.Ink), StrokeWidth = 2.2, VerticalOptions = LayoutOptions.Center };
		_label = new Label { FontAttributes = FontAttributes.Bold, TextColor = coral ? Colors.White : ShelfText, LineBreakMode = LineBreakMode.NoWrap, VerticalOptions = LayoutOptions.Center };
		_row = new HorizontalStackLayout { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Children = { _icon, _label } };
		Content = new Grid { Children = { _row, _hit } };
		if (DesktopTips.IsWindows)
		{
			WireHover();
		}
	}

	public event EventHandler? Clicked;

	/// <summary>The words on the pill; the screen-reader name is the same text (or <paramref name="accessibleName"/> when the label is dropped).</summary>
	public void SetText(string text, string? accessibleName = null)
	{
		_label.Text = text;
		_hit.Text = text;
		string name = accessibleName ?? text;
		SemanticProperties.SetDescription(_hit, name);
		ToolTipProperties.SetText(this, name);
	}

	/// <summary>The screen-reader hint, set on the focusable hit button (a hint on this border is never read).</summary>
	public void SetHint(string hint) => SemanticProperties.SetHint(_hit, hint);

	/// <summary>False drops the words and keeps the icon (frame P1: "the pill drops its label, never the title").</summary>
	public bool LabelShown
	{
		get => _label.IsVisible;
		set => _label.IsVisible = value;
	}

	/// <summary>The width the pill needs with its words, measured with them shown (0 until it has a handler).</summary>
	public double WidthWithLabel()
	{
		bool shown = _label.IsVisible;
		_label.IsVisible = true;
		double width = Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
		_label.IsVisible = shown;
		return width;
	}

	/// <summary>Sizes from the layout rules; <paramref name="fullWidth"/> stretches it across the rail.</summary>
	public void Apply(double height, double labelSize, double padLeft, double padRight, double gap, double iconSize, bool fullWidth)
	{
		HeightRequest = height;
		StrokeShape = new RoundRectangle { CornerRadius = height / 2 };
		Padding = new Thickness(padLeft, 0, padRight, 0);
		HorizontalOptions = fullWidth ? LayoutOptions.Fill : LayoutOptions.End;
		_row.Spacing = gap;
		_label.FontSize = labelSize;
		_icon.Size = iconSize;
		if (_coral)
		{
			Shadow = new Shadow { Brush = new SolidColorBrush(DinoTokens.Get(DinoTokens.CoralEdge)), Offset = new Point(0, height >= TallPill ? TallCoralShadowOffset : CoralShadowOffset), Radius = 0, Opacity = 1 };
		}
	}

	private void WireHover()
	{
		var pointer = new PointerGestureRecognizer();
		pointer.PointerEntered += (_, _) => BackgroundColor = _coral ? CoralHover : ShelfHover;
		pointer.PointerExited += (_, _) => BackgroundColor = _coral ? DinoTokens.Get(DinoTokens.Coral) : ShelfFill;
		GestureRecognizers.Add(pointer);
	}
}

/// <summary>
/// The grown-ups' corner's ink chrome (Slice 325 part A, grown-up mock frames P1-P5, L1, T1, T2, W1): under 600 dp wide an ink header (the lock and "Grown-ups' corner" over the tab's title, the coral
/// "Child's shelf" at the right); from 600 a left rail holding the name, the coral way back, the one-line note, a divider and the tabs. The ink runs under the status bar and the notch (safe areas keep the
/// contents clear). Pure sizes come from <see cref="CornerLayout"/>; the page moves the one <see cref="CornerTabs"/> in and out of the rail. Layout never mirrors (Slice 96).
/// </summary>
internal sealed class CornerShell : Grid
{
	private static readonly Color Amber = Color.FromArgb("#FFC24D");
	private static readonly Color RailText = Color.FromArgb("#cfc6e6");
	private static readonly Color Divider = Color.FromRgba(1, 1, 1, 0.12);
	private const double EyebrowTracking = 0.8;
	private const double EyebrowIcon = 14;
	private const double BarSpacing = 10;
	private const double EyebrowGap = 5;

	private readonly Grid _bar = new() { ColumnSpacing = BarSpacing, RowSpacing = 1 };
	private readonly Grid _rail = new();
	private readonly Label _eyebrow = new() { FontAttributes = FontAttributes.Bold, TextColor = Amber, TextTransform = TextTransform.Uppercase, CharacterSpacing = EyebrowTracking, LineBreakMode = LineBreakMode.NoWrap, VerticalOptions = LayoutOptions.Center };
	private readonly HorizontalStackLayout _eyebrowRow;
	// The corner always shows one element named GalleryTitle (the VM checks and the debug hooks find "the gallery is up" by it): the tab's title in the bar, the corner's name in the rail. The hidden one (bar or rail
	// IsVisible false) is not in the accessibility tree, and an AutomationId is only ever set in a constructor (AutomationIdAssignmentTests).
	private const string TitleAutomationId = "GalleryTitle";
	private readonly Label _title = new() { AutomationId = TitleAutomationId, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 };
	private readonly Label _name = new() { AutomationId = TitleAutomationId, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.WordWrap, MaxLines = 2 };
	private readonly IconView _nameIcon = new() { Kind = IconKind.Lock, Color = Amber, StrokeWidth = 2.2, VerticalOptions = LayoutOptions.Center };
	private readonly Label _note = new() { AutomationId = "GalleryCornerLine", TextColor = RailText };
	private readonly BoxView _divider = new() { Color = Divider, HeightRequest = 1 };
	private readonly Grid _nameRow = new() { ColumnSpacing = 10, ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] };
	private readonly IconView _barLock = new() { Kind = IconKind.Lock, Color = Amber, Size = EyebrowIcon, StrokeWidth = 2.4, VerticalOptions = LayoutOptions.Center };
	private bool _isRail;
	private double _lastWidth;

	public CornerShell()
	{
		FlowDirection = FlowDirection.LeftToRight; // never mirrored (Slice 96)
		BackgroundColor = DinoTokens.Get(DinoTokens.Ink);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		Pill = new PillButton("GalleryDone", coral: true);
		Pill.Clicked += (_, _) => BackRequested?.Invoke();
		SemanticProperties.SetHeadingLevel(_title, SemanticHeadingLevel.Level1);
		_eyebrowRow = new HorizontalStackLayout { Spacing = EyebrowGap, Children = { _barLock, _eyebrow } };
		_nameRow.Add(_nameIcon, 0);
		_nameRow.Add(_name, 1);
		Children.Add(_bar);
		Children.Add(_rail);
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => TitleTapped?.Invoke();
		_title.GestureRecognizers.Add(tap);
		SizeChanged += (_, _) => FitWhenWidthChanged();
	}

	/// <summary>The coral way back ("Child's shelf", or "Ready for my child" during the first hand-over).</summary>
	public PillButton Pill { get; }

	/// <summary>The way back was tapped: the page leaves the corner.</summary>
	public event Action? BackRequested;

	/// <summary>The title was tapped (seven taps flip the developer switch, Slice 91).</summary>
	public event Action? TitleTapped;

	public bool IsRail => _isRail;

	public void SetTitle(string text) => _title.Text = text;

	/// <summary>Sets the fixed words in the app language: the eyebrow, the rail's name and note.</summary>
	public void ApplyTexts()
	{
		_eyebrow.Text = AppResources.Gallery_CornerTitle;
		_name.Text = AppResources.Gallery_CornerTitle;
		_note.Text = AppResources.Gallery_CornerLineShort;
	}

	/// <summary>Arranges the header or the rail for <paramref name="shape"/> with <paramref name="sizes"/>; <paramref name="pillText"/> is the coral button's words for this size.</summary>
	public void Apply(ChromeShape shape, CornerSizes sizes, string pillText, string pillName)
	{
		// Called only from the gallery's pass queue, on the UI thread (Slice 325): it builds the bar or rail from scratch every time.
		_isRail = shape.IsRail;
		_bar.IsVisible = !_isRail;
		_rail.IsVisible = _isRail;
		Pill.SetText(pillText, pillName);
		Pill.LabelShown = true;
		if (_isRail)
		{
			ApplyRail(sizes);
		}
		else
		{
			ApplyBar(sizes);
		}
	}

	private void ApplyBar(CornerSizes s)
	{
		MovePillTo(_bar);
		_bar.Children.Clear();
		_bar.ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)];
		_bar.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];
		_bar.Padding = new Thickness(s.Header.Left, s.Header.Top, s.Header.Right, s.Header.Bottom);
		_eyebrow.FontSize = s.Eyebrow;
		_title.FontSize = s.Title;
		_title.VerticalOptions = LayoutOptions.Center;
		_bar.Add(_eyebrowRow, 0, 0);
		_bar.Add(_title, 0, 1);
		_bar.Add(Pill, 1, 0);
		SetRowSpan((BindableObject)Pill, 2);
		Pill.Apply(s.PillHeight, s.PillLabel, s.PillPadLeft, s.PillPadRight, s.PillGap, s.PillIcon, fullWidth: false);
		_lastWidth = 0;
		FitPill();
	}

	private void ApplyRail(CornerSizes s)
	{
		_rail.Children.Clear();
		_rail.Padding = new Thickness(s.RailPadding.Left, s.RailPadding.Top, s.RailPadding.Right, s.RailPadding.Bottom);
		_rail.RowSpacing = s.RailGap;
		_rail.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];
		_nameIcon.Size = s.NameIcon;
		_name.FontSize = s.NameSize;
		_nameRow.Padding = new Thickness(s.NamePadding.Left, s.NamePadding.Top, s.NamePadding.Right, s.NamePadding.Bottom);
		_note.FontSize = s.NoteSize;
		_note.IsVisible = s.NoteInRail;
		_note.Padding = new Thickness(s.NotePadding.Left, s.NotePadding.Top, s.NotePadding.Right, s.NotePadding.Bottom);
		_divider.Margin = new Thickness(s.DividerMargin.Left, s.DividerMargin.Top, s.DividerMargin.Right, s.DividerMargin.Bottom);
		_rail.Add(_nameRow, 0, 0);
		MovePillTo(_rail);
		_rail.Add(Pill, 0, 1);
		SetRowSpan((BindableObject)Pill, 1);
		Pill.Apply(s.PillHeight, s.PillLabel, 0, 0, s.PillGap, s.PillIcon, fullWidth: true);
		_rail.Add(_note, 0, 2);
		_rail.Add(_divider, 0, 3);
	}

	/// <summary>Puts the one tab strip in the rail's last row; the page takes it back with <see cref="DetachTabs"/> when the chrome becomes a bar.</summary>
	public void AttachTabs(CornerTabs tabs)
	{
		if (tabs.Parent != _rail)
		{
			DetachTabs(tabs);
			_rail.Add(tabs, 0, 4);
		}
	}

	public static void DetachTabs(CornerTabs tabs)
	{
		if (tabs.Parent is Layout parent)
		{
			parent.Remove(tabs);
		}
	}

	private void MovePillTo(Grid host)
	{
		if (Pill.Parent is Layout parent && parent != host)
		{
			parent.Remove(Pill);
		}
	}

	private void FitWhenWidthChanged()
	{
		if (!_isRail && Math.Abs(Width - _lastWidth) > 0.5)
		{
			_lastWidth = Width;
			FitPill();
		}
	}

	// Frame P1 note: if a language overflows, the pill drops its words and keeps the icon, never the title. The title column is as wide as its wider line (the eyebrow or the title).
	private void FitPill()
	{
		if (_isRail || Width <= 0)
		{
			return;
		}

		double column = Math.Max(_eyebrowRow.Measure(double.PositiveInfinity, double.PositiveInfinity).Width, _title.Measure(double.PositiveInfinity, double.PositiveInfinity).Width);
		double room = Width - _bar.Padding.Left - _bar.Padding.Right;
		Pill.LabelShown = CornerLayout.PillKeepsLabel(room, column, Pill.WidthWithLabel(), BarSpacing);
	}
}
