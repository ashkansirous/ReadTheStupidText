using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.Waiting;
using FallbackArea = ReadTheStupidText.Application.Usage.FallbackArea;
using FallbackReason = ReadTheStupidText.Application.Usage.FallbackReason;

namespace DinoReader;

/// <summary>
/// The first-run intro (Slice 155, Decision 160, Batch 32 mock frames 74-76; Slice 228, Batch 43 frames 101 and 101b: a first card naming the kids' shelf and the grown-ups' corner, then a read step that opens a real book): steps of real actions (the store, recording a book,
/// (Slice 278: adding an own book is gone)), each with a picture, one line, <b>Try it</b> (opens the real screen, no gate: the person installing is the grown-up)
/// and <b>Skip</b>; coming back from the screen moves on to the next step. A fourth step (Slice 173, mock frame 86) asks which story languages the
/// child's books should be in, with <b>Done</b> and <b>Skip</b>. <b>Skip all</b> ends it at once. A first-run intro ends on the
/// "Ready for my child" hand-over and saves its step so a restart resumes it; a replay (from grown-up settings) is not saved and ends back
/// where it was opened. No coach-marks, tips or what's-new screens. Slice 325 part E (onboarding mock 2026-11-08, frames O6-O9): every step sits on <see cref="OnboardingShell"/>
/// under a top bar of pager dots, "N of 4" and Skip all; on Windows Enter is Next, Esc is Skip all and the arrows step back and forward.
/// </summary>
public sealed class IntroWizardPage : ContentPage, IDinoKeyTarget
{
	private const double BarHeight = 44;
	private const double BodyPadding = 22;
	private const double CounterSize = 13;
	private const double DotSize = 8;
	private const double CurrentDotWidth = 22;
	private const double SkipHeight = 44;
	private const double TextButtonSize = 15;
	private const double PlaceCardCorner = 16;
	private const double PlaceCardTitleSize = 15.5;
	private const double PlaceCardLineSize = 14;
	private const double StackLineMaxWidth = 280;

	private readonly IStoryLibrary _library;
	private readonly GrownUpMode _mode;
	private readonly UsageCounter _usage;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly Grid _root = new() { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
	private readonly WaitOverlay _waitOverlay = DinoWait.OverPage();
	private readonly WaitController _wait;
	private StoryLanguagePicker? _languagePicker; // the picks of the story-languages step, kept while it shows
	private int _step;
	private bool _replay;
	private bool _restoreKidsMode;
	private bool _launchedStep;
	private bool _canRecord;
	private bool _hasStarter; // a starter book exists to open in the reader (the Read step); a separate question from whether Record can run
	private bool _ended;
	private Size _windowSize;
	private OnboardingShape _shape = OnboardingLayout.Shape(0, 800, DesktopTips.IsWindows, intro: true);
	private Grid? _bar; // the step bar of the step shown, kept so a size change can move its start clear of the camera cut-out
	private OnboardingShell? _shell; // the body of the step shown
	private Action<OnboardingShape>? _place; // places the views of the step shown for a shape; set by whichever builder made the body, so a size change never rebuilds a step
	private Func<Task>? _mainAction; // what the step's main button does (Enter on Windows)
	private Func<Task>? _forwardAction; // what the right arrow does on Windows: the step's Skip, or Next on the first card

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
			_hasStarter = await HasStarterAsync();
			_canRecord = _hasStarter;
		}
		finally
		{
			_wait.End();
		}
	}

	private async Task<bool> HasStarterAsync()
	{
		try
		{
			return await IntroPictures.FirstBookAsync(_library) is not null;
		}
		catch (OperationCanceledException)
		{
			// no-fallback: a cancelled check is not a failure; no starter book to record on
			return false;
		}
		catch (Exception ex)
		{
			DinoFallbacks.Report(FallbackArea.Library, FallbackReason.StoryUnreadable); // Slice 275 part 3: no starter book to record on
			Debug.WriteLine($"Dino Reader could not check the starter book: {ex}");
			return false;
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

	// Windows (frame OW6): Enter is the step's main button, Esc is Skip all, Left and Right step back and forward. Nothing reaches this page off Windows.
	DinoScreen IDinoKeyTarget.Screen => DinoScreen.Intro;

	bool IDinoKeyTarget.Handle(DinoKeyAction action, DinoKey key)
	{
		Func<Task>? run = action switch
		{
			DinoKeyAction.IntroNext => _mainAction,
			DinoKeyAction.IntroSkipAll => SkipAllAsync,
			DinoKeyAction.IntroPrevious => _step > 0 ? PreviousAsync : null,
			DinoKeyAction.IntroForward => _forwardAction,
			_ => null,
		};
		if (run is null || _ended)
		{
			return false;
		}

		// Enter is always Next/Done and always claimed here; Space activates whatever has focus (Slice 325 design note, third strike)
		_ = _busy.RunAsync(run);
		return true;
	}

	// The left arrow shows the step before (nothing counted); a first-run intro saves where it is, like Next does.
	private async Task PreviousAsync()
	{
		_step--;
		if (!_replay)
		{
			IntroState.Step = _step;
		}

		await RefreshCanRecordAsync(); // a book may have come in since, like Next does
		Render();
	}

	private readonly HashSet<int> _countedSteps = [];

	private void Render()
	{
		_root.Children.Clear();
		_place = null;
		_mainAction = null;
		_forwardAction = null;
		_bar = BuildBar();
		_shell = BuildBody();
		_root.Add(_shell, 0, 1);
		_root.Add(_bar, 0, 0); // added last so it sits over the shell when the shell spans both rows
		ApplyShape();
	}

	// Slice 325 part E (onboarding mock frames O6-O9): one column under 600 dp wide, two panes from 600; the step, the replay flag and the language picks are fields of this page,
	// so a size change only re-places the views the step build already made.
	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		(width, height) = WindowShapeSeam.Effective(DesktopTips.IsWindows, width, height);
		if (width <= 0 || height <= 0 || (width == _windowSize.Width && height == _windowSize.Height))
		{
			return;
		}

		_windowSize = new Size(width, height);
		OnboardingShape shape = OnboardingLayout.Shape(width, height, DesktopTips.IsWindows, intro: true);
		if (shape != _shape)
		{
			_shape = shape;
			ApplyShape();
		}
	}

	// Stacked, the bar is a row above the shell. Split (frames OL7, OT6, OTL7, OW6) the washed left pane reaches the top edge and the bar floats over the right pane only.
	private void PlaceBar()
	{
		if (_bar is null || _shell is null)
		{
			return;
		}

		_bar.Margin = new Thickness(_shape.Split ? _shape.LeftPane : 0, 0, 0, 0);
		_shell.RightTopInset = _shape.Split ? BarHeight : 0;
		Grid.SetRow(_shell, _shape.Split ? 0 : 1);
		Grid.SetRowSpan(_shell, _shape.Split ? 2 : 1);
	}

	private void ApplyShape()
	{
		PlaceBar();
		if (_bar is not null)
		{
			_bar.Padding = new Thickness(BodyPadding, 6, 10, 0);
		}

		_shell?.Place(_shape);
		_place?.Invoke(_shape);
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] intro split={_shape.Split} step={IntroSteps.NameAt(_step)} panel={_shape.LeftPane}");
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Redesign] intro step={IntroSteps.NameAt(_step)} shape={(_shape.Split ? "split" : "stack")}");
#endif
	}

	// The top bar (frame O6): pager dots, "N of 4" and Skip all (a 44 dp target).
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

	// The current dot is a wide Brand pill; every other one is a small grey dot (frame O6).
	private Border BuildDot(int index) => new()
	{
		HeightRequest = DotSize,
		WidthRequest = index == _step ? CurrentDotWidth : DotSize,
		StrokeThickness = 0,
		StrokeShape = new RoundRectangle { CornerRadius = DotSize / 2 },
		BackgroundColor = DinoTokens.Get(index == _step ? DinoTokens.Brand : DinoTokens.DotIdle),
	};

	private OnboardingShell BuildBody()
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

		(string title, string line) = step == IntroSteps.Read
			? (AppResources.Intro_ReadTitle, AppResources.Intro_ReadLine)
			: (AppResources.Intro_RecordTitle, AppResources.Intro_RecordLine);
		var heading = new Label { Text = title, FontAttributes = FontAttributes.Bold };
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
		var body = new Label { Text = line, LineHeight = 1.35, TextColor = DinoTokens.Get(DinoTokens.Body) };
		var copy = new VerticalStackLayout { Spacing = 14, Children = { heading, body } };
		if (step == IntroSteps.Record)
		{
			copy.Children.Add(RecordViews.StayNote()); // Slice 230: where recordings go, before the grown-up records anything
		}

		var tryIt = new CoralButton { Text = AppResources.Intro_TryIt, IsVisible = TryItVisible(step) };
		tryIt.Clicked += async (_, _) => await _busy.RunAsync(TryAsync);
		Button skip = TextButton(AppResources.Intro_Skip, () => _busy.RunAsync(SkipAsync), SkipHeight, DinoTokens.Body);
		_mainAction = tryIt.IsVisible ? TryAsync : SkipAsync;
		_forwardAction = SkipAsync;
		return BuildStepShell(IntroArt.Build(step!), copy, tryIt, skip);
	}

	// A step with a picture: the picture left (scaled per shape), the words right, the buttons at the foot. The same views upright and sideways; only their places and sizes change.
	private OnboardingShell BuildStepShell(View art, View copy, CoralButton main, View? skip)
	{
		var artHost = new Grid { Children = { art }, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		var buttons = new Grid { RowSpacing = 4, ColumnSpacing = 8 };
		buttons.Add(main);
		if (skip is not null)
		{
			buttons.Add(skip);
		}

		var shell = new OnboardingShell(artHost, CentredScroll(copy), buttons);
		_place = shape =>
		{
			ScaleArt(artHost, art, shape);
			StyleCopy(copy, shape, StackLineMaxWidth);
			PlaceButtons(buttons, main, skip, shape);
		};
		return shell;
	}

	// The picture is drawn at its own size and scaled about its centre; the host takes the scaled size so the layout makes room for it (80% beside the words or on a short page, 115% and 160% on tablets).
	private static void ScaleArt(Grid host, View art, OnboardingShape shape)
	{
		double scale = OnboardingLayout.ArtScale(shape);
		host.WidthRequest = art.WidthRequest * scale;
		host.HeightRequest = art.HeightRequest * scale;
		art.Scale = scale;
		art.HorizontalOptions = LayoutOptions.Center;
		art.VerticalOptions = LayoutOptions.Center;
	}

	// The words scroll when a large font makes them taller than the pane, and sit vertically centred in the pane when they are not (frames OT6, OW6).
	private static ScrollView CentredScroll(View copy)
	{
		var holder = new Grid { Children = { copy } };
		var scroll = new ScrollView { Content = holder };
		scroll.SizeChanged += (_, _) => holder.MinimumHeightRequest = Math.Max(0, scroll.Height);
		return scroll;
	}

	// Heading and line sizes per shape: 26 / 14.5 stacked (24 / 14 under 700 tall), 24 / 14 on a phone held sideways, 34 / 18 on a tablet held upright, 40 / 19 sideways, 34 / 17 on Windows;
	// centred stacked, left-aligned in a pane. The heading and the line are the first two labels of a step's copy.
	private static void StyleCopy(View copy, OnboardingShape shape, double stackLineMaxWidth)
	{
		if (copy is not VerticalStackLayout stack)
		{
			return;
		}

		(double title, double line) = CopySizes(shape);
		TextAlignment align = shape.Split ? TextAlignment.Start : TextAlignment.Center;
		Label[] labels = [.. stack.Children.OfType<Label>()];
		foreach (Label label in labels)
		{
			label.HorizontalTextAlignment = align; // every text label of the copy, including the Record step's stay note
		}

		if (labels.Length > 0)
		{
			labels[0].FontSize = title;
		}

		if (labels.Length > 1)
		{
			labels[1].FontSize = line;
			labels[1].MaximumWidthRequest = shape.Split ? double.PositiveInfinity : stackLineMaxWidth;
			labels[1].HorizontalOptions = shape.Split ? LayoutOptions.Fill : LayoutOptions.Center;
		}

		stack.VerticalOptions = shape.Split ? LayoutOptions.Center : LayoutOptions.Start;
		stack.Margin = new Thickness(0, shape.Split ? 0 : 10, 0, 0);
	}

	private static (double Title, double Line) CopySizes(OnboardingShape shape)
	{
		if (!shape.Split)
		{
			return shape.Compact ? (24, 14) : (26, 14.5);
		}

		return shape.Family switch
		{
			OnboardingFamily.TabletLandscape => (40, 19),
			OnboardingFamily.TabletPortrait => (34, 18),
			OnboardingFamily.Windows => (34, 17),
			_ => (24, 14),
		};
	}

	// The step's buttons: stacked, the main button over Skip, full width; beside the panes they share one row (main first, then Skip), the main button 220 x 50 (260 x 52 on Windows).
	private static void PlaceButtons(Grid buttons, CoralButton main, View? skip, OnboardingShape shape)
	{
		bool inRow = shape.Split && skip is not null;
		(double width, double height) = OnboardingLayout.ContinueSize(shape);
		main.SetSize(width, height);
		main.HorizontalOptions = shape.Split ? LayoutOptions.Start : LayoutOptions.Fill;
		main.VerticalOptions = LayoutOptions.Center;
		buttons.ColumnDefinitions = inRow ? [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)] : [];
		// Stacked the two buttons need their own rows: a Grid with no row definitions has one implicit row and clamps Grid.Row 1 into it, which drew Skip over Try it (Slice 267).
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

		SetCell(main, 0, 0, 1);
		SetCell(skip, 0, 1, 1);
	}

	private static void SetCell(View view, int row, int column, int rowSpan)
	{
		Grid.SetRow(view, row);
		Grid.SetColumn(view, column);
		Grid.SetRowSpan(view, rowSpan);
	}

	// Read needs a starter to open; Record needs whatever recording needs.
	private bool TryItVisible(string? step) => step == IntroSteps.Read ? _hasStarter : _canRecord;

	// The first card (frame O6): the two places, a picture and one Next button. No Try it and no Skip: Skip all is in the bar.
	private OnboardingShell BuildPlacesBody()
	{
		var heading = new Label { Text = AppResources.Intro_PlacesTitle, FontAttributes = FontAttributes.Bold };
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
		string kidsLine = DesktopTips.IsWindows ? AppResources.Intro_PlacesKidsLineClick : AppResources.Intro_PlacesKidsLine; // "click" on a computer (frame OW6)
		Border kids = PlaceCard(null, AppResources.Intro_PlacesKidsTitle, kidsLine);
		Border grown = PlaceCard(IconKind.Lock, AppResources.Gallery_CornerTitle, AppResources.Intro_PlacesGrownLine);
		var cards = new VerticalStackLayout { Spacing = 10, Children = { kids, grown } };
		var copy = new VerticalStackLayout { Spacing = 14, Children = { heading, cards } };
		var next = new CoralButton { Text = AppResources.Wizard_Next, AutomationId = "IntroNext" };
		next.Clicked += async (_, _) => await _busy.RunAsync(NextAsync);
		_mainAction = NextAsync;
		_forwardAction = NextAsync;
		return BuildStepShell(IntroArt.Build(IntroSteps.Places), copy, next, null);
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
			Padding = new Thickness(14, 12),
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

	// The fourth step (frame O9): the heading and line left (above the list stacked), the app language ticked and locked, up to two more can be ticked; Done saves them and starts their audio, Skip keeps the app language only.
	private OnboardingShell BuildLanguagesBody()
	{
		var heading = new Label { Text = AppResources.StoryLanguages_IntroTitle, FontAttributes = FontAttributes.Bold };
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
		var line = new Label { Text = AppResources.StoryLanguages_IntroLine, LineHeight = 1.35, TextColor = DinoTokens.Get(DinoTokens.Body) };
		StoryLanguagePicker picker = _languagePicker ??= new StoryLanguagePicker(StoryLanguages.Current());
		if (_languagePicker.Parent is ScrollView previousHost)
		{
			previousHost.Content = null; // stepping back and forward rebuilds this step; a view with two parents throws or draws blank
		}

		var done = new CoralButton { Text = AppResources.Common_Done };
		done.Clicked += async (_, _) => await _busy.RunAsync(DoneLanguagesAsync);
		StoryLanguageSet saved = StoryLanguages.Current();
		string skip = saved.Sides.Count == 0 ? AppResources.Format(AppResources.StoryLanguages_SkipOnly, LanguageNames.NativeName(saved.Main)) : AppResources.Intro_Skip; // Skip changes nothing, so it only says "only X" when X is all there is
		Button skipButton = TextButton(skip, () => _busy.RunAsync(SkipAsync), SkipHeight, DinoTokens.Body);
		_mainAction = DoneLanguagesAsync;
		_forwardAction = null; // the last step: the right arrow must not end the intro and drop ticks not yet confirmed with Done or Skip
		var buttons = new Grid { RowSpacing = 4, ColumnSpacing = 8 };
		buttons.Add(done);
		buttons.Add(skipButton);
		var top = new VerticalStackLayout { Spacing = 12, Children = { heading, line } };
		var shell = new OnboardingShell(top, new ScrollView { Content = _languagePicker }, buttons);
		_place = shape =>
		{
			StyleCopy(top, shape, double.PositiveInfinity);
			PlaceButtons(buttons, done, skipButton, shape);
			picker.TabletRows =shape.Family is OnboardingFamily.TabletPortrait or OnboardingFamily.TabletLandscape; // frame OT9: 64 dp rows; Settings keeps its own
		};
		return shell;
	}

	// Saves the ticked languages, starts the pre-made audio of each (it keeps downloading behind the next screens; Settings shows its progress) and counts the set.
	private async Task DoneLanguagesAsync()
	{
		StoryLanguageSet before = StoryLanguages.Current();
		StoryLanguageSet set = _languagePicker?.Set ?? before;
		StoryLanguages.SaveSides(set);
		await StartShelfAudioAsync(set.Sides.Except(before.Sides).ToList()); // Slice 262: Done leaves and the audio of the shelf books carries on in the background (Settings shows the card)
		if (!set.Sides.SequenceEqual(before.Sides))
		{
			StoryLanguageCounts.RecordSet(_usage, set, UsageDetail.LanguagesFromIntro, set.Sides.Except(before.Sides)); // only languages new to this phone count as added
		}

		CountStep(tried: set.Sides.Count > 0); // nothing ticked is a Skip in effect
		await AdvanceAsync();
	}

	// On mobile data only, the grown-up is asked once for all the added languages whether to download now or wait for Wi-Fi.
	private async Task StartShelfAudioAsync(IReadOnlyList<string> added)
	{
		VoiceRequestOutcome outcome = VoiceRequestOutcome.NothingAsked;
		try
		{
			outcome = await ShelfAudioAsk.AskAsync(this, added);
		}
		catch (Exception ex)
		{
			// no-fallback: the question could not be shown; the audio then follows the ordinary Wi-Fi rule
			AudioPackLog.Write($"the mobile data question failed ({ex.GetType().Name})");
		}

		ShelfAudioPacks shelf = _services.GetRequiredService<ShelfAudioPacks>();
		IReadOnlyList<string> selected = StoryLanguages.Current().Sides;
		foreach (string language in added.Where(selected.Contains)) // only those still selected after the question
		{
			shelf.Start(language, outcome);
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

	private void CountStep(bool tried)
	{
		if (!_countedSteps.Add(_step))
		{
			return; // reached again with the left arrow, or counted by a fast second press: already counted
		}

		_usage.RecordGeneral(UsageEventName.IntroStep, LanguageChoice.Current(), UsageDetail.IntroStepAction(IntroSteps.NameAt(_step)!, tried));
	}

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
			DinoFallbacks.Report(FallbackArea.Ui, FallbackReason.TapFailed); // Slice 275 part 3: the step's screen did not open
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
		reading.MarkDemoRead(); // the demo read counts nothing
		reading.PlayForIntro(); // already playing when it opens (frame 101b); it returns here on its last page
		return reading;
	}

	private async Task<Page?> CreateRecordScreenAsync()
	{
		Story? story = await IntroPictures.FirstBookAsync(_library);
		if (story is null)
		{
			return null;
		}

		var record = _services.GetRequiredService<RecordIntroPage>();
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
			DinoFallbacks.Report(FallbackArea.Library, FallbackReason.StoryUnreadable); // Slice 275 part 3: the record step stays as it was
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
			// no-fallback: the modal may already be gone (the app left, or the update page replaced it); the intro has ended either way
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
