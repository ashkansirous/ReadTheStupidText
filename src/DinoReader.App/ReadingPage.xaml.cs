using System.Diagnostics;
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.Waiting;
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
	private const double WaitCornerRadius = 18; // the picture box's corner (frame 64)
	private const double TransportWhileWaiting = 0.4; // the player row is dimmed while the wait overlay shows (frame 64)

	private readonly ReadingSession _session;
	private readonly IStoryLibrary _library;
	private readonly GrownUpGate _gate;
	private readonly IServiceProvider _services;
	private readonly VoicePackInstaller _installer;
	private readonly IVoiceModelService _model;
	private readonly SpokenLanguage _spoken;
	private readonly UsageCounter _usage;
	private bool _finishCounted;
	private bool _openCounted;
	private readonly PageFollower _follower = new();
	private readonly BusyGuard _busy = new();
	private readonly WaitOverlay _waitOverlay = DinoWait.OverPicture();
	private readonly WaitController _wait;
	private bool _loadingSeen;
	private readonly ReadWaitOwner _waitOwner = new();
	private bool _firstLoadDone; // the first Loading is the book opening; later ones are restarts the reader triggered
	private readonly SemaphoreSlim _saveLock = new(1, 1); // the cast save and the mode save each read-modify-write _story: one at a time
	private Story? _story;
	private Task? _offer; // the one translation offer of this open (see OwnTranslationOfferedAsync)
	private bool _offerKeptOriginal; // the parent declined or cancelled the pack: this open shows the book in its own language
	private bool _offering; // the offer's wait is on screen: its Cancel is not the read's
	private readonly TaskCompletionSource _appeared = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private IReadOnlyList<SentenceSpan> _sentences = [];
	private ReadingTheme _theme = ReadingTheme.For(false);
	private bool _visible;
	private bool _onStack;
	private Window? _watchedWindow;
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
		VoicePackInstaller installer, IVoiceModelService model, SpokenLanguage spoken, UsageCounter usage)
	{
		InitializeComponent();
		_spoken = spoken;
		_usage = usage;
		_session = session;
		_library = library;
		_gate = gate;
		_services = services;
		_installer = installer;
		_model = model;
		_session.SentenceChanged += OnSentenceChanged;
		_session.StateChanged += OnSessionStateChanged;
		_wait = BuildWait();
		_session.Failed += OnReadFailed;
		_session.TranslationSkipped += (_, _) => _spoken.Use(_story?.Language ?? UiLanguage.English);
		BookInfo.OriginalChanged += SaveShowOriginalAsync;
		BookInfo.CastRequested += OpenCastAsync;
		BookInfo.RecordRequested += OpenRecordAsync;
		BookInfo.VoiceChanged += SaveUseVoiceAsync;
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
		_shownPages = story.Pages;
		SyncModeSwitch();
		Modes.IsVisible = story.HasPictures; // a book without pictures has only the text view
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

		if (translator.NeedsOwnTranslation(story))
		{
			await OwnTranslationOfferedAsync(story, translator).WaitAsync(cancellation);
			story = _story ?? story;
			if (version != _displayVersion || _offerKeptOriginal || !translator.Applies(story))
			{
				return; // declined or cancelled: the book is shown in its own language this time
			}
		}

		(TranslatedStory? translated, string title) = await TranslateForScreenAsync(story, translator, cancellation);
		if (translated is not null && version == _displayVersion && !cancellation.IsCancellationRequested)
		{
			ShowTranslation(story, translated, title);
		}
	}

	// A parent's own book that has the switch on but no table (say the app language changed) is offered its translation once per open (Slice 159, Decision 164):
	// the pack question, the download and the translation run behind the page's wait with Cancel, and the table is saved inside the book. Nothing downloads unasked.
	// The display awaits this one offer, however often it restarts.
	private Task OwnTranslationOfferedAsync(Story story, StoryTranslator translator) => _offer ??= OfferOwnTranslationAsync(story, translator);

	private async Task OfferOwnTranslationAsync(Story story, StoryTranslator translator)
	{
		await _appeared.Task; // the question needs a page that is on screen
		await _saveLock.WaitAsync(); // the cast and mode saves must not write a stale copy over the table
		_offering = true;
		try
		{
			var prompt = new TranslationPackPrompt(translator, _library);
			OwnTranslationOutcome outcome = await prompt.OfferAsync(this, story.Id, (stage, line) => _wait.BeginCancellable(stage, line), EndWait);
			_offerKeptOriginal = outcome == OwnTranslationOutcome.KeptOriginal;
			if (await _library.GetAsync(story.Id) is { } latest && _story is { } shown)
			{
				_story = shown with { Translations = latest.Translations, TranslateToMyLanguage = latest.TranslateToMyLanguage };
			}
		}
		catch (Exception ex)
		{
			// Never faults or cancels: the one cached offer is awaited by every display restart of this open.
			_offerKeptOriginal = true;
			Debug.WriteLine($"Dino Reader could not offer the translation: {ex.Message}");
		}
		finally
		{
			_offering = false;
			_saveLock.Release();
		}
	}

	private CancellationToken RestartDisplayCancellation()
	{
		CancelDisplay();
		_displayCancellation = new CancellationTokenSource();
		return _displayCancellation.Token;
	}

	private IDispatcherTimer? _recordedPager;
	private ((int First, int Last) Range, int First, int Count) _recordedSpan;
	private int _recordedTurned;
	private TimeSpan _recordedPlayed;
	private TimeSpan _recordedPerPage;
	private static readonly TimeSpan RecordedPagerTick = TimeSpan.FromMilliseconds(250);

	private void CancelDisplay()
	{
		_recordedPager?.Stop();
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

	// The switch offers picture + text only when the pages on screen differ with the words (the original or the translation); a mode the switch cannot offer is folded to picture only here, so the screen and the switch agree.
	private void SyncModeSwitch()
	{
		bool offers = PictureTextBook.IsPictureTextBook(_shownPages);
		Modes.OffersPictureAndText = offers;
		if (!offers && _mode == ReadingMode.PictureAndText)
		{
			_mode = ReadingMode.PictureOnly;
		}

		Modes.Set(_mode, _theme);
	}

	// The title is set in the language it is shown in: right to left for Farsi and Arabic, with the page itself not mirrored (Slice 96).
	private void ApplyTitle(string title, string language)
	{
		TitleLabel.Text = title;
		TitleLabel.FlowDirection = DinoFonts.IsRightToLeft(language) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
	}

	private void ShowContent(ReadingContent content, Story story)
	{
		SyncModeSwitch();
		Surface.Show(content, _mode, _theme);
		_surfaceShown = true;
		if (_session.CurrentSentence >= 0)
		{
			MarkSentence(_session.CurrentSentence);
		}

		UpdateHeaderLayout(Width, Height);
		RenderChrome();
	}

	// A page that plays a parent's recording has no sentence timings, so the whole page gets one soft highlight (Slice 111, mock frame 46).
	private void MarkSentence(int sentence)
	{
		if (_session.RecordedRangeOf(sentence) is { } range)
		{
			Surface.MarkPage(range.First, range.Last, DisplayedPosition);
			return;
		}

		Surface.Mark(sentence, DisplayedPosition);
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
		bool sideControls = compact && Surface.Mode != ReadingMode.Text;
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
		_appeared.TrySetResult();
		CountOpen();
		_visible = true;
		_onStack = true;
		ApplyScreenAwake();
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
				_onStack = false;
				EndOwnedWait();
				ApplyScreenAwake();
				CancelDisplay();
				Unsubscribe();
				_session.Detach();
				_session.Stop();
				_ = _usage.FlushAsync();
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
		MarkSentence(sentence);
		FollowRecordedPages(sentence);
		int page = Surface.PageOfSentence(sentence, DisplayedPosition);
		if (page >= 0)
		{
			// While a layout is pending there is no page to follow; OnLaidOut catches up once it is adopted.
			_follower.Read(page);
			ShowFollowedPage();
		}

		RenderChrome();
	}

	// A recorded page has no sentence timings, so a page that wraps onto several display pages turns them evenly across the recording's length.
	private void FollowRecordedPages(int sentence)
	{
		_recordedPager?.Stop();
		if (_session.RecordedRangeOf(sentence) is not { } range || _session.RecordedSecondsOf(sentence) is not { } seconds)
		{
			return;
		}

		int first = Surface.PageOfSentence(range.First);
		int count = Surface.PageOfSentence(range.Last) - first + 1;
		if (first < 0 || count < 2)
		{
			return;
		}

		_recordedSpan = (range, first, count);
		_recordedTurned = 0;
		_recordedPlayed = TimeSpan.Zero;
		_recordedPerPage = TimeSpan.FromSeconds(Math.Max(1, seconds) / (double)count);
		_recordedPager ??= CreateRecordedPager();
		_recordedPager.Interval = RecordedPagerTick;
		_recordedPager.Start();
	}

	private IDispatcherTimer CreateRecordedPager()
	{
		IDispatcherTimer timer = Dispatcher.CreateTimer();
		timer.Tick += (_, _) => TurnRecordedPage();
		return timer;
	}

	private void TurnRecordedPage()
	{
		bool stillThere = _session.RecordedRangeOf(_session.CurrentSentence) is { } now && now == _recordedSpan.Range;
		if (!stillThere || _recordedTurned >= _recordedSpan.Count - 1)
		{
			_recordedPager?.Stop();
			return;
		}

		if (_session.State != SessionState.Playing)
		{
			return; // paused or loading: the recording's clock stands still, so a resume carries on in step
		}

		_recordedPlayed += RecordedPagerTick;
		if (_recordedPlayed >= _recordedPerPage * (_recordedTurned + 1))
		{
			_recordedTurned++;
			_follower.Read(_recordedSpan.First + _recordedTurned);
			ShowFollowedPage();
			RenderChrome();
		}
	}

	private void ShowFollowedPage()
	{
		if (_follower.ShownPage != Surface.CurrentPage)
		{
			Surface.ShowPage(_follower.ShownPage);
		}
	}

	private void OnSessionStateChanged(object? sender, EventArgs e)
	{
		CountFinish();
		RenderChrome();
		ApplyScreenAwake();
		ApplyWait();
	}

	// Slice 143: a read that is waiting for its first audio (the voice, or the translation it needs) shows the wait overlay after 500 ms
	// and hides it the moment audio plays, or the read stops or fails. Cancel stops the read and goes back to the shelf.
	private WaitController BuildWait()
	{
		SurfaceHost.Children.Add(_waitOverlay);
		_waitOverlay.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == VisualElement.IsVisibleProperty.PropertyName)
			{
				Transport.Opacity = _waitOverlay.IsVisible ? TransportWhileWaiting : 1;
				Transport.InputTransparent = _waitOverlay.IsVisible;
			}
		};
		_waitOverlay.SizeChanged += (_, _) =>
			_waitOverlay.Clip = new Microsoft.Maui.Controls.Shapes.RoundRectangleGeometry(WaitCornerRadius, new Rect(0, 0, _waitOverlay.Width, _waitOverlay.Height));
		var wait = new WaitController(_waitOverlay);
		wait.CancelRequested += OnWaitCancelled;
		return wait;
	}

	private void ApplyWait()
	{
		bool loading = _session.State == SessionState.Loading;
		if (loading)
		{
			_loadingSeen = true;
		}
		else
		{
			_firstLoadDone |= _loadingSeen; // only a Loading that has ended is the book opening; an Idle before it is not
		}

		// Slice 148: this page ends the one wait it began (loading or stalled) as soon as the read no longer needs it; a wait it did not begin (save, import) is left alone.
		ReadWaitAction action = _waitOwner.Next(loading, _session.Stalled, ForeignWaitActive());
		if (action == ReadWaitAction.End)
		{
			EndWait();
		}
		else if (action == ReadWaitAction.Begin)
		{
			BeginReadWait(loading);
		}
	}

	private bool ForeignWaitActive()
	{
		return _wait.IsActive && !_waitOwner.Owns;
	}

	private void BeginReadWait(bool loading)
	{
		if (!loading)
		{
			_wait.Begin(WaitStage.Stalled, AppResources.Wait_Stalled, canCancel: true);
			return;
		}

		bool translating = IsTranslating();
		WaitStage stage = translating ? WaitStage.Translating : WaitStage.PreparingVoice;
		_wait.Begin(stage, translating ? AppResources.Wait_Translating : AppResources.Wait_GettingVoices, canCancel: true); // while a wait runs this re-words it
	}

	// Cancel, or the page leaving: the overlay goes whichever wait showed, and the page owns none.
	private void EndOwnedWait()
	{
		_waitOwner.Cancelled();
		EndWait();
	}

	private void EndWait()
	{
		if (_wait.IsActive)
		{
			_wait.End();
		}
	}

	private bool IsTranslating()
	{
		return _story is { } story && _services.GetRequiredService<StoryTranslator>().Applies(story);
	}

	// Cancel always stops the read; only the wait of opening the book (the first load) also goes back to the shelf. A mid-book restart stays on the page.
	private async void OnWaitCancelled()
	{
		if (_offering)
		{
			return; // the translation offer's own Cancel: its token stops it, the read and the page stay
		}

		bool opening = !_firstLoadDone;
		try
		{
			EndOwnedWait();
			_session.Stop();
			if (opening)
			{
				await _busy.RunAsync(() => Navigation.PopAsync());
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Debug.WriteLine($"Dino Reader could not leave the page after Cancel: {ex}"); // async void: a throw here would crash the app
		}
	}

	// Slice 133: a book counts as opened once, when its page first shows (not when it is built, so a push that fails counts nothing).
	private void CountOpen()
	{
		if (_openCounted || _story is not { } story)
		{
			return;
		}

		_openCounted = true;
		_usage.Record(UsageEventName.BookOpened, story);
	}

	// Slice 133: a book counts as finished once per read to its end; reading on from a swipe or a new Play starts a new count.
	private void CountFinish()
	{
		if (_session.Finished == _finishCounted)
		{
			return;
		}

		_finishCounted = _session.Finished;
		if (_finishCounted && _story is { } story)
		{
			_usage.Record(UsageEventName.BookFinished, story);
		}
	}

	// One rule for the screen: on while the book plays and the page is still on the navigation stack (a gate or
	// sheet over it leaves the audio, and so the screen, running). Paused releases the screen on purpose: the
	// screen stays on only while the book is read aloud. Also retries the window subscription, as Window can
	// still be null in OnAppearing.
	private void ApplyScreenAwake()
	{
		WatchWindow(_onStack);
		ScreenAwake.Set(this, _onStack && ScreenAwake.Wants(_session.State));
	}

	// While the window is stopped the screen is released (audio can keep running and change state in the
	// background); coming back to the front sets it again, since iOS and a recreated Android window drop it.
	private void OnWindowStoppedReleaseScreen(object? sender, EventArgs e)
	{
		ScreenAwake.Suspend(true);
		_ = _usage.FlushAsync();
	}

	private void OnWindowResumedKeepScreen(object? sender, EventArgs e) => ScreenAwake.Suspend(false);

	private void WatchWindow(bool watch)
	{
		Window? target = watch ? Window : null;
		if (ReferenceEquals(_watchedWindow, target))
		{
			return;
		}

		if (_watchedWindow is { } old)
		{
			old.Stopped -= OnWindowStoppedReleaseScreen;
			old.Resumed -= OnWindowResumedKeepScreen;
		}

		_watchedWindow = target;
		if (target is not null)
		{
			target.Stopped += OnWindowStoppedReleaseScreen;
			target.Resumed += OnWindowResumedKeepScreen;
		}
	}

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
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"{ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.PagePrefix} {page}");
#endif
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
		SkipPlan plan = SkipPlanner.Plan(SkipFrom(delta), delta, _sentences.Count, playing);
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

	// A recorded page is one unit: skipping forward leaves from its last sentence, skipping back from its first (Slice 111).
	private int SkipFrom(int delta)
	{
		int current = _session.CurrentSentence;
		return _session.RecordedRangeOf(current) is { } range ? (delta > 0 ? range.Last : range.First) : current;
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

	// A book that is being translated, or has a recorded page, opens the "This book" sheet (Slice 110 and 111, mock frame 43); any other book goes straight to
	// grown-up mode as before. The story stays paused under the sheet and goes on when the sheet is dismissed.
	private Task OpenBookSheetOrManageAsync(SessionState before, int sentence)
	{
		bool translated = _story is { } shown && _services.GetRequiredService<StoryTranslator>().Applies(shown);
		if (_story is not { } story || !(translated || StoryRecordings.Count(story) > 0))
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
		BookInfo.Show(story.Title, translated, story.ShowOriginalText, ReadingText.ShowOriginalHint(original, spoken), VoiceOfferOf(story));
		return Task.CompletedTask;
	}

	// "Read with my voice" shows only once a page is recorded.
	private static BookSheet.VoiceOffer? VoiceOfferOf(Story story) =>
		StoryRecordings.Count(story) is var recorded && recorded > 0 ? new BookSheet.VoiceOffer(recorded, story.Pages.Count, story.UseRecordings) : null;

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

	// "Read with my voice" is remembered per story in story.json; the next read follows it, and the screen shows each page in the right language at once.
	private async Task SaveUseVoiceAsync(bool useVoice)
	{
		if (_story is { } shown)
		{
			_story = shown with { UseRecordings = useVoice };
			_session.SetUseRecordings(useVoice);
		}

		await _saveLock.WaitAsync();
		try
		{
			if (_story is { } current)
			{
				Story latest = await _library.GetAsync(current.Id) ?? current;
				await _library.UpdateAsync(latest with { UseRecordings = current.UseRecordings });
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not save the recording switch: {ex}");
		}
		finally
		{
			_saveLock.Release();
		}

		await ShowStoryAsync();
	}

	// Like the cast screen: this page leaves the stack (and stops reading) for the Record screen, in grown-up mode.
	private async Task OpenRecordAsync()
	{
		if (_story is not { } story)
		{
			return;
		}

		INavigation? root = Window?.Page?.Navigation;
		if (root is null)
		{
			return;
		}

		await root.PopToRootAsync(animated: false);
		_services.GetRequiredService<GrownUpMode>().Enter();
		await _services.GetRequiredService<StoryActions>().RecordAsync(root, story);
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
