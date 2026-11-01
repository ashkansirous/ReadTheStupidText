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
/// adding an own book), each with a picture, one line, <b>Try it</b> (opens the real screen, no gate: the person installing is the grown-up)
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
			_hasStarter = await IntroPictures.ShortestStarterAsync(_library) is not null;
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
		_root.Add(BuildBar(), 0, 0);
		_root.Add(BuildBody(), 0, 1);
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
		(string title, string line) = step switch
		{
			IntroSteps.Read => (AppResources.Intro_ReadTitle, AppResources.Intro_ReadLine),
			IntroSteps.Store => (AppResources.Intro_StoreTitle, AppResources.Intro_StoreLine),
			IntroSteps.Record => (AppResources.Intro_RecordTitle, AppResources.Intro_RecordLine),
			_ => windows ? (AppResources.Intro_AddTitleWindows, AppResources.Intro_AddLineWindows) : (AppResources.Intro_AddTitle, AppResources.Intro_AddLine),
		};
		var heading = new Label { Text = title, FontSize = TitleSize, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
		var body = new Label { Text = line, FontSize = LineSize, LineHeight = 1.35, TextColor = DinoTokens.Get(DinoTokens.Body), HorizontalTextAlignment = TextAlignment.Center, MaximumWidthRequest = LineMaxWidth };
		var text = new VerticalStackLayout
		{
			Spacing = 14,
			VerticalOptions = LayoutOptions.Center,
			Children = { IntroArt.Build(IntroSteps.NameAt(_step)!, _pictures, windows), heading, body },
		};
		var tryIt = new CoralButton { Text = AppResources.Intro_TryIt, IsVisible = TryItVisible(step) };
		tryIt.Clicked += async (_, _) => await _busy.RunAsync(TryAsync);
		var buttons = new VerticalStackLayout
		{
			Spacing = 4,
			Children = { tryIt, TextButton(AppResources.Intro_Skip, () => _busy.RunAsync(SkipAsync), SkipHeight, DinoTokens.Body) },
		};
		var grid = new Grid { Padding = new Thickness(BodyPadding, 0, BodyPadding, 26), RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)] };
		grid.Add(text, 0, 0);
		grid.Add(buttons, 0, 1);
		return grid;
	}

	// Read needs a starter to open; Record needs whatever recording needs; the other steps always offer Try it.
	private bool TryItVisible(string? step) => step switch
	{
		IntroSteps.Read => _hasStarter,
		IntroSteps.Record => _canRecord,
		_ => true,
	};

	// The first card (Batch 43 frame 101): the two places side by side, a picture and one Next button. No Try it and no Skip: Skip all is in the bar.
	private Grid BuildPlacesBody()
	{
		var heading = new Label { Text = AppResources.Intro_PlacesTitle, FontSize = TitleSize, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
		var cards = new VerticalStackLayout
		{
			Spacing = 10,
			Children =
			{
				PlaceCard(null, AppResources.Intro_PlacesKidsTitle, AppResources.Intro_PlacesKidsLine),
				PlaceCard(IconKind.Lock, AppResources.Gallery_CornerTitle, AppResources.Intro_PlacesGrownLine),
			},
		};
		var text = new VerticalStackLayout { Spacing = 14, VerticalOptions = LayoutOptions.Center, Children = { IntroArt.Build(IntroSteps.Places, _pictures, false), heading, cards } };
		var next = new CoralButton { Text = AppResources.Wizard_Next, AutomationId = "IntroNext" };
		next.Clicked += async (_, _) => await _busy.RunAsync(NextAsync);
		var grid = new Grid { Padding = new Thickness(BodyPadding, 0, BodyPadding, 26), RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)] };
		grid.Add(text, 0, 0);
		grid.Add(next, 0, 1);
		return grid;
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
		var buttons = new VerticalStackLayout { Spacing = 4, Children = { done, TextButton(skip, () => _busy.RunAsync(SkipAsync), SkipHeight, DinoTokens.Body) } };
		var top = new VerticalStackLayout { Spacing = 12, Children = { heading, line } };
		var grid = new Grid { Padding = new Thickness(BodyPadding, 0, BodyPadding, 26), RowSpacing = 12, RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)] };
		grid.Add(top, 0, 0);
		grid.Add(new ScrollView { Content = _languagePicker }, 0, 1);
		grid.Add(buttons, 0, 2);
		return grid;
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
		await _services.GetRequiredService<StoryLanguagePacks>().RequestAsync(this, set.Sides, _wait); // on mobile data the grown-up is asked first
		if (!set.Sides.SequenceEqual(before.Sides))
		{
			StoryLanguageCounts.RecordSet(_usage, set, UsageDetail.LanguagesFromIntro, set.Sides.Except(before.Sides)); // only languages new to this phone count as added
		}

		CountStep(tried: set.Sides.Count > 0); // nothing ticked is a Skip in effect
		await AdvanceAsync();
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
		switch (IntroSteps.NameAt(_step))
		{
			case IntroSteps.Store:
				var store = _services.GetRequiredService<StorePage>();
				store.LaunchedFromIntro = true; // its header button reads "Continue" and means "back to the intro, next step" (Slice 229)
				return store;
			case IntroSteps.Record:
				return await CreateRecordScreenAsync();
			case IntroSteps.Read:
				return await CreateReadScreenAsync();
			default:
				var wizard = _services.GetRequiredService<StoryWizardPage>();
				wizard.LaunchedFromIntro = true;
				await wizard.LoadAsync(null);
				return wizard;
		}
	}

	// The read step (Batch 43 frame 101b) opens the shortest starter (the Tortoise and the Hare on a fresh install) in the reader; Back from it moves the intro on.
	private async Task<Page?> CreateReadScreenAsync()
	{
		Story? story = await IntroPictures.ShortestStarterAsync(_library);
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
		Story? story = await IntroPictures.ShortestStarterAsync(_library);
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
			_hasStarter = await IntroPictures.ShortestStarterAsync(_library) is not null;
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
