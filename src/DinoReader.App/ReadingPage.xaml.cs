using System.Diagnostics;
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using FallbackArea = ReadTheStupidText.Application.Usage.FallbackArea;
using FallbackReason = ReadTheStupidText.Application.Usage.FallbackReason;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.Crashes;
using ReadTheStupidText.Mobile.Shared.Waiting;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>
/// The reading screen (Slice 62): the story in picture or text mode, the spoken sentence marked,
/// the page turning by itself as reading reaches it (Decision 80) until the child swipes away, and
/// a tap on a sentence reading from there. <see cref="ReadingSession"/> does the reading; the
/// <see cref="PageFollower"/> decides which page is shown.
/// </summary>
public partial class ReadingPage : ContentPage, IDinoKeyTarget
{
	private const double LanguageButtonHeight = 46;
	private const double MenuGap = 6;
	private const double CompactTitleSize = 17;
	private const double FullTitleSize = 20;
	private const double BackChipSize = 44;
	private const float ChipAlpha = 0.92f;
	private static readonly Thickness CardHeaderMargin = new(22, 14, 0, 0); // Text only held sideways: the header sits in the card's top corner
	private const double SidewaysTextSize = 26; // mock frame 93
	private const double SidewaysTextTopInset = 40; // room under the corner header before the first line
	private static readonly Thickness RailMargin = new(0, 0, 26, 0); // Slice 232: the phone's right-hand rail keeps the screen's right inset
	private static readonly Thickness SurfaceMargin = new(14, 6, 14, 0);
	private const double WaitCornerRadius = 18; // the picture box's corner (frame 64)
	private const double TransportWhileWaiting = 0.4; // the player row is dimmed while the wait overlay shows (frame 64)

	private readonly ReadingSession _session;
	private readonly IStoryLibrary _library;
	private readonly IServiceProvider _services;
	private readonly UsageCounter _usage;
	private bool _finishCounted;
	private bool _openCounted;
	private Action<bool>? _audibleHandler; // Slice 288: the same lifetime, for the listening-time tally
	private Action<Story, ReadVoiceUse, string>? _voiceUsedHandler; // Slice 267: the same lifetime, for the voice-used count
	private Action<ReadingEventKind, ReadStopReason?>? _playbackHandler; // Slice 250: subscribed while the page is on the stack, bound to the book shown then
	private readonly PageFollower _follower = new();
	private readonly BusyGuard _busy = new();
	private readonly WaitOverlay _waitOverlay = DinoWait.OverPicture();
	private readonly WaitController _wait;
	private bool _loadingSeen;
	private readonly ReadWaitOwner _waitOwner = new();
	private bool _firstLoadDone; // the first Loading is the book opening; later ones are restarts the reader triggered
	private readonly SemaphoreSlim _saveLock = new(1, 1); // the mode save read-modify-writes _story: one at a time
	private Story? _story;
	private readonly TaskCompletionSource _appeared = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private Task _languageReady = Task.CompletedTask; // the first work-out of the book's story languages (Slice 173)
	private IReadOnlyList<SentenceSpan> _sentences = [];
	private ReadingTheme _theme = ReadingTheme.For(false);
	private bool _visible;
	private bool _pieceLacksAudio; // the piece now shown has no clip: the audio status line is shown
	private readonly PackRepairCard _soundCard; // Slice 360: SS2-SS4, built in the constructor beside the SS1 pill
	private readonly HashSet<string> _missingOnPlayReported = []; // books (with language) already counted as pack_missing_on_play on this visit
#if DEBUG
	private string? _shownSoundState; // Debug marker dedupe
#endif
	private bool _onStack;
	private Window? _watchedWindow;
	private readonly BeatCoverFacts _beatCover = new(); // Slice 322: the two facts the silent beat's cover is made of; every event ends in PublishBeatCover
	private const double FullHeaderRowSpacing = 2;
	private ReadingLayout _layoutKind;
	private DeviceSize _sizeApplied; // the device size class the margins were last applied for
	private bool _railApplied; // the controls placement the layout was last applied for (height-dependent, so it can change while the kind stays)
	private IReadOnlyList<StoryPage> _shownPages = [];
	private bool _surfaceShown;
	private static readonly Dictionary<string, string> DrawnBackCovers = []; // Slice 359: story id to its drawn back-cover path once found; UI thread only
	private static readonly HashSet<string> MissingBackCoversReported = []; // Slice 359: books whose back_cover_missing fallback was already recorded this run
	private bool _hasBackCover; // the book shows the silent back cover as its last page (Slice 237)
	private bool _translatedDisplay; // the page shows the translation (Slice 110): its offsets are not the story's, so the spoken position is not used
	private int _displayVersion;
	private string? _translationShownFor; // the reading language whose translation the screen shows now (Slice 286): a refresh in that language keeps the recordings playing
	private string? _translationFailedFor; // the reading language whose translation could not be made: the next Play tries it again, so its recordings are not off for good
	private CancellationTokenSource? _displayCancellation; // the screen's translation: stopped when a newer one starts or the page goes away
	private ReadingMode _mode = ReadingMode.Text; // the mode on screen now, which a translation that arrives late must not undo
	private readonly StoryLanguageButton _languageButton; // Slice 173: the child's story-language button, shown only when this book can be read in two or more story languages
	private int _languageButtonVersion; // bumped by every UpdateLanguageButtonAsync call; a result of an older call is dropped
	private IReadOnlyList<string> _readable = []; // the story languages this book can be read in right now (with nothing to download)

	public ReadingPage(ReadingSession session, IStoryLibrary library, IServiceProvider services, UsageCounter usage)
	{
		InitializeComponent();
		DesktopTips.Set(BackButton, AppResources.Common_Back, DinoKey.Escape); // Slice 235 part B: the tooltip names the key (a no-op off Windows)
		DesktopTips.Hover(BackButton); // Slice 235 part C: hand and one-step darker fill under the mouse
		_usage = usage;
		_session = session;
		_library = library;
		_services = services;
		_session.SentenceChanged += OnSentenceChanged;
		_session.StateChanged += OnSessionStateChanged;
		_session.PieceAudio += OnPieceAudio;
		_wait = BuildWait();
		_session.Failed += OnReadFailed;
		_languageButton = new StoryLanguageButton(LanguageButtonHeight, OnLanguageTappedAsync) { IsVisible = false, AutomationId = "ReadingLanguage" };
		HeaderControls.Children.Insert(0, _languageButton);
		_soundCard = AddSoundCard();
	}

	// Slice 360 (Design SS2-SS4): the card for "no internet" / "couldn't get the sound" sits in the same strip as the SS1 pill (ReadingPage.xaml row 2); only one of the two shows at a time.
	private PackRepairCard AddSoundCard()
	{
		var card = new PackRepairCard(language => SwitchLanguageAsync(_story!, language, restart: true, remember: false), StoryLanguages.Current().Main) { IsVisible = false, Margin = new Thickness(14, 10, 14, 0) };
		Grid.SetRow(card, Grid.GetRow(StatusPill));
		((Grid)StatusPill.Parent).Children.Add(card);
		return card;
	}

	private bool _introRead; // Slice 228: opened by the intro's read step, which plays at once and goes back to the intro when the book ends
	private bool _demoRead; // Slice 228: never cleared, so the demo read counts no book open or finish even after the page started leaving
	private bool _introPopQueued; // Slice 228: the one pop back to the intro; consumed by the first Finished, cancelled by any touch or by leaving the page

	/// <summary>Slice 228: marks this page as the intro's demo read, whichever way it opens (even when the voice is not ready and nothing plays), so it counts no book open or finish. Call before the page shows.</summary>
	public void MarkDemoRead() => _demoRead = true;

	/// <summary>Slice 352: where this read was started from (a <see cref="ReadOrigins"/> name); the book shelf unless the opener says otherwise. It is the detail of <c>book_opened</c> and <c>read_started</c>.</summary>
	public string ReadOrigin { get; set; } = ReadOrigins.Shelf;

	/// <summary>Slice 228 (mock Batch 43 frame 101b): the intro's read step. Starts the book playing as soon as the page shows, and pops back to the intro when the read reaches its end. Call after <see cref="Load"/>.</summary>
	public void PlayForIntro()
	{
		_introRead = true;
		MarkDemoRead();
		_ = PlayWhenShownAsync();
	}

	// The grown-up took over (a tap, a skip) or the page is leaving: the intro read neither pops back now nor later.
	private void CancelIntroRead()
	{
		_introRead = false;
		_introPopQueued = false;
	}

	private async Task PlayWhenShownAsync()
	{
		try
		{
			await _appeared.Task;
#if DEBUG
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write("[GrownUps] intro read starts playing");
#endif
			await StartFromPlayAsync();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// no-fallback: logged only; the intro read does not start
			Debug.WriteLine($"Dino Reader could not start the intro read: {ex}");
		}
	}

	private void LeaveWhenIntroReadEnds()
	{
		if (!_introRead || !_session.Finished)
		{
			return;
		}

		_introRead = false; // one-shot: the first Finished consumes it, whether or not the page can pop now
		if (_onStack)
		{
			_introPopQueued = true;
#if DEBUG
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write("[GrownUps] intro read finished, back to the intro");
#endif
			_ = _busy.RunWhenFreeAsync(PopIntroReadAsync); // waits for a running Play/Back handler instead of being dropped
		}
	}

	private Task PopIntroReadAsync()
	{
		bool stillOurs = _introPopQueued && _onStack && Navigation.NavigationStack.LastOrDefault() == this;
		_introPopQueued = false;
		return stillOurs ? Navigation.PopAsync() : Task.CompletedTask;
	}

	/// <summary>The id of the book this page was loaded for (null before Load); the debug test hook compares it.</summary>
	public string? LoadedStoryId => _story?.Id;

	/// <summary>Opens the page on <paramref name="story"/>. Called once, right after the page is created.</summary>
	public void Load(Story story)
	{
		_story = story;
		_session.SetReadingLanguage(StoryLanguages.Current().Main); // the app language until the book says it can be read in the child's pick (OpenWithLanguagesAsync)
		TitleLabel.Text = story.Title;
		SideTitle.Text = story.Title;
		_sentences = RecordSplitBook.SentencesOf(story); // the same cut the Record screen makes, so a clip's sentence numbers match
		_session.Open(story, _sentences);
		_theme = CurrentTheme();
		_mode = story.Mode;
		_shownPages = story.Pages;
		SyncModeSwitch();
		Modes.IsVisible = story.HasPictures; // a book without pictures has only the text view
		ApplyTheme();
		_ = OpenWithLanguagesAsync();
		if (_sentences.Count > 0)
		{
			_session.MoveTo(0);
		}
	}

	// Slice 173: which story languages this book can be read in is settled first, so the screen and the voice start in the right one and the count names it.
	private async Task OpenWithLanguagesAsync()
	{
		_languageReady = SettleLanguagesAsync();
		await _languageReady;
		_ = CountOpenWhenShownAsync();
		await ShowStoryAsync();
	}

	private async Task SettleLanguagesAsync()
	{
		try
		{
			await RefreshLanguageButtonAsync();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// no-fallback: logged only; the language button keeps its last value
			Debug.WriteLine($"Dino Reader could not work out the story languages: {ex}");
		}
	}

	// A read never starts before the book's reading language is settled: Play tapped at once would otherwise read in the app language while the button shows the pick.
	private async Task StartReadAtAsync(int sentence)
	{
		await _languageReady;
		RetryFailedTranslation();
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Reading] read starts story={DinoReader.Core.BookMatch.Slug(_story?.Title ?? string.Empty)}");
#endif
		await _session.StartAtAsync(sentence);
	}

	// Slice 286: a translation that failed leaves that language's recordings off (the screen shows the story as written). Every Play tries it again in the background, once at a time, and the session
	// starts the read again with the recording when it succeeds, so the recordings are never off for good.
	private void RetryFailedTranslation()
	{
		if (_translationFailedFor is { } failed && failed == _session.ReadingLanguage)
		{
			_translationFailedFor = null;
			_ = ShowStoryAsync();
		}
	}

	private async Task CountOpenWhenShownAsync()
	{
		await _appeared.Task;
		CountOpen();
	}

	// A book that can only be read one way (an own book on Windows or without its pack, a family with one story language) has no button and reads in the app
	// language; otherwise the child's pick applies, as long as this book can be read in it. A recorded book keeps the button (Slice 286: each language has its own takes).
	private async Task RefreshLanguageButtonAsync()
	{
		if (_story is not { } story)
		{
			return;
		}

		StoryLanguageSet set = StoryLanguages.Current();
		var translator = _services.GetRequiredService<StoryTranslator>();
		bool switchable = LanguagePick.BookSwitchable(set.HasChoice);
		_readable = switchable ? await ReadableLanguagesAsync(story, set, translator) : [];
		string effective = EffectiveLanguage(set);
		_session.SetReadingLanguage(effective);
		_languageButton.SetLanguage(effective);
		_languageButton.IsVisible = _readable.Count >= 2;
		ApplyLanguageChrome();
	}

	// The child's pick when the book can be read in it (its voice, and for an own book its translation pack, are on the phone); else the app language, or when that is not on the list either the list's first language, so the button
	// never shows a flag that is not in its own list. With no choice to offer the book reads in the app language.
	private string EffectiveLanguage(StoryLanguageSet set)
	{
		return LanguagePick.Effective(_readable, set.Main, StoryLanguages.Pick());
	}

	// The languages the child may switch to (Slice 173, Decision 2): the book can be read in it with nothing to download, and the book's pre-made audio in it is already on the phone.
	// The child can never start a download; the audio comes from Settings or the intro, behind the gate (Slice 278 part B: no voice pack is involved any more).
	private async Task<IReadOnlyList<string>> ReadableLanguagesAsync(Story story, StoryLanguageSet set, StoryTranslator translator)
	{
		var readable = new List<string>();
		var shelfAudio = _services.GetRequiredService<ShelfAudioPacks>();
		foreach (string language in set.All)
		{
			if (shelfAudio.OffersBook(language, set.Main, story.SourceId) && await translator.CanReadInAsync(story, language)) // the snapshot's one rule, never the disk
			{
				readable.Add(language);
			}
		}

		return readable;
	}

	// A pack that finished, or an own book that just got its table, changes what the child can switch to; only the choices are worked out again then.
	// The open book is never re-rendered and ReadingLanguage never changes under a read: a newly installed language is used when the child picks it or opens the book again.
	private async Task UpdateLanguageButtonAsync()
	{
		if (_story is not { } story || !_onStack)
		{
			return;
		}

		int version = ++_languageButtonVersion;
		StoryLanguageSet set = StoryLanguages.Current();
		var translator = _services.GetRequiredService<StoryTranslator>();
		bool switchable = LanguagePick.BookSwitchable(set.HasChoice);
		IReadOnlyList<string> readable = switchable ? await ReadableLanguagesAsync(story, set, translator) : [];
		if (version != _languageButtonVersion || !_onStack)
		{
			return; // a newer call started meanwhile (or the page was left): only the latest result is applied
		}

		if (!readable.SequenceEqual(_readable))
		{
			StoryLanguageMenu.DismissOpen(Root); // a list open over the old choices closes (its Back path resumes the read) rather than staying orphaned
		}

		_readable = readable;
		_languageButton.IsVisible = _readable.Count >= 2;
	}

	// A book's audio pack that finished (the shelf audio run raises Changed after each book) changes what the child can switch to.
	private void OnShelfAudioChanged(object? sender, EventArgs e)
	{
		ShowAudioStatus(); // the audio status line follows the run's progress
		_ = UpdateLanguageButtonSafeAsync();
	}

	private async Task UpdateLanguageButtonSafeAsync()
	{
		try
		{
			await UpdateLanguageButtonAsync();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// no-fallback: logged only; the language button keeps its last value
			Debug.WriteLine($"Dino Reader could not refresh the story languages: {ex}");
		}
	}

	// The button sits on the same quiet fill as the view switch, and on the white chip when it floats over a picture.
	private void ApplyLanguageChrome()
	{
		_languageButton.Colour(_theme.ControlFill, _theme.BackIcon);
		_languageButton.SetOnPicture(FloatingHeader, _theme.TextCard);
	}

	// Reading pauses while the list is open; the pick restarts the sentence in the new voice (mock 89), the same language carries on.
	private async Task OnLanguageTappedAsync()
	{
		UiUsage.Tap(UiControls.Language); // Slice 352: the story-language button
		if (_story is not { } story || _readable.Count < 2)
		{
			return;
		}

		SessionState before = _session.State;
		bool wasActive = before is SessionState.Playing or SessionState.Loading;
		if (wasActive)
		{
			_session.Pause(TraceLines.ReadingReason.Other, counted: false); // the app's own pause is not a count
		}

		_languageButton.SetOpen(true);
		string? picked = await StoryLanguageMenu.PickAsync(Root, _languageButton, _readable, _session.ReadingLanguage, MenuGap);
		_languageButton.SetOpen(false);
		if (picked is null || picked == _session.ReadingLanguage)
		{
			if (wasActive && _session.State == SessionState.Paused)
			{
				_session.Resume(counted: false);
			}

			return;
		}

		await SwitchLanguageAsync(_story ?? story, picked, wasActive);
	}

	// remember = false is the "Read in {0}" button of the sound card (Slice 360): this reading only, the child's stored pick is left alone.
	private async Task SwitchLanguageAsync(Story story, string language, bool restart, bool remember = true)
	{
		int sentence = Math.Max(0, _session.CurrentSentence);
		if (remember)
		{
			StoryLanguages.SetPick(language);
		}

		_session.Stop(ReadStopReason.LanguageChanged);
		_session.SetReadingLanguage(language);
		_languageButton.SetLanguage(language);
		_usage.Record(UsageEventName.StoryLanguageSwitched, story, language: language);
		await ShowStoryAsync();
		if (restart)
		{
			await _session.StartAtAsync(sentence);
		}
	}

	// Slice 110, Decision 112: while the story is read in the grown-up's language the page shows the translation too. The source shows at once and the translation replaces it when ready (one rebuild); it stays when
	// there is nothing to translate or a language model is missing. A newer call supersedes an older one.
	private async Task ShowStoryAsync()
	{
		try
		{
			await ShowStoryCoreAsync();
		}
		catch (OperationCanceledException)
		{
			// no-fallback: cancellation, a newer call or the page leaving superseded this one
			// A newer call, or the page leaving, superseded this one.
		}
		catch (Exception ex)
		{
			// no-fallback: logged only; the screen keeps what it showed (the translation case reports in TranslateForScreenAsync)
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
		string reading = _session.ReadingLanguage;
		if (!translator.Applies(story, reading))
		{
			_translationShownFor = null;
			_translationFailedFor = null;
			_session.SetScreenTextMatchesReading(true);
			ShowSource(story);
			return;
		}

		// Slice 286: the reading language's recordings play only while the screen shows that language's words; until the translation is in they do not, and the session starts the running read again
		// when it arrives. A refresh in the language already on screen leaves the flag alone, so a running read is never cut by it.
		if (_translationShownFor != reading)
		{
			_translationShownFor = null;
			_session.SetScreenTextMatchesReading(false);
		}

		if (!_surfaceShown)
		{
			ShowSource(story);
		}

		(TranslatedStory? translated, string title) = await TranslateForScreenAsync(story, translator, reading, cancellation);
		if (version != _displayVersion || cancellation.IsCancellationRequested)
		{
			return;
		}

		if (translated is null)
		{
			_translationFailedFor = reading; // the page shows the story as written; Play tries the translation again (StartReadAtAsync)
			return;
		}

		_translationFailedFor = null;
		_translationShownFor = reading;
		_session.SetScreenTextMatchesReading(true);
		ShowTranslation(story, translated, title);
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
		Story story, StoryTranslator translator, string reading, CancellationToken cancellation)
	{
		try
		{
			var voicing = _services.GetRequiredService<StoryVoicing>();
			Task<string> title = TitleOrSourceAsync(story, translator, reading, cancellation);
			TranslatedStory? translated = await voicing.TranslatedDisplayAsync(story, _sentences, reading, cancellation);
			return (translated, translated is null ? story.Title : await title);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Debug.WriteLine($"Dino Reader could not translate the page: {ex}");
			_usage.RecordFallback(story, FallbackArea.Translation, FallbackReason.TranslationError, reading); // Slice 273 part D: the page shows the story as written
			return (null, story.Title);
		}
	}

	// The title is cosmetic: any failure of its own leaves the source title and never costs the translated text.
	private async Task<string> TitleOrSourceAsync(Story story, StoryTranslator translator, string reading, CancellationToken cancellation)
	{
		try
		{
			return await translator.TitleAsync(story, reading, cancellation);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Debug.WriteLine($"Dino Reader could not translate the title: {ex}");
			_usage.RecordFallback(story, FallbackArea.Translation, FallbackReason.TranslationError, reading); // Slice 273 part D: the title shows as written
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
		string mine = _session.ReadingLanguage;
		_translatedDisplay = true;
		_shownPages = translated.Pages;
		ApplyTitle(title, mine);
		var scans = story.HasPictures ? new ScanSource(translated.Pages, file => _library.TryResolveFile(story.Id, file), story.TextInPictures) : null;
		var content = new ReadingContent(translated.Text, translated.Sentences, DinoFonts.ReadingFor(mine), DinoFonts.IsRightToLeft(mine), scans);
		ShowContent(content, story);
	}

	// The switch offers picture + text only when the pages on screen differ with the words (the original or the translation); a mode the switch cannot offer is folded to picture only here, so the screen and the switch agree.
	private void SyncModeSwitch()
	{
		bool offers = PictureTextBook.IsPictureTextBook(_shownPages);
		Modes.OffersPictureAndText = offers;
		_mode = ReadingLayoutRules.Offered(_mode, offers);
		Modes.Set(_mode, _theme);
	}

	// The title is set in the language it is shown in: right to left for Farsi and Arabic, with the page itself not mirrored (Slice 96).
	private void ApplyTitle(string title, string language)
	{
		TitleLabel.Text = title;
		SideTitle.Text = title;
		if (!TitleLabel.IsVisible)
		{
			SemanticProperties.SetDescription(Header, title);
		}

		TitleLabel.FlowDirection = DinoFonts.IsRightToLeft(language) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
		SideTitle.FlowDirection = TitleLabel.FlowDirection;
		FitOverlayTransport(); // the tablet title and the card's lowering follow the new title at once
#if WINDOWS
		DinoTitleBar.Refresh(this); // Slice 235 part B: the window title names the book
#endif
	}

	/// <summary>The title on screen (the translated one when the book is shown translated); the Windows window title names the book with it.</summary>
	internal string? BookTitle => TitleLabel.Text;

	// Slice 235 part B (mock Batch 46 frame 160): the reading screen answers the Windows window's keyboard. The back cover is the silent last page, where Space reads the book again.
	DinoScreen IDinoKeyTarget.Screen => _hasBackCover && Surface.PageCount > 0 && Surface.CurrentPage == Surface.PageCount - 1 ? DinoScreen.ReadingBackCover : DinoScreen.Reading;

	bool IDinoKeyTarget.Handle(DinoKeyAction action, DinoKey key)
	{
		if (!_onStack || !_visible)
		{
			return false;
		}

		// Esc goes back even while the book is still loading or failed to load; only the playback keys wait for the pages to be on screen.
		if (!_surfaceShown && action != DinoKeyAction.Back)
		{
			return false;
		}

		// While the page is leaving (LeaveBookAsync holds the guard) every key does nothing: no new read may start on the page being popped.
		if (_busy.IsBusy)
		{
			return true;
		}

#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Keyboard] reading {action}");
#endif
		switch (action)
		{
			case DinoKeyAction.PlayPause:
				OnPlayTapped(this, EventArgs.Empty);
				return true;
			case DinoKeyAction.ReadAgain:
				ReadAgainFromKey();
				return true;
			case DinoKeyAction.PreviousPage:
				OnPreviousTapped(this, EventArgs.Empty);
				return true;
			case DinoKeyAction.NextPage:
				OnNextTapped(this, EventArgs.Empty);
				return true;
			case DinoKeyAction.Back:
				return BackFromKey();
			default:
				return false;
		}
	}

	// The back cover has no sentence, so Play would only start over by accident of the finished flag; the key says what it means: from the top, on the first page.
	// Like the Play handler (async void, so an exception surfaces there too instead of dying in a dropped task); a read still running is stopped first, never run beside the new one.
	private async void ReadAgainFromKey()
	{
		CancelIntroRead();
		if (_session.State is SessionState.Loading or SessionState.Playing or SessionState.Paused)
		{
			_session.Stop(ReadStopReason.Restarted);
		}

		_follower.ResumeAt(Math.Max(0, Surface.PageOfSentence(0)));
		await StartReadAtAsync(0);
	}

	// Esc does what the Back button does, except that it first closes an open story-language list, as the phone's back button does.
	private bool BackFromKey()
	{
		if (StoryLanguageMenu.DismissOpen(Root))
		{
			return true;
		}

		_ = LeaveBookAsync();
		return true;
	}

	// The one way out of the book: the on-screen Back button and the Esc key both call it, so they can never behave differently.
	// Slice 248: after the pop has finished the page is off the stack; clean up here too in case no lifecycle event saw it leave (idempotent through _onStack).
	private async Task LeaveBookAsync()
	{
		try
		{
			await _busy.RunAsync(() => Navigation.PopAsync());
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// no-fallback: logged only; the page stays open
			Debug.WriteLine($"Dino Reader could not leave the page: {ex}"); // fire-and-forget from Esc and async void from Back: a throw here would crash the app
		}
		finally
		{
			LeavePageIfOffStack();
		}
	}

	private void ShowContent(ReadingContent content, Story story)
	{
		SyncModeSwitch();
		BackCoverView? backCover = BackCoverOf(story);
		_hasBackCover = backCover is not null;
		Surface.Show(content with { Back = backCover }, _mode, _theme);
		_surfaceShown = true;
		if (_session.CurrentSentence >= 0)
		{
			MarkSentence(_session.CurrentSentence);
		}

		UpdateHeaderLayout(Width, Height);
		RenderChrome();
	}

	// Slice 237: the silent back cover, worded in the language the text is shown in; the picture is the book's cover (null for a colour cover), used by the live page of a book with no drawn back cover (Slice 359).
	private BackCoverView? BackCoverOf(Story story)
	{
		if (story.BackCover is not { } cover)
		{
			return null;
		}

		string language = _translatedDisplay ? _session.ReadingLanguage : story.Language;
		string? picture = story.CoverImageFile is { } file ? _library.TryResolveFile(story.Id, file) : null;
		return new BackCoverView(BackCoverText.Lines(cover, language), Color.FromArgb(CoverPalette.ColourAt(story.CoverColourIndex)), picture, DrawnBackCover(story, cover));
	}

	// Slice 359: the drawn picture's path; a book that names one whose file is missing shows the live page and reports the fallback (a book that names none, Good Enough, is not one).
	private string? DrawnBackCover(Story story, BackCover cover)
	{
		if (cover.PictureFile is not { } name)
		{
			return null;
		}

		if (DrawnBackCovers.TryGetValue(story.Id, out string? known))
		{
			return known; // a found picture is not looked for again on a re-render (mode or language switch)
		}

		string? path = _library.TryResolveFile(story.Id, name);
		if (path is not null && File.Exists(path))
		{
			DrawnBackCovers[story.Id] = path;
			return path;
		}

		if (MissingBackCoversReported.Add(story.Id)) // once per book per app run, so a broken book does not inflate the Slice 273 counts
		{
			_usage.RecordFallback(story, FallbackArea.Library, FallbackReason.BackCoverMissing);
		}

		return null;
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
		story.HasPictures ? new ScanSource(story.Pages, file => _library.TryResolveFile(story.Id, file), story.TextInPictures) : null;

	// The four shapes of the reading screen (Slice 98, Slice 174, mock Batch 37 frames 91-93): upright, picture only held sideways (the controls under the
	// right half), picture + text held sideways (picture 40% left with the controls floating over its bottom, text 60% on the right at full height, the
	// back button and view switch floating over the picture's corner) and text held sideways (a slim top row, the card, the controls below).
	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		(width, height) = WindowShapeSeam.Effective(DesktopTips.IsWindows, width, height); // Slice 235 part C: the Windows window picks its shape through the seam, phones and tablets pass through
		_layoutSize = new Size(width, height);
		UpdateHeaderLayout(width, height);
		FitOverlayTransport();
	}

	private ReadingLayout LayoutFor(double width, double height) => LayoutFor(width, height, Surface.Mode);

	private ReadingLayout LayoutFor(double width, double height, ReadingMode mode)
	{
		(width, height) = WindowShapeSeam.Effective(DesktopTips.IsWindows, width, height); // the same size the page was allocated, whichever caller asks
		return ReadingLayoutRules.For(width, height, mode, PictureTextBook.IsPictureTextBook(_shownPages));
	}

	private void UpdateHeaderLayout(double width, double height)
	{
		(width, height) = WindowShapeSeam.Effective(DesktopTips.IsWindows, width, height);
		ReadingLayout kind = LayoutFor(width, height);
		bool rail = kind is not (ReadingLayout.Upright or ReadingLayout.Stacked) && LandscapeLayout.ReadingControls(height) == ControlsPlacement.Rail;
		DeviceSize size = LandscapeLayout.SizeOf(width, height); // the surface margins depend on the size class too (a window resized across 600 dp)
		if (kind != _layoutKind || rail != _railApplied || size != _sizeApplied)
		{
			_layoutKind = kind;
			_railApplied = rail;
			_sizeApplied = size;
			Root.SizeChanged -= OnRootSizeChanged;
			Root.SizeChanged += OnRootSizeChanged;
			ApplyHeaderLayout();
			ApplyTransportLayout();
			RenderChrome();
		}
	}

	private void OnRootSizeChanged(object? sender, EventArgs e) => FitOverlayTransport();

	private bool Stacked => _layoutKind == ReadingLayout.Stacked;

	// Held sideways: the three landscape shapes (Slice 234 added Stacked, a tablet held upright, which is neither upright-phone nor sideways).
	private bool Sideways => _layoutKind is not (ReadingLayout.Upright or ReadingLayout.Stacked);

	// Slice 232 (mock Batch 44): the back button, view switch and flag are a chip over the picture (or in the card's corner), in every sideways view; Slice 234: and in the stacked view (frames 134, 135).
	private bool FloatingHeader => Sideways || Stacked;

	// Under 480 dp tall (a phone held sideways) the controls stand in a rail at the right; taller (a tablet) they are a row under the text.
	private bool ControlsInRail => _railApplied;

	// Where the controls and the page card go: the surface takes the whole page (less the rail), with the chip floating over its corner.
	private void ApplyTransportLayout()
	{
		bool rail = _railApplied;
		Surface.SetControls(Sideways, rail ? ControlsPlacement.Rail : ControlsPlacement.Row, Stacked);
		Root.ColumnDefinitions = rail ? [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] : [];
		Grid.SetColumnSpan(Header, 1);
		Grid.SetColumnSpan(SurfaceHost, 1);
		Grid.SetRow(SurfaceHost, FloatingHeader ? 0 : 1);
		Grid.SetRowSpan(SurfaceHost, !FloatingHeader ? 1 : 2); // sideways or stacked: over the floating header's row too, but above the status line and the dots (rows 2 and 3)
		Grid.SetRow(Transport, rail ? 0 : 4);
		Grid.SetRowSpan(Transport, rail ? 5 : 1);
		Grid.SetColumn(Transport, rail ? 1 : 0);
		Transport.Compact = rail;
		Transport.Rail = rail;
		ReadingLayoutRules.SurfaceInsets inset = Stacked ? ReadingLayoutRules.StackedSurface : SideInsets();
		SurfaceHost.Margin = !FloatingHeader ? SurfaceMargin : new Thickness(inset.Left, inset.Top, inset.Right, inset.Bottom);
		(double size, double topInset) = TextStyleFor(_layoutKind, Surface.Mode);
		Surface.SetTextStyle(size, topInset);
		Transport.Wide = Stacked;
		FitOverlayTransport();
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] reading controls={(rail ? "rail" : FloatingHeader ? "row" : "upright")} layout={_layoutKind}");
#endif
	}

	// The text size and the room above the first line for a layout in a mode: text held sideways 26 (mock 93), text stacked 36 (mock 135, room for the chip), else the phone's size.
	private static (double Size, double TopInset) TextStyleFor(ReadingLayout kind, ReadingMode mode) => kind switch
	{
		ReadingLayout.TextRow => (SidewaysTextSize, SidewaysTextTopInset),
		ReadingLayout.Stacked when mode == ReadingMode.Text => (ReadingLayoutRules.StackedTextSizeFor(mode), ReadingLayoutRules.StackedTextOnlyTopInset),
		_ => (ReadingMetrics.FontSize, 0),
	};

	// Held sideways on a tablet the row sits under the text card, not under the picture; the rail keeps the screen's right inset.
	private void FitOverlayTransport()
	{
		double cardLeft = Sideways ? HeldTextCardLeft() : 0;
		ApplySideTitle(cardLeft);
		if (ControlsInRail)
		{
			Transport.Margin = RailMargin;
			return;
		}

		bool underText = Sideways && _layoutKind == ReadingLayout.PictureOverlay;
		Thickness margin = underText ? new Thickness(SideInsets().Left + cardLeft, 0, 0, 0) : default;
		if (Transport.Margin != margin)
		{
			Transport.Margin = margin;
		}
	}

	// The size of the last layout pass (the one UpdateHeaderLayout used), so the insets never read a Width/Height that moved on since.
	private Size _layoutSize;

	private ReadingLayoutRules.SurfaceInsets SideInsets() =>
		ReadingLayoutRules.SidewaysSurface(_layoutSize.Width, _layoutSize.Height, ControlsInRail ? ControlsPlacement.Rail : ControlsPlacement.Row);

	// Mock frame 127: on a tablet (controls a row) the book title stands above the text card, in the column beside the picture; PictureTextPages lowers the card by the same title block.
	private void ApplySideTitle(double cardLeft)
	{
		ControlsPlacement controls = ControlsInRail ? ControlsPlacement.Rail : ControlsPlacement.Row;
		bool drawsCard = Surface.FirstSentenceOnPage(Surface.CurrentPage) >= 0;
		bool hasTitle = !string.IsNullOrWhiteSpace(SideTitle.Text);
		bool show = ReadingLayoutRules.ShowsTabletTitle(_layoutKind, controls, cardLeft > 0, drawsCard, hasTitle);
		SideTitle.TextColor = _theme.Ink;
		SideTitle.Margin = new Thickness(cardLeft, 0, 0, 0);
		if (SideTitle.IsVisible != show)
		{
			SideTitle.IsVisible = show;
#if DEBUG
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] reading tablet title={(show ? "shown" : "hidden")}");
#endif
		}

		Surface.SetCardTop(CardTopFor(show)); // the one value: it and the title's visibility come from the same `show`
	}

	// The title is one line with a tail ellipsis whose height follows the font (system font scale); the card is lowered by that same measured line.
	private double CardTopFor(bool titleShown)
	{
		double measured = titleShown ? SideTitle.Measure(Math.Max(1, SurfaceHost.Width - SideTitle.Margin.Left), double.PositiveInfinity).Height : 0;
		return ReadingLayoutRules.CardTop(titleShown, ReadingLayoutRules.TitleLine(measured));
	}

	// The text card's left edge on the page held, worked out here (outside any measure pass) by the same LandscapeLayout function the page's own columns use,
	// from the held page's own picture and the surface's size.
	private double HeldTextCardLeft()
	{
		int page = Surface.CurrentPage;
		if (_story is not { } story || page < 0 || page >= _shownPages.Count || Surface.LaidOutFor.Width <= 0 || Surface.LaidOutFor.Height <= 0)
		{
			return 0;
		}

		StoryPage held = _shownPages[page];
		string? image = held.ImageFile is null ? null : _library.TryResolveFile(story.Id, held.ImageFile);
		double? aspect = image is null ? null : AspectOf(image);
		bool pictureOnly = PictureTextPages.IsPictureOnly(Surface.Mode == ReadingMode.PictureAndText, story.TextInPictures, held);
		return LandscapeLayout.TextCardLeft(Surface.LaidOutFor.Width, Surface.LaidOutFor.Height, aspect, pictureOnly);
	}

	// One file read per picture: resize, rotation and swipes ask again for the same held page, so the last answer is kept by path.
	private (string Path, double Aspect) _heldAspect;

	private double AspectOf(string image)
	{
		if (_heldAspect.Path == image)
		{
			return _heldAspect.Aspect;
		}

		double aspect = PageImageSize.Read(image) is { Height: > 0 } size ? size.Width / size.Height : 0;
		_heldAspect = (image, aspect);
		return aspect;
	}

	private void ApplyHeaderLayout()
	{
		bool floating = FloatingHeader;
		bool compact = false;
		bool noTitle = FloatingHeader;
		Grid.SetRow(TitleLabel, compact ? 0 : 1);
		Grid.SetColumn(TitleLabel, compact ? 1 : 0);
		Grid.SetColumnSpan(TitleLabel, compact ? 1 : 3);
		TitleLabel.IsVisible = !noTitle;
		TitleLabel.FontSize = compact ? CompactTitleSize : FullTitleSize;
		TitleLabel.MaxLines = compact ? 1 : -1;
		TitleLabel.LineBreakMode = compact ? LineBreakMode.TailTruncation : LineBreakMode.WordWrap;
		TitleLabel.VerticalOptions = compact ? LayoutOptions.Center : LayoutOptions.Fill;
		// The title shares row 0 when compact (and is gone when floating), so the second row and the gap before it must not take any height.
		Header.RowDefinitions[1].Height = compact || noTitle ? new GridLength(0) : GridLength.Auto;
		Header.RowSpacing = compact || noTitle ? 0 : FullHeaderRowSpacing;
		Header.HorizontalOptions = floating ? LayoutOptions.Start : LayoutOptions.Fill;
		Header.VerticalOptions = floating ? LayoutOptions.Start : LayoutOptions.Fill;
		Header.Margin = HeaderMargin();
		ApplyHeaderHeading(noTitle);
		ApplyBackChip();
	}

	// The chip's place: stacked, 12 dp in from the top left of the picture panel or the text card (frames 134, 135); text held sideways, the card's corner; sideways with a picture, the picture's corner.
	private Thickness HeaderMargin()
	{
		if (Stacked)
		{
			ReadingLayoutRules.SurfaceInsets chip = ReadingLayoutRules.StackedChip;
			return new Thickness(chip.Left, chip.Top, 0, 0);
		}

		return _layoutKind == ReadingLayout.TextRow ? CardHeaderMargin : Sideways ? new Thickness(SideInsets().Left, ReadingLayoutRules.SidewaysTop, 0, 0) : default;
	}

	// With no visible title the header itself is the screen's level-1 heading, so a screen reader still hears which book this is.
	private void ApplyHeaderHeading(bool noTitle)
	{
		SemanticProperties.SetHeadingLevel(Header, noTitle ? SemanticHeadingLevel.Level1 : SemanticHeadingLevel.None);
		SemanticProperties.SetDescription(Header, noTitle ? TitleLabel.Text : null);
	}

	// Floating over the picture or in the card's corner the back button is a 44 px white circle (mock 92), and the view switch sits on the same chip, so both read on any picture.
	private void ApplyBackChip()
	{
		Modes.OnPicture = FloatingHeader;
		BackButton.BackgroundColor = FloatingHeader ? _theme.TextCard.WithAlpha(ChipAlpha) : Colors.Transparent;
		BackButton.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = BackChipSize / 2 };
		BackButton.Shadow = FloatingHeader ? new Shadow { Brush = Brush.Black, Opacity = 0.18f, Radius = 6, Offset = new Point(0, 2) } : null;
		ApplyLanguageChrome();
	}

	// Back closes the language list first; only then does it leave the book.
	protected override bool OnBackButtonPressed() => StoryLanguageMenu.DismissOpen(Root) || base.OnBackButtonPressed();

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_appeared.TrySetResult();
		_visible = true;
		_onStack = true;
		_missingOnPlayReported.Clear(); // once per book and language per visit, so a page kept alive between visits counts each one
		_beatCover.Appeared();
		ApplyScreenAwake(); // (re)subscribes to the window; a new subscription reads its real state once
		PublishBeatCover();
		Unsubscribe();
		SubscribePlayback();
		SystemTheme.Changed += OnThemeChanged;
		var shelfAudio = _services.GetRequiredService<ShelfAudioPacks>();
		shelfAudio.Changed += OnShelfAudioChanged;
		shelfAudio.Repair.Changed += OnRepairChanged;
		_ = shelfAudio.RescanAsync(); // a book opened: its language choices follow the packs on the phone
		RepairMissingSound(); // Slice 360, Decision 2: the book's missing packs are tried now, on any network
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
		_introPopQueued = false; // a pop that has not run yet must not fire once the page is covered or leaving
		_beatCover.Disappeared();
		PublishBeatCover(); // a piece shown for a beat has no sound, so it must not run on unseen
		Dispatcher.Dispatch(LeavePageIfOffStack);
	}

	// Slice 248: on iOS a PopAsync raises OnDisappearing before the page leaves the stack, so the check above still sees it there.
	// OnNavigatedFrom comes after the removal on both platforms; whichever of the two sees the page off the stack first cleans up.
	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		LeavePageIfOffStack();
	}

	private void LeavePageIfOffStack()
	{
		if (_onStack && !Navigation.NavigationStack.Contains(this))
		{
			LeavePage();
		}
	}

	// Runs once per visit: _onStack is set only by OnAppearing and cleared here.
	private void LeavePage()
	{
		_onStack = false;
		EndOwnedWait();
		ApplyScreenAwake();
		CancelDisplay();
		Unsubscribe();
		_session.Detach();
		if (_story is { } leftBook)
		{
			RecordBookCount(UsageEventName.BookLeft, leftBook); // Slice 250: before the stop, so a session reads "book left, read stopped (left_book)"
		}

		_session.Stop(ReadStopReason.LeftBook);
		UnsubscribePlayback(); // after the stop, so "read stopped (left_book)" is still counted; a late callback after the page left counts nothing
		_usage.EndReading(); // Slice 288: the one place that closes a reading on leaving: a still-open one gets its read_stopped with the seconds, then audio is no longer heard
		_ = _usage.FlushAsync();
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write("[Reading] left the page, read stopped");
#endif
	}

	private void Unsubscribe()
	{
		SystemTheme.Changed -= OnThemeChanged;
		var shelfAudio = _services.GetRequiredService<ShelfAudioPacks>();
		shelfAudio.Changed -= OnShelfAudioChanged;
		shelfAudio.Repair.Changed -= OnRepairChanged;
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
		SideTitle.TextColor = _theme.Ink;
		BackIcon.Color = _theme.BackIcon;
		ApplyBackChip();
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
			FitOverlayTransport();
		}
	}

	private void OnSessionStateChanged(object? sender, EventArgs e)
	{
		CountFinish();
		LeaveWhenIntroReadEnds();
		RenderChrome();
		ApplyScreenAwake();
		ApplyWait();
		ShowAudioStatus();
	}

	private void OnPieceAudio(bool hasClip)
	{
		_pieceLacksAudio = !hasClip;
		ShowAudioStatus();
	}

	// A piece with no clip is only shown for a beat, so the page says why there is no sound (Slice 360, Design SS1-SS4): the book's sound is downloading (SS1, the percent), there is no internet (SS2, or SS4
	// with nothing to switch to), the pack is missing on the server or failed (SS3), or the book simply has none. Shown while a read is going and its current piece has no clip; the intro's demo read is this same page.
	private void ShowAudioStatus()
	{
		if (_session.State == SessionState.Idle)
		{
			_pieceLacksAudio = false; // a stopped read says nothing; the next read's first piece sets it again
		}

		if (!_pieceLacksAudio || _story is not { } book)
		{
			HideAudioStatus();
			return;
		}

		string language = ReadIn(book);
		string nativeName = LanguageNames.NativeName(language);
		PackSoundView view = PackView(book, language);
		ReportMissingOnPlay(book, language, view);
		LogSoundState(view);
		switch (view.Kind)
		{
			case PackSoundKind.Getting:
				_soundCard.IsVisible = false;
				StatusPill.Show(new SoundStatusPillState(SoundStatusKind.Getting, view.Percent, PercentTotal), AppResources.Format(AppResources.Reading_GettingBookSound, nativeName, view.Percent), nativeName);
				StatusPill.IsVisible = true;
				break;
			case PackSoundKind.NoInternet:
			case PackSoundKind.Failed:
				StatusPill.IsVisible = false;
				_soundCard.Show(view, nativeName, view.SwitchTo is { } other ? LanguageNames.NativeName(other) : null);
				_soundCard.IsVisible = true;
				break;
			default:
				_soundCard.IsVisible = false;
				StatusPill.Show(new SoundStatusPillState(SoundStatusKind.NoSound), AppResources.Reading_NoSound, nativeName);
				StatusPill.IsVisible = true;
				break;
		}
	}

	private const int PercentTotal = 100;

	private void HideAudioStatus()
	{
		StatusPill.IsVisible = false;
		_soundCard.IsVisible = false;
	}

	// Never promises sound that is not coming: a book with no store id (an own book) is "no sound"; a store book's pack is judged by the repair state (a try running, no internet, a failed or missing pack).
	// "Read in {other}" is offered only for a family language whose sound for this book is on the phone (the language button's own list).
	private PackSoundView PackView(Story book, string language)
	{
		if (book.SourceId is not { } id)
		{
			return PackSoundView.Nothing;
		}

		var shelfAudio = _services.GetRequiredService<ShelfAudioPacks>();
		string? switchTo = PackSoundView.OtherInstalledLanguage(_readable, language, other => shelfAudio.HasPack(other, id));
		// The one expectation rule decides whether a pack is coming (known listing, else a store book in a language other than its own); an installed or not-expected pack says nothing.
		return shelfAudio.Repair.ViewOf(id, language, switchTo, shelfAudio.ExpectationFor(book, language), shelfAudio.HasPack(language, id));
	}

	// Slice 360: the case this slice is about shows in the data, once per book and language per visit.
	private void ReportMissingOnPlay(Story book, string language, PackSoundView view)
	{
		if (view.Kind is (PackSoundKind.NoInternet or PackSoundKind.Failed) && _missingOnPlayReported.Add($"{book.SourceId}/{language}"))
		{
			_usage.RecordFallback(book, FallbackArea.PremadeAudio, FallbackReason.PackMissingOnPlay, language);
		}
	}

	private void LogSoundState(PackSoundView view)
	{
#if DEBUG
		string state = $"kind={view.Kind} percent={view.Percent} switch={(view.HasSwitch ? "yes" : "no")}";
		if (state != _shownSoundState)
		{
			_shownSoundState = state;
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Reading] sound-state {state}"); // Slice 360: a flow reads which of the four states the page chose
		}
#endif
	}

	// Slice 360 (Decision 2): opening a book whose sound is missing in a family language tries its packs at once, ignoring the hour; the repair state shows the result on the page.
	private void RepairMissingSound()
	{
		if (_story is not { SourceId: { } id } book || _demoRead)
		{
			return;
		}

		var shelfAudio = _services.GetRequiredService<ShelfAudioPacks>();
		IReadOnlyList<string> missing = shelfAudio.MissingExpectedLanguages(book);
		if (missing.Count > 0)
		{
			shelfAudio.Repair.OnBookOpened(id, missing); // even with no listing: offline it records Offline entries, so the network coming back retries
		}
	}

	private void OnRepairChanged() => MainThread.BeginInvokeOnMainThread(ShowAudioStatus);

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
		wait.ShownChanged += shown => ReadStartTrace.Write(shown ? $"wait overlay shown ({wait.Stage})" : "wait overlay hidden"); // Slice 259 item 1: Debug trace, proves the 500 ms rule on a device
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
		ReadWaitAction action = _waitOwner.Next(loading, stalled: false, ForeignWaitActive()); // nothing stalls any more: no engine builds audio while the read waits
		if (action == ReadWaitAction.End)
		{
			EndWait();
		}
		else if (action == ReadWaitAction.Begin)
		{
			BeginReadWait();
		}
	}

	private bool ForeignWaitActive()
	{
		return _wait.IsActive && !_waitOwner.Owns;
	}

	private void BeginReadWait()
	{
		bool translating = IsTranslating();
		WaitStage stage = translating ? WaitStage.Translating : WaitStage.PreparingVoice;
		_wait.Begin(stage, translating ? AppResources.Wait_Translating : AppResources.Wait_GettingBook, canCancel: true); // while a wait runs this re-words it
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
		return _story is { } story && _services.GetRequiredService<StoryTranslator>().Applies(story, _session.ReadingLanguage);
	}

	// Cancel always stops the read; only the wait of opening the book (the first load) also goes back to the shelf. A mid-book restart stays on the page.
	private async void OnWaitCancelled()
	{
		bool opening = !_firstLoadDone;
		try
		{
			EndOwnedWait();
			_session.Stop(ReadStopReason.Cancelled);
			if (opening)
			{
				await _busy.RunAsync(() => Navigation.PopAsync());
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// no-fallback: logged only; the page stays open
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
		RecordBookCount(UsageEventName.BookOpened, story, ReadOrigin); // Slice 352: the open says where it was started from
	}

	// Slice 173: a book counts in the language it was read in (the child's pick), not the book's own.
	private string ReadIn(Story story) => _services.GetRequiredService<StoryTranslator>().SpokenLanguage(story, _session.ReadingLanguage);

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
			RecordBookCount(UsageEventName.BookFinished, story);
		}
	}

	// Slice 250: each playback verb the session raises is one count about this book; a stop's detail is its closed reason.
	private void SubscribePlayback()
	{
		UnsubscribePlayback();
		if (_story is not { } book)
		{
			return;
		}

		_playbackHandler = (kind, reason) => OnPlayback(book, kind, reason);
		_session.Playback += _playbackHandler;
		_voiceUsedHandler = (voiced, use, reading) => OnVoiceUsed(book, voiced, use, reading);
		_session.VoiceUsed += _voiceUsedHandler;
		_audibleHandler = _usage.SetAudible; // Slice 288: the listening-time tally runs on the session's audio output alone
		_session.Audible += _audibleHandler;
		_usage.SetAudible(_session.AudibleNow); // a flip while unsubscribed raised nothing here: read the current state once
	}

	private void UnsubscribePlayback()
	{
		if (_playbackHandler is not null)
		{
			_session.Playback -= _playbackHandler;
			_playbackHandler = null;
		}

		if (_voiceUsedHandler is not null)
		{
			_session.VoiceUsed -= _voiceUsedHandler;
			_voiceUsedHandler = null;
		}

		if (_audibleHandler is not null)
		{
			_session.Audible -= _audibleHandler;
			_audibleHandler = null;
			_usage.SetAudible(false); // no flip reaches the tally while unsubscribed; the next subscribe reads the session's state again
		}
	}

	// Slice 267: one count per read, naming the voice it used (a book's own reads only; the intro's demo read counts no book event).
	// Slice 273: it names the pieces read by clips and by the device voice (never "premade" for a read that fell back part-way, Slice 272), and a read that fell back also sends the fallback count.
	// Slice 297: the language is the one the read started with (the session carries it to the first clip), not whatever the reading language is when the count is made.
	private void OnVoiceUsed(Story book, Story voiced, ReadVoiceUse use, string reading)
	{
		if ((ReferenceEquals(book, voiced) || book.Id == voiced.Id) && DemoReadCounting.Counts(UsageEventName.ReadVoice, _demoRead))
		{
			string language = _services.GetRequiredService<StoryTranslator>().SpokenLanguage(book, reading);
			_usage.Record(UsageEventName.ReadVoice, book, use.Detail, language, use.ClipPieces, use.DevicePieces);
			if (use.FallbackReason is { } reason)
			{
				_usage.RecordFallback(book, FallbackArea.PremadeAudio, reason, language);
			}
		}
	}

	private void OnPlayback(Story story, ReadingEventKind kind, ReadStopReason? reason)
	{
		string name = ReadPlaybackNames.EventOf(kind);
		if (DemoReadCounting.Counts(name, _demoRead))
		{
			_usage.Record(name, story, reason is { } why ? ReadPlaybackNames.DetailOf(why) : name == UsageEventName.ReadStarted ? ReadOrigin : string.Empty, ReadIn(story)); // Slice 352: a start says where the read began
		}
	}

	// Slice 228: the intro's demo read is not a real reading of the book, so it counts no open or finish (the one rule is DemoReadCounting).
	private void RecordBookCount(string name, Story story, string detail = "")
	{
		if (DemoReadCounting.Counts(name, _demoRead))
		{
			_usage.Record(name, story, detail, ReadIn(story));
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
		_beatCover.WindowStopped(); // Slice 322: a lock raises Window.Stopped, not OnDisappearing, on Android. The event wins; the platform state is never re-read here (it lags the event).
		PublishBeatCover();
		_ = _usage.FlushAsync();
	}

	private void OnWindowActivatedShowBeat(object? sender, EventArgs e)
	{
		_beatCover.WindowActivated(); // a recreated activity gets onResume (Activated) with no onRestart (Resumed)
		PublishBeatCover();
	}

	private void OnWindowResumedKeepScreen(object? sender, EventArgs e)
	{
		ScreenAwake.Suspend(false);
		_beatCover.WindowResumed(); // the event wins: Resumed is raised before Android's activity is Started and before iOS is Active, so a platform read here would still say background
		PublishBeatCover();
	}

	// The silent beat's cover is one fact made of two: hidden unless the page is shown and its window is in front (Slice 322, third design; BeatCoverFacts in Core is the tested rule).
	// Recomputes use the stored facts only; the platform is read once per window subscription (WatchWindow), where no event will arrive.
	private void PublishBeatCover() => _session.SetBeatHidden(_beatCover.Hidden, _beatCover.Cause);

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
			old.Activated -= OnWindowActivatedShowBeat;
		}

		_watchedWindow = target;
		if (target is not null)
		{
			target.Stopped += OnWindowStoppedReleaseScreen;
			target.Resumed += OnWindowResumedKeepScreen;
			target.Activated += OnWindowActivatedShowBeat;
			_beatCover.WindowWatched(WindowForeground.IsInForeground()); // the only platform read: a new window may already be stopped (locked) and no event will say so; unknown keeps the fact
			PublishBeatCover();
		}
	}

	// The pages were laid out again (a new size or mode). A follower goes with the reading; a child who
	// had wandered keeps the page that now holds what they were looking at (the surface remapped it).
	private void OnLaidOut(object? sender, EventArgs e)
	{
		int reading = Math.Max(0, Surface.PageOfSentence(_session.CurrentSentence, DisplayedPosition));
		_follower.Relaid(reading, Surface.CurrentPage);
		ShowFollowedPage();
		FitOverlayTransport();
		RenderChrome();
#if DEBUG
		// Slice 232: a rotation re-lays the surface out; this line proves which page was held and which way up the screen was.
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] reading laid out landscape={LandscapeLayout.IsLandscape(Width, Height)} page={Surface.CurrentPage} layout={_layoutKind}");
#endif
	}

	// After a finished read a swipe also means "from here": Play then starts at the shown page.
	private void OnPageSwiped(object? sender, int page)
	{
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"{ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.PagePrefix} {page}");
#endif
		_session.ClearFinished();
		_follower.Swiped(page);
		FitOverlayTransport();
		RenderChrome();
	}

	// The wander pill and the transport row follow the same three facts: the page shown,
	// whether the child has wandered from the page being read, and whether anything is reading.
	private void RenderChrome()
	{
		SessionState state = _session.State;
		bool wandering = _follower.IsWandering && state != SessionState.Idle;
		Transport.Apply(state, _theme, ((IDinoKeyTarget)this).Screen == DinoScreen.ReadingBackCover);
		WanderPill.IsVisible = wandering;
		PillLabel.Text = ReadingText.ReadingPage(_follower.ReadingPage + 1);
	}

	private void OnModeChanged(object? sender, ReadingMode mode)
	{
		_mode = mode;
		(double size, double topInset) = TextStyleFor(LayoutFor(Width, Height, mode), mode);
		Surface.SetMode(mode, size, topInset);
		UpdateHeaderLayout(Width, Height);
		_ = SaveModeAsync(mode);
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
			// no-fallback: logged only; the mode applies for this visit and is not stored
			Debug.WriteLine($"Dino Reader could not save the reading mode: {ex}");
		}
		finally
		{
			_saveLock.Release();
		}
	}

	private void OnSentenceTapped(object? sender, int sentence)
	{
		UiUsage.Tap(UiControls.Sentence); // Slice 352: a tap on a sentence starts the read there

		CancelIntroRead(); // the grown-up took over: no auto-leave
		_follower.ResumeAt(Surface.CurrentPage);
		_ = StartReadAtAsync(sentence);
	}

	private async void OnPlayTapped(object? sender, EventArgs e)
	{
		UiUsage.Tap(_session.State == SessionState.Playing ? UiControls.Pause : UiControls.Play); // Slice 352: what the press asks for, from the state it found
		CancelIntroRead();
		switch (_session.State)
		{
			case SessionState.Loading:
				_session.Stop(ReadStopReason.Cancelled);
				break;
			case SessionState.Playing when _follower.IsWandering:
				JumpBackToReading();
				break;
			case SessionState.Playing:
				_session.Pause(TraceLines.ReadingReason.User);
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

	// Slice 237: a page with no sentence (the silent back cover, a text-in-picture page) starts the read at the next page that has one;
	// with none ahead (the back cover) the book starts again from sentence 0, as a finished book does.
	private int SentenceToPlayFromShownPage()
	{
		for (int page = _follower.ShownPage; page < Surface.PageCount; page++)
		{
			int first = Surface.FirstSentenceOnPage(page);
			if (first >= 0)
			{
				return first;
			}
		}

		return 0;
	}

	// Play from nothing reads from the top after a finished read, from the page the child swiped to,
	// else from the sentence the highlight is on.
	private async Task StartFromPlayAsync()
	{
		int sentence = _session.Finished ? 0
			: _follower.IsWandering ? SentenceToPlayFromShownPage()
			: Math.Max(0, _session.CurrentSentence);
		_follower.ResumeAt(Math.Max(0, Surface.PageOfSentence(sentence)));
		await StartReadAtAsync(sentence);
	}

	private async void OnPreviousTapped(object? sender, EventArgs e)
	{
		UiUsage.Tap(UiControls.PreviousPage); // Slice 352
		await SkipAsync(-1);
	}

	private async void OnNextTapped(object? sender, EventArgs e)
	{
		UiUsage.Tap(UiControls.NextPage); // Slice 352
		await SkipAsync(1);
	}

	// Skip moves by one sentence, the highlight unit. While nothing plays (idle or paused) it only
	// moves the mark and starts no audio; a paused read is dropped so Play starts at the mark.
	private async Task SkipAsync(int delta)
	{
		CancelIntroRead();
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
			await StartReadAtAsync(plan.Target);
			return;
		}

		if (state == SessionState.Paused)
		{
			_session.Stop(ReadStopReason.Skipped);
		}

		_session.MoveTo(plan.Target);
	}

	// A recorded page is one unit: skipping forward leaves from its last sentence, skipping back from its first (Slice 111).
	private int SkipFrom(int delta)
	{
		int current = _session.CurrentSentence;
		return _session.RecordedRangeOf(current) is { } range ? (delta > 0 ? range.Last : range.First) : current;
	}

	private async void OnBackTapped(object? sender, TappedEventArgs e)
	{
		UiUsage.Tap(UiControls.Back); // Slice 352
		await LeaveBookAsync();
	}

	private async void OnReadFailed(object? sender, EventArgs e) =>
		await DisplayAlertAsync(ReadingText.CouldntRead, ReadingText.ReadFailed, ReadingText.Ok);
}
