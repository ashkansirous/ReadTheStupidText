using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>
/// The first-run intro (Slice 155, Decision 160, Batch 32 mock frames 74-76; Slice 228, Batch 43 frames 101 and 101b: a first card naming the kids' shelf and the grown-ups' corner, then a read step that opens a real book): steps of real actions (the store, recording a book,
/// (Slice 278: adding an own book is gone)), each with a picture, one line, <b>Try it</b> (opens the real screen, no gate: the person installing is the grown-up)
/// and <b>Skip</b>; coming back from the screen moves on to the next step. A fourth step (Slice 173, mock frame 86) asks which story languages the
/// child's books should be in, with <b>Done</b> and <b>Skip</b>. <b>Skip all</b> ends it at once. A first-run intro ends on the
/// "Ready for my child" hand-over and saves its step so a restart resumes it; a replay (from grown-up settings) is not saved and ends back
/// where it was opened. No coach-marks, tips or what's-new screens.
/// </summary>
public sealed class IntroWizardPage : ContentPage
{
	private const double BarHeight = 44;
	private const double BodyPadding = 22;
	private const double TitleSize = 26;
	private const double LineSize = 14.5;
	private const double LineMaxWidth = 280;
	private const double CounterSize = 13;
	private const double DotSize = 8;
	private const double CurrentDotWidth = 22;
	private const double SkipHeight = 44;
	private const double TextButtonSize = 15;
	private const double PlaceCardCorner = 16;
	private const double PlaceCardTitleSize = 16;
	private const double PlaceCardLineSize = 14;
	private const double TabletTitleSize = 40;
	private const double TabletLineSize = 19;
	private const double TabletBodyPadding = 40;
	private const double TabletPanelCorner = 32;

	private readonly IStoryLibrary _library;
	private readonly GrownUpMode _mode;
	private readonly UsageCounter _usage;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly Grid _root = new() { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
	private IntroPictures _pictures = IntroPictures.None;
	private readonly WaitOverlay _waitOverlay = DinoWait.OverPage();
	private readonly WaitController _wait;
	private StoryLanguageSet? _openedLanguages; // the saved set when the story-languages step opened: a side missing from the saved set later was unticked by a failed download
	private StoryLanguagePicker? _languagePicker; // the picks of the story-languages step, kept while it shows
	private int _step;
	private bool _replay;
	private bool _restoreKidsMode;
	private bool _launchedStep;
	private bool _canRecord;
	private bool _hasStarter; // a starter book exists to open in the reader (the Read step); a separate question from whether Record can run
	private bool _ended;
	private Size _windowSize;
	private IntroShape _shape = OnboardingLayout.Intro(0, 0);
	private Grid? _bar; // the step bar of the step shown, kept so a size change can move its start clear of the camera cut-out
	private Action<IntroShape>? _place; // places the views of the step shown for a shape; set by whichever builder made the body, so a size change never rebuilds a step

	public IntroWizardPage(IStoryLibrary library, GrownUpMode mode, UsageCounter usage, IServiceProvider services)
	{
		_library = library;
		_mode = mode;
		_usage = usage;
		_services = services;
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		BackgroundColor = DinoTokens.Get(DinoTokens.Cream);
		_wait = new WaitController(_waitOverlay);
		Content = new Grid { Children = { _root, _waitOverlay } };
	}

	/// <summary>
	/// Shows the intro on its first step (a replay) or on the saved one (<paramref name="firstRun"/>: right after "See what Dino can do", or after a
	/// restart). It is pushed modally inside its own <see cref="NavigationPage"/> first, so no swipe-back can remove it (iOS has no per-page switch for
	/// that), and the picture load waits behind the wait overlay (shown only after 500 ms).
	/// </summary>
	public async Task PresentAsync(INavigation navigation, bool firstRun)
	{
		_replay = !firstRun;
		_step = firstRun && IntroState.InProgress ? IntroState.Step : 0;
		_launchedStep = false;
		_ended = false;
		_languagePicker = null;
		_restoreKidsMode = _replay && !_mode.IsGrownUp; // a replay from Settings (gate passed) must not leave the shelf in grown-up mode
		_mode.Enter();
		await navigation.PushModalAsync(new NavigationPage(this) { BarBackgroundColor = Colors.Transparent }, animated: false);
		await LoadPicturesAsync();
		Render();
	}

	private async Task LoadPicturesAsync()
	{
		_wait.Begin(WaitStage.GettingFile, AppResources.Wait_GettingBook, canCancel: false);
		try
		{
			_pictures = await IntroPictures.LoadAsync(_library);
			_hasStarter = await IntroPictures.FirstBookAsync(_library) is not null;
			_canRecord = _hasStarter;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not load the intro pictures: {ex}");
			_pictures = IntroPictures.None;
			_hasStarter = false;
			_canRecord = false;
		}
		finally
		{
			_wait.End();
		}
	}

	/// <summary>Counts the start of the intro; called once by whoever starts it, not on a resume.</summary>
	public void CountStarted(bool replay) =>
		_usage.RecordGeneral(UsageEventName.IntroStarted, LanguageChoice.Current(), replay ? UsageDetail.IntroReplay : UsageDetail.IntroFirstRun);

	/// <summary>Shows a replay over grown-up settings; it closes itself when it ends.</summary>
	public static async Task ReplayAsync(INavigation navigation, IServiceProvider services)
	{
		var page = services.GetRequiredService<IntroWizardPage>();
		page.CountStarted(replay: true);
		await page.PresentAsync(navigation, firstRun: false);
	}

	// The only thing that moves the step on: the screen Try it opened was closed (_launchedStep is set right before it is pushed and cleared here).
	// Every other appearing (first show, a gate or dialog over the wizard) leaves the step alone.
	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (_launchedStep)
		{
			_launchedStep = false;
			_mode.Enter(); // the read step opened the book in the kids' reader; the wizard itself runs in grown-up mode
			_ = _busy.RunWhenFreeAsync(AdvanceAsync);
		}
	}

	// Android back is Skip all: a half-done intro never lingers (the modal host has no swipe-back on iOS).
	protected override bool OnBackButtonPressed()
	{
		_ = _busy.RunAsync(SkipAllAsync);
		return true;
	}

	private void Render()
	{
		_root.Children.Clear();
		_place = null;
		_bar = BuildBar();
		_root.Add(_bar, 0, 0);
		_root.Add(BuildBody(), 0, 1);
		ApplyShape();
	}

	// Slice 232 part E (mock Batch 44 frames 121, 130): held sideways every step splits (picture panel left, copy right, Skip and the main button in one row); the
	// step, the replay flag and the language picks are fields of this page, so a size change only re-places the views the step build already made.
	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		(width, height) = WindowShapeSeam.Effective(DesktopTips.IsWindows, width, height);
		if (width <= 0 || height <= 0 || (width == _windowSize.Width && height == _windowSize.Height))
		{
			return;
		}

		_windowSize = new Size(width, height);
		IntroShape shape = OnboardingLayout.Intro(width, height);
		if (shape != _shape)
		{
			_shape = shape;
			ApplyShape();
		}
	}

	private void ApplyShape()
	{
		if (_bar is not null)
		{
			_bar.Padding = new Thickness(BodyPadding, 6, 10, 0);
		}

		_place?.Invoke(_shape);
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] intro split={_shape.Split} step={IntroSteps.NameAt(_step)} panel={_shape.PanelHeight}");
#endif
	}

	private Grid BuildBar()
	{
		var dots = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
		for (int i = 0; i < IntroSteps.Count; i++)
		{
			dots.Children.Add(BuildDot(i));
		}

		var counter = new Label
		{
			Text = AppResources.Format(AppResources.Intro_Counter, _step + 1, IntroSteps.Count),
			FontSize = CounterSize,
			FontAttributes = FontAttributes.Bold,
			TextColor = DinoTokens.Get(DinoTokens.Muted),
			VerticalOptions = LayoutOptions.Center,
		};
		var left = new HorizontalStackLayout { Spacing = 10, VerticalOptions = LayoutOptions.Center, Children = { dots, counter } };
		var bar = new Grid { HeightRequest = BarHeight, Padding = new Thickness(BodyPadding, 6, 10, 0), ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		bar.Add(left, 0);
		bar.Add(TextButton(AppResources.Intro_SkipAll, () => _busy.RunAsync(SkipAllAsync), BarHeight), 1);
		return bar;
	}

	// Done and current dots are brand-blue, the current one a wider pill; the ones ahead are grey.
	private Border BuildDot(int index) => new()
	{
		HeightRequest = DotSize,
		WidthRequest = index == _step ? CurrentDotWidth : DotSize,
		StrokeThickness = 0,
		StrokeShape = new RoundRectangle { CornerRadius = DotSize / 2 },
		BackgroundColor = DinoTokens.Get(index <= _step ? DinoTokens.Brand : DinoTokens.DotIdle),
	};

	private Grid BuildBody()
	{
		string? step = IntroSteps.NameAt(_step);
		if (step == IntroSteps.Languages)
		{
			return BuildLanguagesBody();
		}

		if (step == IntroSteps.Places)
		{
			return BuildPlacesBody();
		}

		bool windows = DeviceInfo.Platform == DevicePlatform.WinUI;
		(string title, string line) = step == IntroSteps.Read
			? (AppResources.Intro_ReadTitle, AppResources.Intro_ReadLine)
			: (AppResources.Intro_RecordTitle, AppResources.Intro_RecordLine);
		var heading = new Label { Text = title, FontSize = TitleSize, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
		var body = new Label { Text = line, FontSize = LineSize, LineHeight = 1.35, TextColor = DinoTokens.Get(DinoTokens.Body), HorizontalTextAlignment = TextAlignment.Center, MaximumWidthRequest = LineMaxWidth };
		var copy = new VerticalStackLayout { Spacing = 14, Children = { heading, body } };
		if (step == IntroSteps.Record)
		{
			Label stayNote = RecordViews.StayNote(); // Slice 230: where recordings go, before the grown-up records anything
			stayNote.HorizontalTextAlignment = TextAlignment.Center;
			copy.Children.Add(stayNote);
		}

		var tryIt = new CoralButton { Text = AppResources.Intro_TryIt, IsVisible = TryItVisible(step) };
		tryIt.Clicked += async (_, _) => await _busy.RunAsync(TryAsync);
		return BuildSplitBody(IntroArt.Build(IntroSteps.NameAt(_step)!, _pictures, windows), copy, tryIt, TextButton(AppResources.Intro_Skip, () => _busy.RunAsync(SkipAsync), SkipHeight, DinoTokens.Body));
	}

	// The body of a step with a picture (all but the languages step): one grid whose cells are re-placed per shape, the same views upright and sideways.
	// Rows: spacer, picture, copy, spacer, buttons (the two spacers centre the picture and copy upright; sideways the picture spans the left panel and the copy the right).
	private Grid BuildSplitBody(View art, View copy, View main, View? skip)
	{
		var buttons = new Grid { RowSpacing = 4, ColumnSpacing = 8 };
		buttons.Add(main);
		if (skip is not null)
		{
			buttons.Add(skip);
		}

		var grid = new Grid { Padding = new Thickness(BodyPadding, 0, BodyPadding, 26), RowSpacing = 0, ColumnSpacing = 10 };
		grid.RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)];
		// The cream panel behind the picture exists only on a tablet held upright (Slice 234 part B, frame 141); it is added first so the picture draws over it.
		var panel = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = TabletPanelCorner }, BackgroundColor = ReadingTheme.For(false).PicturePanel, IsVisible = false, InputTransparent = true };
		grid.Add(panel);
		grid.Add(art);
		grid.Add(copy);
		grid.Add(buttons);
		_place = shape => PlaceSplitBody(grid, new SplitViews(panel, art, copy, buttons, main, skip), shape);
		return grid;
	}

	private sealed record SplitViews(Border Panel, View Art, View Copy, Grid Buttons, View Main, View? Skip);

	private static void PlaceSplitBody(Grid grid, SplitViews views, IntroShape shape)
	{
		(Border panel, View art, View copy, Grid buttons, View main, View? skip) = (views.Panel, views.Art, views.Copy, views.Buttons, views.Main, views.Skip);
		bool stacked = shape.PanelHeight > 0;
		grid.Padding = stacked ? new Thickness(TabletBodyPadding, 0, TabletBodyPadding, TabletBodyPadding) : new Thickness(BodyPadding, 0, BodyPadding, 26);
		grid.ColumnDefinitions = shape.Split ? [new ColumnDefinition(new GridLength(shape.PicturePanel)), new ColumnDefinition(GridLength.Star)] : [];
		grid.RowDefinitions[0].Height = stacked ? new GridLength(0) : GridLength.Star; // stacked, the picture panel starts at the top and the buttons sit at the foot
		int column = shape.Split ? 1 : 0;
		SetCell(art, shape.Split ? 0 : 1, 0, shape.Split ? 5 : 1);
		SetCell(panel, 1, 0, 1);
		SetCell(copy, shape.Split ? 1 : 2, column, shape.Split ? 2 : 1);
		SetCell(buttons, 4, column, 1);
		panel.IsVisible = stacked;
		panel.HeightRequest = stacked ? shape.PanelHeight : -1;
		art.VerticalOptions = LayoutOptions.Center;
		art.HorizontalOptions = LayoutOptions.Center;
		copy.VerticalOptions = stacked ? LayoutOptions.Start : LayoutOptions.Center;
		copy.HorizontalOptions = stacked ? LayoutOptions.Start : LayoutOptions.Center;
		copy.Margin = new Thickness(0, shape.Split ? 0 : stacked ? 20 : 14, 0, 0);
		copy.MaximumWidthRequest = shape.TextMaxWidth;
		StyleCopy(copy, stacked, shape.TextMaxWidth, LineMaxWidth);
		PlaceButtons(buttons, main, skip, shape.ButtonsInRow);
	}

	// The tablet's copy is left aligned with a bigger heading and line (frame 141); the heading and the line are the first two labels of a step's copy.
	private static void StyleCopy(View copy, bool stacked, double textMaxWidth, double plainLineMaxWidth)
	{
		if (copy is not VerticalStackLayout stack)
		{
			return;
		}

		Label[] labels = [.. stack.Children.OfType<Label>()];
		TextAlignment align = stacked ? TextAlignment.Start : TextAlignment.Center;
		foreach (Label label in labels)
		{
			label.HorizontalTextAlignment = align; // every text label of the copy, including the Record step's stay note
		}

		if (labels.Length > 0)
		{
			labels[0].FontSize = stacked ? TabletTitleSize : TitleSize;
		}

		if (labels.Length > 1)
		{
			labels[1].FontSize = stacked ? TabletLineSize : LineSize;
			labels[1].MaximumWidthRequest = stacked ? textMaxWidth : plainLineMaxWidth;
		}
	}

	// Skip and the main button share one row sideways (Skip left); upright the main button sits over Skip.
	private static void PlaceButtons(Grid buttons, View main, View? skip, bool inRow)
	{
		buttons.ColumnDefinitions = inRow && skip is not null ? [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] : [];
		// Upright the two buttons need their own rows: a Grid with no row definitions has one implicit row and clamps Grid.Row 1 into it, which drew Skip over Try it (Slice 267).
		buttons.RowDefinitions = skip is not null && !inRow ? [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)] : [new RowDefinition(GridLength.Auto)];
		if (skip is null || !inRow)
		{
			SetCell(main, 0, 0, 1);
			if (skip is not null)
			{
				SetCell(skip, 1, 0, 1);
			}

			return;
		}

		SetCell(skip, 0, 0, 1);
		SetCell(main, 0, 1, 1);
	}

	private static void SetCell(View view, int row, int column, int rowSpan)
	{
		Grid.SetRow(view, row);
		Grid.SetColumn(view, column);
		Grid.SetRowSpan(view, rowSpan);
	}

	// Read needs a starter to open; Record needs whatever recording needs.
	private bool TryItVisible(string? step) => step == IntroSteps.Read ? _hasStarter : _canRecord;

	// The first card (Batch 43 frame 101): the two places side by side, a picture and one Next button. No Try it and no Skip: Skip all is in the bar.
	private Grid BuildPlacesBody()
	{
		var heading = new Label { Text = AppResources.Intro_PlacesTitle, FontSize = TitleSize, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
		Border kids = PlaceCard(null, AppResources.Intro_PlacesKidsTitle, AppResources.Intro_PlacesKidsLine);
		Border grown = PlaceCard(IconKind.Lock, AppResources.Gallery_CornerTitle, AppResources.Intro_PlacesGrownLine);
		var cards = new Grid { RowSpacing = 10, ColumnSpacing = 10, Children = { kids, grown } }; // one above the other upright, side by side sideways (frame 121)
		var copy = new VerticalStackLayout { Spacing = 14, Children = { heading, cards } };
		var next = new CoralButton { Text = AppResources.Wizard_Next, AutomationId = "IntroNext" };
		next.Clicked += async (_, _) => await _busy.RunAsync(NextAsync);
		Grid body = BuildSplitBody(IntroArt.Build(IntroSteps.Places, _pictures, false), copy, next, null);
		Action<IntroShape> placeBody = _place!;
		_place = shape =>
		{
			placeBody(shape);
			cards.ColumnDefinitions = shape.Split ? [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)] : [];
			SetCell(kids, 0, 0, 1);
			SetCell(grown, shape.Split ? 0 : 1, shape.Split ? 1 : 0, 1);
		};
		return body;
	}

	private static Border PlaceCard(IconKind? icon, string title, string line)
	{
		var titleLabel = new Label { Text = title, FontSize = PlaceCardTitleSize, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
		var head = new HorizontalStackLayout { Spacing = 6, Children = { titleLabel } };
		if (icon is { } kind)
		{
			head.Children.Insert(0, new IconView { Kind = kind, Color = DinoTokens.Get(DinoTokens.Ink), Size = 18, StrokeWidth = 2.2, VerticalOptions = LayoutOptions.Center });
		}

		var body = new Label { Text = line, FontSize = PlaceCardLineSize, TextColor = DinoTokens.Get(DinoTokens.Body) };
		return new Border
		{
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			Padding = new Thickness(16, 12),
			StrokeShape = new RoundRectangle { CornerRadius = PlaceCardCorner },
			Content = new VerticalStackLayout { Spacing = 2, Children = { head, body } },
		};
	}

	// Next on the first card counts as having seen the step.
	private Task NextAsync()
	{
		CountStep(tried: true);
		return AdvanceAsync();
	}

	// The fourth step (mock 86): the app language is ticked and locked, up to two more can be ticked; Done saves them and starts their voices, Skip keeps the app language only.
	private Grid BuildLanguagesBody()
	{
		var heading = new Label { Text = AppResources.StoryLanguages_IntroTitle, FontSize = TitleSize, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
		var line = new Label { Text = AppResources.StoryLanguages_IntroLine, FontSize = LineSize, LineHeight = 1.35, TextColor = DinoTokens.Get(DinoTokens.Body), HorizontalTextAlignment = TextAlignment.Center };
		if (_languagePicker is null)
		{
			_openedLanguages = StoryLanguages.Current();
			_languagePicker = new StoryLanguagePicker(_openedLanguages, null);
			StoryLanguagePacks packs = _services.GetRequiredService<StoryLanguagePacks>();
			EventHandler lost = (_, _) => _languagePicker?.Untick(LostSides(StoryLanguages.Current()));
			packs.Changed += lost;
			Unloaded += (_, _) => packs.Changed -= lost;
		}
		var done = new CoralButton { Text = AppResources.Common_Done };
		done.Clicked += async (_, _) => await _busy.RunAsync(DoneLanguagesAsync);
		StoryLanguageSet saved = StoryLanguages.Current();
		string skip = saved.Sides.Count == 0 ? AppResources.Format(AppResources.StoryLanguages_SkipOnly, LanguageNames.NativeName(saved.Main)) : AppResources.Intro_Skip; // Skip changes nothing, so it only says "only X" when X is all there is
		Button skipButton = TextButton(skip, () => _busy.RunAsync(SkipAsync), SkipHeight, DinoTokens.Body);
		var buttons = new Grid { RowSpacing = 4, ColumnSpacing = 8 };
		buttons.Add(done);
		buttons.Add(skipButton);
		var top = new VerticalStackLayout { Spacing = 12, Children = { heading, line } };
		var picker = new ScrollView { Content = _languagePicker };
		var grid = new Grid { Padding = new Thickness(BodyPadding, 0, BodyPadding, 26), RowSpacing = 12, ColumnSpacing = 18, RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)] };
		grid.Add(top);
		grid.Add(picker);
		grid.Add(buttons);
		_place = shape => PlaceLanguagesBody(grid, top, picker, buttons, done, skipButton, shape);
		return grid;
	}

	// Sideways the heading and line sit in the left panel, the picker and the Skip / Done row on the right; the picker object is kept across the move.
	private static void PlaceLanguagesBody(Grid grid, View top, View picker, Grid buttons, View done, View skip, IntroShape shape)
	{
		grid.Padding = new Thickness(BodyPadding, 0, BodyPadding, 26);
		grid.ColumnDefinitions = shape.Split ? [new ColumnDefinition(new GridLength(shape.PicturePanel)), new ColumnDefinition(GridLength.Star)] : [];
		int column = shape.Split ? 1 : 0;
		SetCell(top, 0, 0, shape.Split ? 3 : 1);
		top.VerticalOptions = shape.Split ? LayoutOptions.Center : LayoutOptions.Start;
		StyleCopy(top, shape.PanelHeight > 0, shape.TextMaxWidth, double.PositiveInfinity);
		SetCell(picker, shape.Split ? 0 : 1, column, shape.Split ? 2 : 1);
		SetCell(buttons, 2, column, 1);
		PlaceButtons(buttons, done, skip, shape.ButtonsInRow);
	}

	// A side saved when the step opened but gone from the saved set now was unticked behind the picker's back (a failed download): the picker shows it unticked, and Done must not tick it again.
	private StoryLanguageSet WithoutLostSides(StoryLanguageSet picked, StoryLanguageSet saved) =>
		LostSides(saved).Aggregate(picked, (set, side) => set.WithoutSide(side));

	private IEnumerable<string> LostSides(StoryLanguageSet saved) => _openedLanguages?.Sides.Where(side => !saved.Contains(side)) ?? [];

	// Saves the ticked languages, starts the voice of each (they keep downloading behind the next screens; Settings shows their progress) and counts the set.
	private async Task DoneLanguagesAsync()
	{
		StoryLanguageSet before = StoryLanguages.Current();
		StoryLanguageSet set = WithoutLostSides(_languagePicker?.Set ?? before, before);
		StoryLanguages.SaveSides(set);
		StoryLanguagePacks packs = _services.GetRequiredService<StoryLanguagePacks>();
		HashSet<string> asked = [.. set.Sides.Select(UiLanguage.Resolve).Where(code => packs.StateOf(code) == LanguageVoiceState.NotInstalled)]; // the languages the request below is about
		VoiceRequestOutcome outcome = await packs.RequestAsync(this, set.Sides, _wait); // on mobile data the grown-up is asked first
		StartShelfAudio(set.Sides.Except(before.Sides), asked, outcome); // Slice 262: Done leaves and the audio of the shelf books carries on in the background (Settings shows the card)
		if (!set.Sides.SequenceEqual(before.Sides))
		{
			StoryLanguageCounts.RecordSet(_usage, set, UsageDetail.LanguagesFromIntro, set.Sides.Except(before.Sides)); // only languages new to this phone count as added
		}

		CountStep(tried: set.Sides.Count > 0); // nothing ticked is a Skip in effect
		await AdvanceAsync();
	}

	// The request's outcome belongs to the languages it was about (their voice was missing); an added language whose voice was already there gets NothingAsked, so a Cancel on the prompt does not stop its audio.
	private void StartShelfAudio(IEnumerable<string> added, ISet<string> asked, VoiceRequestOutcome outcome)
	{
		ShelfAudioPacks shelf = _services.GetRequiredService<ShelfAudioPacks>();
		foreach (string language in added)
		{
			shelf.Start(language, asked.Contains(UiLanguage.Resolve(language)) ? outcome : VoiceRequestOutcome.NothingAsked); // a cancelled voice request starts nothing (ShelfAudioStart decides)
		}
	}

	/// <summary>A plain text button (frames 73-76): brand-coloured unless <paramref name="colourKey"/> says otherwise.</summary>
	internal static Button TextButton(string text, Func<Task> tapped, double height, string colourKey = DinoTokens.Brand)
	{
		var button = new Button
		{
			Text = text,
			FontSize = TextButtonSize,
			FontAttributes = FontAttributes.Bold,
			TextColor = DinoTokens.Get(colourKey),
			BackgroundColor = Colors.Transparent,
			HeightRequest = height,
			Padding = new Thickness(12, 0),
		};
		button.Clicked += async (_, _) => await tapped();
		return button;
	}

	private void CountStep(bool tried) =>
		_usage.RecordGeneral(UsageEventName.IntroStep, LanguageChoice.Current(), UsageDetail.IntroStepAction(IntroSteps.NameAt(_step)!, tried));

	private async Task TryAsync()
	{
		if (IntroSteps.NameAt(_step) == IntroSteps.Read)
		{
			_mode.Leave(); // the book opens in the kids' reader
		}
		else
		{
			_mode.Enter();
		}

		if (await OpenStepScreenAsync())
		{
			CountStep(tried: true);
			return;
		}

		_mode.Enter(); // nothing opened (no starter, a load error, a failed push), so OnAppearing will not restore grown-up mode: do it here
	}

	// A screen that cannot be opened is logged and the step stays as it is (nothing counted, no advance); Skip is still there.
	private async Task<bool> OpenStepScreenAsync()
	{
		try
		{
			Page? screen = await CreateStepScreenWithWaitAsync();
			if (screen is null)
			{
				Debug.WriteLine($"Dino Reader has no screen to open for intro step {IntroSteps.NameAt(_step)}.");
				return false;
			}

			_launchedStep = true;
			await Navigation.PushAsync(screen);
			return true;
		}
		catch (Exception ex)
		{
			_launchedStep = false;
			Debug.WriteLine($"Dino Reader could not open the intro step: {ex}");
			return false;
		}
	}

	private async Task<Page?> CreateStepScreenWithWaitAsync()
	{
		_wait.Begin(WaitStage.GettingFile, AppResources.Wait_GettingBook, canCancel: false);
		try
		{
			return await CreateStepScreenAsync();
		}
		finally
		{
			_wait.End();
		}
	}

	private async Task<Page?> CreateStepScreenAsync()
	{
		return IntroSteps.NameAt(_step) switch
		{
			IntroSteps.Record => await CreateRecordScreenAsync(),
			IntroSteps.Read => await CreateReadScreenAsync(),
			_ => null, // the places and languages steps open no screen
		};
	}

	// The read step (Batch 43 frame 101b) opens the first book the grown-up picked (Slice 261; else the shortest book from the catalogue) in the reader; Back from it moves the intro on.
	private async Task<Page?> CreateReadScreenAsync()
	{
		Story? story = await IntroPictures.FirstBookAsync(_library);
		if (story is null)
		{
			return null;
		}

		var reading = _services.GetRequiredService<ReadingPage>();
		reading.Load(story);
		reading.MarkDemoRead(); // the demo read counts nothing, voice ready or not
		if (VoiceReadyToPlay())
		{
			reading.PlayForIntro(); // already playing when it opens (frame 101b); it returns here on its last page
		}
		// else the voice is still downloading or failed: the reader shows its own pack status and Back still moves the intro on, so no auto-play over a silent wait
		return reading;
	}

	private bool VoiceReadyToPlay() =>
		_services.GetRequiredService<VoicePackInstaller>().State is PackInstallState.Idle or PackInstallState.Ready;

	private async Task<Page?> CreateRecordScreenAsync()
	{
		Story? story = await IntroPictures.FirstBookAsync(_library);
		if (story is null)
		{
			return null;
		}

		var record = _services.GetRequiredService<RecordPage>();
		record.LaunchedFromIntro = true;
		await record.LoadAsync(story);
		return record;
	}

	// A book may have come in during an earlier step (the store), so the Record step checks again.
	private async Task RefreshCanRecordAsync()
	{
		try
		{
			_hasStarter = await IntroPictures.FirstBookAsync(_library) is not null;
			_canRecord = _hasStarter;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not check for a book to record on: {ex}");
		}
	}

	private Task SkipAsync()
	{
		CountStep(tried: false);
		return AdvanceAsync();
	}

	private async Task AdvanceAsync()
	{
		_step++;
		if (!IntroSteps.IsStep(_step))
		{
			await EndAsync(UsageDetail.IntroFinished);
			return;
		}

		if (!_replay)
		{
			IntroState.Step = _step;
		}

		await RefreshCanRecordAsync();
		Render();
	}

	private Task SkipAllAsync() => EndAsync(UsageDetail.IntroSkippedAll);

	// A first run lands on the gallery in grown-up mode with the "Ready for my child" button (the hand-over is pending); a replay returns to settings.
	private async Task EndAsync(string how)
	{
		if (!Finish(how))
		{
			return;
		}

		try
		{
			await Navigation.PopModalAsync(animated: false);
		}
		catch (Exception ex)
		{
			// The modal may already be gone (the app left, or the update page replaced it): the intro has ended either way.
			Debug.WriteLine($"Dino Reader intro modal pop failed: {ex}");
		}
	}

	// Records the end once; false when it had already ended.
	private bool Finish(string how)
	{
		if (_ended)
		{
			return false;
		}

		_ended = true;
		_usage.RecordGeneral(UsageEventName.IntroEnded, LanguageChoice.Current(), how);
		FirstBooksGate.FirstPick = null; // every way out clears it (replay and skip too), so a later intro never opens a stale pick
		if (!_replay)
		{
			IntroState.Clear();
			FirstRunState.HandoverPending = true;
		}

		if (_restoreKidsMode)
		{
			_mode.Leave();
		}
		else
		{
			_mode.Enter();
		}

		return true;
	}
}
