using System.Diagnostics;
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>
/// The reading screen (Slice 62): the story in picture or text mode, the spoken sentence marked,
/// the page turning by itself as reading reaches it (Decision 80) until the child swipes away, and
/// a tap on a sentence reading from there. <see cref="ReadingSession"/> does the reading; the
/// <see cref="PageFollower"/> decides which page is shown.
/// </summary>
public partial class ReadingPage : ContentPage
{
	private const double CompactTitleSize = 17;
	private const double FullTitleSize = 20;

	private readonly ReadingSession _session;
	private readonly IStoryLibrary _library;
	private readonly GrownUpGate _gate;
	private readonly IServiceProvider _services;
	private readonly VoicePackInstaller _installer;
	private readonly IVoiceModelService _model;
	private readonly SpokenLanguage _spoken;
	private readonly PageFollower _follower = new();
	private readonly BusyGuard _busy = new();
	private readonly SemaphoreSlim _saveLock = new(1, 1); // the cast save and the mode save each read-modify-write _story: one at a time
	private Story? _story;
	private IReadOnlyList<SentenceSpan> _sentences = [];
	private ReadingTheme _theme = ReadingTheme.For(false);
	private bool _visible;
	private bool _stoppedDuringGate;
	private const double FullHeaderRowSpacing = 2;
	private bool _compactHeader;
	private bool _sideControls;
	private IReadOnlyList<StoryPage> _shownPages = [];
	private bool _surfaceShown;
	private bool _translatedDisplay; // the page shows the translation (Slice 110): its offsets are not the story's, so the spoken position is not used
	private int _displayVersion;
	private CancellationTokenSource? _displayCancellation; // the screen's translation: stopped when a newer one starts or the page goes away
	private ReadingMode _mode = ReadingMode.Text; // the mode on screen now, which a translation that arrives late must not undo
	private (SessionState State, int Sentence)? _sheetResume; // what the story was doing when the book sheet opened

	public ReadingPage(ReadingSession session, IStoryLibrary library, GrownUpGate gate, IServiceProvider services,
		VoicePackInstaller installer, IVoiceModelService model, SpokenLanguage spoken)
	{
		InitializeComponent();
		_spoken = spoken;
		_session = session;
		_library = library;
		_gate = gate;
		_services = services;
		_installer = installer;
		_model = model;
		_session.SentenceChanged += OnSentenceChanged;
		_session.StateChanged += OnSessionStateChanged;
		_session.Failed += OnReadFailed;
		_session.TranslationSkipped += (_, _) => _spoken.Use(_story?.Language ?? UiLanguage.English);
		BookInfo.OriginalChanged += SaveShowOriginalAsync;
		BookInfo.CastRequested += OpenCastAsync;
		BookInfo.Dismissed += OnBookSheetDismissed;
		ShowPackStatus();
	}

	/// <summary>Opens the page on <paramref name="story"/>. Called once, right after the page is created.</summary>
	public void Load(Story story)
	{
		_story = story;
		TitleLabel.Text = story.Title;
		_spoken.Use(story);
		_sentences = StorySentences.Build(story.Text, LanguageRules.For(story.Language), story.BreakMap());
		_session.Open(story, _sentences, SaveCastAsync);
		_theme = CurrentTheme();
		_mode = story.Mode;
		Modes.Set(story.Mode, _theme);
		ApplyTheme();
		_ = ShowStoryAsync();
		if (_sentences.Count > 0)
		{
			_session.MoveTo(0);
		}
	}

	// Slice 110, Decision 112: while the story is read in the grown-up's language the page shows the translation too, unless the
	// grown-up chose "Show original text". The source shows at once and the translation replaces it when ready (one rebuild); it stays when
	// there is nothing to translate or a language model is missing. A newer call supersedes an older one.
	private async Task ShowStoryAsync()
	{
		try
		{
			await ShowStoryCoreAsync();
		}
		catch (OperationCanceledException)
		{
			// A newer call, or the page leaving, superseded this one.
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not show the story: {ex}");
		}
	}

	private async Task ShowStoryCoreAsync()
	{
		if (_story is not { } story)
		{
			return;
		}

		int version = ++_displayVersion;
		CancellationToken cancellation = RestartDisplayCancellation();
		var translator = _services.GetRequiredService<StoryTranslator>();
		if (story.ShowOriginalText || !translator.Applies(story))
		{
			ShowSource(story);
			return;
		}

		if (!_surfaceShown)
		{
			ShowSource(story);
		}

		(TranslatedStory? translated, string title) = await TranslateForScreenAsync(story, translator, cancellation);
		if (translated is not null && version == _displayVersion && !cancellation.IsCancellationRequested)
		{
			ShowTranslation(story, translated, title);
		}
	}

	private CancellationToken RestartDisplayCancellation()
	{
		CancelDisplay();
		_displayCancellation = new CancellationTokenSource();
		return _displayCancellation.Token;
	}

	private void CancelDisplay()
	{
		_displayCancellation?.Cancel();
		_displayCancellation?.Dispose();
		_displayCancellation = null;
	}

	// The title and the text translate side by side; the voice shares the engine's pieces with the text (StoryTranslator), so none is translated twice.
	private async Task<(TranslatedStory? Translated, string Title)> TranslateForScreenAsync(
		Story story, StoryTranslator translator, CancellationToken cancellation)
	{
		try
		{
			var voicing = _services.GetRequiredService<StoryVoicing>();
			Task<string> title = TitleOrSourceAsync(story, translator, cancellation);
			TranslatedStory? translated = await voicing.TranslatedDisplayAsync(story, _sentences, cancellation);
			return (translated, translated is null ? story.Title : await title);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Debug.WriteLine($"Dino Reader could not translate the page: {ex}");
			return (null, story.Title);
		}
	}

	// The title is cosmetic: any failure of its own leaves the source title and never costs the translated text.
	private static async Task<string> TitleOrSourceAsync(Story story, StoryTranslator translator, CancellationToken cancellation)
	{
		try
		{
			return await translator.TitleAsync(story, cancellation);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Debug.WriteLine($"Dino Reader could not translate the title: {ex}");
			return story.Title;
		}
	}

	private void ShowSource(Story story)
	{
		_translatedDisplay = false;
		_shownPages = story.Pages;
		ApplyTitle(story.Title, story.Language);
		var content = new ReadingContent(story.Text, _sentences, DinoFonts.ReadingFor(story.Language),
			DinoFonts.IsRightToLeft(story.Language), ScansOf(story));
		ShowContent(content, story);
	}

	private void ShowTranslation(Story story, TranslatedStory translated, string title)
	{
		string mine = StoryTranslator.MyLanguage;
		_translatedDisplay = true;
		_shownPages = translated.Pages;
		ApplyTitle(title, mine);
		var scans = story.HasPictures ? new ScanSource(translated.Pages, file => _library.TryResolveFile(story.Id, file)) : null;
		var content = new ReadingContent(translated.Text, translated.Sentences, DinoFonts.ReadingFor(mine), DinoFonts.IsRightToLeft(mine), scans);
		ShowContent(content, story);
	}

	// The title is set in the language it is shown in: right to left for Farsi and Arabic, with the page itself not mirrored (Slice 96).
	private void ApplyTitle(string title, string language)
	{
		TitleLabel.Text = title;
		TitleLabel.FlowDirection = DinoFonts.IsRightToLeft(language) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
	}

	private void ShowContent(ReadingContent content, Story story)
	{
		Surface.Show(content, _mode, _theme);
		_surfaceShown = true;
		if (_session.CurrentSentence >= 0)
		{
			Surface.Mark(_session.CurrentSentence, DisplayedPosition);
		}

		UpdateHeaderLayout(Width, Height);
		RenderChrome();
	}

	// A translated page has its own text offsets, so only the sentence (never the story index inside it) is followed.
	private int DisplayedPosition => _translatedDisplay ? -1 : _session.CurrentPosition;

	// A story with pictures draws its real pages in picture mode; one without keeps the generated pages.
	private ScanSource? ScansOf(Story story) =>
		story.HasPictures ? new ScanSource(story.Pages, file => _library.TryResolveFile(story.Id, file)) : null;

	// Slice 98 (mock screen 35): a picture + text book held sideways has the title on the top bar beside the back button
	// (one line, ellipsis) and no page dots, to save height.
	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		UpdateHeaderLayout(width, height);
	}

	private void UpdateHeaderLayout(double width, double height)
	{
		bool compact = width > 0 && width > height && PictureTextBook.IsPictureTextBook(_shownPages);
		bool sideControls = compact && Surface.Mode == ReadingMode.Picture;
		if (compact != _compactHeader || sideControls != _sideControls)
		{
			_compactHeader = compact;
			_sideControls = sideControls;
			ApplyHeaderLayout();
			ApplyTransportLayout();
			RenderChrome();
		}
	}

	// Mock screen 35: in picture mode the transport row sits under the text half on the right, and the page card runs the
	// full height (the text card leaves room for the controls). Otherwise the row is centred below the page as usual.
	private void ApplyTransportLayout()
	{
		Root.ColumnDefinitions = _sideControls
			? [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)]
			: [];
		Grid.SetColumnSpan(Header, _sideControls ? 2 : 1);
		Grid.SetColumnSpan(SurfaceHost, _sideControls ? 2 : 1);
		Grid.SetRowSpan(SurfaceHost, _sideControls ? 4 : 1);
		Grid.SetColumn(Transport, _sideControls ? 1 : 0);
		Transport.Compact = _sideControls;
		SurfaceHost.Margin = _sideControls ? new Thickness(14, 6, 14, 8) : new Thickness(14, 6, 14, 0);
	}

	private void ApplyHeaderLayout()
	{
		Grid.SetRow(TitleLabel, _compactHeader ? 0 : 1);
		Grid.SetColumn(TitleLabel, _compactHeader ? 1 : 0);
		Grid.SetColumnSpan(TitleLabel, _compactHeader ? 1 : 4);
		TitleLabel.FontSize = _compactHeader ? CompactTitleSize : FullTitleSize;
		TitleLabel.MaxLines = _compactHeader ? 1 : -1;
		TitleLabel.LineBreakMode = _compactHeader ? LineBreakMode.TailTruncation : LineBreakMode.WordWrap;
		TitleLabel.VerticalOptions = _compactHeader ? LayoutOptions.Center : LayoutOptions.Fill;
		// The title shares row 0 when compact, so the second row and the gap before it must not take any height.
		Header.RowDefinitions[1].Height = _compactHeader ? new GridLength(0) : GridLength.Auto;
		Header.RowSpacing = _compactHeader ? 0 : FullHeaderRowSpacing;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_visible = true;
		Unsubscribe();
		_session.Attach();
		_installer.Changed += OnInstallerChanged;
		_model.ReadyChanged += OnInstallerChanged;
		SystemTheme.Changed += OnThemeChanged;
		ShowPackStatus();
		if (CurrentTheme() != _theme)
		{
			_theme = CurrentTheme();
			ApplyTheme();
		}
	}

	// Disappearing also fires for a locked screen, another app or the gate over this page; the read
	// carries on then (it is audio). Only a page that has left the navigation stack stops it. The
	// check runs one tick later so it doesn't depend on whether the pop has already removed the page.
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_visible = false;
		Dispatcher.Dispatch(() =>
		{
			if (!Navigation.NavigationStack.Contains(this))
			{
				CancelDisplay();
				Unsubscribe();
				_session.Detach();
				_session.Stop();
			}
		});
	}

	private void Unsubscribe()
	{
		_installer.Changed -= OnInstallerChanged;
		_model.ReadyChanged -= OnInstallerChanged;
		SystemTheme.Changed -= OnThemeChanged;
	}

	// The app pins UserAppTheme to Light (kids' palette), so RequestedTheme is always Light and MAUI raises no
	// theme event; the night colours follow the system's own theme through SystemTheme.IsDark
	// (PlatformAppTheme on Android, UISettings on Windows), signalled by SystemTheme.
	private static ReadingTheme CurrentTheme() =>
		ReadingTheme.For(SystemTheme.IsDark);

	private void OnThemeChanged()
	{
		_theme = CurrentTheme();
		ApplyTheme();
	}

	private void ApplyTheme()
	{
		BackgroundColor = _theme.Page;
		if (_visible)
		{
			SystemBarIcons.ForPage(this);
		}

		TitleLabel.TextColor = _theme.Ink;
		BackIcon.Color = _theme.BackIcon;
		LockButton.BackgroundColor = _theme.ControlFill;
		LockIcon.Color = _theme.ControlIcon;
		LockIcon.BadgeColor = _theme.SwitchActive;
		LockIcon.PlusColor = _theme.SwitchActiveIcon;
		WanderPill.BackgroundColor = _theme.PillFill;
		PillLabel.TextColor = _theme.PillText;
		Modes.Set(Modes.Mode, _theme);
		Surface.SetTheme(_theme);
		RenderChrome();
	}

	private void OnSentenceChanged(object? sender, int sentence)
	{
		Surface.Mark(sentence, DisplayedPosition);
		int page = Surface.PageOfSentence(sentence, DisplayedPosition);
		if (page >= 0)
		{
			// While a layout is pending there is no page to follow; OnLaidOut catches up once it is adopted.
			_follower.Read(page);
			ShowFollowedPage();
		}

		RenderChrome();
	}

	private void ShowFollowedPage()
	{
		if (_follower.ShownPage != Surface.CurrentPage)
		{
			Surface.ShowPage(_follower.ShownPage);
		}
	}

	private void OnSessionStateChanged(object? sender, EventArgs e) => RenderChrome();

	// The pages were laid out again (a new size or mode). A follower goes with the reading; a child who
	// had wandered keeps the page that now holds what they were looking at (the surface remapped it).
	private void OnLaidOut(object? sender, EventArgs e)
	{
		int reading = Math.Max(0, Surface.PageOfSentence(_session.CurrentSentence, DisplayedPosition));
		_follower.Relaid(reading, Surface.CurrentPage);
		ShowFollowedPage();
		RenderChrome();
	}

	// After a finished read a swipe also means "from here": Play then starts at the shown page.
	private void OnPageSwiped(object? sender, int page)
	{
		_session.ClearFinished();
		_follower.Swiped(page);
		RenderChrome();
	}

	// Dots, the wander pill and the transport row all follow the same three facts: the page shown,
	// whether the child has wandered from the page being read, and whether anything is reading.
	private void RenderChrome()
	{
		SessionState state = _session.State;
		bool wandering = _follower.IsWandering && state != SessionState.Idle;
		Dots.Show(Surface.PageCount, Surface.CurrentPage, _theme);
		Dots.IsVisible = Dots.HasDots && !_compactHeader;
		Transport.Apply(state, wandering, _theme);
		WanderPill.IsVisible = wandering;
		PillLabel.Text = ReadingText.ReadingPage(_follower.ReadingPage + 1);
	}

	private void OnModeChanged(object? sender, ReadingMode mode)
	{
		_mode = mode;
		Surface.SetMode(mode);
		UpdateHeaderLayout(Width, Height);
		_ = SaveModeAsync(mode);
	}

	// The casts made on a first read in another language are kept in story.json, on top of whatever the page has saved since (the reading mode).
	private async Task SaveCastAsync(Story withCast)
	{
		await _saveLock.WaitAsync();
		try
		{
			// Merged into the latest stored story (the cast screen may have saved edits meanwhile): only a language that has no cast there yet is added.
			Story latest = await _library.GetAsync(withCast.Id) ?? _story ?? withCast;
			Story updated = AddNewCasts(latest, withCast);
			if (!ReferenceEquals(updated, latest))
			{
				await _library.UpdateAsync(updated);
			}

			_story = _story is { } shown ? AddNewCasts(shown, updated) : updated;
		}
		finally
		{
			_saveLock.Release();
		}
	}

	private static Story AddNewCasts(Story target, Story source)
	{
		Story merged = target;
		foreach (string language in source.LanguageCasts?.Keys ?? [])
		{
			if (!merged.HasCastIn(language) && source.CastIn(language).Count > 0)
			{
				merged = merged.WithCastIn(language, source.CastIn(language));
			}
		}

		return merged;
	}

	// The choice is remembered per story in story.json. A failed save only loses the preference.
	private async Task SaveModeAsync(ReadingMode mode)
	{
		await _saveLock.WaitAsync();
		try
		{
			if (_story is { } story)
			{
				Story updated = story with { PreferredMode = mode };
				await _library.UpdateAsync(updated);
				_story = updated;
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not save the reading mode: {ex}");
		}
		finally
		{
			_saveLock.Release();
		}
	}

	private void OnSentenceTapped(object? sender, int sentence)
	{
		_follower.ResumeAt(Surface.CurrentPage);
		_ = _session.StartAtAsync(sentence);
	}

	private async void OnPlayTapped(object? sender, EventArgs e)
	{
		switch (_session.State)
		{
			case SessionState.Loading:
				_session.Stop();
				break;
			case SessionState.Playing when _follower.IsWandering:
				JumpBackToReading();
				break;
			case SessionState.Playing:
				_session.Pause();
				break;
			case SessionState.Paused:
				JumpBackToReading();
				_session.Resume();
				break;
			default:
				await StartFromPlayAsync();
				break;
		}
	}

	private void JumpBackToReading()
	{
		_follower.Resume();
		ShowFollowedPage();
		RenderChrome();
	}

	// Play from nothing reads from the top after a finished read, from the page the child swiped to,
	// else from the sentence the highlight is on.
	private async Task StartFromPlayAsync()
	{
		int sentence = _session.Finished ? 0
			: _follower.IsWandering ? Surface.FirstSentenceOnPage(_follower.ShownPage)
			: Math.Max(0, _session.CurrentSentence);
		_follower.ResumeAt(Math.Max(0, Surface.PageOfSentence(sentence)));
		await _session.StartAtAsync(sentence);
	}

	private async void OnPreviousTapped(object? sender, EventArgs e) => await SkipAsync(-1);

	private async void OnNextTapped(object? sender, EventArgs e) => await SkipAsync(1);

	// Skip moves by one sentence, the highlight unit. While nothing plays (idle or paused) it only
	// moves the mark and starts no audio; a paused read is dropped so Play starts at the mark.
	private async Task SkipAsync(int delta)
	{
		SessionState state = _session.State;
		bool playing = state is SessionState.Playing or SessionState.Loading;
		SkipPlan plan = SkipPlanner.Plan(_session.CurrentSentence, delta, _sentences.Count, playing);
		if (plan.Target < 0)
		{
			return;
		}

		_follower.ResumeAt(Math.Max(0, Surface.PageOfSentence(plan.Target)));
		if (plan.StartsAudio)
		{
			await _session.StartAtAsync(plan.Target);
			return;
		}

		if (state == SessionState.Paused)
		{
			_session.Stop();
		}

		_session.MoveTo(plan.Target);
	}

	// The "This book" sheet is part of this page, so the system back button closes it first instead of leaving the story.
	protected override bool OnBackButtonPressed()
	{
		if (BookInfo.IsVisible)
		{
			BookInfo.Dismiss();
			return true;
		}

		return base.OnBackButtonPressed();
	}

	private async void OnBackTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(() => Navigation.PopAsync());

	private async void OnLockTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(OpenGrownUpAsync);

	// The story pauses while the gate is open (a read still loading is stopped, its sentence kept) and
	// goes on if the grown-up backs out, but only when the child is looking at this page: not when the
	// gate was closed because the app went to the background, and not when something else is on top.
	private async Task OpenGrownUpAsync()
	{
		SessionState before = _session.State;
		int sentence = Math.Max(0, _session.CurrentSentence);
		PauseForGate(before);
		if (await AskGateAsync())
		{
			await OpenBookSheetOrManageAsync(before, sentence);
			return;
		}

		if (!_stoppedDuringGate && IsOnTop())
		{
			await ResumeAfterGateAsync(before, sentence);
		}
	}

	private void PauseForGate(SessionState state)
	{
		if (state == SessionState.Loading)
		{
			_session.Stop();
			return;
		}

		_session.Pause();
	}

	private async Task<bool> AskGateAsync()
	{
		_stoppedDuringGate = false;
		Window? window = Window;
		if (window is not null)
		{
			window.Stopped += OnWindowStopped;
		}

		try
		{
			return await _gate.RequestAsync(Navigation);
		}
		finally
		{
			if (window is not null)
			{
				window.Stopped -= OnWindowStopped;
			}
		}
	}

	private void OnWindowStopped(object? sender, EventArgs e) => _stoppedDuringGate = true;

	private bool IsOnTop() =>
		Navigation.ModalStack.Count == 0 && Navigation.NavigationStack.LastOrDefault() == this;

	private async Task ResumeAfterGateAsync(SessionState before, int sentence)
	{
		switch (before)
		{
			case SessionState.Playing:
				_session.Resume();
				break;
			case SessionState.Loading:
				await _session.StartAtAsync(sentence);
				break;
		}
	}

	// A book that is being translated opens the "This book" sheet (Slice 110, mock frame 43); any other book goes straight to grown-up mode as before.
	// The story stays paused under the sheet and goes on when the sheet is dismissed.
	private Task OpenBookSheetOrManageAsync(SessionState before, int sentence)
	{
		if (_story is not { } story || !_services.GetRequiredService<StoryTranslator>().Applies(story))
		{
			return GoToManageAsync();
		}

		if (_sheetResume is not null)
		{
			// The sheet is open already: keep the state recorded when it opened, so dismissing it resumes the read.
			return Task.CompletedTask;
		}

		string original = LanguageNames.NativeName(story.Language);
		string spoken = LanguageNames.NativeName(StoryTranslator.MyLanguage);
		_sheetResume = (before, sentence);
		BookInfo.Show(story.Title, offerOriginal: true, story.ShowOriginalText, ReadingText.ShowOriginalHint(original, spoken));
		return Task.CompletedTask;
	}

	private async void OnBookSheetDismissed()
	{
		if (_sheetResume is not { } resume)
		{
			return;
		}

		_sheetResume = null;
		if (!_stoppedDuringGate && IsOnTop())
		{
			await ResumeAfterGateAsync(resume.State, resume.Sentence);
		}
	}

	// "Show original text" is remembered per story in story.json; the screen follows at once, the audio stays translated.
	private async Task SaveShowOriginalAsync(bool showOriginal)
	{
		if (_story is { } shown)
		{
			_story = shown with { ShowOriginalText = showOriginal };
		}

		await _saveLock.WaitAsync();
		try
		{
			// The latest stored story and the latest switch position, whatever order quick toggles reach the lock in; cast edits saved meanwhile stay.
			if (_story is { } current)
			{
				Story latest = await _library.GetAsync(current.Id) ?? current;
				await _library.UpdateAsync(latest with { ShowOriginalText = current.ShowOriginalText });
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not save the original-text switch: {ex}");
		}
		finally
		{
			_saveLock.Release();
		}

		await ShowStoryAsync();
	}

	private async Task OpenCastAsync()
	{
		if (_story is not { } story)
		{
			return;
		}

		// The window's own navigation outlives this page, which leaves the stack (and stops reading) for the cast screen.
		INavigation? root = Window?.Page?.Navigation;
		if (root is null)
		{
			return;
		}

		await root.PopToRootAsync(animated: false);
		_services.GetRequiredService<GrownUpMode>().Enter();
		await _services.GetRequiredService<StoryActions>().CastAsync(root, story);
	}

	// Grown-up mode is the gallery's own state: return to the gallery with it on; leaving this page stops the read.
	private async Task GoToManageAsync()
	{
		await Navigation.PopToRootAsync(animated: false);
		_services.GetRequiredService<GrownUpMode>().Enter();
	}

	private async void OnReadFailed(object? sender, EventArgs e) =>
		await DisplayAlertAsync(ReadingText.CouldntRead, ReadingText.ReadFailed, ReadingText.Ok);

	private void OnInstallerChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(ShowPackStatus);

	private void OnPackStatusTapped(object? sender, TappedEventArgs e)
	{
		if (_installer.CanRetry)
		{
			_ = _installer.StartAsync();
		}
	}

	// Every non-ready pack state, Failed included, has copy (with its retry hint) in
	// PackStatusText; tapping the label retries when the installer allows it.
	private void ShowPackStatus()
	{
		string? status = LocalizedPackStatus.Compose(_installer.State, _installer.Progress);
		StatusLabel.Text = status;
		StatusLabel.IsVisible = status is not null;
	}
}
