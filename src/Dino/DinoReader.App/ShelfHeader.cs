using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The kids' shelf's chrome of the 2026-11-08 design (Slice 325 part C, grown-up mock frames K1-K5, KL1, KT1, KT2, KW1): the corner's skeleton with cream for ink and no tabs. Under 600 dp wide a header row
/// (the Dino icon, the title, the flag of the story language, the Grown-ups pill); from 600 a cream rail with the name row (icon, title, flag at the row's end) and the full-width Grown-ups button below, where
/// the corner's coral button sits. The title is the first thing to give way (full, short, hidden); the pill and the flag never shrink (<see cref="ShelfTitleRules.Fit"/>). Layout never mirrors (Slice 96).
/// </summary>
internal sealed class ShelfHeader : Grid
{
	private static readonly Color RailWash = Color.FromRgb(244, 236, 222); // cream under 5% ink
	private const double RailNameSpacing = 10;

	private readonly Border _iconFrame = new() { StrokeThickness = 0, VerticalOptions = LayoutOptions.Center };
	private readonly Label _title = new() { AutomationId = "GalleryTitle", FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), LineBreakMode = LineBreakMode.NoWrap, MaxLines = 1, VerticalOptions = LayoutOptions.Center };
	private readonly StoryLanguageButton _flag;
	private readonly Grid _row = new();
	private readonly Grid _nameRow = new();
	private ShelfSizes _sizes = ShelfTitleRules.SizesOf(CornerLayout.ChromeOf(393, 852, false));
	private bool _isRail;
	private bool _flagShown;
	private double _lastWidth;

	public ShelfHeader(StoryLanguageButton flag)
	{
		_flag = flag;
		FlowDirection = FlowDirection.LeftToRight; // never mirrored (Slice 96)
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		GrownUps = UiUsage.Press(new PillButton("GalleryGrownUps", coral: false), UiControls.OpenCorner, () => GrownUpsRequested?.Invoke()); // Slice 352: counted before the gate opens
		_iconFrame.Content = new Image { Source = "dino_icon", Aspect = Aspect.AspectFill };
		SemanticProperties.SetHeadingLevel(_title, SemanticHeadingLevel.Level1);
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => TitleTapped?.Invoke(); // no-tap-count: the title's seven taps open the developer switch, not a control
		_title.GestureRecognizers.Add(tap);
		Children.Add(_row);
		SizeChanged += (_, _) => FitWhenWidthChanged();
	}

	/// <summary>The "Grown-ups" pill; a tap runs the gate (the page decides).</summary>
	public PillButton GrownUps { get; }

	public event Action? GrownUpsRequested;

	public event Action? TitleTapped;

	/// <summary>Raised after every fit, with the title that was kept.</summary>
	public event Action<ShelfTitle>? Fitted;

	/// <summary>How much of the title the last fit kept.</summary>
	public ShelfTitle TitleState { get; private set; } = ShelfTitle.Full;

	public void ApplyTexts()
	{
		SemanticProperties.SetDescription(_iconFrame, AppResources.App_Name);
		GrownUps.SetText(AppResources.Gallery_GrownUpsPill, AppResources.Gallery_GrownUpsButtonLocked);
		GrownUps.SetHint(AppResources.Gallery_OpensGateHint);
		Fit();
	}

	/// <summary>Arranges the header or the rail for <paramref name="shape"/> with <paramref name="sizes"/>; <paramref name="flagShown"/> is whether the language button shows.</summary>
	public void Apply(ChromeShape shape, ShelfSizes sizes, bool flagShown)
	{
		_isRail = shape.IsRail;
		_sizes = sizes;
		_flagShown = flagShown;
		BackgroundColor = _isRail ? RailWash : DinoTokens.Get(DinoTokens.Cream);
		_iconFrame.WidthRequest = sizes.Icon;
		_iconFrame.HeightRequest = sizes.Icon;
		_iconFrame.StrokeShape = new RoundRectangle { CornerRadius = sizes.IconRadius };
		_title.FontSize = sizes.Title;
		_flag.SetRound(sizes.FlagButton, sizes.Flag);
		_flag.IsVisible = flagShown;
		GrownUps.Apply(sizes.PillHeight, sizes.PillLabel, sizes.PillPadLeft, sizes.PillPadRight, sizes.PillGap, sizes.PillIcon, fullWidth: _isRail);
		if (_isRail)
		{
			ApplyRail();
		}
		else
		{
			ApplyBar();
		}

		_lastWidth = 0;
		Fit();
	}

	/// <summary>The language button appeared or went: the title has less or more room, so it fits again.</summary>
	public void SetFlagShown(bool shown)
	{
		if (_flagShown != shown)
		{
			_flagShown = shown;
			_flag.IsVisible = shown;
			Fit();
		}
	}

	private void ClearHosts()
	{
		_row.Children.Clear();
		_nameRow.Children.Clear();
	}

	private void ApplyBar()
	{
		ClearHosts();
		_row.ColumnSpacing = _sizes.HeaderGap;
		_row.RowSpacing = 0;
		_row.RowDefinitions = [];
		_row.Padding = new Thickness(_sizes.Header.Left, _sizes.Header.Top, _sizes.Header.Right, _sizes.Header.Bottom);
		_row.ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)];
		_row.Add(_iconFrame, 0, 0);
		_row.Add(_title, 1, 0);
		_row.Add(_flag, 2, 0);
		_row.Add(GrownUps, 3, 0);
	}

	private void ApplyRail()
	{
		ClearHosts();
		_row.ColumnSpacing = 0;
		_row.ColumnDefinitions = [];
		_row.RowSpacing = _sizes.RailGap;
		_row.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];
		_row.Padding = new Thickness(_sizes.RailPadding.Left, _sizes.RailPadding.Top, _sizes.RailPadding.Right, _sizes.RailPadding.Bottom);
		_nameRow.ColumnSpacing = RailNameSpacing;
		_nameRow.ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)];
		_nameRow.Add(_iconFrame, 0, 0);
		_nameRow.Add(_title, 1, 0);
		_nameRow.Add(_flag, 2, 0);
		_row.Add(_nameRow, 0, 0);
		_row.Add(GrownUps, 0, 1);
	}

	private void FitWhenWidthChanged()
	{
		if (Math.Abs(Width - _lastWidth) > 0.5)
		{
			_lastWidth = Width;
			Fit();
		}
	}

	/// <summary>Chooses the title that fits the row: "My stories", "Stories", or none. A no-op until the header has a width.</summary>
	public void Fit()
	{
		if (Width <= 0)
		{
			return;
		}

		double full = MeasureTitle(AppResources.Gallery_Title);
		double shortWidth = MeasureTitle(AppResources.Gallery_ShelfTitleShort);
		double flag = _flagShown ? _sizes.FlagButton : 0;
		double available = _isRail ? RailRoom(flag) : ShelfTitleRules.AvailableForTitle(Width, _sizes.Header.Left + _sizes.Header.Right, _sizes.Icon, flag, GrownUps.WidthWithLabel(), _sizes.HeaderGap);
		TitleState = ShelfTitleRules.Fit(available, full, shortWidth);
		_title.IsVisible = TitleState != ShelfTitle.Hidden;
		_title.Text = TitleState == ShelfTitle.Short ? AppResources.Gallery_ShelfTitleShort : AppResources.Gallery_Title;
		OwnScript.InOwnScriptAligned(_title);
		Fitted?.Invoke(TitleState);
	}

	private double RailRoom(double flag)
	{
		int gaps = flag > 0 ? 2 : 1;
		return Math.Max(0, Width - _sizes.RailPadding.Left - _sizes.RailPadding.Right - _sizes.Icon - flag - (gaps * RailNameSpacing));
	}

	private double MeasureTitle(string text)
	{
		_title.Text = text;
		return _title.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
	}
}
