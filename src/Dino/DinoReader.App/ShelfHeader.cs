using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The one shelf's chrome (Slice 325 part C; Slice 418 mock L1 to L6, WL1, WL2): under 600 dp wide a header row (the Dino icon on Books, the title, the "Unlocked · 24s" chip while unlocked, the flag of the reading language);
/// from 600 a cream rail with the name row (icon, "My stories") and the menu (<see cref="CornerTabs"/>) below it, the flag and the chip then sit in the page's title row. The Grown-ups pill is gone. The title is the first thing to
/// give way (full, short, hidden); the chip and the flag never shrink (<see cref="ShelfTitleRules.Fit"/>). Layout never mirrors (Slice 96).
/// </summary>
internal sealed class ShelfHeader : Grid
{
	private static readonly Color RailWash = Color.FromArgb("#FFE9C7"); // the rail's wash in the 2026-11-19 mock (WL1, WL2)
	private const double RailNameSpacing = 10;

	private readonly Border _iconFrame = new() { StrokeThickness = 0, VerticalOptions = LayoutOptions.Center };
	private const string TitleAutomationId = "GalleryTitle";
	private const string FullTitleAutomationId = "GalleryTitleFull";
	private const string ShortTitleAutomationId = "GalleryTitleShort";
	// Slice 387 item 3 (two failed iOS runs: a live label whose Text was swapped kept a stale short width; a probe label in the cell laid out 0 x 0): two title labels in the title's cell, one with the full words and
	// one with the short words, each with a Text that only changes with the language. Both stay IsVisible (each has a native view to measure); Fit shows one by Opacity 1 and puts it in the accessibility tree.
	// Both labels sit in one container that always carries the id the flows, VM checks and debug hooks look for (GalleryTitle), whatever Fit chose (Full, Short or Hidden); the labels have their own ids (an AutomationId is set once).
	private readonly Label _titleFull = NewTitle(FullTitleAutomationId, inTree: true);
	private readonly Label _titleShort = NewTitle(ShortTitleAutomationId, inTree: false);
	private readonly Grid _titleHost = new() { AutomationId = TitleAutomationId, VerticalOptions = LayoutOptions.Center };
	private readonly StoryLanguageButton _flag;
	private readonly UnlockChip _chip;
	private readonly Grid _row = new();
	private readonly Grid _nameRow = new();
	private ShelfSizes _sizes = ShelfTitleRules.SizesOf(CornerLayout.ChromeOf(393, 852, false));
	private bool _isRail;
	private bool _flagShown;
	private double _lastWidth;
	private bool _remeasure; // a Text or FontSize changed: the next Fit waits for a dispatched pass
	private bool _raisePending; // Fitted is owed for a change not yet raised
	private CornerTab _tab = CornerTab.Books; // Books shows the icon and "My stories"; Store and Settings show their own name (mock L5, L6)

	public ShelfHeader(StoryLanguageButton flag, UnlockChip chip)
	{
		_flag = flag;
		_chip = chip;
		FlowDirection = FlowDirection.LeftToRight; // never mirrored (Slice 96)
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		_iconFrame.Content = new Image { Source = "dino_icon", Aspect = Aspect.AspectFill };
		foreach (Label title in new[] { _titleFull, _titleShort })
		{
			SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
			var tap = new TapGestureRecognizer();
			tap.Tapped += (_, _) => TitleTapped?.Invoke(); // no-tap-count: the title's seven taps open the developer switch, not a control
			title.GestureRecognizers.Add(tap);
			title.HandlerChanged += (_, _) => FitWhenHandled(title); // Slice 387: a no-op without both handlers, so a rebuilt label is measured only once it has a native view
			_titleHost.Children.Add(title);
#if DEBUG
			title.SizeChanged += (_, _) => LogTitleSize(title);
#endif
		}

		Children.Add(_row);
		SizeChanged += (_, _) => FitWhenWidthChanged();
		_chip.HandlerChanged += (_, _) => FitWhenChipChanged(); // Slice 387: the chip can only be measured once it has a native view
		_chip.SizeChanged += (_, _) => FitWhenChipChanged(); // and its words grow with the font scale
	}

	// MAUI allows an AutomationId to be set once, so each label keeps its own id for good; Fit moves the accessibility tree and the opacity between them.
	private static Label NewTitle(string automationId, bool inTree)
	{
		var title = new Label { AutomationId = automationId, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1, VerticalOptions = LayoutOptions.Center, Opacity = 0, InputTransparent = true };
		AutomationProperties.SetIsInAccessibleTree(title, inTree);
		return title;
	}

	public event Action? TitleTapped;

	/// <summary>Raised after every fit, with the title that was kept.</summary>
	public event Action<ShelfTitle>? Fitted;

	/// <summary>How much of the title the last fit kept.</summary>
	public ShelfTitle TitleState { get; private set; } = ShelfTitle.Full;

	public void ApplyTexts()
	{
		SemanticProperties.SetDescription(_iconFrame, AppResources.App_Name);
		Fit();
	}

	/// <summary>
	/// The open tab: on Books the icon and "My stories" (or "Stories"); on Store and Settings the tab's own name with no icon (mock L5, L6). In a rail the name row always keeps the icon and "My stories".
	/// </summary>
	public void SetTab(CornerTab tab)
	{
		if (_tab == tab)
		{
			return;
		}

		_tab = tab;
		_iconFrame.IsVisible = _isRail || tab == CornerTab.Books;
		Fit();
	}

	/// <summary>Takes the flag and the chip out of the header row (a rail keeps them in the page's title row); a bar's <see cref="Apply"/> adds them back.</summary>
	public void ReleaseFlagAndChip()
	{
		_row.Remove(_flag);
		_row.Remove(_chip);
	}

	/// <summary>The chip appeared or went (unlocked or locked): the title has less or more room, so it fits again.</summary>
	public void SetChipShown() => Fit();

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
		SetTitleFont(sizes.Title);
		_raisePending = true; // the chrome changed: the log line carries it even when the title state is the same
		_flag.SetRound(sizes.FlagButton, sizes.Flag);
		_flag.IsVisible = flagShown;
		_iconFrame.IsVisible = _isRail || _tab == CornerTab.Books;
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
		// Mock L4: the chip sits right after the title (the title column is as wide as the words that fit, Fit sets it), the flag at the far end.
		_row.ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)];
		_row.Add(_iconFrame, 0, 0);
		_row.Add(_titleHost, 1, 0);
		_row.Add(_chip, 2, 0);
		_row.Add(_flag, 4, 0);
	}

	// Mock WL1: the name row (icon 46, "My stories" 24) and the menu under it; the flag and the chip are in the page's title row, so neither is added here.
	private void ApplyRail()
	{
		ClearHosts();
		_row.ColumnSpacing = 0;
		_row.ColumnDefinitions = [];
		_row.RowSpacing = _sizes.RailGap;
		_row.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)];
		_row.Padding = new Thickness(_sizes.RailPadding.Left, _sizes.RailPadding.Top, _sizes.RailPadding.Right, _sizes.RailPadding.Bottom);
		_nameRow.ColumnSpacing = RailNameSpacing;
		_nameRow.ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)];
		_nameRow.Add(_iconFrame, 0, 0);
		_nameRow.Add(_titleHost, 1, 0);
		_row.Add(_nameRow, 0, 0);
	}

	/// <summary>Puts the menu in the rail's last row (a rail only); the page takes it back with <see cref="DetachTabs"/> when the chrome becomes a bar.</summary>
	public void AttachTabs(CornerTabs tabs)
	{
		if (tabs.Parent != _row)
		{
			DetachTabs(tabs);
			_row.Add(tabs, 0, 1);
		}
	}

	public static void DetachTabs(CornerTabs tabs)
	{
		if (tabs.Parent is Layout parent)
		{
			parent.Remove(tabs);
		}
	}

	private void FitWhenWidthChanged()
	{
		if (Math.Abs(Width - _lastWidth) > 0.5)
		{
			_lastWidth = Width;
			Fit();
		}
	}

	private void FitWhenChipChanged()
	{
		double chip = ChipRoom();
		if (Math.Abs(chip - _lastChip) > 0.5)
		{
			Fit();
		}
	}

	private double _lastChip;

	// The chip's width in the row: 0 while it is hidden (locked) or in a rail (it is in the title row there); its measured width once it has a native view.
	private double ChipRoom()
	{
		if (_isRail || !_chip.IsVisible)
		{
			return 0;
		}

		return Math.Max(_chip.Width, _chip.Measure(double.PositiveInfinity, double.PositiveInfinity).Width);
	}

	/// <summary>Chooses the title that fits the row: "My stories", "Stories", or none. A no-op until the header has a width.</summary>
	public void Fit()
	{
		_remeasure |= SetTitleTexts();
		if (_remeasure)
		{
			_remeasure = false;
			_raisePending = true;
			InvalidateTitles();
			Dispatcher.Dispatch(Fit); // a new Text or FontSize is measured only after the next layout pass: iOS returns the old width in the same pass (run 43675329629)
			return;
		}

		if (Width <= 0 || _titleFull.Handler is null || _titleShort.Handler is null)
		{
			return; // a label re-added by a rebuild has no native view yet and would measure about 0; its HandlerChanged fits again
		}

		// Each title label measures its own fixed words (no Text swap, no probe): its desired width with no limit.
		double full = _titleFull.Measure(double.PositiveInfinity, double.PositiveInfinity).Width + ShelfTitleRules.TitleSlack;
		double shortWidth = _titleShort.Measure(double.PositiveInfinity, double.PositiveInfinity).Width + ShelfTitleRules.TitleSlack;
		double flag = _isRail ? 0 : _flagShown ? _sizes.FlagButton : 0;
		_lastChip = ChipRoom();
		bool icon = _isRail || _tab == CornerTab.Books;
		double available = _isRail ? RailRoom(0) : ShelfTitleRules.AvailableForTitle(Width, _sizes.Header.Left + _sizes.Header.Right, icon ? _sizes.Icon : 0, flag, _lastChip, _sizes.HeaderGap);
		ShelfTitle previous = TitleState;
		TitleState = ShelfTitleRules.Fit(available, full, shortWidth);
#if DEBUG
		LogFit(full, shortWidth, available, flag); // Slice 387 item 3: the numbers behind the iOS "My s..." cut
#endif
		ShowTitle(_titleFull, TitleState == ShelfTitle.Full);
		ShowTitle(_titleShort, TitleState == ShelfTitle.Short);
		_titleHost.WidthRequest = _isRail ? -1 : TitleState == ShelfTitle.Full ? full : TitleState == ShelfTitle.Short ? shortWidth : 0; // the chip hugs the words that were kept
		RaiseFitted(previous);
	}

	// Fitted is raised after a fit that ran past its deferral, once per change (a Text, a FontSize or a chrome change) or when the kept title differs from the last fit; never for a repeat of the same fit.
	private void RaiseFitted(ShelfTitle previous)
	{
		if (_raisePending || previous != TitleState)
		{
			_raisePending = false;
			Fitted?.Invoke(TitleState);
		}
	}

	private void FitWhenHandled(Label title)
	{
		if (title.Handler is not null)
		{
			Fit(); // a detach (Handler null) cannot be measured
		}
	}

	private void SetTitleFont(double size)
	{
		if (Math.Abs(_titleFull.FontSize - size) > 0.01)
		{
			_titleFull.FontSize = size;
			_titleShort.FontSize = size;
			_remeasure = true;
		}
	}

	// The shown label is opaque, touchable and in the accessibility tree; the other is transparent, transparent to touch and out of the tree.
	private static void ShowTitle(Label title, bool shown)
	{
		title.Opacity = shown ? 1 : 0;
		title.InputTransparent = !shown;
		AutomationProperties.SetIsInAccessibleTree(title, shown);
	}

	// A label's Text is set only when it differs (a language change), and Fit never measures in the pass that set it: a changed Text is what left the live label's size stale on iOS. Returns whether a Text changed.
	// The script (direction, alignment, font) is applied at every call, and the font goes back to the default when the words are not Persian.
	private bool SetTitleTexts()
	{
		bool books = _isRail || _tab == CornerTab.Books;
		string tabName = _tab == CornerTab.Store ? AppResources.Store_Title : AppResources.Settings_Title;
		bool changed = SetText(_titleFull, books ? AppResources.Gallery_Title : tabName);
		changed |= SetText(_titleShort, books ? AppResources.Gallery_ShelfTitleShort : tabName);
		return changed;
	}

	private static bool SetText(Label title, string text)
	{
		bool changed = title.Text != text;
		if (changed)
		{
			title.Text = text;
		}

		OwnScript.InOwnScriptAligned(title);
		if (title.FlowDirection != FlowDirection.RightToLeft)
		{
			title.FontFamily = null; // InOwnScript never resets the Persian family after a switch back
		}

		return changed;
	}

	private void InvalidateTitles()
	{
		_titleFull.InvalidateMeasure();
		_titleShort.InvalidateMeasure();
	}

#if DEBUG
	private void LogFit(double full, double shortWidth, double available, double flag)
	{
		string line = $"[Shelf] fit Width={Width:0.#} rail={_isRail} full={full:0.#} shortWidth={shortWidth:0.#} available={available:0.#} chipRoom={_lastChip:0.#} chipWidth={_chip.Width:0.#} flag={flag:0.#} icon={_sizes.Icon:0.#} gap={_sizes.HeaderGap:0.#} state={TitleState} titleFont={_titleFull.FontSize:0.#}";
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
		string line = $"[Shelf] title laid-out Width={title.Width:0.#} Height={title.Height:0.#} text='{title.Text}' opacity={title.Opacity:0.#} state={TitleState} headerWidth={Width:0.#} chipWidth={_chip.Width:0.#}";
		WriteFitFile(line);
	}
#endif

	private double RailRoom(double flag)
	{
		int gaps = flag > 0 ? 2 : 1;
		return Math.Max(0, Width - _sizes.RailPadding.Left - _sizes.RailPadding.Right - _sizes.Icon - flag - (gaps * RailNameSpacing));
	}
}
