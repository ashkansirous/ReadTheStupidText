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
	// Slice 418: there is no ink corner header any more, so the status bar's clock and battery are always dark on the cream (Slice 325 A4 made them light over the corner's ink).
	bool IInkUnderStatusBar.InkUnderStatusBar => false;

	private const double LanguageMenuGap = 6;
	private const string TileTitleHeightKey = "TileTitleHeight";
	private const string TileWidthKey = "TileWidth";
	private const string TileTitleLinesKey = "TileTitleLines";
	private const string TileSpacingKey = "TileSpacing";
	private const string TileTitleSizeKey = "TileTitleSize";
	private const string TileCoverRadiusKey = "TileCoverRadius";
	private const string TileShadowOpacityKey = "TileShadowOpacity";
	private const string TileShadowOffsetKey = "TileShadowOffset";
	private const string TileMoreSizeKey = "TileMoreSize";
	private const string TileMoreRadiusKey = "TileMoreRadius";
	private const string TileMoreInsetKey = "TileMoreInset";
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
	private readonly StoryLanguageButton _languageButton; // Slice 173 (mock 79): the reading-language button, at the header's end (the title row's end beside a rail), with two or three story languages
	private readonly CornerTabs _tabs = new(); // Slice 418: Books, Store, Settings and the lock: the bottom bar under 600 dp wide, rows in the rail from 600
	private readonly ShelfHeader _shelfHeader; // the one header (a bar) or the rail's name row
	private readonly UnlockChip _chip = new(); // "Unlocked · 24s" next to the title while unlocked (decision 15)
	private readonly LockOverlay _lockOverlay = new(); // the dark hint and the dark "Locked again" message
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
		BuildQueueBanner();
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
		_shelfHeader = new ShelfHeader(_languageButton, _chip);
		AddChrome();
		AddLockOverlay();
		AddReorderView();
		WaitOverlay overlay = DinoWait.OverPage();
		Grid.SetRowSpan(overlay, GalleryRoot.RowDefinitions.Count);
		Grid.SetColumnSpan(overlay, GalleryRoot.ColumnDefinitions.Count);
		GalleryRoot.Children.Add(overlay);
		_wait = new WaitController(overlay);
	}

	// Slice 325, Slice 418: the header and the menu sit in the root grid under everything else (the sheet, the reorder layer and the wait overlay are added after them). ApplyShape places the header (row 0, or
	// the rail in column 0) and moves the menu between the page's bottom row and the rail.
	private void AddChrome()
	{
		GalleryRoot.Children.Insert(0, _shelfHeader);
		_shelfHeader.TitleTapped += OnTitleTapped;
		_shelfHeader.Fitted += LogShelf;
		_tabs.TabRequested += OnTabRequested;
		_tabs.LockRequested += OnLockRequested;
		_tabs.HandlerChanged += (_, _) => { if (_tabs.Handler is not null) { Replay(); } }; // work that waited for the tabs' native side
		GalleryRoot.Children.Insert(0, _tabs);
		_tabs.IsVisible = false;
	}

	// Slice 418: the hint and the "Locked again" message lie over the content's lower edge; they never take a touch.
	private void AddLockOverlay()
	{
		Grid.SetRowSpan(_lockOverlay, 1);
		Grid.SetRow(_lockOverlay, 1);
		Grid.SetColumnSpan(_lockOverlay, GalleryRoot.ColumnDefinitions.Count);
		GalleryRoot.Children.Add(_lockOverlay);
	}

	// Slice 264: the reorder mode is a layer over the whole page (its own ink bar, hint and grid), above the sheet's scrim so it never shows through, below the wait overlay added after it.
	private void AddReorderView()
	{
		Grid.SetRowSpan(_reorder, GalleryRoot.RowDefinitions.Count);
		Grid.SetColumnSpan(_reorder, GalleryRoot.ColumnDefinitions.Count);
		GalleryRoot.Children.Add(_reorder);
		_reorder.DoneRequested += DoneReorderAsync;
		_reorder.CancelRequested += CancelReorder;
		_reorder.Activity += _mode.GrownUpTap; // a drag or a key in the reorder mode keeps the shelf unlocked (Slice 418 decision 13)
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

	// Slice 418: shown locked or unlocked (the lock leaves the reading language alone), when two or three story languages are on offer. The shelf's rules take "grown-up mode" as false: there is one shelf now.
	private void ApplyLanguageButton()
	{
		StoryLanguageSet set = StoryLanguages.Current();
		IReadOnlyList<string> choosable = ChoosableLanguages(set);
		_languageButton.IsVisible = LanguagePick.ShelfButtonShown(grownUp: false, choosable);
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
		catch (OperationCanceledException ex) when (Handler is null && ex.InnerException is not TimeoutException)
		{
			// no-fallback: the page was torn down (its handler is gone) while the pass ran, so a cancel here is the teardown, not a failure; the shelf stays pending.
			// An HttpClient timeout (TaskCanceledException wrapping a TimeoutException) is never silent, handler or not: it reports below.
		}
		catch (Exception ex)
		{
			// Slice 317 item 6 (313 check 3 #1): with the page alive, no token of the gallery's own reaches this pass (the wait is canCancel: false), so an OperationCanceledException here is an HttpClient timeout
			// or a cancelled inner wait, a real failure: it is reported like any other, not dropped without a line.
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
		return LanguagePick.ShelfTitles(grownUp: false, ChoosableLanguages(set), StoryLanguages.Pick(), set.Main); // Slice 418: one shelf, so the titles follow the reading language locked or unlocked
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
		_mode.CountdownChanged -= OnCountdownChanged;
		_mode.CountdownChanged += OnCountdownChanged;
		_mode.Locked -= OnLocked;
		_mode.Locked += OnLocked;
		StoreClient store = _services.GetRequiredService<StoreClient>();
		store.LibraryChanged -= OnLibraryChanged;
		store.LibraryChanged += OnLibraryChanged;
		store.Sales.Purchases.Changed -= OnPurchasesChanged; // Slice 384: a subscription that ended or came back locks or opens its books
		store.Sales.Purchases.Changed += OnPurchasesChanged;
		Connectivity.Current.ConnectivityChanged -= OnNetworkChanged; // Slice 398: the offline notice follows the network
		Connectivity.Current.ConnectivityChanged += OnNetworkChanged;
		StartBannerTimer();
		store.Background.Changed -= OnBackgroundBooksChanged; // Slice 459: the first run's background queue shows on the shelf while it runs
		store.Background.Changed += OnBackgroundBooksChanged;
		_ = store.Background.ResumeAsync(); // Slice 459 (kill recovery): once per launch, the first run's saved jobs go on; it never throws
#if DEBUG
		NetworkProbe.OverrideChanged -= OnNetworkOverrideChanged;
		NetworkProbe.OverrideChanged += OnNetworkOverrideChanged;
#endif
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
			ShowPendingLockMessage(); // Slice 418: a lock that happened while another page was in front is told now
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

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_appeared = false;
		SyncTabViews();
		CancelSheetCount();
		_mode.PropertyChanged -= OnModeChanged;
		_mode.CountdownChanged -= OnCountdownChanged;
		_mode.Locked -= OnLocked;
		_lockOverlay.HideAll();
		_services.GetRequiredService<StoreClient>().LibraryChanged -= OnLibraryChanged;
		_services.GetRequiredService<StoreClient>().Sales.Purchases.Changed -= OnPurchasesChanged;
		Connectivity.Current.ConnectivityChanged -= OnNetworkChanged;
		StopBannerTimer();
		_services.GetRequiredService<StoreClient>().Background.Changed -= OnBackgroundBooksChanged;
#if DEBUG
		NetworkProbe.OverrideChanged -= OnNetworkOverrideChanged;
#endif
		_services.GetRequiredService<ShelfAudioPacks>().Changed -= OnShelfAudioChanged;
		_services.GetRequiredService<StagedBookUpdates>().Staged -= OnBookUpdateStaged;
	}

	// Slice 384: the store said something new (raised on any thread); the shelf is built again so a lapsed subscription's books grey out or leave, and a renewed one's come back.
	private void OnPurchasesChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(async () =>
	{
		await RefreshShelfAsync();
	});

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
			_shelfHeader.SetTab(_mode.Tab);
			RequestTab();
			LogCorner();
			ShowScreen();
			return;
		}

		ApplyMode();
		ShowScreen();
		_ = RequestShelfRefresh(true); // Slice 384: a locked shelf leaves out the books a lapsed subscription locked; an unlocked one shows them greyed, so the tiles are built again for the new state
		if (!_mode.IsGrownUp)
		{
			_settingsTab?.Reset(); // Slice 418 decision 14: a sub-page closes, and the next unlock starts at the Settings list
			CloseReorder("locked"); // Slice 264: locking cancels the reorder mode; nothing was saved
			BookInfo.Dismiss(); // also refreshes the tiles when a switch was saved
		}
	});

	private void ApplyMode()
	{
		_ = RequestShelfCheckAsync(); // the app language or the title language changed: the shelf titles (and the Add tile, rebuilt with them) follow it

		ApplyTexts();

		ApplyModeChrome(_mode.IsGrownUp);
		ApplyLockState();
	}

	// Slice 418: what the lock changes in the chrome: Store and Settings fade, the lock is the open one, the chip shows with the seconds left.
	private void ApplyLockState()
	{
		bool unlocked = _mode.IsGrownUp;
		_tabs.Locked = !unlocked;
		_chip.IsVisible = unlocked;
		UpdateCountdown();
		_shelfHeader.SetChipShown();
		LockLog(unlocked ? "unlocked" : "locked");
	}

	// The seconds on the chip and on the rail's line; raised once a second while unlocked (GrownUpMode.CountdownChanged, from any thread).
	private void OnCountdownChanged() => MainThread.BeginInvokeOnMainThread(UpdateCountdown);

	private void UpdateCountdown()
	{
		int seconds = _mode.Clock.SecondsLeft;
		_chip.SetSeconds(seconds);
		_tabs.SetSeconds(seconds);
	}

	private void OnLocked(LockReason reason) => MainThread.BeginInvokeOnMainThread(ShowPendingLockMessage);

	// "Locked again: no grown-up taps for 30 seconds." after the timer, "Locked." after a tap on the lock (mock L7). Told once; a lock that happened under another page is told when this page appears.
	private void ShowPendingLockMessage()
	{
		if (_mode.TakePendingMessage() is not { } reason)
		{
			return;
		}

		_lockOverlay.ShowToast(reason == LockReason.Timer ? AppResources.Lock_LockedAgain : AppResources.Lock_LockedByHand);
		LockLog($"message reason={reason}");
	}

	private static void LockLog(string line)
	{
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Lock] {line}"); // the lock flow's expect-log markers (RTST-TEST tag)
#endif
		Debug.WriteLine($"Dino Reader lock {line}");
	}

	// The lock was tapped (Slice 418 decisions 11 and 12): locked opens the unlock popup (hold 3 seconds, then the sum); unlocked locks at once, with no question.
	private async void OnLockRequested() => await _busy.RunAsync(ToggleLockAsync);

	private async Task ToggleLockAsync()
	{
		if (_mode.IsGrownUp)
		{
			await CloseSettingsSubPageAsync(); // a language change made on a sub-page is saved through its own Back; a busy one is closed by the lock all the same
			_mode.Lock(LockReason.ByHand);
			return;
		}

		if (await _gate.RequestAsync(Navigation))
		{
			_mode.Enter();
		}
	}

	// Windows Ctrl+L (Slice 418): the same as a tap on the lock; nothing while another action runs.
	internal bool TryToggleLockFromKeyboard()
	{
		if (_busy.IsBusy || _reorder.IsVisible || BookInfo.IsVisible || StoryLanguageMenu.IsOpenIn(GalleryRoot))
		{
			return false;
		}

		OnLockRequested();
		return true;
	}

	// A tap on a faded Store or Settings tab (decision 9): the dark hint over the lock and an amber pulse of 2 seconds (none with reduce motion).
	private void ShowLockedHint()
	{
		_lockOverlay.ShowHint(AppResources.Lock_GrownUpsHint);
		_tabs.PulseLock();
		LockLog("hint");
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
		BookSales sales = _services.GetRequiredService<BookSales>();
		_lastShelfStories = stories;
		_lockedShelfKey = LockedShelfKey(sales);
		IReadOnlyList<Story> shown = _mode.IsGrownUp ? stories : [.. stories.Where(story => !sales.IsLapsed(story))]; // Slice 384: a lapsed subscription's books are removed from the child's shelf, greyed on the parents'; their files stay
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Subscription] shelf total={stories.Count} shown={shown.Count} lapsed={stories.Count(story => sales.IsLapsed(story))} grownUp={_mode.IsGrownUp}"); // the subscription flow's expect-log markers
#endif
		foreach (StoryTile item in GalleryItems.Build(shown, _library, _mode, story => _services.GetRequiredService<StoryTranslator>().ShelfTitle(story, titleLanguage), story => sales.IsLapsed(story), story => LockedBookPrice(sales, story)))
		{
			_items.Add(item);
		}

		ApplyShelfBanners();

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

	// Slice 398: the store's own price for a locked book's "Buy · price" pill; null for any other book or while the store has not given one (the pill then says "Buy this book").
	private string? LockedBookPrice(BookSales sales, Story story)
	{
		if (!sales.IsLapsed(story) || story.SourceId is not { } sourceId || _services.GetRequiredService<StoreClient>().ListedItem(sourceId) is not { } item)
		{
			return null;
		}

		return BookAccess.PriceOf(item, sales.Purchases.Snapshot.Prices);
	}

	// Locking and unlocking restyle the one chrome (Store and Settings fade or open, the lock and the chip change) and rebuild the tiles; the chrome's shape follows the window size only, never the lock.
	private void ApplyModeChrome(bool grownUp)
	{
		ApplyShelfRing();
		ApplyLanguageButton();
		ApplyShape(); // the key holds the chrome and the open tab, so another shape lays everything out again and the same one does nothing (a shelf rebuild must not relayout the chrome)
		ApplyBarColours(); // at once, even before the first window size: the chrome kind depends on the window size only, never on the lock
	}

	// The tab on show: Books while locked (a locked shelf has no other), else the open one.
	private CornerTab ShownTab => _mode.IsGrownUp ? _mode.Tab : CornerTab.Books;

	// Slice 349, Slice 418: the one place that sets what sits behind the system bars: the page colour, the content host's colour, the bar icons and (Windows) the title bar.
	// Asked on every lock change, after every chrome pass and on first appearance, never behind ApplyShapeOnce's size gate. Books is cream (mock L1, L4), Store and Settings are the sand of their own pages (L5, L6).
	private void ApplyBarColours()
	{
		if (CurrentChrome() is null)
		{
			return; // no layout pass yet: the first SizeChanged / ApplyShapeOnce paints, with no guessed window size
		}

		string content = ShownTab == CornerTab.Books ? DinoTokens.Cream : DinoTokens.Sand;
		BackgroundColor = DinoTokens.Get(content);
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
		int columns = ShelfTitleRules.Columns(chrome, _windowSize.Width); // Slice 418: one shelf, one grid, locked or unlocked (mock L1 and L4 draw the same covers)
		string key = $"{chrome}|{columns}";
		_shelfGeometry = ShelfTitleRules.Geometry(chrome, columns);
		if (rail)
		{
			_shelfGeometry = _shelfGeometry with { Top = 0 }; // beside a rail the title row (ContentHeader) carries the space above the grid
		}

		WindowShapeSeam.ShelfGeometry shelfOnPage = rail ? _shelfGeometry.BesideRail(chrome.RailWidth) : _shelfGeometry; // the layer spans the whole page, so beside a rail it starts where the shelf list starts on the page
		bool tablet = chrome.IsTabletPortrait; // the corner's and the shelf's classification are one now
		bool useShelf = WindowShapeSeam.ReorderUsesShelfGeometry(tablet, _windowSize.Width, _windowSize.Height); // no mock frame for a tablet or a phone held sideways
		_reorderGeometry = ReorderShape(tablet, useShelf, shelfOnPage);
		_reorder.Reshape(_reorderGeometry); // a rotation or resize while the mode is open re-fits it
		if (grownUp != _shapeGrownUp)
		{
			_anchorBookId = null; // the other state lists other tiles (a lapsed book is hidden while locked): its first book is not this one's
			_shapeGrownUp = grownUp;
		}

		if (key == _shapeKey)
		{
			ApplyTabShape(); // the same chrome at another width: the Store tab's columns follow the content width
			return;
		}

		_shapeKey = key;
		_chrome = chrome;
		_chromeBuilt = true;
#if WINDOWS
		WindowTrace.Write($"Dino shelf: columns={columns} sideways=False rail={rail} grownUp={grownUp} content={_windowSize.Width:0}x{_windowSize.Height:0}"); // the VM check reads the shelf's shape from this
#endif
		ApplyChrome(chrome, grownUp);
		ApplyTileSizes(chrome);
		ApplyShelfList(columns);
		LogShape(chrome, columns, grownUp);
		ApplyBarColours();
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
		_shelfHeader.IsVisible = true;
		Grid.SetRow(_shelfHeader, 0);
		Grid.SetColumn(_shelfHeader, 0);
		Grid.SetRowSpan(_shelfHeader, rail ? GalleryRoot.RowDefinitions.Count : 1);
		Grid.SetColumnSpan(_shelfHeader, rail ? 1 : GalleryRoot.ColumnDefinitions.Count);
		Grid.SetRow(ContentHost, rail ? 0 : 1);
		Grid.SetRowSpan(ContentHost, rail ? GalleryRoot.RowDefinitions.Count : 1);
		Grid.SetColumn(ContentHost, rail ? 1 : 0);
		Grid.SetColumnSpan(ContentHost, rail ? 1 : GalleryRoot.ColumnDefinitions.Count);
		ContentHost.SafeAreaEdges = ContentSafeArea(rail);
		PlaceLockOverlay(rail);
		MoveFlagAndChip(rail);
		_shelfHeader.Apply(chrome, ShelfTitleRules.SizesOf(chrome), _languageButton.IsVisible);
		ApplyMenu(chrome);
		if (rail)
		{
			ApplyContentHeader(chrome);
		}
		else
		{
			ContentHeader.IsVisible = false;
		}

		ApplyTab(); // Slice 325 part B: after the chrome, so the hosted view takes this pass's sizes
	}

	// The content keeps clear of the notch on its sides; beside a rail it keeps clear of the top too. A bar's covers run under the menu's own safe area.
	private static SafeAreaEdges ContentSafeArea(bool rail) =>
		new(SafeAreaRegions.Container, rail ? SafeAreaRegions.Container : SafeAreaRegions.None, SafeAreaRegions.Container, rail ? SafeAreaRegions.Container : SafeAreaRegions.None);

	// The hint and the message sit over the content (a bar: the row between the header and the menu; a rail: the column right of it).
	private void PlaceLockOverlay(bool rail)
	{
		Grid.SetRow(_lockOverlay, rail ? 0 : 1);
		Grid.SetRowSpan(_lockOverlay, rail ? GalleryRoot.RowDefinitions.Count : 1);
		Grid.SetColumn(_lockOverlay, rail ? 1 : 0);
		Grid.SetColumnSpan(_lockOverlay, rail ? 1 : GalleryRoot.ColumnDefinitions.Count);
		_lockOverlay.Place(rail);
	}

	// A view has one parent: the flag and the chip leave the title row before the header takes them back (a bar), and are put in the title row after the header gave them up (a rail).
	private void MoveFlagAndChip(bool rail)
	{
		ContentHeader.Remove(_languageButton);
		ContentHeader.Remove(_chip);
		if (rail)
		{
			_shelfHeader.ReleaseFlagAndChip();
			ContentHeader.Add(_chip, 1, 0);
			ContentHeader.Add(_languageButton, 3, 0);
		}
	}

	// The menu is the bottom bar under 600 dp wide and the rail's rows from 600 (the lock a plain row at the rail's foot).
	private void ApplyMenu(ChromeShape chrome)
	{
		CornerSizes sizes = CornerLayout.SizesOf(chrome);
		_tabs.Apply(chrome.IsRail, sizes);
		_tabs.Selected = ShownTab;
		_tabs.Locked = !_mode.IsGrownUp;
		if (chrome.IsRail)
		{
			_tabs.IsVisible = true;
			_shelfHeader.AttachTabs(_tabs);
		}
		else
		{
			ShelfHeader.DetachTabs(_tabs);
			Grid.SetRow(_tabs, 2);
			Grid.SetColumn(_tabs, 0);
			Grid.SetColumnSpan(_tabs, GalleryRoot.ColumnDefinitions.Count);
			if (_tabs.Parent != GalleryRoot)
			{
				GalleryRoot.Children.Insert(0, _tabs);
			}

			_tabs.IsVisible = true;
		}
	}

	// Mock WL1, WL2: beside a rail the title row holds the tab's name (26 bold), the chip and the flag; the shelf's own sides and top frame it.
	private void ApplyContentHeader(ChromeShape chrome)
	{
		ShelfSizes s = ShelfTitleRules.SizesOf(chrome);
		CornerSizes c = CornerLayout.SizesOf(chrome);
		ContentHeader.IsVisible = true;
		ContentTitle.IsVisible = true;
		ContentTitle.FontSize = c.ContentTitle;
		ContentHeader.Padding = new Thickness(s.Content.Left, s.Content.Top, s.Content.Right, c.ContentGap + 4);
	}

	// The open tab's name is the header's title (under a bar, ShelfHeader.SetTab) and the content's title (beside a rail).
	private void ApplyContentHeaderTexts()
	{
		CornerTab tab = ShownTab;
		string title = tab switch
		{
			CornerTab.Store => AppResources.Store_Title,
			CornerTab.Settings => AppResources.Settings_Title,
			_ => AppResources.Corner_TabBooks,
		};
		ContentTitle.Text = title;
		_shelfHeader.SetTab(tab);
	}

	// The cover, its title and the grown-ups' "⋯" dot follow the window's family; they are page resources the tiles bind to, written only when the value really changes.
	// Slice 418: the covers are the shelf's (mock L1 and L4 draw the same cover locked and unlocked); the ••• badge is the mock's 32 dp white circle, 6 dp from the cover's top right.
	private void ApplyTileSizes(ChromeShape chrome)
	{
		ShelfSizes s = ShelfTitleRules.SizesOf(chrome);
		SetResourceIfChanged(TileMoreSizeKey, MoreBadgeSize);
		SetResourceIfChanged(TileMoreRadiusKey, new CornerRadius(MoreBadgeSize * MoreDotRadiusShare));
		SetResourceIfChanged(TileMoreInsetKey, new Thickness(0, MoreBadgeInset, MoreBadgeInset, 0));
		SetResourceIfChanged(TileCoverRadiusKey, new CornerRadius(s.CoverRadius));
		SetResourceIfChanged(TileTitleSizeKey, s.TileTitle);
		SetResourceIfChanged(TileSpacingKey, s.TileGap);
		SetResourceIfChanged(TileShadowOpacityKey, ShelfCoverShadowOpacity);
		SetResourceIfChanged(TileShadowOffsetKey, s.ShadowOffset); // 4 dp, and 5 on a tablet held upright (mock KT1); StoryCover.ShadowDepth carries it to Shadow.Offset
	}

	private const double MoreBadgeSize = 32;
	private const double MoreBadgeInset = 6;

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

#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Subscription] shelf columns={columns} grownUp={_mode.IsGrownUp}"); // the subscription flow pins the mock's 3 columns on a phone held upright (SB6)
#endif
		SetResourceIfChanged(TileWidthKey, -1.0);
		SetResourceIfChanged(TileTitleLinesKey, 2);
		ApplyShelfMargin(_shelfGeometry);
		KeepFirstBookOnScreen();
	}

	// The list's own sides and top: a pure function of the chrome (CornerLayout.BooksGeometry, ShelfTitleRules.Geometry), never of the list's measured size (a layout cycle on the VM, Slice 235 part C).
	private void ApplyShelfMargin(WindowShapeSeam.ShelfGeometry geometry)
	{
		var margin = new Thickness(geometry.Left, ShelfBanners.IsVisible ? ListTopUnderBanner : geometry.Top, geometry.Right, 0);
		if (StoryList.Margin != margin)
		{
			StoryList.Margin = margin;
		}

		ShelfBanners.Margin = new Thickness(geometry.Left, BannerTop, geometry.Right, BannerBottom); // Slice 398 (mock SB6: 12 above, 8 below): the banners sit on the list's own sides
	}

	private const double BannerTop = 12;
	private const double BannerBottom = 8;
	private const double ListTopUnderBanner = 4; // the mock's list starts 4 dp under the banner's 8: the banner carries the gap above the grid, the list's own top is not added to it

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
	// "Close Dino: Kids Books & Stories?" (part C4).
	// Slice 418: the one shelf reports the corner's screen locked or unlocked, so Ctrl+1/2/3 always resolve (a locked Store or Settings key acts like a tap); Esc on it asks to close the app, as on the old kids' shelf.
	DinoScreen IDinoKeyTarget.Screen => _reorder.IsVisible ? DinoScreen.Reorder : BookInfo.IsVisible ? DinoScreen.Sheet : DinoScreen.Corner;

	bool IDinoKeyTarget.Handle(DinoKeyAction action, DinoKey key)
	{
		UiUsage.NoteGrownUpKey(_reorder.IsVisible || BookInfo.IsVisible); // a key inside Store, Settings, a sheet or reorder is a grown-up tap; a key on Books never is (Slice 418 decision 13)
		if (_reorder.IsVisible)
		{
			return HandleReorderKey(action); // Slice 264: Esc cancels the reorder mode, Enter is Done (Space lifts and drops a book)
		}

		if (action is DinoKeyAction.AskToClose or DinoKeyAction.LeaveCorner or DinoKeyAction.CornerBooks or DinoKeyAction.CornerStore or DinoKeyAction.CornerSettings or DinoKeyAction.OpenRow)
		{
			return HandleCornerKey(action);
		}

		if (action == DinoKeyAction.OpenBookMenu)
		{
			return OpenFocusedBookMenu(); // Slice 420: the Menu key or Shift+F10 on a focused book (Windows only: no other head sends the action)
		}

		if (action is DinoKeyAction.FocusRowUp or DinoKeyAction.FocusRowDown)
		{
			return _mode.IsGrownUp && _mode.Tab == CornerTab.Settings && MoveSettingsFocus(down: action == DinoKeyAction.FocusRowDown); // Slice 420: the Settings arrows; Books keeps the native grid navigation
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
			case DinoKeyAction.LeaveCorner or DinoKeyAction.AskToClose: // Slice 418: Esc is Android's Back: a Settings sub-page goes up, Store or Settings goes to Books, and only on Books does it ask to close the app
				return OnBackButtonPressed();
			case DinoKeyAction.CornerBooks:
				OnTabRequested(CornerTab.Books);
				return true;
			case DinoKeyAction.CornerStore:
				OnTabRequested(CornerTab.Store); // locked: a tap's hint and pulse
				return true;
			case DinoKeyAction.CornerSettings:
				OnTabRequested(CornerTab.Settings);
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
				await LockHolds.WhileAsync("alert", () => DisplayAlertAsync(AppResources.Reorder_SaveFailedTitle, AppResources.Reorder_SaveFailedBody, AppResources.Common_Ok));
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

	/// <summary>Slice 433 item 6, the scroll-shelf hook: scrolls the books list down by <paramref name="fraction"/> of its visible height (up when negative) over <paramref name="milliseconds"/>, animated. Empty when it did, else why not.</summary>
	internal async Task<string> DebugScrollShelfAsync(double fraction, int milliseconds)
	{
		if (!StoryList.IsVisible || _reorder.IsVisible || StoryList.Height <= 0 || Navigation.NavigationStack.Count > 1)
		{
			return "shelf not showing";
		}

		int pixels = (int)Math.Round(fraction * StoryList.Height);
		if (pixels == 0)
		{
			return "scroll distance rounds to 0 pixels";
		}

		bool moved = await Testing.ShelfScroller.ScrollAsync(StoryList, pixels, milliseconds);
		return moved ? string.Empty : "no native list";
	}

	/// <summary>Debug-only hook for the shelf-reorder flow: opens the mode if it is closed, then moves the book at 1-based position <paramref name="from"/> before the one at <paramref name="to"/>, as a drop does.</summary>
	internal string DebugReorderMove(int from, int to)
	{
		OpenReorder(null);
		return _reorder.IsVisible ? _reorder.DebugMove(from, to) : string.Empty;
	}
#endif

	private async void OnStoryTapped(object? sender, TappedEventArgs e) => await ReadTappedTileAsync(sender); // no-tap-count: OpenTileAsync counts the press (UiUsage.Tap)

	// Slice 420: on the unlocked shelf the overlay's backdrop sits over the cover; a tap on it reads the book exactly like a tap on a bare cover (decision 6), and only the ••• opens the sheet.
	private async void OnReadRequested(object? sender, EventArgs e) => await ReadTappedTileAsync(sender); // no-tap-count: OpenTileAsync counts the press (UiUsage.Tap)

	// The one tap-to-read path of a bare cover and of the overlay's backdrop, so the hold-suppression guard cannot drift between the two.
	private async Task ReadTappedTileAsync(object? sender)
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

	// A tap on a cover: it reads the book, locked or unlocked (Slice 418 decision 6). The Windows keyboard (Enter or Space on a focused book) takes the same path.
	private Task OpenTileAsync(StoryTile tile)
	{
		UiUsage.Tap(UiControls.Book); // Slice 352: it starts the read; the screen in the count says whether the shelf was locked
		if (tile.IsLapsed)
		{
			return Task.CompletedTask; // Slice 384: a greyed book opens nothing; its tile carries its two actions (Subscribe again, Buy this book)
		}

		return _busy.RunAsync(() => _actions.ReadAsync(Navigation, tile.Story)); // Slice 418 decision 6: a tap on a cover reads, unlocked too; the ••• opens the options
	}

	// Slice 235 part C: Windows-only wiring on the native list (Enter, Space, right-click, focus return after a sheet), in Platforms/Windows/GalleryPage.Desktop.cs. No-ops elsewhere.
	partial void WireDesktopShelf();

	partial void ApplyShelfRing();

	partial void RememberTileFocus(Story opened);

	partial void RestoreTileFocus();

	// Slice 420: opens the sheet of the book that has the keyboard (Windows); true when it did. Other heads have no such key, so false.
	private bool OpenFocusedBookMenu()
	{
		bool opened = false;
		OpenFocusedBookMenu(ref opened);
		return opened;
	}

	partial void OpenFocusedBookMenu(ref bool opened);

	// Slice 420: the Up and Down arrows on the Settings tab move the keyboard focus one row (Windows); true when the focus moved. Other heads have no arrow keys here, so false.
	private bool MoveSettingsFocus(bool down)
	{
		bool moved = false;
		MoveSettingsFocus(down, ref moved);
		return moved;
	}

	partial void MoveSettingsFocus(bool down, ref bool moved);

	// Grown-up mode: a book's "⋯" (never its bare cover, which reads) opens its options sheet (Slice 189, mock 85b; it grew out of the "This book" sheet of Slices 110 and 111):
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
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[GrownUps] read-now opens {story.Id} and stays unlocked"); // the corner-read-now flow's expect-log marker (RTST-TEST tag)
#endif
		if (_services.GetRequiredService<BookSales>().IsLapsed(story))
		{
			return; // Slice 384: a book the ended subscription locked is not opened; it offers Subscribe again and Buy this book
		}

		// Slice 418 decision 6: reading neither resets nor locks, as a cover tap; the shelf stays unlocked and says nothing when the grown-up comes back.
		await _actions.ReadAsync(Navigation, story, ReadOrigins.Corner);
	}

	// Slice 384: the greyed book's two actions on the parents' shelf. Subscribe again opens the Store tab where the plans are; Buy this book opens the store's own sheet for the book (the wait overlay shows past 500 ms).
	private void OnLapsedSubscribeClicked(object? sender, EventArgs e) => _mode.SelectTab(CornerTab.Store); // no-tap-count: the closed UiControls list (shared with the Worker's CONTROLS) has no subscription control yet and the tab switch is counted as the Store screen

	private Border _queueBanner = null!;
	private Label _queueLine = null!;
	private Button _queueCancel = null!;
	private Button _queueRetry = null!;

	// Slice 459 (Decision 3: progress on the shelf): the first run's background queue (the other ticked books, then the other languages' audio) shows while it runs, in the same sand strip as the two banners, with the
	// shelf's own Cancel. Built in code beside the XAML banners; hidden with no queue. Parents' Books tab only, like them.
	private void BuildQueueBanner()
	{
		_queueLine = new Label { AutomationId = "QueueBannerLine", FontSize = 14.5, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#3f3a52"), VerticalOptions = LayoutOptions.Center };
		_queueCancel = new Button { AutomationId = "QueueBannerCancel", Text = AppResources.Common_Cancel, Style = (Style)Resources["BannerLink"], HorizontalOptions = LayoutOptions.Start };
		UiUsage.Press(_queueCancel, UiControls.Cancel, () => _services.GetRequiredService<StoreClient>().Background.Cancel());
		_queueRetry = new Button { AutomationId = "QueueBannerRetry", Text = AppResources.Store_TryAgain, Style = (Style)Resources["BannerLink"], HorizontalOptions = LayoutOptions.Start, IsVisible = false };
		UiUsage.Press(_queueRetry, UiControls.Retry, () => _services.GetRequiredService<StoreClient>().Background.Retry());
		var icon = new IconView { Kind = IconKind.Download, Color = Color.FromArgb("#3f3a52"), Size = 20, StrokeWidth = 2.2, VerticalOptions = LayoutOptions.Start };
		var links = new HorizontalStackLayout { Spacing = 16, Children = { _queueRetry, _queueCancel } };
		var column = new VerticalStackLayout { Spacing = 2, Children = { _queueLine, links } };
		var row = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)], ColumnSpacing = 10 };
		row.Add(icon, 0);
		row.Add(column, 1);
		_queueBanner = new Border { AutomationId = "QueueBanner", IsVisible = false, Padding = new Thickness(14, 12), BackgroundColor = Color.FromArgb("#F0E8D8"), StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 }, Content = row };
		ShelfBanners.Children.Add(_queueBanner);
	}

	private void OnBackgroundBooksChanged() => MainThread.BeginInvokeOnMainThread(ApplyShelfBanners);

	private void ApplyQueueBanner(bool books)
	{
		BackgroundBooks queue = _services.GetRequiredService<StoreClient>().Background;
		QueueLine line = books ? FirstBooks.LineFor(queue.Remaining, queue.BooksRemaining, queue.Failed) : QueueLine.Hidden;
		_queueBanner.IsVisible = line != QueueLine.Hidden;
		_queueRetry.IsVisible = line == QueueLine.Failed;
		_queueRetry.Text = AppResources.Store_TryAgain;
		_queueLine.Text = line switch
		{
			QueueLine.Failed => AppResources.FirstBooks_Queue_Failed,
			QueueLine.OneBook => AppResources.FirstBooks_Queue_One,
			QueueLine.ManyBooks => AppResources.Format(AppResources.FirstBooks_Queue_Other, queue.BooksRemaining),
			QueueLine.Audio => AppResources.FirstBooks_Queue_Audio,
			_ => string.Empty,
		};
		_queueCancel.Text = AppResources.Common_Cancel;
		SemanticProperties.SetDescription(_queueCancel, AppResources.Common_Cancel);
#if DEBUG
		if (line != _lastQueueLine)
		{
			_lastQueueLine = line;
			Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[FirstBackground] banner {line}");
		}
#endif
	}

#if DEBUG
	private QueueLine _lastQueueLine = QueueLine.Hidden;
#endif

	// Slice 398 (mock SB6, SB7): the Books tab's two banners. The "why" banner shows while any book is locked (it says it once for all of them); the calm sand notice shows only while offline, past the last end date the store
	// gave and inside the 3 days (SubscriptionAccess.OfflineNotice). Both are the parents' Books tab only; the child's shelf never sees either.
	private void ApplyShelfBanners()
	{
		BookSales sales = _services.GetRequiredService<BookSales>();
		bool books = _mode.IsGrownUp && _mode.Tab == CornerTab.Books;
		int locked = books ? BookAccess.LockedCount(_items.OfType<StoryTile>().Select(tile => tile.IsLapsed ? ShelfAccess.LockedByLapse : ShelfAccess.Readable)) : 0;
		DateTimeOffset? locksOn = books && sales.SwitchKnownOn ? SubscriptionAccess.OfflineNotice(DateTimeOffset.UtcNow, sales.Purchases.Subscription.Record, NetworkProbe.IsOffline()) : null;
		LockedBanner.IsVisible = locked > 0;
		OfflineBanner.IsVisible = locksOn is not null;
		ApplyQueueBanner(books);
		ShelfBanners.IsVisible = LockedBanner.IsVisible || OfflineBanner.IsVisible || _queueBanner.IsVisible;
		ApplyShelfMargin(_shelfGeometry); // the list's own top follows whether a banner stands above it
		if (locked > 0)
		{
			LockedBannerTitle.Text = AppResources.Sub_BannerTitle;
			LockedBannerBody.Text = locked == 1 ? AppResources.Sub_BannerBody_One : AppResources.Format(AppResources.Sub_BannerBody_Other, locked);
			LockedBannerPlans.Text = AppResources.Sub_BannerPlans;
			LockedBannerRestore.Text = AppResources.Store_RestorePurchases;
			SemanticProperties.SetDescription(LockedBannerPlans, AppResources.Sub_BannerPlans);
			SemanticProperties.SetDescription(LockedBannerRestore, AppResources.Store_RestorePurchases);
		}

		if (locksOn is { } lockDate)
		{
			OfflineBannerTitle.Text = AppResources.Sub_OfflineTitle;
			OfflineBannerBody.Text = AppResources.Format(AppResources.Sub_OfflineBody, SubscriptionAccess.FormatLockDate(lockDate, System.Globalization.CultureInfo.CurrentUICulture));
		}

#if DEBUG
		LogBannerState($"[Subscription] banners locked={locked} offline={(locksOn is null ? "none" : "shown")}"); // the subscription flow's expect-log markers
#endif
	}

#if DEBUG
	private string _lastBannerLog = string.Empty;

	// Written only when the state differs from the last line, so an expect-log can never match a stale earlier line of the same text.
	private void LogBannerState(string line)
	{
		if (line != _lastBannerLog)
		{
			_lastBannerLog = line;
			Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write(line);
		}
	}
#endif

	private const double LockBadgeTopShare = 0.22; // mock SB6: the badge's top edge is 22% of the cover's height down

	// The grid this is raised on holds the cover alone (the title and pills are outside it), so its height is the cover's; the badge's margin follows it and the badge sits where the mock puts it at every tile size.
	private void OnTileCoverSizeChanged(object? sender, EventArgs e)
	{
		if (sender is Grid tile && tile.Children.OfType<Border>().FirstOrDefault(child => child.AutomationId == "LapsedLockBadge") is { } badge && tile.Width > 0)
		{
			badge.Margin = new Thickness(0, tile.Height * LockBadgeTopShare, 0, 0);
		}
	}

	// Slice 398: the banners and the locked tiles depend on the clock (the end date, the 3 days after it) and on the store's catalog, which can land after the shelf was built, so while the page shows a
	// minute timer (and every tab change and appearing) works them out again; only a real difference in what is locked, or in a locked book's price, rebuilds the shelf.
	private static readonly TimeSpan BannerCheckGap = TimeSpan.FromMinutes(1);
	private IDispatcherTimer? _bannerTimer;
	private string _lockedShelfKey = string.Empty;
	private IReadOnlyList<Story> _lastShelfStories = [];

	private void StartBannerTimer()
	{
		if (_bannerTimer is null)
		{
			_bannerTimer = Dispatcher.CreateTimer();
			_bannerTimer.Interval = BannerCheckGap;
		}

		_bannerTimer.Tick -= OnBannerTick;
		_bannerTimer.Tick += OnBannerTick;
		_bannerTimer.Start();
	}

	private void StopBannerTimer()
	{
		if (_bannerTimer is not null)
		{
			_bannerTimer.Stop();
			_bannerTimer.Tick -= OnBannerTick; // detached, so a stopped timer does not hold the page
		}
	}

	private void OnBannerTick(object? sender, EventArgs e) => CheckLockedShelf();

	private bool _lockedRebuildRunning;
	private bool _lockedRebuildWanted;

	private void CheckLockedShelf()
	{
		ApplyShelfBanners();
		if (LockedShelfKey(_services.GetRequiredService<BookSales>()) != _lockedShelfKey)
		{
			// Not while a reorder, a book sheet or another page is open over the shelf: the key still differs, so the next minute tick or the next appearing is the wanted rebuild.
			if (ShelfFreeForSwap())
			{
				_ = RebuildLockedShelfAsync();
			}
		}
	}

	// One rebuild at a time: a check that finds one running marks one more as wanted and returns; the running one repeats once when it ends if the key still differs.
	private async Task RebuildLockedShelfAsync()
	{
		if (_lockedRebuildRunning)
		{
			_lockedRebuildWanted = true;
			return;
		}

		_lockedRebuildRunning = true;
		try
		{
			do
			{
				_lockedRebuildWanted = false;
				await RefreshShelfAsync();
			}
			while (_lockedRebuildWanted && LockedShelfKey(_services.GetRequiredService<BookSales>()) != _lockedShelfKey);
		}
		finally
		{
			_lockedRebuildRunning = false;
		}
	}

	// Which books are locked and what each one's Buy pill says, as one string: equal while nothing about them changed.
	private string LockedShelfKey(BookSales sales) =>
		string.Join('|', _lastShelfStories.Where(story => sales.IsLapsed(story)).Select(story => $"{story.Id}={LockedBookPrice(sales, story)}"));

	// The banner's "See plans in the Store" is the same Store tab as Subscribe again.
	private void OnBannerPlansClicked(object? sender, EventArgs e) => _mode.SelectTab(CornerTab.Store); // no-tap-count: as OnLapsedSubscribeClicked

	// The banner's "Restore purchases" leaves one request for the Store tab and shows that tab, as Settings' Restore row does: the Store owns the wait text and the result message.
	private void OnBannerRestoreClicked(object? sender, EventArgs e)
	{
		// no-tap-count: the Store tab's own restore counts the Restore tap (UiControls.Restore)
		StorePage.RequestRestore(); // before the tab switch, so the Store's Appearing finds the request however fast it runs
		_mode.SelectTab(CornerTab.Store);
	}

	// The network came or went: the offline notice follows it (the store answer decides at once online).
	private void OnNetworkChanged(object? sender, ConnectivityChangedEventArgs e) => MainThread.BeginInvokeOnMainThread(ApplyShelfBanners);

#if DEBUG
	private void OnNetworkOverrideChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(ApplyShelfBanners);
#endif

	private async void OnLapsedBuyClicked(object? sender, EventArgs e)
	{
		// no-tap-count: as above, the store sheet's outcome is the fact that matters and the store holds it
		if (TileOf(sender) is { } tile && _mode.IsGrownUp)
		{
			await _busy.RunAsync(() => BuyLapsedBookAsync(tile.Story.Id));
		}
	}

	private async Task BuyLapsedBookAsync(string bookId)
	{
		StoreClient store = _services.GetRequiredService<StoreClient>();
		BuyOutcome outcome = await _wait.RunAsync(WaitStage.GettingFile, AppResources.Wait_BuyingBook, canCancel: false, async _ =>
		{
			StoreListing listing = await store.ListAsync(CancellationToken.None);
			StoreItem? item = listing.Items.FirstOrDefault(i => string.Equals(i.Id, bookId, StringComparison.OrdinalIgnoreCase));
			string? product = item?.Products.FirstOrDefault(p => !SubscriptionProducts.Is(p));
			if (product is not null)
			{
				store.Sales.Purchases.RecordShown(product, listing.Items); // Slice 455: the buy prompt of a locked book opened (once per press, not per redraw)
			}

			return product is null ? BuyOutcome.Failed : await store.Sales.Purchases.BuyAsync(product, listing.Items, CancellationToken.None);
		});
		if (outcome == BuyOutcome.Failed && _mode.IsGrownUp)
		{
			await LockHolds.WhileAsync("alert", () => DisplayAlertAsync(AppResources.Store_Title, AppResources.Store_BuyFailed, AppResources.Common_Ok)); // as the store page does: a purchase that did not go through says so
		}
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
	private async void OnTabRequested(CornerTab tab)
	{
		if (!_mode.IsGrownUp && tab != CornerTab.Books)
		{
			ShowLockedHint(); // Slice 418 decision 9: Store and Settings are faded while locked; a tap says why and pulses the lock
			return;
		}

		await SelectTabAsync(tab);
	}

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
		CheckLockedShelf();
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
		ContentHeader.IsVisible = _chrome.IsRail && !subPage; // Slice 418: beside a rail the title row (tab name, chip, flag) shows on every tab; under a bar the header above it does
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

	// Slice 418: Back closes the language list, the reorder layer, the sheet; on Settings a sub-page goes up to the list first; on Store or Settings it goes to Books; on Books it asks to close the app (Slice 174).
	protected override bool OnBackButtonPressed()
	{
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

		if (_mode.IsGrownUp && _mode.Tab == CornerTab.Settings && _settingsTab is { SubPageOpen: true } settings)
		{
			_ = settings.GoBackAsync();
			return true;
		}

		if (_mode.IsGrownUp && _mode.Tab != CornerTab.Books)
		{
			_ = SelectTabAsync(CornerTab.Books);
			return true;
		}

		return AskToClose();
	}

	// The kids' shelf is home (Slice 174, Decision 169, mock 94): Android back asks "Close Dino: Kids Books & Stories?" instead of leaving, and so does Esc at the Windows window (Slice 325 part C4). iOS has no back
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

}
