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
	private const string TitleAutomationId = "GalleryTitle";
	// Slice 387 item 3 (two failed iOS runs: a live label whose Text was swapped kept a stale short width; a probe label in the cell laid out 0 x 0): two title labels in the title's cell, one with the full words and
	// one with the short words, each with a Text that never changes after it is set. Both stay IsVisible (each has a native view to measure); Fit shows one by Opacity 1 and gives it the id and the accessibility tree.
	private readonly Label _titleFull = NewTitle();
	private readonly Label _titleShort = NewTitle();
	private readonly StoryLanguageButton _flag;
	private readonly Grid _row = new();
	private readonly Grid _nameRow = new();
	private ShelfSizes _sizes = ShelfTitleRules.SizesOf(CornerLayout.ChromeOf(393, 852, false));
	private bool _isRail;
	private bool _flagShown;
	private double _lastWidth;
	private double _lastPill;

	public ShelfHeader(StoryLanguageButton flag)
	{
		_flag = flag;
		FlowDirection = FlowDirection.LeftToRight; // never mirrored (Slice 96)
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		GrownUps = UiUsage.Press(new PillButton("GalleryGrownUps", coral: false), UiControls.OpenCorner, () => GrownUpsRequested?.Invoke()); // Slice 352: counted before the gate opens
		_iconFrame.Content = new Image { Source = "dino_icon", Aspect = Aspect.AspectFill };
		foreach (Label title in Titles())
		{
			SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
			var tap = new TapGestureRecognizer();
			tap.Tapped += (_, _) => TitleTapped?.Invoke(); // no-tap-count: the title's seven taps open the developer switch, not a control
			title.GestureRecognizers.Add(tap);
			title.HandlerChanged += (_, _) => Fit(); // Slice 387: a no-op without both handlers, so a rebuilt label is measured only once it has a native view
#if DEBUG
			title.SizeChanged += (_, _) => LogTitleSize(title);
#endif
		}

		Children.Add(_row);
		SizeChanged += (_, _) => FitWhenWidthChanged();
		GrownUps.HandlerChanged += (_, _) => FitWhenPillChanged(); // Slice 387: the pill can only be measured once it has a native view
		GrownUps.SizeChanged += (_, _) => FitWhenPillChanged(); // and its words grow with the font scale
	}

	// Both carry the id the flows look for (MAUI allows an AutomationId to be set once, so it cannot move between them); only the shown one is in the accessibility tree, which is what the flows and the VM checks read.
	private static Label NewTitle() => new() { AutomationId = TitleAutomationId, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1, VerticalOptions = LayoutOptions.Center, Opacity = 0, InputTransparent = true };

	private IEnumerable<Label> Titles() => [_titleFull, _titleShort];

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
		_titleFull.FontSize = sizes.Title;
		_titleShort.FontSize = sizes.Title;
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
		_row.Add(_titleFull, 1, 0);
		_row.Add(_titleShort, 1, 0);
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
		_nameRow.Add(_titleFull, 1, 0);
		_nameRow.Add(_titleShort, 1, 0);
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

	private void FitWhenPillChanged()
	{
		double pill = PillRoom();
		if (Math.Abs(pill - _lastPill) > 0.5)
		{
			Fit();
		}
	}

	private double PillRoom()
	{
		int length = AppResources.Gallery_GrownUpsPill.Length;
		return ShelfTitleRules.PillWidth(GrownUps.Width, GrownUps.WidthWithLabel(), length, _sizes.PillLabel, _sizes.PillPadLeft, _sizes.PillPadRight, _sizes.PillIcon, _sizes.PillGap);
	}

	/// <summary>Chooses the title that fits the row: "My stories", "Stories", or none. A no-op until the header has a width.</summary>
	public void Fit()
	{
		SetTitleTexts();
		if (Width <= 0 || _titleFull.Handler is null || _titleShort.Handler is null)
		{
			return; // a label re-added by a rebuild has no native view yet and would measure about 0; its HandlerChanged fits again
		}

		double full = MeasureTitle(_titleFull) + ShelfTitleRules.TitleSlack;
		double shortWidth = MeasureTitle(_titleShort) + ShelfTitleRules.TitleSlack;
		double flag = _flagShown ? _sizes.FlagButton : 0;
		_lastPill = PillRoom();
		double available = _isRail ? RailRoom(flag) : ShelfTitleRules.AvailableForTitle(Width, _sizes.Header.Left + _sizes.Header.Right, _sizes.Icon, flag, _lastPill, _sizes.HeaderGap);
		TitleState = ShelfTitleRules.Fit(available, full, shortWidth);
#if DEBUG
		LogFit(full, shortWidth, available, flag); // Slice 387 item 3: the numbers behind the iOS "My s..." cut
#endif
		ShowTitle(_titleFull, TitleState == ShelfTitle.Full);
		ShowTitle(_titleShort, TitleState == ShelfTitle.Short);
		Fitted?.Invoke(TitleState);
	}

	// The shown label is opaque, touchable and in the accessibility tree; the other is transparent, transparent to touch and out of the tree.
	private static void ShowTitle(Label title, bool shown)
	{
		title.Opacity = shown ? 1 : 0;
		title.InputTransparent = !shown;
		AutomationProperties.SetIsInAccessibleTree(title, shown);
	}

	// A label's Text is set only when it differs (a language change), never during a measure: a changed Text is what left the live label's size stale on iOS.
	private void SetTitleTexts()
	{
		SetText(_titleFull, AppResources.Gallery_Title);
		SetText(_titleShort, AppResources.Gallery_ShelfTitleShort);
	}

	private static void SetText(Label title, string text)
	{
		if (title.Text != text)
		{
			title.Text = text;
			OwnScript.InOwnScriptAligned(title);
		}
	}

#if DEBUG
	private void LogFit(double full, double shortWidth, double available, double flag)
	{
		string line = $"[Shelf] fit Width={Width:0.#} rail={_isRail} full={full:0.#} shortWidth={shortWidth:0.#} available={available:0.#} pillRoom={_lastPill:0.#} pillWidth={GrownUps.Width:0.#} flag={flag:0.#} icon={_sizes.Icon:0.#} gap={_sizes.HeaderGap:0.#} state={TitleState} titleFont={_titleFull.FontSize:0.#}";
		WriteFitFile(line);
	}

	// Slice 387 item 3: the iOS unified log does not carry Console or Debug output, so the lines go to a file the iOS simulator workflow copies out of the app's data folder.
	// The file is truncated by the first write of each app start, so a kept data folder never mixes launches. The lines stay out of TestHookLog (one line per state change, grepped by the Android flows).
	private static bool _fitFileStarted;

	private static void WriteFitFile(string line)
	{
		try
		{
			string path = System.IO.Path.Combine(FileSystem.AppDataDirectory, "shelf-fit.log");
			if (_fitFileStarted)
			{
				File.AppendAllText(path, line + "\n");
			}
			else
			{
				_fitFileStarted = true;
				File.WriteAllText(path, line + "\n");
			}
		}
		catch (Exception)
		{
			// no-fallback: Debug-only diagnostics file; a failed write must not change the shelf
		}
	}

	private void LogTitleSize(Label title)
	{
		string line = $"[Shelf] title laid-out Width={title.Width:0.#} Height={title.Height:0.#} text='{title.Text}' opacity={title.Opacity:0.#} state={TitleState} headerWidth={Width:0.#} pillWidth={GrownUps.Width:0.#}";
		WriteFitFile(line);
	}
#endif

	private double RailRoom(double flag)
	{
		int gaps = flag > 0 ? 2 : 1;
		return Math.Max(0, Width - _sizes.RailPadding.Left - _sizes.RailPadding.Right - _sizes.Icon - flag - (gaps * RailNameSpacing));
	}

	// Each title label measures its own fixed words (no Text swap, no probe): its desired width with no limit.
	private static double MeasureTitle(Label title) => title.Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
}
