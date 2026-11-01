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
	private const double LanguageButtonHeight = 46;
	private const double MenuGap = 6;
	private const double CompactTitleSize = 17;
	private const double FullTitleSize = 20;
	private const double BackChipSize = 44;
	private const float ChipAlpha = 0.92f;
	private static readonly Thickness OverlayHeaderMargin = new(14, 6, 0, 0);
	private static readonly Thickness CardHeaderMargin = new(22, 14, 0, 0); // Text only held sideways: the header sits in the card's top corner
	private const double SidewaysTextSize = 26; // mock frame 93
	private const double SidewaysTextTopInset = 40; // room under the corner header before the first line
	private const double OverlayTransportSpan = 172; // 52 + 68 + 52: the three buttons without their gaps
	private const double OverlayPictureShare = 0.4;
	private const double OverlayPictureGap = 6; // the card's gap beside the picture (PictureTextPages.SidewaysGap)
	private const double CompactGapDefault = 20;
	private const double MinOverlayGap = 6;
	private const double MaxOverlayGap = 20;
	private static readonly Thickness SurfaceMargin = new(14, 6, 14, 0);
	private static readonly Thickness SideSurfaceMargin = new(14, 6, 14, 8);
	private static readonly Thickness OverlaySurfaceMargin = new(14, 8, 14, 8);
	private const double WaitCornerRadius = 18; // the picture box's corner (frame 64)
	private const double TransportWhileWaiting = 0.4; // the player row is dimmed while the wait overlay shows (frame 64)

	private readonly ReadingSession _session;
	private readonly IStoryLibrary _library;
	private readonly IServiceProvider _services;
	private readonly VoicePackInstaller _installer;
	private readonly IVoiceModelService _model;
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
	private readonly TaskCompletionSource _appeared = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private Task _languageReady = Task.CompletedTask; // the first work-out of the book's story languages (Slice 173)
	private IReadOnlyList<SentenceSpan> _sentences = [];
	private ReadingTheme _theme = ReadingTheme.For(false);
	private bool _visible;
	private bool _onStack;
	private Window? _watchedWindow;
	private const double FullHeaderRowSpacing = 2;
	private ReadingLayout _layoutKind;
	private IReadOnlyList<StoryPage> _shownPages = [];
	private bool _surfaceShown;
	private bool _translatedDisplay; // the page shows the translation (Slice 110): its offsets are not the story's, so the spoken position is not used
	private int _displayVersion;
	private CancellationTokenSource? _displayCancellation; // the screen's translation: stopped when a newer one starts or the page goes away
	private ReadingMode _mode = ReadingMode.Text; // the mode on screen now, which a translation that arrives late must not undo
	private readonly StoryLanguageButton _languageButton; // Slice 173: the child's story-language button, shown only when this book can be read in two or more story languages
	private string _packStates = string.Empty; // the side languages' voice states the list was worked out with
	private IReadOnlyList<string> _readable = []; // the story languages this book can be read in right now (with nothing to download)

	public ReadingPage(ReadingSession session, IStoryLibrary library, IServiceProvider services,
		VoicePackInstaller installer, IVoiceModelService model, UsageCounter usage)
	{
		InitializeComponent();
		_usage = usage;
		_session = session;
		_library = library;
		_services = services;
		_installer = installer;
		_model = model;
		_session.SentenceChanged += OnSentenceChanged;
		_session.StateChanged += OnSessionStateChanged;
		_wait = BuildWait();
		_session.Failed += OnReadFailed;
		_languageButton = new StoryLanguageButton(LanguageButtonHeight, OnLanguageTappedAsync) { IsVisible = false, AutomationId = "ReadingLanguage" };
		HeaderControls.Children.Insert(0, _languageButton);
		ShowPackStatus();
	}

	private bool _introRead; // Slice 228: opened by the intro's read step, which plays at once and goes back to the intro when the book ends
	private bool _demoRead; // Slice 228: never cleared, so the demo read counts no book open or finish even after the page started leaving
	private bool _introPopQueued; // Slice 228: the one pop back to the intro; consumed by the first Finished, cancelled by any touch or by leaving the page

	/// <summary>Slice 228: marks this page as the intro's demo read, whichever way it opens (even when the voice is not ready and nothing plays), so it counts no book open or finish. Call before the page shows.</summary>
	public void MarkDemoRead() => _demoRead = true;

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
		_sentences = StorySentences.Build(story.Text, LanguageRules.For(story.Language), story.BreakMap());
		_session.Open(story, _sentences, SaveCastAsync);
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
			Debug.WriteLine($"Dino Reader could not work out the story languages: {ex}");
		}
	}

	// A read never starts before the book's reading language is settled: Play tapped at once would otherwise read in the app language while the button shows the pick.
	private async Task StartReadAtAsync(int sentence)
	{
		await _languageReady;
		await _session.StartAtAsync(sentence);
	}

	private async Task CountOpenWhenShownAsync()
	{
		await _appeared.Task;
		CountOpen();
	}

	// A book that can only be read one way (a recorded book, "Show original text", an own book on Windows or without its pack) has no button and reads in the app
	// language; otherwise the child's pick applies, as long as this book can be read in it.
	private async Task RefreshLanguageButtonAsync()
	{
		if (_story is not { } story)
		{
			return;
		}

		StoryLanguageSet set = StoryLanguages.Current();
		var translator = _services.GetRequiredService<StoryTranslator>();
		bool switchable = LanguagePick.BookSwitchable(set.HasChoice, story.ShowOriginalText, StoryRecordings.Count(story));
		_readable = switchable ? await ReadableLanguagesAsync(story, set, translator) : [];
		_packStates = PackStates(set);
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

	// The languages the child may switch to (Slice 173, Decision 2): the book can be read in it with nothing to download, and its voice is already on the phone.
	// The child can never start a download; the voices come from Settings or the intro, behind the gate.
	private async Task<IReadOnlyList<string>> ReadableLanguagesAsync(Story story, StoryLanguageSet set, StoryTranslator translator)
	{
		var packs = _services.GetRequiredService<StoryLanguagePacks>();
		var readable = new List<string>();
		foreach (string language in set.All)
		{
			bool voiceThere = language == set.Main || packs.StateOf(language) == LanguageVoiceState.Ready;
			if (voiceThere && await translator.CanReadInAsync(story, language))
			{
				readable.Add(language);
			}
		}

		return readable;
	}

	// A pack that finished, or an own book that just got its table, changes what the child can switch to; the list and the button are worked out again then.
	private async Task UpdateLanguageButtonAsync()
	{
		if (_story is not { } story || !_onStack)
		{
			return;
		}

		StoryLanguageSet set = StoryLanguages.Current();
		var translator = _services.GetRequiredService<StoryTranslator>();
		bool switchable = LanguagePick.BookSwitchable(set.HasChoice, story.ShowOriginalText, StoryRecordings.Count(story));
		IReadOnlyList<string> readable = switchable ? await ReadableLanguagesAsync(story, set, translator) : [];
		if (!readable.SequenceEqual(_readable))
		{
			StoryLanguageMenu.DismissOpen(Root); // a list open over the old choices closes (its Back path resumes the read) rather than staying orphaned
		}

		_readable = readable;
		_languageButton.IsVisible = _readable.Count >= 2;
		string shown = LanguagePick.Shown(_readable, _session.ReadingLanguage, set.Main, StoryLanguages.Pick());
		bool moved = shown != _session.ReadingLanguage;
		_session.SetReadingLanguage(shown); // the flag and the reads (and usage counts) always name the same language
		_languageButton.SetLanguage(shown);
		_packStates = PackStates(set);
		if (moved)
		{
			await ShowStoryAsync();
		}
	}

	private string PackStates(StoryLanguageSet set)
	{
		var packs = _services.GetRequiredService<StoryLanguagePacks>();
		return string.Join(',', set.Sides.Select(side => packs.StateOf(side)));
	}

	private void OnLanguagePacksChanged(object? sender, EventArgs e)
	{
		if (PackStates(StoryLanguages.Current()) != _packStates)
		{
			_ = UpdateLanguageButtonSafeAsync();
		}
	}

	private async Task UpdateLanguageButtonSafeAsync()
	{
		try
		{
			await UpdateLanguageButtonAsync();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
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
		if (_story is not { } story || _readable.Count < 2)
		{
			return;
		}

		SessionState before = _session.State;
		bool wasActive = before is SessionState.Playing or SessionState.Loading;
		if (wasActive)
		{
			_session.Pause();
		}

		_languageButton.SetOpen(true);
		string? picked = await StoryLanguageMenu.PickAsync(Root, _languageButton, _readable, _session.ReadingLanguage, MenuGap);
		_languageButton.SetOpen(false);
		if (picked is null || picked == _session.ReadingLanguage)
		{
			if (wasActive && _session.State == SessionState.Paused)
			{
				_session.Resume();
			}

			return;
		}

		await SwitchLanguageAsync(_story ?? story, picked, wasActive);
	}

	private async Task SwitchLanguageAsync(Story story, string language, bool restart)
	{
		int sentence = Math.Max(0, _session.CurrentSentence);
		StoryLanguages.SetPick(language);
		_session.Stop();
		_session.SetReadingLanguage(language);
		_languageButton.SetLanguage(language);
		_usage.Record(UsageEventName.StoryLanguageSwitched, story, language: language);
		await ShowStoryAsync();
		if (restart)
		{
			await _session.StartAtAsync(sentence);
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
		string reading = _session.ReadingLanguage;
		if (story.ShowOriginalText || !translator.Applies(story, reading))
		{
			ShowSource(story);
			return;
		}

		if (!_surfaceShown)
		{
			ShowSource(story);
		}

		(TranslatedStory? translated, string title) = await TranslateForScreenAsync(story, translator, reading, cancellation);
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
			return (null, story.Title);
		}
	}

	// The title is cosmetic: any failure of its own leaves the source title and never costs the translated text.
	private static async Task<string> TitleOrSourceAsync(Story story, StoryTranslator translator, string reading, CancellationToken cancellation)
	{
		try
		{
			return await translator.TitleAsync(story, reading, cancellation);
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
		if (!TitleLabel.IsVisible)
		{
			SemanticProperties.SetDescription(Header, title);
		}

		TitleLabel.FlowDirection = DinoFonts.IsRightToLeft(language) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
	}

	private void ShowContent(ReadingContent content, Story story)
	{
		SyncModeSwitch();
		Surface.Show(content with { Back = BackCoverOf(story) }, _mode, _theme);
		_surfaceShown = true;
		if (_session.CurrentSentence >= 0)
		{
			MarkSentence(_session.CurrentSentence);
		}

		UpdateHeaderLayout(Width, Height);
		RenderChrome();
	}

	// Slice 237: the silent back cover, worded in the language the text is shown in; the picture is the book's cover (null for a colour cover).
	private BackCoverView? BackCoverOf(Story story)
	{
		if (story.BackCover is not { } cover)
		{
			return null;
		}

		string language = _translatedDisplay ? _session.ReadingLanguage : story.Language;
		string? picture = story.CoverImageFile is { } file ? _library.TryResolveFile(story.Id, file) : null;
		return new BackCoverView(BackCoverText.Lines(cover, language), Color.FromArgb(CoverPalette.ColourAt(story.CoverColourIndex)), picture);
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
		UpdateHeaderLayout(width, height);
	}

	private ReadingLayout LayoutFor(double width, double height) => LayoutFor(width, height, Surface.Mode);

	private ReadingLayout LayoutFor(double width, double height, ReadingMode mode)
	{
		return ReadingLayoutRules.For(width, height, mode, PictureTextBook.IsPictureTextBook(_shownPages));
	}

	private void UpdateHeaderLayout(double width, double height)
	{
		ReadingLayout kind = LayoutFor(width, height);
		if (kind != _layoutKind)
		{
			_layoutKind = kind;
			Root.SizeChanged -= OnRootSizeChanged;
			Root.SizeChanged += OnRootSizeChanged;
			ApplyHeaderLayout();
			ApplyTransportLayout();
			RenderChrome();
		}
	}

	private void OnRootSizeChanged(object? sender, EventArgs e) => FitOverlayTransport();

	private bool Floating => _layoutKind == ReadingLayout.PictureOverlay;

	// The back button and the view switch float over the picture (picture + text) or in the card's corner (text only held sideways).
	private bool FloatingHeader => _layoutKind is ReadingLayout.PictureOverlay or ReadingLayout.TextRow;

	private bool SideControls => _layoutKind is ReadingLayout.PictureSide or ReadingLayout.PictureOverlay;

	// Where the controls and the page card go: the side layouts put the controls in the right half (picture only) or over the picture (picture + text), the card running the full height.
	private void ApplyTransportLayout()
	{
		Root.ColumnDefinitions = _layoutKind switch
		{
			ReadingLayout.PictureSide => [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)],
			ReadingLayout.PictureOverlay => [new ColumnDefinition(new GridLength(2, GridUnitType.Star)), new ColumnDefinition(new GridLength(3, GridUnitType.Star))],
			_ => [],
		};
		Grid.SetColumnSpan(Header, _layoutKind == ReadingLayout.PictureSide ? 2 : 1);
		Grid.SetColumnSpan(SurfaceHost, SideControls ? 2 : 1);
		Grid.SetRow(SurfaceHost, FloatingHeader ? 0 : 1);
		Grid.SetRowSpan(SurfaceHost, Floating ? 5 : _layoutKind == ReadingLayout.TextRow ? 2 : _layoutKind == ReadingLayout.PictureSide ? 4 : 1);
		Grid.SetColumn(Transport, _layoutKind == ReadingLayout.PictureSide ? 1 : 0);
		Transport.Compact = _layoutKind != ReadingLayout.Upright;
		SurfaceHost.Margin = Floating ? OverlaySurfaceMargin : SideControls ? SideSurfaceMargin : SurfaceMargin;
		bool textRow = _layoutKind == ReadingLayout.TextRow;
		Surface.SetTextStyle(textRow ? SidewaysTextSize : ReadingMetrics.FontSize, textRow ? SidewaysTextTopInset : 0);
		FitOverlayTransport();
	}

	// Picture + text held sideways: the controls are centred on the picture they float over, and their gaps shrink on a narrow phone so the outer buttons stay on it.
	private void FitOverlayTransport()
	{
		if (!Floating)
		{
			Transport.Margin = default;
			Transport.Spacing = Transport.Compact ? CompactGapDefault : Transport.Spacing;
			return;
		}

		double inner = (Root.Width > 0 ? Root.Width : Width) - OverlaySurfaceMargin.HorizontalThickness;
		double picture = inner * OverlayPictureShare - OverlayPictureGap;
		double column = (Root.Width > 0 ? Root.Width : Width) * OverlayPictureShare;
		Transport.Margin = new Thickness(OverlaySurfaceMargin.Left, 0, Math.Max(0, column - OverlaySurfaceMargin.Left - picture), 0);
		Transport.Spacing = Math.Clamp((picture - OverlayTransportSpan) / 2, MinOverlayGap, MaxOverlayGap);
	}

	private void ApplyHeaderLayout()
	{
		bool floating = FloatingHeader;
		bool compact = _layoutKind == ReadingLayout.PictureSide;
		bool noTitle = _layoutKind is ReadingLayout.PictureOverlay or ReadingLayout.TextRow;
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
		Header.Margin = Floating ? OverlayHeaderMargin : _layoutKind == ReadingLayout.TextRow ? CardHeaderMargin : default;
		ApplyHeaderHeading(noTitle);
		ApplyBackChip();
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
		ApplyScreenAwake();
		Unsubscribe();
		_session.Attach();
		_installer.Changed += OnInstallerChanged;
		_model.ReadyChanged += OnInstallerChanged;
		SystemTheme.Changed += OnThemeChanged;
		_services.GetRequiredService<StoryLanguagePacks>().Changed += OnLanguagePacksChanged;
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
		_introPopQueued = false; // a pop that has not run yet must not fire once the page is covered or leaving
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
		_services.GetRequiredService<StoryLanguagePacks>().Changed -= OnLanguagePacksChanged;
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
		}
	}

	private void OnSessionStateChanged(object? sender, EventArgs e)
	{
		CountFinish();
		LeaveWhenIntroReadEnds();
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
		return _story is { } story && _services.GetRequiredService<StoryTranslator>().Applies(story, _session.ReadingLanguage);
	}

	// Cancel always stops the read; only the wait of opening the book (the first load) also goes back to the shelf. A mid-book restart stays on the page.
	private async void OnWaitCancelled()
	{
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
		RecordBookCount(UsageEventName.BookOpened, story);
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

	// Slice 228: the intro's demo read is not a real reading of the book, so it counts no open or finish (the one rule is DemoReadCounting).
	private void RecordBookCount(string name, Story story)
	{
		if (DemoReadCounting.Counts(name, _demoRead))
		{
			_usage.Record(name, story, language: ReadIn(story));
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

	// The wander pill and the transport row follow the same three facts: the page shown,
	// whether the child has wandered from the page being read, and whether anything is reading.
	private void RenderChrome()
	{
		SessionState state = _session.State;
		bool wandering = _follower.IsWandering && state != SessionState.Idle;
		Transport.Apply(state, _theme);
		WanderPill.IsVisible = wandering;
		PillLabel.Text = ReadingText.ReadingPage(_follower.ReadingPage + 1);
	}

	private void OnModeChanged(object? sender, ReadingMode mode)
	{
		_mode = mode;
		bool textRow = LayoutFor(Width, Height, mode) == ReadingLayout.TextRow;
		Surface.SetMode(mode, textRow ? SidewaysTextSize : ReadingMetrics.FontSize, textRow ? SidewaysTextTopInset : 0);
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
		CancelIntroRead(); // the grown-up took over: no auto-leave
		_follower.ResumeAt(Surface.CurrentPage);
		_ = StartReadAtAsync(sentence);
	}

	private async void OnPlayTapped(object? sender, EventArgs e)
	{
		CancelIntroRead();
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

	private async void OnPreviousTapped(object? sender, EventArgs e) => await SkipAsync(-1);

	private async void OnNextTapped(object? sender, EventArgs e) => await SkipAsync(1);

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

	private async void OnBackTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(() => Navigation.PopAsync());

	private async void OnReadFailed(object? sender, EventArgs e) =>
		await DisplayAlertAsync(ReadingText.CouldntRead, ReadingText.ReadFailed, ReadingText.Ok);

	private void OnInstallerChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(ShowPackStatus);

	private void OnPackStatusTapped(object? sender, TappedEventArgs e)
	{
		if (_installer.CanRetry)
		{
			_ = _installer.StartAsync(PackStartReason.User);
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
