using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using Sirous.Reading.Application.Reading;
using Sirous.Reading.Application.Settings;
using FallbackArea = Sirous.Reading.Application.Usage.FallbackArea;
using FallbackReason = Sirous.Reading.Application.Usage.FallbackReason;
using Sirous.Reading.Domain.Reading;
using Sirous.Reading.Mobile.Shared.Waiting;

namespace DinoReader;

public partial class GalleryPage : ContentPage, IDinoKeyTarget, IInkUnderStatusBar
{
	// Slice 325 A4: the corner's ink header sits under the status bar on a phone held upright (the rail has none), so the clock and battery are light there and dark again on the shelf.
	bool IInkUnderStatusBar.InkUnderStatusBar => StatusBarIcons.InkUnderBar(_mode.IsGrownUp, CurrentChrome() is { IsRail: true });

	private const double LanguageMenuGap = 6;
	private const string TileTitleHeightKey = "TileTitleHeight";
	private const string TileWidthKey = "TileWidth";
	private const string TileTitleLinesKey = "TileTitleLines";
	private const string TileSpacingKey = "TileSpacing";
	private const string TileTitleSizeKey = "TileTitleSize";
	private const string TileCoverRadiusKey = "TileCoverRadius";
	private const string TileShadowOpacityKey = "TileShadowOpacity";
	private const string TileShadowOffsetKey = "TileShadowOffset";
	private const double DefaultShadowOffset = 4;
	private const string TileMoreSizeKey = "TileMoreSize";
	private const string TileMoreRadiusKey = "TileMoreRadius";
	private const string TileMoreInsetKey = "TileMoreInset";
	private const double CornerCoverShadowOpacity = 0; // the corner's covers have none (grown-up mock P1)
	private const double ShelfCoverShadowOpacity = 0.08;
	private const double MoreDotRadiusShare = 0.5;
	private const long TapSuppressMilliseconds = 800;

	private readonly IStoryLibrary _library;
	private readonly GrownUpGate _gate;
	private readonly GrownUpMode _mode;
	private readonly StoryActions _actions;
	private readonly StarterStories _starters;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly ShelfReorderView _reorder = new();
	private WindowShapeSeam.ShelfGeometry _shelfGeometry = new(LandscapeLayout.PhoneShelfSidePadding, 0, LandscapeLayout.PhoneShelfSidePadding, 3, 12, 12); // the shelf list's margins, columns and gaps from the last layout pass (ApplyShape); the Reorder layer copies it (Slice 319)
	private WindowShapeSeam.ReorderLayerShape _reorderGeometry = WindowShapeSeam.ReorderLayerShelf(default); // the Reorder layer's own grid and hint, from the mock per platform (Slice 321); a tablet copies the shelf's geometry, moved right by the rail's width
	private bool _savingOrder; // Done is saving the order
	private long _suppressTapUntil; // Environment.TickCount64: the release that ends a 1 s hold must not open a sheet
	private readonly ObservableCollection<object> _items = [];
	private Story? _sheetStory; // the book the "This book" sheet is open on
	private CancellationTokenSource? _sheetCountCts; // the sheet's background count of each language's recordings: cancelled when the sheet closes or another book opens
	private string? _reopenSheetFor; // the book whose Record flow is open: the sheet opens again on it when the flow is left
	private bool _sheetChanged; // a switch was saved while the sheet was open: the tiles are rebuilt when it closes
	private readonly SemaphoreSlim _sheetSaveLock = new(1, 1);
	private readonly StoryLanguageButton _languageButton; // Slice 173 (mock 79): the child's story-language button, left of the grown-ups button, with two or three story languages
	private readonly CornerShell _corner = new(); // Slice 325 part A: the grown-ups' corner's ink header or rail
	private readonly CornerTabs _tabs = new(); // the corner's three tabs: the bottom bar under 600 dp wide, rows in the rail from 600
	private readonly ShelfHeader _shelfHeader; // Slice 325 part C: the kids' shelf's header or rail
	private ChromeShape _chrome = CornerLayout.ChromeOf(393, 852, false); // the chrome the last layout pass built
	private StorePage? _store; // Slice 325 part B: the Store tab's view, made the first time the tab shows and kept, so a download and the filters survive a switch to another tab
	private SettingsTab? _settingsTab; // the Settings tab's view, kept the same way
	private ICornerTabView? _shownTab; // the tab view last told it is on screen (SyncTabViews)
	private bool _appeared; // the gallery is the page on screen (OnAppearing to OnDisappearing)

	public GalleryPage(IStoryLibrary library, GrownUpGate gate, GrownUpMode mode, StoryActions actions, StarterStories starters, IServiceProvider services)
	{
		InitializeComponent();
		_passes = new LatestWinsQueue<UiWork>((a, b) => a | b, RunUiPassAsync, ReportPassFailure);
		_library = library;
		_gate = gate;
		_mode = mode;
		_actions = actions;
		_starters = starters;
		_services = services;
		StoryList.ItemsSource = _items;
		WireDesktopShelf();
		StoryList.Scrolled += (_, e) =>
		{
			TileOverlay.NotifyScrollStarted(); // the platform scroller may have taken a touch that a cover's 1 s hold is still timing
			RememberFirstBook(e.FirstVisibleItemIndex);
		};
		BookInfo.VoiceChanged += SaveUseVoiceAsync;
		BookInfo.ReadRequested += ReadSheetBookAsync;
		BookInfo.RecordRequested += OpenSheetRecordAsync;
		BookInfo.ExportRequested += ExportSheetBookAsync;
		BookInfo.RemoveRequested += RemoveSheetBookAsync;
		BookInfo.Dismissed += OnBookSheetDismissed;
		BookInfo.ReorderRequested += OpenSheetReorderAsync;
		_languageButton = new StoryLanguageButton(ShelfTitleRules.SizesOf(_chrome).FlagButton, OpenLanguageMenuAsync) { IsVisible = false, AutomationId = "GalleryLanguage" };
		_shelfHeader = new ShelfHeader(_languageButton);
		AddChrome();
		AddReorderView();
		WaitOverlay overlay = DinoWait.OverPage();
		Grid.SetRowSpan(overlay, GalleryRoot.RowDefinitions.Count);
		Grid.SetColumnSpan(overlay, GalleryRoot.ColumnDefinitions.Count);
		GalleryRoot.Children.Add(overlay);
		_wait = new WaitController(overlay);
	}

	// Slice 325: the two chromes sit in the root grid under everything else (the sheet, the reorder layer and the wait overlay are added after them). Only the one for the current mode is visible;
	// ApplyShape places it (a header in row 0, or the rail in column 0) and moves the corner's tab strip between the page and the rail.
	private void AddChrome()
	{
		GalleryRoot.Children.Insert(0, _shelfHeader);
		GalleryRoot.Children.Insert(0, _corner);
		_corner.BackRequested += CompleteHandover;
		_corner.TitleTapped += OnTitleTapped;
		_shelfHeader.TitleTapped += OnTitleTapped;
		_shelfHeader.GrownUpsRequested += OnGrownUpsRequested;
		_shelfHeader.Fitted += LogShelf;
		_tabs.TabRequested += OnTabRequested;
		_tabs.HandlerChanged += (_, _) => { if (_tabs.Handler is not null) { Replay(); } }; // work that waited for the tabs' native side
		_corner.IsVisible = false;
		GalleryRoot.Children.Insert(0, _tabs);
		_tabs.IsVisible = false;
	}

	// Slice 264: the reorder mode is a layer over the whole page (its own ink bar, hint and grid), above the sheet's scrim so it never shows through, below the wait overlay added after it.
	private void AddReorderView()
	{
		Grid.SetRowSpan(_reorder, GalleryRoot.RowDefinitions.Count);
		Grid.SetColumnSpan(_reorder, GalleryRoot.ColumnDefinitions.Count);
		GalleryRoot.Children.Add(_reorder);
		_reorder.DoneRequested += DoneReorderAsync;
		_reorder.CancelRequested += CancelReorder;
	}

	// The same list as the reading page (88): one pick for all books, remembered. The tile titles follow it (Slice 323, Decision 173).
	private async Task OpenLanguageMenuAsync()
	{
		await _busy.RunAsync(async () =>
		{
			StoryLanguageSet set = StoryLanguages.Current();
			string current = StoryLanguages.Pick();
			_languageButton.SetOpen(true);
			string? picked = await StoryLanguageMenu.PickAsync(GalleryRoot, _languageButton, ChoosableLanguages(set), current, LanguageMenuGap);
			_languageButton.SetOpen(false);
			if (picked is null || picked == current)
			{
				return;
			}

			StoryLanguages.SetPick(picked);
			_services.GetRequiredService<UsageCounter>().RecordGeneral(UsageEventName.StoryLanguageSwitched, picked);
			ApplyLanguageButton();
			_ = RequestShelfCheckAsync();
		});
	}

	// Shown in the kids' shelf only, and only when the parent set two or three story languages.
	private void ApplyLanguageButton()
	{
		StoryLanguageSet set = StoryLanguages.Current();
		IReadOnlyList<string> choosable = ChoosableLanguages(set);
		_languageButton.IsVisible = LanguagePick.ShelfButtonShown(_mode.IsGrownUp, choosable);
		_languageButton.SetLanguage(LanguagePick.ShelfShown(choosable, StoryLanguages.Pick(), set.Main));
		_shelfHeader.SetFlagShown(_languageButton.IsVisible); // the flag gives the title less room, so the header fits again
	}

	private bool _pageAppeared; // the first appearing started; before it a check-only trigger never builds the first shelf
	private string? _shelfTitleLanguage; // the language the tile titles were built in (Slice 323); set only after a build finished, so a failed one is tried again

	// Slice 325, "Rebuild/chrome design after the third strike": every shelf rebuild and every chrome re-layout is a request to this ONE queue. One pass runs at a time, a request during a pass replaces
	// the single pending one (flags OR-ed, so a forced rebuild is never downgraded), a pass that throws is reported once and the next request rebuilds from scratch. No retry, no skip-if-unchanged memory.
	// ONE model for "work that still has to be done" (the review's stale marks were set and replayed in several places and each round found a path that lost one): every request ORs its bit into _pending
	// and posts a pass; a pass takes the bits it can run now and gives back every bit whose work did not finish (it could not run, or it threw); Replay posts a pass when anything is waiting.
	private readonly PendingUiWork _pending = new();
	private bool _chromeApplying; // ApplyChrome detaches and attaches the tabs: their HandlerChanged must not replay while it runs
	private bool _replayWanted; // a Replay asked for while the chrome was applied: run once when it ends

	protected override void OnHandlerChanged()
	{
		base.OnHandlerChanged();
		if (Handler is not null)
		{
			Replay();
		}
	}

	private readonly LatestWinsQueue<UiWork> _passes;

	// Called when something that work may have waited for happened: the page's handler, the tabs' handler, a real window size. Posts a pass only when work is waiting, and never while the chrome is being applied.
	private void Replay()
	{
		if (_chromeApplying)
		{
			_replayWanted = true; // run right after the chrome is applied, so a HandlerChanged during it is not lost
			return;
		}

		if (UiWorkRules.ShouldReplay(_pending.Snapshot(), RunnableUiWork()))
		{
			_ = _passes.Request(UiWork.None);
		}
	}

	private Task<bool> RequestWork(UiWork work)
	{
		_pending.Request(work);
		return _passes.Request(work);
	}

	// The one shelf refresh path (Slice 323): every trigger (appearing, library change, staged update, mode, pick, pack change) comes here. A check-only request (force false) is skipped by the pass
	// when the languages the tiles were built in are still the current ones. The task never faults; its result is false when the shelf pass failed or was dropped (a torn-down page).
	private Task<bool> RequestShelfRefresh(bool force) =>
		RequestWork(force ? UiWork.Shelf | UiWork.ShelfForce : UiWork.Shelf);

	private void RequestChrome() => _ = RequestWork(UiWork.Chrome);

	private void RequestTab() => _ = RequestWork(UiWork.Tab);

	// One pass: the shelf first (the list loads off the UI thread, one dispatch applies it), then the chrome, the tabs and the tab view, applied on the UI thread as one piece. A part that cannot run now
	// (no handler, no window size yet) stays pending for Replay. The result is false when the requested shelf work is still pending after the pass.
	private async Task<bool> RunUiPassAsync(UiWork request)
	{
		if (Handler is not null && _pending.TryTake(UiWorkRules.ShelfMask, out UiWork shelf))
		{
			await RunShelfPassAsync(shelf);
		}

		if (_pending.TryTake(RunnableUiWork(), out UiWork ui))
		{
			await PostToUiThreadAsync(() => ApplyChromeAndTab(ui));
		}

		return (request & UiWork.Shelf) == UiWork.None || !_pending.Has(UiWork.Shelf);
	}

	private UiWork RunnableUiWork()
	{
		return UiWorkRules.Runnable(Handler is not null, _tabs.Handler is not null, _windowSize.Width > 0);
	}

	// Every bit this piece does not finish (a part that cannot run, or an exception that leaves it) goes back to _pending. An exception is not swallowed: it reaches the queue's onError.
	private void ApplyChromeAndTab(UiWork taken)
	{
		UiWork left = taken;
		try
		{
			if (Handler is null)
			{
				return;
			}

			if ((left & UiWork.Chrome) != UiWork.None && _windowSize.Width > 0)
			{
				ApplyShapeOnce();
				left &= ~UiWork.Chrome;
			}

			left = RebuildTabs(left);
			if ((left & UiWork.Tab) != UiWork.None)
			{
				ApplyTab();
				left &= ~UiWork.Tab;
			}
		}
		finally
		{
			_pending.Restore(left);
			if (_replayWanted) // a handler event during the chrome: decided now that the unfinished bits are back in the record
			{
				_replayWanted = false;
				Replay();
			}
		}
	}

	// The corner tabs change their Grid only while their own handler is attached (a Grid with no native side is rebuilt when it attaches, by Replay).
	private UiWork RebuildTabs(UiWork left)
	{
		if (!_tabs.NeedsRebuild)
		{
			return left & ~UiWork.TabBuild;
		}

		if (_tabs.Handler is null)
		{
			return left | UiWork.TabBuild;
		}

		_tabs.RebuildIfNeeded();
		return left & ~UiWork.TabBuild;
	}

	// Always posts to the dispatcher (never inline), so a chrome or tab change asked from OnAppearing or a layout callback runs after that callback returned, not inside it.
	private Task PostToUiThreadAsync(Action action)
	{
		var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		bool queued = Dispatcher.Dispatch(() =>
		{
			try
			{
				action();
				done.SetResult();
			}
			catch (Exception ex)
			{
				// no-fallback: handed back to the queue's pass, whose onError reports it
				done.SetException(ex);
			}
		});
		if (!queued)
		{
			done.SetException(new InvalidOperationException("The dispatcher did not accept the chrome pass."));
		}

		return done.Task;
	}

	private static Task OnUiThreadAsync(Action action)
	{
		if (MainThread.IsMainThread)
		{
			action();
			return Task.CompletedTask;
		}

		return MainThread.InvokeOnMainThreadAsync(action);
	}

	// A failed pass leaves the half-built chrome uncounted (the next pass builds it again) and is reported once through the app's fallback reporting.
	private void ReportPassFailure(Exception ex)
	{
		_shapeKey = null;
		Debug.WriteLine($"Dino Reader gallery pass failed: {ex}");
		DinoFallbacks.Report(FallbackArea.Ui, FallbackReason.LayoutFailed);
	}

	// The shelf bits are given back (as a forced rebuild) unless the rebuild was applied, so the next pass of any kind rebuilds, even a check-only one.
	private async Task RunShelfPassAsync(UiWork taken)
	{
		bool force = (taken & UiWork.ShelfForce) != UiWork.None;
		if (!force && !ShelfRefreshRule.ShouldRebuild(_shelfLanguage, _shelfTitleLanguage, LanguageChoice.Current(), ShelfTitleLanguage(), _pageAppeared))
		{
			return;
		}

		bool applied = false;
		try
		{
			applied = await RebuildShelfAsync();
		}
		catch (OperationCanceledException)
		{
			// no-fallback: a wait cancelled by the gallery's teardown is not a failure; the shelf stays pending
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader gallery refresh failed: {ex}");
			DinoFallbacks.Report(FallbackArea.Library, FallbackReason.StoryUnreadable); // Slice 325: the shelf keeps what it showed and is rebuilt on the next request; the same for every merged requester
		}
		finally
		{
			_pending.Restore(UiWorkRules.ShelfUnfinished(taken, applied)); // a check stays a check, and a shelf that did not apply is tried once more, not on every replay
		}
	}

	// A trigger nobody waits for. RequestShelfRefresh never faults, so it needs no catch.
	private async Task RequestShelfCheckAsync() => await RequestShelfRefresh(false);

	// A book is named the same way on its tile, its sheet and its delete prompt (Slice 323): the title its tile shows; a book not on the shelf is named by the same rule.
	private string ShelfTitleOf(Story story) =>
		_items.OfType<StoryTile>().FirstOrDefault(tile => tile.Story.Id == story.Id)?.Title
		?? _services.GetRequiredService<StoryTranslator>().ShelfTitle(story, ShelfTitleLanguage());

	// The tile titles' language right now (Slice 323).
	private string ShelfTitleLanguage()
	{
		StoryLanguageSet set = StoryLanguages.Current();
		return LanguagePick.ShelfTitles(_mode.IsGrownUp, ChoosableLanguages(set), StoryLanguages.Pick(), set.Main);
	}

	// The child can pick only a language whose audio is installed for a book on the shelf (Slice 173): a download starts from Settings or the intro, behind the gate. The one rule and the one
	// snapshot are ShelfAudioPacks' (Slice 278 part B): this page reads no pack folder and does not look at a running download, so the reading page gives the same list.
	private IReadOnlyList<string> ChoosableLanguages(StoryLanguageSet set) =>
		_services.GetRequiredService<ShelfAudioPacks>().ShelfLanguages(set);

	// Changed is raised on the main thread; the snapshot is already updated.
	private void OnShelfAudioChanged(object? sender, EventArgs e)
	{
		ApplyLanguageButton();
		_ = RequestShelfCheckAsync();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		_appeared = true;
		ApplyBarColours(); // first appearance: the colours and bar icons stand before any size is known
		string? reopenSheetFor = _reopenSheetFor; // taken now: only this appearing (the return from the Record push) may reopen the sheet; any other one clears it
		_reopenSheetFor = null;
		_mode.PropertyChanged -= OnModeChanged;
		_mode.PropertyChanged += OnModeChanged;
		StoreClient store = _services.GetRequiredService<StoreClient>();
		store.LibraryChanged -= OnLibraryChanged;
		store.LibraryChanged += OnLibraryChanged;
		ShelfAudioPacks shelfAudio = _services.GetRequiredService<ShelfAudioPacks>();
		shelfAudio.Changed -= OnShelfAudioChanged;
		shelfAudio.Changed += OnShelfAudioChanged;
		_ = shelfAudio.RescanAsync(); // the gallery is shown: a pack that changed meanwhile shows in the child's list
		StagedBookUpdates staged = _services.GetRequiredService<StagedBookUpdates>();
		staged.Staged -= OnBookUpdateStaged;
		staged.Staged += OnBookUpdateStaged; // Slice 313: while the shelf is shown no book is open, so a newly staged text is swapped in at once
		ApplyMode();
		SyncTabViews();
		ShowScreen();
		_ = OnboardingRoute.DeleteAbandonedTryAsync(_services); // Slice 362: a recorded try left behind (setup abandoned, or an earlier delete failed) is deleted now, under the store's one lock
		_ = CleanOwnBooksAsync(); // started on its own, so a failing starter install or refresh never keeps it from running
		_services.GetRequiredService<HighResUpgradeRunner>().Start(); // Slice 375 D: once per process; swaps high-res pictures in on Wi-Fi, in the background
		try
		{
			// The starters are on the shelf before the first list is read (Slice 91); a later call is a no-op.
			await _starters.InstallOnceAsync();
			_pageAppeared = true; // only now may a check-only trigger build a first shelf
			await RequestShelfRefresh(true); // forced on every appearing, so a stale shelf heals here
			await ResumeTourAsync();
			await ReopenSheetAfterRecordAsync(reopenSheetFor);
		}
		catch (Exception ex)
		{
			// no-fallback: logged only; the shelf stays as it was until the next refresh
			Debug.WriteLine($"Dino Reader gallery refresh failed: {ex}");
		}
	}

	// Back from the Record intro lands on the book sheet it was opened from (mock frame 178), on Android, iOS and Windows.
	private async Task ReopenSheetAfterRecordAsync(string? id)
	{
		if (id is null || !_mode.IsGrownUp || await _library.GetAsync(id) is not { } story)
		{
			return;
		}

		await _busy.RunWhenFreeAsync(() => OpenBookSheetAsync(story)); // queued behind a running grown-up action, never dropped
	}

	// Slice 278 A3: the one-time deletion of the families' own books runs in the background, once per app run; the library never lists them, so the shelf needs no reload.
	private async Task CleanOwnBooksAsync()
	{
		try
		{
			await _services.GetRequiredService<OwnBooksCleanup>().RunOnceAsync();
		}
		catch (Exception ex)
		{
			// no-fallback: the cleanup reports its own failures; this only guards the fire-and-forget task
			Debug.WriteLine($"Dino Reader own-books cleanup failed: {ex}");
		}
	}

	// Slice 362 (mock N8a to N10): the last step of the first run is the corner tour, a layer over this page. It starts when onboarding's saved step is the corner (after the voice step, or after a restart on the tour);
	// the corner is open behind it (the grown-up is the one setting up). A tour that already shows is not started twice.
	private async Task ResumeTourAsync()
	{
		if (OnboardingState.Step != OnboardingStep.Corner || _tour is not null || _tourStarting || _tourGateDeclined)
		{
			return;
		}

		_tourStarting = true; // a second appearing while the gate or the delay is open must not start a second tour
		try
		{
			// A fresh start from the voice step has entered grown-up mode already. Kids' mode here means the app was left on the tour and relaunched, so a child may be holding it: the gate comes first.
			if (!_mode.IsGrownUp && !await _gate.RequestAsync(Navigation))
			{
				_tourGateDeclined = true; // asked again when a grown-up enters through the pill
				return;
			}

			_mode.Enter();
			await Task.Delay(TourStartDelay); // the corner's chrome is laid out first: the tour reads where the tabs are
			if (_tour is null)
			{
				BeginTour();
			}
		}
		finally
		{
			_tourStarting = false;
		}
	}

	private bool _tourStarting;
	private bool _tourGateDeclined;

	private void BeginTour()
	{
		var tour = new CornerTour(tab => _tabs.TabView(tab), () => _corner.Pill, () => _shelfHeader.GrownUps, tab => _mode.SelectTab(tab), () => _mode.Leave(), FinishTour);
		Grid.SetRowSpan(tour, GalleryRoot.RowDefinitions.Count);
		Grid.SetColumnSpan(tour, GalleryRoot.ColumnDefinitions.Count);
		GalleryRoot.Children.Add(tour);
		_tour = tour;
		tour.Begin();
	}

	// Start reading: onboarding is over, every later launch opens on the shelf.
	private void FinishTour()
	{
		if (_tour is { } tour)
		{
			GalleryRoot.Children.Remove(tour);
			_tour = null;
		}

		OnboardingRoute.Finish(_services);
	}

	private CornerTour? _tour;
	private static readonly TimeSpan TourStartDelay = TimeSpan.FromMilliseconds(400);

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_appeared = false;
		SyncTabViews();
		CancelSheetCount();
		_mode.PropertyChanged -= OnModeChanged;
		_services.GetRequiredService<StoreClient>().LibraryChanged -= OnLibraryChanged;
		_services.GetRequiredService<ShelfAudioPacks>().Changed -= OnShelfAudioChanged;
		_services.GetRequiredService<StagedBookUpdates>().Staged -= OnBookUpdateStaged;
	}

	// Slice 313: a newer book text was staged while the shelf shows (raised on a background thread). The refresh applies it behind the "Updating your books" overlay when the shelf is free;
	// the packs made for the new text follow through StagedBookUpdates.Swapped (ShelfAudioPacks plans again), so nothing is retried here: a text that could not be applied waits for the next appearing.
	private void OnBookUpdateStaged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(async () =>
	{
		// One refresh at a time for staged updates (main thread only): a second signal while one runs only asks for one more pass after it, so two refreshes never swap and reload the shelf at once.
		if (_stagedRefreshRunning)
		{
			_stagedRefreshAgain = true;
			return;
		}

		_stagedRefreshRunning = true;
		try
		{
			do
			{
				_stagedRefreshAgain = false;
				await RefreshAfterStagedUpdateAsync();
			}
			while (_stagedRefreshAgain); // a failed pass still gets the pass a later signal asked for
		}
		finally
		{
			_stagedRefreshRunning = false;
		}
	});

	// RequestShelfRefresh never throws and reports its own failure; this caller keeps the event Slice 313 counts: a rebuild that left the shelf stale is an UpdateCheckFailed. The shelf stays as it was until the next refresh.
	private async Task RefreshAfterStagedUpdateAsync()
	{
		if (!await RequestShelfRefresh(true))
		{
			DinoFallbacks.Report(FallbackArea.Store, FallbackReason.UpdateCheckFailed); // Slice 313
		}
	}

	private bool _stagedRefreshRunning;
	private bool _stagedRefreshAgain;

	// A store download can finish on a background thread while the gallery shows; reload the list on the UI thread (Slice 92).
	private void OnLibraryChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(async () =>
	{
		await RefreshShelfAsync();
	});

	// The tiles follow the mode through their binding; only the chrome changes here. A tab change swaps what the content area shows (ApplyTab).
	// Slice 352: the child's shelf, or the corner's open tab, is the screen while this page is the one on show.
	private void ShowScreen()
	{
		if (_appeared)
		{
			UiScreen.Show(UiScreen.OfGallery(_mode.IsGrownUp, _mode.Tab));
		}
	}

	private void OnModeChanged(object? sender, PropertyChangedEventArgs e) => MainThread.BeginInvokeOnMainThread(() =>
	{
		if (e.PropertyName == nameof(GrownUpMode.Tab))
		{
			_tabs.Selected = _mode.Tab;
			RequestTab();
			LogCorner();
			ShowScreen();
			return;
		}

		ApplyMode();
		ShowScreen();
		if (!_mode.IsGrownUp)
		{
			_settingsTab?.Reset(); // the next visit to the corner starts at the Settings list
			CloseReorder("left grown-up mode"); // Slice 264: leaving grown-up mode cancels the reorder mode; nothing was saved
			BookInfo.Dismiss(); // also refreshes the tiles when a switch was saved
		}
	});

	private void ApplyMode()
	{
		_ = RequestShelfCheckAsync(); // the app language or the title language changed: the shelf titles (and the Add tile, rebuilt with them) follow it

		ApplyTexts();

		ApplyModeChrome(_mode.IsGrownUp);
	}

	private string? _textLanguage; // the app language the fixed labels were set in

	// The labels are x:Static (bound once on this cached page), so a language change re-applies them here.
	private void ApplyTexts()
	{
		string language = LanguageChoice.Current();
		if (_textLanguage == language)
		{
			return;
		}

		_textLanguage = language;
		_shelfHeader.ApplyTexts();
		_corner.ApplyTexts();
		_tabs.ApplyTexts(); // only marks the tabs: the queue's next pass rebuilds them (TabBuild), never from here: this runs inside OnAppearing
		_pending.Request(UiWork.TabBuild);
		ApplyContentHeaderTexts();
		if (_windowSize.Width > 0)
		{
			_shapeKey = null; // the words changed: the corner's button text and the shelf's title are placed again
		}

		ApplyShape();
	}

	private readonly WaitController _wait;

	private string? _shelfLanguage; // the app language the shelf titles were built in

	private async Task RefreshShelfAsync()
	{
		await RequestShelfRefresh(true); // never faults: the loop reports a failed build itself
	}

	// Slice 181: staged store updates are swapped in once per process, and the shelf is never read before that finished, whoever asks (first load, library change, resume).
	// The wait is logged so the 500 ms loading-state rule can be measured; past 500 ms the "Updating your books" overlay shows (no Cancel: a half swap is journaled), and it ends even on failure.
	private async Task WaitForStagedUpdatesAsync()
	{
		var clock = Stopwatch.StartNew();
		await _wait.RunAsync(WaitStage.Saving, AppResources.Gallery_UpdatingBooks, canCancel: false, async _ =>
		{
			await _starters.InstallOnceAsync();
			StagedBookUpdates staged = _services.GetRequiredService<StagedBookUpdates>();
			await staged.ApplyOnceAsync();
			if (staged.HasPending && ShelfFreeForSwap())
			{
				await staged.ApplyPendingAsync(); // Slice 313: a text staged since the last apply, whenever the shelf shows and nothing is open over it
			}

			return true;
		});
		Debug.WriteLine($"[BookUpdate] gallery waited {clock.ElapsedMilliseconds} ms for the staged apply");
	}

	// Slice 313: a book is swapped (deleted and imported again under a new id) only while the shelf really is the page in front with nothing open over it: no book opening, no sheet, no modal,
	// no other page pushed. Otherwise the staged text waits for the next refresh (the next appearing).
	private bool ShelfFreeForSwap() =>
		!_busy.IsBusy && _sheetStory is null && !BookInfo.IsVisible && !_reorder.IsVisible && Navigation.ModalStack.Count == 0
		&& (Navigation.NavigationStack.LastOrDefault() is not { } top || top == this);

	// True when the built list was applied; false when the page was torn down meanwhile (the job is dropped and the shelf stays stale).
	private async Task<bool> RebuildShelfAsync()
	{
		await WaitForStagedUpdatesAsync();
		IReadOnlyList<Story> stories = await _library.ListAsync();
		_ = _services.GetRequiredService<ShelfAudioPacks>().RescanAsync(); // the shelf list changed: the snapshot of installed audio follows it
		string appLanguage = LanguageChoice.Current();
		string titleLanguage = ShelfTitleLanguage();
		// The awaits above can resume on the thread that finished the file read (the 0xc0000005 stack: ListAsync completing straight into this method), and everything below touches the shelf's
		// WinUI children: hop to the UI thread (a no-op when already there), and stop for a page whose window is gone. (Slice 325)
		bool applied = false;
		await OnUiThreadAsync(() => applied = ApplyBuiltShelf(stories, appLanguage, titleLanguage));
		return applied;
	}

	private bool ApplyBuiltShelf(IReadOnlyList<Story> stories, string appLanguage, string titleLanguage)
	{
		if (Handler is null)
		{
			return false; // a torn-down page drops the job
		}

#if DEBUG
		ReorderLog($"shelf order {string.Join(",", stories.Select(s => s.Id))}");
#endif
		_items.Clear();
		foreach (StoryTile item in GalleryItems.Build(stories, _library, _mode, story => _services.GetRequiredService<StoryTranslator>().ShelfTitle(story, titleLanguage)))
		{
			_items.Add(item);
		}

#if DEBUG
		if (_shelfTitleLanguage is not null && _shelfTitleLanguage != titleLanguage)
		{
			Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[ShelfTitles] {_shelfTitleLanguage} -> {titleLanguage}"); // the shelf-titles-follow-story-language flow's expect-log markers (RTST-TEST tag)
		}
#endif
		_shelfLanguage = appLanguage; // recorded only now: a build that failed leaves them stale, so the next ask retries
		_shelfTitleLanguage = titleLanguage;

		ApplyModeChrome(_mode.IsGrownUp);
		return true;
	}

	// Entering or leaving the corner swaps the whole chrome (ink header or rail with tabs, or the cream header or rail): the two places never look alike.
	private void ApplyModeChrome(bool grownUp)
	{
		ApplyShelfRing();
		ApplyLanguageButton();
		ApplyShape(); // the key holds the mode, so another mode lays everything out again and the same mode does nothing (a shelf rebuild must not relayout the chrome)
		ApplyBarColours(); // at once, even before the first window size: the chrome kind depends on the window size only, never on the mode
	}

	// Slice 349: the one place that sets what sits behind the system bars: the page colour, the content host's colour, the bar icons and (Windows) the title bar.
	// Asked on every mode change, after every chrome pass and on first appearance, never behind ApplyShapeOnce's size gate.
	// Under a bar the corner's page is ink (the mock: the header runs up behind the status bar) and the content host paints the sand below it; otherwise the page keeps its own colour and the host paints the same.
	private void ApplyBarColours()
	{
		if (CurrentChrome() is not { } chrome)
		{
			return; // no layout pass yet: the first SizeChanged / ApplyShapeOnce paints, with no guessed window size
		}

		bool grownUp = _mode.IsGrownUp;
		bool ink = StatusBarIcons.InkUnderBar(grownUp, chrome.IsRail);
		string content = grownUp ? DinoTokens.Sand : DinoTokens.Cream;
		BackgroundColor = DinoTokens.Get(ink ? DinoTokens.Ink : content);
		ContentHost.BackgroundColor = DinoTokens.Get(content);
		SystemBarIcons.ForPage(this);
#if WINDOWS
		DinoTitleBar.Refresh(this); // Slice 235 part B: the title bar reads the page colour set just above
#endif
	}

	// The chrome the layout last built: the one value the bar colours, the status-bar icons and the layout all read (never _windowSize on its own, which runs ahead of the layout between a resize and its pass). Null until the first pass.
	private ChromeShape? CurrentChrome() => _chromeBuilt ? _chrome : null;

	private bool _chromeBuilt;

	private Size _windowSize;
	private string? _shapeKey; // what the last layout pass built, so a pass that changes nothing does nothing
	private string? _anchorBookId; // the first book on screen (its id, not an index), kept across a rotation; null means the first book
	private ScrollSwapPhase _swapPhase; // the list raises Scrolled itself when its layout is replaced and for the ScrollTo that follows; those events are not the user scrolling
	private int _swapGeneration; // bumped by every layout swap, so a swap's timeout never ends a newer swap
	private static readonly TimeSpan SwapTimeout = TimeSpan.FromMilliseconds(400);
	private bool _shapeGrownUp;

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		(width, height) = WindowShapeSeam.Effective(DesktopTips.IsWindows, width, height);
		if (width > 0 && height > 0 && (width != _windowSize.Width || height != _windowSize.Height))
		{
			_windowSize = new Size(width, height);
			ApplyShape();
			return;
		}

		if (_windowSize.Width > 0)
		{
			Replay(); // work that waited for a real window size (or any other part) is served now
		}
	}

	// Slice 232 part B (mock Batch 44 frames 110, 111, 125, 126): every size comes from one pure call on the window size (LandscapeLayout.ShelfLayout / CornerLayout); this page only applies it.
	// Rotation changes the layout only: grown-up mode, the open sheet and the first book on screen are kept.
	// Asked from anywhere (a size or mode change, even from inside a pass): it is a request to the one queue, which applies it on the UI thread as one piece, after the running pass.
	private void ApplyShape() => RequestChrome();

	private void ApplyShapeOnce()
	{
		if (_windowSize.Width <= 0)
		{
			return;
		}

		bool grownUp = _mode.IsGrownUp;
		ChromeShape chrome = CornerLayout.ChromeOf(_windowSize.Width, _windowSize.Height, DesktopTips.IsWindows);
		bool rail = chrome.IsRail;
		int columns = grownUp ? CornerLayout.BookColumns(chrome, _windowSize.Width) : ShelfTitleRules.Columns(chrome, _windowSize.Width);
		string key = $"{chrome}|{columns}|{grownUp}";
		_shelfGeometry = grownUp ? CornerLayout.BooksGeometry(chrome, columns) : ShelfTitleRules.Geometry(chrome, columns);
		WindowShapeSeam.ShelfGeometry shelfOnPage = rail ? _shelfGeometry.BesideRail(chrome.RailWidth) : _shelfGeometry; // the layer spans the whole page, so beside a rail it starts where the shelf list starts on the page
		bool tablet = chrome.IsTabletPortrait; // the corner's and the shelf's classification are one now
		bool useShelf = WindowShapeSeam.ReorderUsesShelfGeometry(tablet, _windowSize.Width, _windowSize.Height); // no mock frame for a tablet or a phone held sideways
		_reorderGeometry = ReorderShape(tablet, useShelf, shelfOnPage);
		_reorder.Reshape(_reorderGeometry); // a rotation or resize while the mode is open re-fits it
		if (key == _shapeKey)
		{
			ApplyTabShape(); // the same chrome at another width: the Store tab's columns follow the content width
			return;
		}

		_shapeKey = key;
		_chrome = chrome;
		_chromeBuilt = true;
		if (grownUp != _shapeGrownUp)
		{
			_anchorBookId = null; // the other mode lists other tiles: its first book is not this one's
			_shapeGrownUp = grownUp;
		}

#if WINDOWS
		WindowTrace.Write($"Dino shelf: columns={columns} sideways=False rail={rail} grownUp={grownUp} content={_windowSize.Width:0}x{_windowSize.Height:0}"); // the VM check reads the shelf's shape from this
#endif
		ApplyChrome(chrome, grownUp);
		ApplyTileSizes(chrome, grownUp);
		ApplyShelfList(columns);
		LogShape(chrome, columns, grownUp);
		ApplyBarColours(); // a rotation moves the ink header under or away from the status bar
	}

	// Slice 325: the debug markers the redesign flows read, through TestHookLog (RTST-TEST tag). Compiled out of store builds.
	private void LogShape(ChromeShape chrome, int columns, bool grownUp)
	{
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] shelf layout={(chrome.IsRail ? "rail" : "grid")} columns={columns} grownUp={grownUp} railWidth={chrome.RailWidth} tabletUpright={chrome.IsTabletPortrait && !grownUp} tabletCorner={chrome.IsTabletPortrait && grownUp}");
		if (grownUp)
		{
			LogCorner();
		}
#endif
	}

	// [Redesign] shelf title=full|short|hidden chrome=bar|rail columns=<n> (Slice 325 C4), written when the header has fitted its title, so the marker tells what is on screen.
	private void LogShelf(ShelfTitle title)
	{
#if DEBUG
		if (!_mode.IsGrownUp)
		{
			int columns = ShelfTitleRules.Columns(_chrome, _windowSize.Width);
			Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Redesign] shelf title={title.ToString().ToLowerInvariant()} chrome={(_chrome.IsRail ? "rail" : "bar")} columns={columns}");
		}
#endif
	}

	// [Redesign] corner tab=<tab> chrome=bar|rail railWidth=<n> columns=<n> (Slice 325 A6).
	private void LogCorner()
	{
#if DEBUG
		ChromeShape chrome = _chrome;
		int columns = CornerLayout.BookColumns(chrome, _windowSize.Width);
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Redesign] corner tab={_mode.Tab.ToString().ToLowerInvariant()} chrome={(chrome.IsRail ? "rail" : "bar")} railWidth={chrome.RailWidth} columns={columns}");
#endif
	}

	// The Reorder layer's grid and hint (Slice 321): the Windows head takes the mock's frame 176 over the whole page (the layer covers the rail, so no rail offset), a tablet or a phone held sideways the shelf's geometry, a phone upright frames 173/174.
	private WindowShapeSeam.ReorderLayerShape ReorderShape(bool tablet, bool useShelf, WindowShapeSeam.ShelfGeometry shelfOnPage)
	{
		if (WindowShapeSeam.ReorderUsesDesktopGeometry(DesktopTips.IsWindows, tablet, _windowSize.Width, _windowSize.Height))
		{
			return WindowShapeSeam.ReorderLayerDesktop(_windowSize.Width);
		}

		return useShelf ? WindowShapeSeam.ReorderLayerShelf(shelfOnPage) : WindowShapeSeam.ReorderLayerPhone();
	}

	// Slice 325: the corner's ink header (or rail, with the tab strip inside it and the tab bar gone) or the shelf's cream header (or rail). The rail column is 0 wide under a bar.
	private void ApplyChrome(ChromeShape chrome, bool grownUp)
	{
		_chromeApplying = true; // the detach and attach of the tabs below fire their HandlerChanged: Replay ignores it until this returns
		try
		{
			ApplyChromeCore(chrome, grownUp);
		}
		finally
		{
			_chromeApplying = false;
		}
	}

	private void ApplyChromeCore(ChromeShape chrome, bool grownUp)
	{
		bool rail = chrome.IsRail;
		GalleryRoot.ColumnDefinitions[0].Width = new GridLength(chrome.RailWidth);
		_corner.IsVisible = grownUp;
		_shelfHeader.IsVisible = !grownUp;
		View active = grownUp ? _corner : _shelfHeader;
		Grid.SetRow(active, 0);
		Grid.SetColumn(active, 0);
		Grid.SetRowSpan(active, rail ? GalleryRoot.RowDefinitions.Count : 1);
		Grid.SetColumnSpan(active, rail ? 1 : GalleryRoot.ColumnDefinitions.Count);
		Grid.SetRow(ContentHost, rail ? 0 : 1);
		Grid.SetRowSpan(ContentHost, rail ? GalleryRoot.RowDefinitions.Count : 1);
		Grid.SetColumn(ContentHost, rail ? 1 : 0);
		Grid.SetColumnSpan(ContentHost, rail ? 1 : GalleryRoot.ColumnDefinitions.Count);
		ContentHost.SafeAreaEdges = ContentSafeArea(rail, grownUp);
		if (grownUp)
		{
			ApplyCorner(chrome);
		}
		else
		{
			CornerShell.DetachTabs(_tabs);
			_tabs.IsVisible = false;
			ContentHeader.IsVisible = false;
			_shelfHeader.Apply(chrome, ShelfTitleRules.SizesOf(chrome), _languageButton.IsVisible);
		}

		ApplyTab(); // Slice 325 part B: after the chrome, so the hosted view takes this pass's sizes
	}

	// The content keeps clear of the notch on its sides; the corner's bottom bar or the shelf's covers (K2: they run under the home bar) decide the bottom.
	private static SafeAreaEdges ContentSafeArea(bool rail, bool grownUp)
	{
		SafeAreaRegions bottom = rail && grownUp ? SafeAreaRegions.Container : SafeAreaRegions.None;
		return new SafeAreaEdges(SafeAreaRegions.Container, rail ? SafeAreaRegions.Container : SafeAreaRegions.None, SafeAreaRegions.Container, bottom);
	}

	private void ApplyCorner(ChromeShape chrome)
	{
		CornerSizes sizes = CornerLayout.SizesOf(chrome);
		bool handover = FirstRunState.HandoverPending;
		string pill = handover ? AppResources.Gallery_ReadyForChild : chrome.FullWords ? AppResources.Gallery_BackToShelf : AppResources.Corner_ChildsShelf;
		_corner.Apply(chrome, sizes, pill, handover ? AppResources.Gallery_ReadyForChild : AppResources.Gallery_BackToShelf);
		_tabs.Apply(chrome.IsRail, sizes);
		_tabs.Selected = _mode.Tab;
		if (chrome.IsRail)
		{
			_tabs.IsVisible = true;
			_corner.AttachTabs(_tabs);
		}
		else
		{
			CornerShell.DetachTabs(_tabs);
			Grid.SetRow(_tabs, 2);
			Grid.SetColumn(_tabs, 0);
			Grid.SetColumnSpan(_tabs, GalleryRoot.ColumnDefinitions.Count);
			if (_tabs.Parent != GalleryRoot)
			{
				GalleryRoot.Children.Insert(0, _tabs);
			}

			_tabs.IsVisible = true;
		}

		ApplyContentHeader(chrome, sizes);
	}

	// Under a bar the one-line note ("Only grown-ups see this. Your child's shelf is one tap away.") sits above the books; in a rail the content has the tab's title, and a phone held sideways puts the short note beside it.
	private void ApplyContentHeader(ChromeShape chrome, CornerSizes s)
	{
		ContentHeader.IsVisible = true;
		ContentTitle.IsVisible = chrome.IsRail;
		ContentNote.IsVisible = !chrome.IsRail || !s.NoteInRail; // the tablet and Windows rails hold the note themselves
		ContentTitle.FontSize = s.ContentTitle;
		ContentNote.FontSize = s.NoteSize;
		ContentNote.Text = chrome.IsRail ? AppResources.Gallery_CornerLineShort : AppResources.Gallery_CornerLine;
		Grid.SetColumn(ContentNote, chrome.IsRail ? 1 : 0);
		Grid.SetColumnSpan(ContentNote, chrome.IsRail ? 1 : 2);
		ContentHeader.Padding = chrome.IsRail
			? new Thickness(s.Content.Left, s.Content.Top, s.Content.Right, s.ContentGap)
			: new Thickness(s.Note.Left, s.Note.Top, s.Note.Right, s.Note.Bottom);
	}

	// The tab's name is the ink header's title (under a bar) and the content's title (in a rail); the Store tab has its sub-line under it (frame T2).
	private void ApplyContentHeaderTexts()
	{
		CornerTab tab = _mode.IsGrownUp ? _mode.Tab : CornerTab.Books;
		string title = tab switch
		{
			CornerTab.Store => AppResources.Store_Title,
			CornerTab.Settings => AppResources.Settings_Title,
			_ => AppResources.Corner_TabBooks,
		};
		ContentTitle.Text = title;
		_corner.SetTitle(title);
		ContentSub.IsVisible = tab == CornerTab.Store;
		ContentSub.Text = AppResources.Store_Subtitle;
	}

	// The cover, its title and the grown-ups' "⋯" dot follow the window's family; they are page resources the tiles bind to, written only when the value really changes.
	private void ApplyTileSizes(ChromeShape chrome, bool grownUp)
	{
		double radius;
		double title;
		double gap;
		double shadowOpacity = grownUp ? CornerCoverShadowOpacity : ShelfCoverShadowOpacity;
		if (grownUp)
		{
			CornerSizes s = CornerLayout.SizesOf(chrome);
			(radius, title, gap) = (s.CoverRadius, s.TileTitle, s.TileGap);
			SetResourceIfChanged(TileMoreSizeKey, s.DotSize);
			SetResourceIfChanged(TileMoreRadiusKey, new CornerRadius(s.DotSize * MoreDotRadiusShare));
			SetResourceIfChanged(TileMoreInsetKey, new Thickness(0, s.DotInset, s.DotInset, 0));
		}
		else
		{
			ShelfSizes s = ShelfTitleRules.SizesOf(chrome);
			(radius, title, gap) = (s.CoverRadius, s.TileTitle, s.TileGap);
		}

		SetResourceIfChanged(TileCoverRadiusKey, new CornerRadius(radius));
		SetResourceIfChanged(TileTitleSizeKey, title);
		SetResourceIfChanged(TileSpacingKey, gap);
		SetResourceIfChanged(TileShadowOpacityKey, shadowOpacity);
		SetResourceIfChanged(TileShadowOffsetKey, grownUp ? DefaultShadowOffset : ShelfTitleRules.SizesOf(chrome).ShadowOffset); // 4 dp, and 5 on a tablet held upright (mock KT1); StoryCover.ShadowDepth carries it to Shadow.Offset
	}

	// A grid of this many columns (the sideways row retired with the 2026-11-08 design). The first book on screen stays on screen.
	private void ApplyShelfList(int columns)
	{
		double horizontalGap = _shelfGeometry.GapX; // the same record the Reorder layer copies, so the two gaps cannot drift
		double verticalGap = _shelfGeometry.GapY;
		_swapPhase = ScrollSwapPhase.SwapStarted;
		_swapGeneration++;
		if (StoryList.ItemsLayout is not GridItemsLayout)
		{
			StoryList.ItemsLayout = new GridItemsLayout(columns, ItemsLayoutOrientation.Vertical) { HorizontalItemSpacing = horizontalGap, VerticalItemSpacing = verticalGap };
		}
		else
		{
			var grid = (GridItemsLayout)StoryList.ItemsLayout;
			if (grid.Span != columns)
			{
				grid.Span = columns;
			}

			if (grid.HorizontalItemSpacing != horizontalGap)
			{
				grid.HorizontalItemSpacing = horizontalGap;
			}

			if (grid.VerticalItemSpacing != verticalGap)
			{
				grid.VerticalItemSpacing = verticalGap;
			}
		}

		SetResourceIfChanged(TileWidthKey, -1.0);
		SetResourceIfChanged(TileTitleLinesKey, 2);
		ApplyShelfMargin(_shelfGeometry);
		KeepFirstBookOnScreen();
	}

	// The list's own sides and top: a pure function of the chrome (CornerLayout.BooksGeometry, ShelfTitleRules.Geometry), never of the list's measured size (a layout cycle on the VM, Slice 235 part C).
	private void ApplyShelfMargin(WindowShapeSeam.ShelfGeometry geometry)
	{
		var margin = new Thickness(geometry.Left, geometry.Top, geometry.Right, 0);
		if (StoryList.Margin != margin)
		{
			StoryList.Margin = margin;
		}
	}

	// A resource write re-measures every tile bound to it, so it is made only when the value really changes.
	private void SetResourceIfChanged(string key, object value)
	{
		if (!Resources.TryGetValue(key, out object? current) || !Equals(current, value))
		{
			Resources[key] = value;
		}
	}

	// Replacing ItemsLayout alone left the list blank on Android after a rotation (the RecyclerView kept its old layout manager's state; likely, not proven from MAUI source): a fresh bind draws the covers on both platforms.
	private void ReattachItems()
	{
		StoryList.ItemsSource = null;
		StoryList.ItemsSource = _items;
	}

	private string BookIdAt(int index) => index >= 0 && index < _items.Count && _items[index] is StoryTile tile ? tile.Story.Id : string.Empty;

	// A Scrolled event moves the anchor only when no swap is pending; the first one after the post-swap ScrollTo ends the swap (ScrollAnchorRules).
	private void RememberFirstBook(int index)
	{
		if (ScrollAnchorRules.ShouldUpdateAnchor(_swapPhase) && BookIdAt(index) is { Length: > 0 } id)
		{
			_anchorBookId = id;
		}

		_swapPhase = ScrollAnchorRules.AfterScrolled(_swapPhase);
	}

	// After a layout swap: scroll to the anchor book (found by id), then wait for that ScrollTo's own Scrolled event; a short timeout ends the swap if none comes (the list was already there).
	private void KeepFirstBookOnScreen()
	{
		if (_items.Count == 0)
		{
			_swapPhase = ScrollSwapPhase.Idle;
			return;
		}

		int generation = _swapGeneration;
		Dispatcher.Dispatch(() =>
		{
			if (generation != _swapGeneration)
			{
				return; // a newer swap took over
			}

			List<string> ids = Enumerable.Range(0, _items.Count).Select(BookIdAt).ToList();
			StoryList.ScrollTo(ScrollAnchorRules.IndexOfAnchor(ids, _anchorBookId), position: ScrollToPosition.Start, animate: false);
			_swapPhase = ScrollAnchorRules.AfterScrollToIssued(_swapPhase);
			Dispatcher.DispatchDelayed(SwapTimeout, () => EndSwapAfterTimeout(generation));
		});
	}

	private void EndSwapAfterTimeout(int generation)
	{
		if (generation == _swapGeneration)
		{
			_swapPhase = ScrollSwapPhase.Idle;
		}
	}

	private static StoryTile? TileOf(object? sender) => (sender as BindableObject)?.BindingContext as StoryTile;

	/// <summary>True while the shelf shows the grown-ups' corner; the Windows title bar goes ink then (Slice 235 part B).</summary>
	internal bool InCorner => _mode.IsGrownUp;

	// Slice 235 part B: the shelf answers the Windows window's keyboard. Esc closes the book's options sheet while it is open; on the corner it goes back to the child's shelf (Slice 325 part A); on the shelf it asks
	// "Close Dino Book Reader?" (part C4).
	DinoScreen IDinoKeyTarget.Screen => _tour is { IsActive: true } ? DinoScreen.FirstBooks : _reorder.IsVisible ? DinoScreen.Reorder : BookInfo.IsVisible ? DinoScreen.Sheet : _mode.IsGrownUp ? DinoScreen.Corner : DinoScreen.Shelf;

	bool IDinoKeyTarget.Handle(DinoKeyAction action, DinoKey key)
	{
		if (_tour is { IsActive: true })
		{
			// Slice 362 (Windows): Enter is the card's main button; Esc and Space do nothing while the tour shows (it reports DinoScreen.FirstBooks, so Enter is PickContinue)
			return action == DinoKeyAction.PickContinue ? _tour.Advance() : action is DinoKeyAction.Back or DinoKeyAction.PickToggle;
		}

		if (_reorder.IsVisible)
		{
			return HandleReorderKey(action); // Slice 264: Esc cancels the reorder mode, Enter is Done (Space lifts and drops a book)
		}

		if (action is DinoKeyAction.AskToClose or DinoKeyAction.LeaveCorner or DinoKeyAction.CornerBooks or DinoKeyAction.CornerStore or DinoKeyAction.CornerSettings or DinoKeyAction.OpenRow)
		{
			return HandleCornerKey(action);
		}

		if (action == DinoKeyAction.SheetActivate)
		{
			return BookInfo.IsVisible && BookInfo.ActivateFocused();
		}

		if (action != DinoKeyAction.CloseSheet || !BookInfo.IsVisible)
		{
			return false;
		}

		BookInfo.Dismiss();
		return true;
	}

	// Esc and Ctrl+1/2/3 (Slice 325): an open language list closes first; then the corner's way back is the coral pill's path, a tab key picks a tab, and on the shelf Esc asks to close the app.
	private bool HandleCornerKey(DinoKeyAction action)
	{
		if (StoryLanguageMenu.DismissOpen(GalleryRoot))
		{
			return true;
		}

		switch (action)
		{
			case DinoKeyAction.LeaveCorner when _mode.IsGrownUp:
				CompleteHandover();
				return true;
			case DinoKeyAction.AskToClose when !_mode.IsGrownUp:
				return AskToClose();
			case DinoKeyAction.CornerBooks when _mode.IsGrownUp:
				_ = SelectTabAsync(CornerTab.Books);
				return true;
			case DinoKeyAction.CornerStore when _mode.IsGrownUp:
				_ = SelectTabAsync(CornerTab.Store);
				return true;
			case DinoKeyAction.CornerSettings when _mode.IsGrownUp:
				_ = SelectTabAsync(CornerTab.Settings);
				return true;
			case DinoKeyAction.OpenRow when _mode.IsGrownUp && _mode.Tab == CornerTab.Settings: // the Right arrow opens the focused Settings row (frame 157), or moves the focus when none is
				return _settingsTab?.OpenFocusedRow() == true;
			default:
				return false;
		}
	}

	// Slice 264: the reorder mode ("Reorder" in a book's sheet, or a 1 second press on a cover). It works on a copy of the shelf's order held by the layer; only Done saves it, through the library,
	// so Cancel, Back, leaving grown-up mode, the app going to the background and the app being killed all keep the stored order as it was.
	private bool HandleReorderKey(DinoKeyAction action)
	{
		switch (action)
		{
			case DinoKeyAction.ReorderCancel:
				CancelReorder();
				return true;
			case DinoKeyAction.ReorderDone:
				_ = DoneReorderAsync(_reorder.Order);
				return true;
			default:
				return _reorder.HandleKey(action);
		}
	}

	private void OpenReorder(string? liftedId)
	{
		List<StoryTile> tiles = [.. _items.OfType<StoryTile>()];
		if (!_mode.IsGrownUp || _reorder.IsVisible || tiles.Count == 0)
		{
			return;
		}

		ReorderTrace.Step($"page open begin books={tiles.Count}");
		CancelSheetCount();
		BookInfo.Hide();
		ReorderTrace.Step("page sheet hidden");
		_suppressTapUntil = Environment.TickCount64 + TapSuppressMilliseconds;
		_reorder.Open(tiles, liftedId, _reorderGeometry, (double)Resources[TileTitleHeightKey]);
		ReorderTrace.Step("page open end");
		ReorderLog($"opened with {tiles.Count} books, lifted={liftedId ?? "none"}");
	}

	// The sheet closed with Hide, not Dismiss, so a switch saved in it is refreshed here before the layer takes its snapshot of the shelf.
	private Task OpenSheetReorderAsync() => RunGrownUpAsync(async () =>
	{
		ReorderTrace.Step("sheet reorder row chosen");
		await SettledSheetStoryAsync();
		ReorderTrace.Step("sheet settled");
		if (_sheetChanged)
		{
			_sheetChanged = false;
			await RefreshShelfAsync();
		}

		OpenReorder(_sheetStory?.Id);
	});

	private void OnHoldRequested(object? sender, HoldRequestedEventArgs e)
	{
		if (TileOf(sender) is { } tile && _mode.IsGrownUp && !BookInfo.IsVisible)
		{
			OpenReorder(tile.Story.Id);
			e.Opened = _reorder.IsVisible;
		}
	}

	private async Task DoneReorderAsync(IReadOnlyList<string> ids)
	{
		if (!_reorder.IsVisible || _savingOrder)
		{
			return;
		}

		_savingOrder = true; // Enter then a tap on Done during the save must not save twice
		try
		{
			if (!await TrySaveOrderAsync(ids))
			{
				await DisplayAlertAsync(AppResources.Reorder_SaveFailedTitle, AppResources.Reorder_SaveFailedBody, AppResources.Common_Ok);
				return; // the mode stays open with the edited order; Done tries again, Cancel leaves
			}

			CloseReorder("done");
			await RefreshShelfAsync();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// no-fallback: logged only; the reorder mode stays as it was
			Debug.WriteLine($"Dino Reader could not finish the reorder: {ex}"); // never unobserved out of the keyboard path
		}
		finally
		{
			_savingOrder = false;
		}
	}

	private async Task<bool> TrySaveOrderAsync(IReadOnlyList<string> ids)
	{
		try
		{
			if (_library is not FileStoryLibrary file)
			{
				Debug.WriteLine("Dino Reader cannot save the shelf order: the library is not the file library");
				return false;
			}

			await file.SaveOrderAsync(ids);
			ReorderLog($"done saved {ids.Count} books: {string.Join(",", ids)}");
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) // ArgumentException: an id that is not a story id (Slice 298)
		{
			// no-fallback: the save fails and the person is told (false); no default is used
			Debug.WriteLine($"Dino Reader could not save the shelf order: {ex}"); // the stored order stays as it was
			return false;
		}
	}

	// Cancel while Done's save runs would claim "nothing saved" after the order was written, so the save wins.
	private void CancelReorder()
	{
		if (!_savingOrder)
		{
			CloseReorder("cancel");
		}
	}

	private void CloseReorder(string why)
	{
		if (_reorder.IsVisible)
		{
			_reorder.Close();
			ReorderLog($"closed: {why}");
		}
	}

	[Conditional("DEBUG"), Conditional("DINO_TEST_BOOKS")] // compiled out of store builds
	private static void SheetLog(string line)
	{
		ReorderTrace.Step("sheet: " + line); // Release-visible on Windows (window-trace.log)
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[BookSheet] {line}"); // tells a sheet that never opened from one that opened and was hidden
#endif
	}

	private static void ReorderLog(string line)
	{
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Reorder] {line}"); // the shelf-reorder flow's expect-log markers (RTST-TEST tag)
#endif
	}

#if DEBUG
	/// <summary>Debug-only hook (<c>enter-grown-up tab=</c>, Slice 325 A6): picks a tab of the corner the way a tap on it does.</summary>
	internal Task DebugSelectTabAsync(CornerTab tab) => SelectTabAsync(tab);

	/// <summary>Debug-only hook for the shelf-reorder flow: opens the mode if it is closed, then moves the book at 1-based position <paramref name="from"/> before the one at <paramref name="to"/>, as a drop does.</summary>
	internal string DebugReorderMove(int from, int to)
	{
		OpenReorder(null);
		return _reorder.IsVisible ? _reorder.DebugMove(from, to) : string.Empty;
	}
#endif

	private async void OnStoryTapped(object? sender, TappedEventArgs e)
	{
		if (_reorder.IsVisible || Environment.TickCount64 < _suppressTapUntil)
		{
			SheetLog(_reorder.IsVisible ? "open dropped: reorder mode is open" : "open dropped: tap suppressed after a press");
			return;
		}

		if (TileOf(sender) is { } tile)
		{
			await OpenTileAsync(tile); // no-tap-count: OpenTileAsync counts the press (UiUsage.Tap)
		}
	}

	// A tap on a cover (kids' mode: read it; grown-up mode: its options sheet). The Windows keyboard (Enter or Space on a focused book) takes the same path.
	private Task OpenTileAsync(StoryTile tile)
	{
		UiUsage.Tap(UiControls.Book); // Slice 352: on the shelf it starts the read, in the corner it opens the book's sheet; the screen in the count says which
		return _busy.RunAsync(() => _mode.IsGrownUp ? OpenBookSheetAsync(tile.Story) : _actions.ReadAsync(Navigation, tile.Story));
	}

	// Slice 235 part C: Windows-only wiring on the native list (Enter, Space, right-click, focus return after a sheet), in Platforms/Windows/GalleryPage.Desktop.cs. No-ops elsewhere.
	partial void WireDesktopShelf();

	partial void ApplyShelfRing();

	partial void RememberTileFocus(Story opened);

	partial void RestoreTileFocus();

	// Grown-up mode: a book's "⋯" or its bare cover opens its options sheet (Slice 189, mock 85b; it grew out of the "This book" sheet of Slices 110 and 111):
	// Read with my voice, Record, Cast, Edit pages (own books), Export (developer switch) and Remove from the shelf.
	private async Task OpenBookSheetAsync(Story tapped)
	{
		RememberTileFocus(tapped);
		Story story = await _library.GetAsync(tapped.Id) ?? tapped;
		_sheetStory = story;
		_sheetChanged = false;
		BookSheet.Content content = SheetContentOf(story);
		BookInfo.Show(content);
		SheetLog($"opened, visible={BookInfo.IsVisible}");
		CancelSheetCount();
		var counting = new CancellationTokenSource();
		_sheetCountCts = counting;
		_ = ShowVoiceWhenReadyAsync(story, content.Plan, counting.Token);
	}

	private void CancelSheetCount()
	{
		_sheetCountCts?.Cancel();
		_sheetCountCts = null;
	}

	private BookSheet.Content SheetContentOf(Story story)
	{
		BookOrigin origin = BookOrigins.Of(story);
		StoryTile tile = StoryTile.From(story, _library, _mode);
		string title = ShelfTitleOf(story);
		var plan = BookSheetPlan.For(origin, StoryRecordings.Count(story) > 0, DeveloperMode.Enabled);
		return new BookSheet.Content(title, BookOrigins.Label(origin), tile.CoverImagePath, tile.CoverFill, plan, Voice: null);
	}

	// The count needs the whole book split into sentences and speakers, which can take a while on a long book: the sheet opens at once and the voice row and the count show when that is ready (Slice 279).
	private async Task ShowVoiceWhenReadyAsync(Story story, BookSheetPlan plan, CancellationToken cancellation)
	{
		BookSheet.VoiceOffer? offer;
		try
		{
			offer = await VoiceOfferOfAsync(story, cancellation);
		}
		catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
		{
			// no-fallback: the sheet closed or another book opened; the count is dropped
			return;
		}
		catch (Exception ex)
		{
			// no-fallback: the sheet stays without the voice row and the count; nothing is lost
			Debug.WriteLine($"Dino Reader could not count the recordings of a book: {ex.Message}");
			return;
		}

		if (offer is not null && !cancellation.IsCancellationRequested && BookInfo.IsVisible && _sheetStory?.Id == story.Id)
		{
			BookInfo.ShowVoice(plan, offer with { On = _sheetStory.UseRecordings });
		}
	}

	// "Read with my voice" and the Record row's count show only once something is recorded (Slice 279): lines recorded of all lines, or pages for a book recorded as whole pages.
	private async Task<BookSheet.VoiceOffer?> VoiceOfferOfAsync(Story story, CancellationToken cancellation)
	{
		if (StoryRecordings.Count(story) == 0)
		{
			return null;
		}

		RecordBook source = await Task.Run(() => RecordSplitBook.Of(story), cancellation);
		// every story language that has recordings, in story-language order, each counted in its own mode's unit and from its own lines (Slice 286, frames 193 and 195)
		var languages = new List<BookSheet.LanguageProgress>();
		foreach (string language in StoryLanguages.Current().All.Where(l => StoryRecordings.Count(story, l) > 0))
		{
			languages.Add(await LanguageProgressOfAsync(story, source, language, cancellation));
		}

		return languages.Count == 0 ? null : new BookSheet.VoiceOffer(languages, story.UseRecordings);
	}

	// One language's count from its own words (the translation's, when it reads translated). When the translation cannot be made (offline) the language still shows, with the pages recorded of all pages
	// (the same in every language), so the voice row never vanishes while recordings exist; the next time the sheet opens the words are tried again.
	private async Task<BookSheet.LanguageProgress> LanguageProgressOfAsync(Story story, RecordBook source, string language, CancellationToken cancellation)
	{
		try
		{
			RecordBook? book = await RecordBooks.InAsync(_services, story, source, language, cancellation);
			if (book is null)
			{
				return PagesOnly(story, source, language);
			}

			RecordMode mode = RecordChoices.ModeOf(story, language);
			IReadOnlyList<RecordPiece> lines = mode == RecordMode.PerCharacter ? await Task.Run(() => RecordBooks.AllLines(story, book, language), cancellation) : [];
			return new BookSheet.LanguageProgress(language, RecordBooks.Progress(story, book, lines, mode, language));
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !cancellation.IsCancellationRequested)
		{
			// no-fallback: the language shows the pages recorded; nothing is lost
			Debug.WriteLine($"Dino Reader could not count the recordings in {language} for the sheet: {ex.Message}");
			return PagesOnly(story, source, language);
		}
	}

	// A language recorded per line cannot be counted in pages (a page needs every sentence), so it shows the clips recorded alone, never a wrong "0 of N"; whole pages are the same in every language.
	private static BookSheet.LanguageProgress PagesOnly(Story story, RecordBook source, string language) =>
		RecordChoices.ModeOf(story, language) == RecordMode.WholePage
			? new(language, RecordSplitWalk.Progress(story, source, RecordMode.WholePage, language))
			: new(language, new RecordProgress(StoryRecordings.Count(story, language), 0, RecordUnit.Lines), TotalKnown: false);

	private Task SaveUseVoiceAsync(bool useVoice) => SaveSheetStoryAsync(story => story with { UseRecordings = useVoice });

	// Each switch is remembered per story in story.json, on top of the latest stored copy (cast edits saved meanwhile stay); one save at a time. A failed save only loses the preference.
	private async Task SaveSheetStoryAsync(Func<Story, Story> change)
	{
		await _sheetSaveLock.WaitAsync();
		try
		{
			if (_sheetStory is { } shown)
			{
				Story updated = change(await _library.GetAsync(shown.Id) ?? shown);
				await _library.UpdateAsync(updated);
				_sheetStory = updated;
				_sheetChanged = true;
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// no-fallback: logged only; the book switch keeps its old value
			Debug.WriteLine($"Dino Reader could not save the book switch: {ex}");
		}
		finally
		{
			_sheetSaveLock.Release();
		}
	}

	// A switch tapped just before an action (the quiet rows close the sheet first) is saved before the action reads the story, so the editor never saves the pre-switch copy.
	private async Task<Story?> SettledSheetStoryAsync()
	{
		await _sheetSaveLock.WaitAsync();
		_sheetSaveLock.Release();
		return _sheetStory;
	}

	// Read now (Slice 228): the sheet is already closed; the page leaves grown-up mode (ending a pending hand-over, as "Ready for my child" would) and opens the book, so Back from the reader lands on the kids' shelf.
	private async Task ReadSheetBookAsync()
	{
		if (await SettledSheetStoryAsync() is { } story)
		{
			await RunGrownUpAsync(() => LeaveAndReadAsync(story));
		}
	}

	private async Task LeaveAndReadAsync(Story story)
	{
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[GrownUps] read-now leaves the corner and opens {story.Id}"); // the corner-read-now flow's expect-log marker (RTST-TEST tag)
#endif
		FirstRunState.HandoverPending = false;
		_mode.Leave();
		await _actions.ReadAsync(Navigation, story, ReadOrigins.Corner);
	}

	private async Task OpenSheetRecordAsync()
	{
		if (await SettledSheetStoryAsync() is { } story)
		{
			await RunGrownUpAsync(async () =>
			{
				if (await _actions.RecordAsync(Navigation, story))
				{
					_reopenSheetFor = story.Id; // set only once the intro is pushed: Back from the record flow returns to this book's sheet (mock frame 178)
				}
			});
		}
	}

	private async Task ExportSheetBookAsync()
	{
		if (await SettledSheetStoryAsync() is { } story)
		{
			await RunGrownUpAsync(() => _actions.ExportAsync(story));
		}
	}

	// Remove from the shelf (Slice 189): asks first, with the sheet still open behind the question; a Store book says it can come back for free. Declined leaves the sheet as it was.
	private async Task RemoveSheetBookAsync()
	{
		if (_sheetStory is { } story)
		{
			await RunGrownUpAsync(() => RemoveBookAsync(story));
		}
	}

	// Holds the sheet's save lock from the question to the delete, so a pending switch save cannot write the folder back; a save queued behind it finds no sheet story and skips.
	private async Task RemoveBookAsync(Story story)
	{
		await _sheetSaveLock.WaitAsync();
		try
		{
			DeleteOutcome outcome = await _actions.ConfirmDeleteAsync(this, story, RemovePrompt(story, ShelfTitleOf(story)));
			if (outcome == DeleteOutcome.Declined)
			{
				return;
			}

			CancelSheetCount();
			BookInfo.Hide();
			_sheetStory = null;
			_sheetChanged = false;
		}
		finally
		{
			_sheetSaveLock.Release();
		}

		await RefreshShelfAsync(); // also after a failed delete: a half-deleted story no longer lists
	}

	// The question names the book as the shelf and the sheet do (its translated title).
	private static DeletePrompt RemovePrompt(Story story, string shelfTitle)
	{
		bool fromStore = BookOrigins.Of(story) == BookOrigin.Store;
		string body = fromStore ? AppResources.Book_RemoveBodyStore : AppResources.Delete_Body;
		if (StoryRecordings.Count(story) > 0)
		{
			body = string.Concat(body, "\n\n", AppResources.Record_RemoveWarn); // Slice 279: a book with recordings says they go too
		}

		return new DeletePrompt(AppResources.Format(AppResources.Book_RemoveTitle, shelfTitle), body, AppResources.Book_RemoveConfirm);
	}

	// Waits for a save still in flight (a quick Back after a switch) so the tiles never keep the old flags; never throws out of async void.
	private async void OnBookSheetDismissed()
	{
		CancelSheetCount();
		RestoreTileFocus(); // Slice 235 part C: Esc or a click outside returns the keyboard to the book that opened the sheet (Windows only)
		try
		{
			await _sheetSaveLock.WaitAsync();
			_sheetSaveLock.Release();
			if (_sheetChanged)
			{
				_sheetChanged = false;
				await RefreshShelfAsync();
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// no-fallback: logged only; the shelf stays as it was
			Debug.WriteLine($"Dino Reader could not refresh the shelf after the book sheet: {ex}");
		}
	}

	private void OnTitleProbeSizeChanged(object? sender, EventArgs e)
	{
		if (TitleProbe.Height > 0 && Math.Abs(TitleProbe.Height - (double)Resources[TileTitleHeightKey]) > 0.5)
		{
			Resources[TileTitleHeightKey] = Math.Ceiling(TitleProbe.Height);
		}
	}

	// Every grown-up action re-checks the mode right before it runs: a queued tap must not outlive the mode.
	private Task RunGrownUpAsync(Func<Task> work) => _busy.RunAsync(() =>
	{
		if (!_mode.IsGrownUp)
		{
			SheetLog("grown-up action dropped: not in grown-up mode");
			return Task.CompletedTask;
		}

		return work();
	});

	// A tab of the corner (Slice 325 parts A and B): Books is the grid on this page; the Store and Settings tabs are views shown in TabHost inside the corner's chrome (ApplyTab), so the bar or rail never goes
	// away and nothing is pushed. Ctrl+1/2/3 and the debug hook come here too.
	private async void OnTabRequested(CornerTab tab) => await SelectTabAsync(tab);

	private Task SelectTabAsync(CornerTab tab) => RunGrownUpAsync(async () =>
	{
		if (tab != CornerTab.Settings && !await CloseSettingsSubPageAsync())
		{
			return; // the open sub-page is busy (a backup under way) or rebuilt the app: the tab stays
		}

		_mode.SelectTab(tab);
	});

	// Leaving the Settings tab, or the corner, goes through the open sub-page's own Back (it saves, counts a language change, asks the mobile data question and rebuilds, or refuses while it is busy), never around it.
	// True when no sub-page is open any more and the caller may go on.
	private async Task<bool> CloseSettingsSubPageAsync()
	{
		if (_settingsTab is not { SubPageOpen: true } settings)
		{
			return true;
		}

		await settings.GoBackAsync();
		if (settings.SubPageOpen)
		{
			RefuseWhileBusy();
		}

		return !settings.SubPageOpen;
	}

	// The sub-page is busy and refused Back: its own wait overlay stays on screen, and the tap answers with a click so it is not taken for a dead button.
	private void RefuseWhileBusy()
	{
		SheetLog("settings sub-page busy: back refused");
		try
		{
			HapticFeedback.Default.Perform(HapticFeedbackType.Click);
		}
		catch (FeatureNotSupportedException)
		{
			// no-fallback: a device without haptics just gets the overlay
		}
	}

	// What the content area shows now: the books grid, or the Store or Settings view in place of it (grown-up mock frames T2 and W1). Safe to call at any time; it only reads the mode and the last layout pass.
	private void ApplyTab()
	{
		bool corner = _mode.IsGrownUp;
		CornerTab tab = corner ? _mode.Tab : CornerTab.Books;
		bool hosted = corner && tab != CornerTab.Books;
		StoryList.IsVisible = !hosted;
		TabHost.IsVisible = hosted;
		if (hosted)
		{
			TabHost.Content = (View)TabViewOf(tab);
		}

		ApplyContentHeaderTexts();
		ApplyTabShape();
		SyncTabViews();
	}

	// The tab views are made the first time their tab shows and kept for the gallery's life: a download that runs on the Store tab and the filters survive a switch.
	private ICornerTabView TabViewOf(CornerTab tab)
	{
		if (tab == CornerTab.Store)
		{
			return _store ??= _services.GetRequiredService<StorePage>();
		}

		if (_settingsTab is null)
		{
			_settingsTab = _services.GetRequiredService<SettingsTab>();
			_settingsTab.Attach(this);
			_settingsTab.SubPageChanged += RequestTab;
		}

		return _settingsTab;
	}

	// The shown tab view is told it appeared or disappeared, once per change: only while this page is on screen, in the corner, on that tab.
	private void SyncTabViews()
	{
		ICornerTabView? wanted = _appeared && _mode.IsGrownUp && _mode.Tab != CornerTab.Books ? TabViewOf(_mode.Tab) : null;
		if (ReferenceEquals(wanted, _shownTab))
		{
			return;
		}

		_shownTab?.Disappearing();
		_shownTab = wanted;
		_shownTab?.Appearing();
	}

	// The hosted views take the window's size, and the host takes the corner's side margins (the lists inside have none of their own). With a Settings sub-page open the tab's own title row steps aside for the page's header.
	private void ApplyTabShape()
	{
		if (_windowSize.Width <= 0)
		{
			return;
		}

		CornerSizes sizes = CornerLayout.SizesOf(_chrome);
		bool subPage = _mode.Tab == CornerTab.Settings && _settingsTab is { SubPageOpen: true };
		double top = subPage && _chrome.IsRail ? sizes.Content.Top : 0;
		TabHost.Margin = new Thickness(sizes.Content.Left, top, sizes.Content.Right, 0);
		_store?.ApplyShape(SettingsLayout.ContentWidth(_windowSize.Width, _chrome.RailWidth));
		_settingsTab?.ApplyShape(_windowSize.Width, _windowSize.Height);
		ContentHeader.IsVisible = _mode.IsGrownUp && !subPage;
	}

	private async void OnOpenRequested(object? sender, EventArgs e)
	{
		if (_reorder.IsVisible || Environment.TickCount64 < _suppressTapUntil)
		{
			return;
		}

		if (TileOf(sender) is { } tile)
		{
			UiUsage.Tap(UiControls.BookMore); // Slice 352: the tile's "more" button
			await RunGrownUpAsync(() => OpenBookSheetAsync(tile.Story));
		}
	}

	// Seven taps on the title in grown-up mode flip the developer switch (Slice 91); the tiles are rebuilt, and the book sheet shows or hides Export.
	private void OnTitleTapped(object? sender, TappedEventArgs e) => OnTitleTapped();

	private async void OnTitleTapped()
	{
		if (!_mode.IsGrownUp)
		{
			DeveloperMode.ResetTaps();
			return;
		}

		try
		{
			if (DeveloperMode.RegisterTap())
			{
				await RequestShelfRefresh(true);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// no-fallback: logged only; the shelf stays as it was until the next refresh
			Debug.WriteLine($"Dino Reader gallery refresh failed: {ex}");
		}
	}

	// The Grown-ups pill (Slice 228): the gate first, then grown-up mode. A failed gate leaves the shelf as it was.
	private async void OnGrownUpsRequested() => await _busy.RunAsync(EnterGrownUpModeAsync);

	private async Task EnterGrownUpModeAsync()
	{
		if (await _gate.RequestAsync(Navigation))
		{
			_mode.Enter();
			_tourGateDeclined = false;
			await ResumeTourAsync(); // a tour left unfinished by a relaunch opens now that a grown-up is here
		}
	}

	// Back from grown-up mode lands on the kids' shelf, not out of the app (Slice 172, Decision 169, mock 94). Slice 325 part B: Back on a Settings sub-page goes up to the list first; on any tab it is the coral pill.
	protected override bool OnBackButtonPressed()
	{
		if (_tour is { IsActive: true })
		{
			return true; // Slice 362: Back does nothing during the corner tour
		}

		if (StoryLanguageMenu.DismissOpen(GalleryRoot))
		{
			return true; // the open language list closes first
		}

		if (_reorder.IsVisible)
		{
			CancelReorder(); // Back is Cancel (Slice 264)
			return true;
		}

		if (BookInfo.IsVisible)
		{
			BookInfo.Dismiss();
			return true;
		}

		if (!_mode.IsGrownUp)
		{
			return AskToClose();
		}

		if (_mode.Tab == CornerTab.Settings && _settingsTab is { SubPageOpen: true } settings)
		{
			_ = settings.GoBackAsync();
			return true;
		}

		CompleteHandover();
		return true;
	}

	// The kids' shelf is home (Slice 174, Decision 169, mock 94): Android back asks "Close Dino Book Reader?" instead of leaving, and so does Esc at the Windows window (Slice 325 part C4). iOS has no back
	// button and no Esc, so nothing changes there.
	private bool AskToClose()
	{
		if (!OperatingSystem.IsAndroid() && !OperatingSystem.IsWindows())
		{
			return base.OnBackButtonPressed();
		}

		if (Navigation.ModalStack.Count > 0)
		{
			return true; // a modal is open or still closing: this back press is swallowed, never closes the app
		}

		_ = _busy.RunAsync(CloseIfConfirmedAsync);
		return true;
	}

	private async Task CloseIfConfirmedAsync()
	{
		if (await CloseQuestionPage.AskAsync(Navigation))
		{
			Application.Current?.Quit();
		}
	}

	// Leaves grown-up mode (the coral pill, Esc, Android Back on any tab); Back during the first hand-over counts as "Ready for my child", so the flag never outlives the mode.
	private async void CompleteHandover()
	{
		if (!await CloseSettingsSubPageAsync())
		{
			return; // a busy sub-page keeps the corner open; a language change rebuilt the app in the corner
		}

		FirstRunState.HandoverPending = false;
		_mode.Leave();
	}
}
