using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using FallbackArea = ReadTheStupidText.Application.Usage.FallbackArea;
using FallbackReason = ReadTheStupidText.Application.Usage.FallbackReason;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

public partial class GalleryPage : ContentPage, IDinoKeyTarget
{
	private const double LanguageButtonHeight = 48;
	private const double LanguageMenuGap = 6;
	private const string TileTitleHeightKey = "TileTitleHeight";
	private const string TileWidthKey = "TileWidth";
	private const string TileTitleLinesKey = "TileTitleLines";
	private const double SidewaysItemSpacing = 14;
	private const double GridItemSpacing = 12;
	private const double TabletColumnSpacing = 20; // Slice 234, mock Batch 45 frame 133: 20 across, 28 down
	private const double TabletRowSpacing = 28;
	private const double TabletTitleSize = 28;
	private const double ReorderPhonePadding = 14;
	private const double ReorderTabletPadding = 40;
	private const long TapSuppressMilliseconds = 800;
	private const double TabletIconSize = 52;
	private static readonly Thickness TabletHeaderPadding = new(LandscapeLayout.TabletPortraitShelfSidePadding, 32, LandscapeLayout.TabletPortraitShelfSidePadding, 0);
	private static readonly Color RailLine = Color.FromArgb("#cfc6e6"); // the corner line on the ink rail
	private static readonly Color RailButtonFill = Color.FromArgb("#1AFFFFFF"); // white at 10%, Add and Store on the ink rail
	private static readonly Color CornerGearFill = Color.FromArgb("#1FFFFFFF"); // white at 12%, the gear tile in the corner's ink bar

	private readonly IStoryLibrary _library;
	private readonly GrownUpGate _gate;
	private bool _introOpening;
	private bool _introGateDeclined;
	private readonly GrownUpMode _mode;
	private readonly StoryActions _actions;
	private readonly StarterStories _starters;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly ShelfReorderView _reorder = new();
	private int _reorderColumns = 3; // what the last layout pass says the reorder grid fits (ApplyShape)
	private double _reorderSidePadding = ReorderPhonePadding;
	private double _reorderGap = GridItemSpacing;
	private bool _savingOrder; // Done is saving the order
	private long _suppressTapUntil; // Environment.TickCount64: the release that ends a 1 s hold must not open a sheet
	private readonly ObservableCollection<object> _items = [];
	private Story? _sheetStory; // the book the "This book" sheet is open on
	private CancellationTokenSource? _sheetCountCts; // the sheet's background count of each language's recordings: cancelled when the sheet closes or another book opens
	private string? _reopenSheetFor; // the book whose Record flow is open: the sheet opens again on it when the flow is left
	private bool _sheetChanged; // a switch was saved while the sheet was open: the tiles are rebuilt when it closes
	private readonly SemaphoreSlim _sheetSaveLock = new(1, 1);
	private readonly StoryLanguageButton _languageButton; // Slice 173 (mock 79): the child's story-language button, left of the grown-ups button, with two or three story languages

	public GalleryPage(IStoryLibrary library, GrownUpGate gate, GrownUpMode mode, StoryActions actions, StarterStories starters, IServiceProvider services)
	{
		InitializeComponent();
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
		_languageButton = AddLanguageButton();
		AddReorderView();
		WaitOverlay overlay = DinoWait.OverPage();
		Grid.SetRowSpan(overlay, GalleryRoot.RowDefinitions.Count);
		Grid.SetColumnSpan(overlay, GalleryRoot.ColumnDefinitions.Count);
		GalleryRoot.Children.Add(overlay);
		_wait = new WaitController(overlay);
	}

	// Added beside the grown-ups button in code (the header is a three-column grid): the language button takes column 2, the gear its own column 3, and the
	// grown-ups pill moves to column 4, so no two controls ever share a cell.
	private StoryLanguageButton AddLanguageButton()
	{
		var header = (Grid)GrownUpsButton.Parent;
		header.ColumnDefinitions.Insert(2, new ColumnDefinition(GridLength.Auto));
		header.ColumnDefinitions.Insert(3, new ColumnDefinition(GridLength.Auto));
		Grid.SetColumn(SettingsButton, 3);
		Grid.SetColumn(GrownUpsButton, 4);
		var button = new StoryLanguageButton(LanguageButtonHeight, OpenLanguageMenuAsync) { IsVisible = false, AutomationId = "GalleryLanguage" };
		header.Add(button, 2, 0);
		return button;
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

	// The same list as the reading page (88): one pick for all books, remembered. The shelf titles stay in the app language.
	private async Task OpenLanguageMenuAsync()
	{
		await _busy.RunAsync(async () =>
		{
			StoryLanguageSet set = StoryLanguages.Current();
			string current = StoryLanguages.Pick();
			_languageButton.SetOpen(true);
			string? picked = await StoryLanguageMenu.PickAsync((Grid)_languageButton.Parent.Parent, _languageButton, ChoosableLanguages(set), current, LanguageMenuGap);
			_languageButton.SetOpen(false);
			if (picked is null || picked == current)
			{
				return;
			}

			StoryLanguages.SetPick(picked);
			_services.GetRequiredService<UsageCounter>().RecordGeneral(UsageEventName.StoryLanguageSwitched, picked);
			ApplyLanguageButton();
		});
	}

	// Shown in the kids' shelf only, and only when the parent set two or three story languages.
	private void ApplyLanguageButton()
	{
		StoryLanguageSet set = StoryLanguages.Current();
		IReadOnlyList<string> choosable = ChoosableLanguages(set);
		_languageButton.IsVisible = !_mode.IsGrownUp && choosable.Count >= 2;
		string pick = StoryLanguages.Pick();
		_languageButton.SetLanguage(LanguagePick.ShelfShown(choosable, pick, set.Main));
	}

	// The child can pick only a language whose audio is installed for a book on the shelf (Slice 173): a download starts from Settings or the intro, behind the gate. The one rule and the one
	// snapshot are ShelfAudioPacks' (Slice 278 part B): this page reads no pack folder and does not look at a running download, so the reading page gives the same list.
	private IReadOnlyList<string> ChoosableLanguages(StoryLanguageSet set) =>
		_services.GetRequiredService<ShelfAudioPacks>().ShelfLanguages(set);

	// Changed is raised on the main thread; the snapshot is already updated.
	private void OnShelfAudioChanged(object? sender, EventArgs e) => ApplyLanguageButton();

	protected override async void OnAppearing()
	{
		base.OnAppearing();
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
		if (Window is { } window)
		{
			window.Resumed -= OnWindowResumed;
			window.Resumed += OnWindowResumed;
		}

		_ = CleanOwnBooksAsync(); // started on its own, so a failing starter install or refresh never keeps it from running
		try
		{
			// The starters are on the shelf before the first list is read (Slice 91); a later call is a no-op.
			await _starters.InstallOnceAsync();
			await RefreshAsync();
			await ResumeIntroAsync();
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

	// The first-run intro (Slice 155) opens on top of the gallery: right after "See what Dino can do", after a restart mid-intro (its step is
	// saved), and after leaving the app popped it. It is not pushed twice.
	private async Task ResumeIntroAsync()
	{
		if (!IntroState.InProgress || _introOpening || _introGateDeclined || Navigation.ModalStack.Any(p => p is NavigationPage { RootPage: IntroWizardPage }))
		{
			return;
		}

		_introOpening = true;
		try
		{
			// A fresh start comes from the first-run page, which already entered grown-up mode. Kids' mode here means the app was
			// left mid-intro and reopened, so a child may be holding it: the gate comes first.
			if (!_mode.IsGrownUp && !await _gate.RequestAsync(Navigation))
			{
				_introGateDeclined = true;
				return;
			}

			var intro = _services.GetRequiredService<IntroWizardPage>();
			await intro.PresentAsync(Navigation, firstRun: true);
		}
		catch (Exception ex)
		{
			// A bad saved step must never crash-loop the app: drop it and carry on with the gallery.
			Debug.WriteLine($"Dino Reader could not resume the intro: {ex}");
			DinoFallbacks.Report(FallbackArea.Config, FallbackReason.IntroStateReset); // Slice 273 part D: the saved intro step is cleared and progress is lost
			IntroState.Clear();
		}
		finally
		{
			_introOpening = false;
		}
	}

	// A declined gate asks again only after the app comes back (not when the gate's own modal closes, which re-fires OnAppearing).
	private void OnWindowResumed(object? sender, EventArgs e)
	{
		_introGateDeclined = false;
		_ = ResumeIntroAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		if (Window is { } window)
		{
			window.Resumed -= OnWindowResumed;
		}

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
				if (!await TryRefreshAfterStagedUpdateAsync())
				{
					break; // cancelled (the gallery went away): no further pass
				}
			}
			while (_stagedRefreshAgain); // a failed pass still gets the pass a later signal asked for
		}
		finally
		{
			_stagedRefreshRunning = false;
		}
	});

	// False only when the refresh was cancelled. Nothing escapes (this runs on a background-triggered path and would crash the app); the shelf stays as it was until the next refresh.
	private static async Task<bool> TryRefreshAfterStagedUpdateAsync(Func<Task> refresh)
	{
		try
		{
			await refresh();
			return true;
		}
		catch (OperationCanceledException)
		{
			// no-fallback: a refresh cancelled by the gallery's teardown is not a failure
			return false;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader gallery refresh after a book update failed: {ex}");
			DinoFallbacks.Report(FallbackArea.Store, FallbackReason.UpdateCheckFailed); // Slice 313
			return true;
		}
	}

	private Task<bool> TryRefreshAfterStagedUpdateAsync() => TryRefreshAfterStagedUpdateAsync(RefreshAsync);

	private bool _stagedRefreshRunning;
	private bool _stagedRefreshAgain;

	// A store download can finish on a background thread while the gallery shows; reload the list on the UI thread (Slice 92).
	private void OnLibraryChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(async () =>
	{
		try
		{
			await RefreshAsync();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// no-fallback: logged only; the shelf stays as it was until the next refresh
			Debug.WriteLine($"Dino Reader gallery refresh failed: {ex}");
		}
	});

	// The tiles follow the mode through their binding; only the Add entry and the chrome change here.
	private void OnModeChanged(object? sender, PropertyChangedEventArgs e) => MainThread.BeginInvokeOnMainThread(() =>
	{
		ApplyMode();
		if (!_mode.IsGrownUp)
		{
			CloseReorder("left grown-up mode"); // Slice 264: leaving grown-up mode cancels the reorder mode; nothing was saved
			BookInfo.Dismiss(); // also refreshes the tiles when a switch was saved
		}

		if (_mode.IsGrownUp)
		{
			// A grown-up got in (settings gate): the saved intro may be offered again.
			_introGateDeclined = false;
			_ = ResumeIntroAsync();
		}
	});

	private void ApplyMode()
	{
		if (_shelfLanguage is not null && _shelfLanguage != LanguageChoice.Current())
		{
			_ = RefreshShelfAsync(); // the app language changed: the shelf titles (and the Add tile, rebuilt with them) follow it
		}

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
		AppIcon.SetValue(SemanticProperties.DescriptionProperty, AppResources.App_Name);
		GrownUpsButton.SetValue(SemanticProperties.DescriptionProperty, AppResources.Gallery_GrownUpsButtonLocked);
		GrownUpsButton.SetValue(ToolTipProperties.TextProperty, AppResources.Gallery_GrownUpsButtonLocked);
		GrownUpsLabel.Text = AppResources.Gallery_GrownUpsPill;
		CornerLine.Text = AppResources.Gallery_CornerLine;
		GrownUpsButton.SetValue(SemanticProperties.HintProperty, AppResources.Gallery_OpensGateHint);
		SettingsButton.SetValue(SemanticProperties.DescriptionProperty, AppResources.Settings_GearLabel);
		SettingsButton.SetValue(ToolTipProperties.TextProperty, AppResources.Settings_GearLabel);
		StoreLabel.Text = AppResources.Gallery_GetMoreBooks;
		StoreButton.SetValue(SemanticProperties.DescriptionProperty, AppResources.Gallery_GetMoreBooks);
	}

	private readonly WaitController _wait;

	private string? _shelfLanguage; // the app language the shelf titles were built in

	private async Task RefreshShelfAsync()
	{
		try
		{
			await RefreshAsync();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// no-fallback: logged only; the shelf stays as it was until the next refresh
			Debug.WriteLine($"Dino Reader gallery refresh failed: {ex}");
		}
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

	private async Task RefreshAsync()
	{
		await WaitForStagedUpdatesAsync();
		IReadOnlyList<Story> stories = await _library.ListAsync();
		_ = _services.GetRequiredService<ShelfAudioPacks>().RescanAsync(); // the shelf list changed: the snapshot of installed audio follows it
		_shelfLanguage = LanguageChoice.Current();
#if DEBUG
		ReorderLog($"shelf order {string.Join(",", stories.Select(s => s.Id))}");
#endif
		_items.Clear();
		// The Add button above the shelf replaced the Add tile (Slice 172, mock 85).
		foreach (object item in GalleryItems.Build(stories, _library, _mode, _services.GetRequiredService<StoryTranslator>().ShelfTitle).Where(item => item is not AddStoryEntry))
		{
			_items.Add(item);
		}

		ApplyModeChrome(_mode.IsGrownUp);
	}

	private void ApplyModeChrome(bool grownUp)
	{
		bool handover = grownUp && FirstRunState.HandoverPending;
		ApplyShelfRing();
		GrownUpsButton.IsVisible = !grownUp;
		SettingsButton.IsVisible = grownUp;
		ApplyLanguageButton();
		HandoverButton.IsVisible = grownUp;
		HandoverButton.Text = handover ? AppResources.Gallery_ReadyForChild : AppResources.Gallery_BackToShelf;
		AppIcon.IsVisible = !grownUp;
		TitleLabel.Text = grownUp ? AppResources.Gallery_CornerTitle : AppResources.Gallery_Title;
		GrownUpActions.IsVisible = grownUp;
		ApplyCornerHeader(grownUp);
		BackgroundColor = DinoTokens.Get(grownUp ? DinoTokens.Sand : DinoTokens.Cream);
#if WINDOWS
		DinoTitleBar.Refresh(this); // Slice 235 part B: entering or leaving the corner changes the title bar colour at once
#endif
	}

	// Slice 228 (mock Batch 43 frames 97 and 98): the grown-ups' corner has an ink header bar with an amber lock, a white title and a gear in a translucent tile, and three tiles across;
	// the kids' shelf has none of that, so the two places never look alike.
	private void ApplyCornerHeader(bool grownUp)
	{
		HeaderBar.BackgroundColor = grownUp ? DinoTokens.Get(DinoTokens.Ink) : Colors.Transparent;
		CornerLock.IsVisible = grownUp;
		TitleLabel.TextColor = grownUp ? Colors.White : DinoTokens.Get(DinoTokens.Ink);
		SettingsButton.BackgroundColor = grownUp ? CornerGearFill : DinoTokens.Get(DinoTokens.ControlFill);
		GearPath.Stroke = new SolidColorBrush(grownUp ? Colors.White : DinoTokens.Get(DinoTokens.Body));
		ApplyShape();
	}

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
		}
	}

	// Slice 232 part B (mock Batch 44 frames 110, 111, 125, 126): every size comes from one pure call on the window size (LandscapeLayout.ShelfLayout / CornerLayout); this page only applies it.
	// Rotation changes the layout only: grown-up mode, the open sheet and the first book on screen are kept.
	private void ApplyShape()
	{
		if (_windowSize.Width <= 0)
		{
			return;
		}

		bool grownUp = _mode.IsGrownUp;
		CornerShape corner = LandscapeLayout.CornerLayout(_windowSize.Width, _windowSize.Height);
		ShelfShape shelf = WindowShapeSeam.ShelfLayout(DesktopTips.IsWindows, _windowSize.Width, _windowSize.Height); // a phone-landscape Windows window gets a grid, not the sideways row
		bool rail = grownUp && corner.Rail;
		bool sideways = !grownUp && shelf.Sideways;
		int columns = grownUp ? corner.Columns : shelf.Columns;
		bool tabletShelf = !grownUp && shelf.TabletUpright;
		string key = $"{sideways}|{rail}|{corner.RailWidth}|{corner.FullRowButtons}|{columns}|{shelf.CoverHeight}|{grownUp}|{tabletShelf}|{corner.TabletUpright}";
		ApplyShelfMargin(sideways, shelf, rail, tabletShelf); // the one thing that follows the width pixel by pixel (the 1600 dp cap on Windows); a no-op when the value is unchanged
		// The reorder layer spans the whole page, rail included, so its columns come from the full window width, not the width left of the rail.
		_reorderColumns = !rail ? columns : corner.TabletUpright ? LandscapeLayout.TabletPortraitShelfColumns(_windowSize.Width) : LandscapeLayout.CornerColumnsFor(_windowSize.Width);
		_reorderSidePadding = grownUp && corner.TabletUpright ? ReorderTabletPadding : ReorderPhonePadding;
		_reorderGap = grownUp && corner.TabletUpright ? TabletColumnSpacing : GridItemSpacing;
		_reorder.Reshape(_reorderColumns, _reorderSidePadding, _reorderGap); // a rotation or resize while the mode is open re-fits it
		if (key == _shapeKey)
		{
			return;
		}

		_shapeKey = key;
		if (grownUp != _shapeGrownUp)
		{
			_anchorBookId = null; // the other mode lists other tiles: its first book is not this one's
			_shapeGrownUp = grownUp;
		}

#if WINDOWS
		WindowTrace.Write($"Dino shelf: columns={columns} sideways={sideways} rail={rail} grownUp={grownUp} content={_windowSize.Width:0}x{_windowSize.Height:0}"); // the VM check reads the shelf's shape from this
#endif
		ApplyRail(rail, corner);
		ApplyHeaderShape(rail, sideways, corner, tabletShelf);
		ApplyShelfList(sideways, shelf, columns, rail, tabletShelf);
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] shelf layout={(sideways ? "sideways" : rail ? "rail" : "grid")} columns={columns} grownUp={grownUp} railWidth={corner.RailWidth} tabletUpright={tabletShelf} tabletCorner={rail && corner.TabletUpright}");
#endif
	}

	// The grown-ups' corner's ink rail holds the header, the line, Add and Store and the way back; the books take the second column. Anywhere else the rail column is 0 wide and everything spans both.
	private void ApplyRail(bool rail, CornerShape corner)
	{
		GalleryRoot.ColumnDefinitions[0].Width = new GridLength(rail ? corner.RailWidth : 0);
		RailBackdrop.IsVisible = rail;
		int span = rail ? 1 : 2;
		Grid.SetColumnSpan(HeaderBar, span);
		Grid.SetColumnSpan(ActionsScroll, span);
		Grid.SetRow(ActionsScroll, rail ? 2 : 1); // the rail's middle takes the star row, so it scrolls and the way back stays at the foot
		Grid.SetColumnSpan(HandoverButton, span);
		Grid.SetColumn(StoryList, rail ? 1 : 0);
		Grid.SetColumnSpan(StoryList, span);
		Grid.SetRow(StoryList, rail ? 0 : 2);
		Grid.SetRowSpan(StoryList, rail ? GalleryRoot.RowDefinitions.Count : 1);
		double inset = corner.FullRowButtons ? TabletRailInset : LandscapeLayout.SidewaysInset;
		HandoverButton.Margin = rail ? new Thickness(inset, 12, 14, 16) : new Thickness(14, 12, 14, 24);
		ApplyRailButtons(rail, corner, inset);
	}

	private const double TabletRailInset = 40;

	// Add and Store: white and violet bars in the portrait bar; on the rail translucent tiles, a pair on a phone and two full rows on a tablet.
	private void ApplyRailButtons(bool rail, CornerShape corner, double inset)
	{
		bool rows = rail && corner.FullRowButtons;
		CornerLine.TextColor = rail ? RailLine : Color.FromArgb("#5a5470");
		CornerLine.FontSize = rail ? 12.5 : 13.5;
		CornerLine.Padding = rail ? new Thickness(inset, 0, 14, 10) : new Thickness(18, 12, 18, 10);
		ActionPair.Padding = rail ? new Thickness(inset, 0, 14, 10) : new Thickness(14, 0, 14, 14);
		double height = rows ? 56 : rail ? 44 : 52;
		ApplyButtonHeights(rail, height);
		// Slice 234 part B (mock Batch 45 frame 139): on a tablet held upright the store button is solid indigo in the rail, as in the portrait bar.
		StoreButton.BackgroundColor = rail && !corner.TabletUpright ? RailButtonFill : DinoTokens.Get(DinoTokens.Brand);
		StoreLabel.FontSize = rail && !rows ? 14 : 15;
		StoreContent.HorizontalOptions = rows ? LayoutOptions.Start : LayoutOptions.Center;
		StoreButton.Padding = rows ? new Thickness(18, 0) : new Thickness(0);
	}

	// Portrait keeps the fixed 52 dp tiles with a truncated label; only on the rail does a label wrap to two lines and make its tile taller.
	private void ApplyButtonHeights(bool rail, double height)
	{
		StoreButton.HeightRequest = rail ? -1 : height;
		StoreButton.MinimumHeightRequest = rail ? height : -1;
		StoreLabel.LineBreakMode = rail ? LineBreakMode.WordWrap : LineBreakMode.TailTruncation;
		StoreLabel.MaxLines = rail ? 2 : -1;
	}

	// The header: a compact bar in the kids' sideways shelf, the rail's top in the corner (the title may take two lines there), else the portrait bar.
	// A tablet held upright (Slice 234, frame 133): 40 dp sides, a 52 dp icon and "Grown-ups" target, the title at 28.
	private void ApplyHeaderShape(bool rail, bool sideways, CornerShape corner, bool tabletShelf)
	{
		double inset = corner.FullRowButtons ? TabletRailInset : LandscapeLayout.SidewaysInset;
		HeaderBar.Padding = rail ? new Thickness(inset, corner.FullRowButtons ? 28 : 14, 14, 10)
			: tabletShelf ? TabletHeaderPadding
			: sideways ? new Thickness(LandscapeLayout.SidewaysInset, 6, 12, 4) : new Thickness(16, 10, 12, 12);
		TitleLabel.FontSize = rail ? (corner.FullRowButtons ? 24 : 19) : tabletShelf ? TabletTitleSize : 22;
		TitleLabel.MaxLines = rail ? 2 : 1;
		double icon = tabletShelf ? TabletIconSize : PhoneIconSize;
		AppIconFrame.WidthRequest = icon;
		AppIconFrame.HeightRequest = icon;
		GrownUpsButton.HeightRequest = tabletShelf ? LandscapeLayout.TabletHeaderTarget : PhoneGrownUpsHeight;
		SettingsButton.HeightRequest = tabletShelf ? LandscapeLayout.TabletHeaderTarget : rail && corner.TabletUpright ? TabletCornerGearSize : PhoneGearSize;
		SettingsButton.WidthRequest = SettingsButton.HeightRequest;
		_languageButton.HeightRequest = tabletShelf ? LandscapeLayout.TabletHeaderTarget : LanguageButtonHeight;
	}

	private const double PhoneIconSize = 40;
	private const double PhoneGrownUpsHeight = 48;
	private const double PhoneGearSize = 44;
	private const double TabletCornerGearSize = 48; // the gear tile in the tablet-upright corner rail (mock Batch 45 frame 139)

	// A phone held sideways: one row of covers that scrolls sideways, the last one cut off to say "more"; else a grid of this many columns. The first book on screen stays on screen.
	private void ApplyShelfList(bool sideways, ShelfShape shelf, int columns, bool rail, bool tabletShelf)
	{
		bool roomy = tabletShelf || (DesktopTips.IsWindows && !sideways && !rail); // Slice 235 part C (frames 151, 154): a desktop shelf uses the tablet's gaps and 40 dp sides
		double horizontalGap = roomy ? TabletColumnSpacing : GridItemSpacing;
		double verticalGap = roomy ? TabletRowSpacing : GridItemSpacing;
		bool wasSideways = StoryList.ItemsLayout is LinearItemsLayout;
		_swapPhase = ScrollSwapPhase.SwapStarted;
		_swapGeneration++;
		if (sideways)
		{
			StoryList.ItemsLayout = wasSideways ? StoryList.ItemsLayout : new LinearItemsLayout(ItemsLayoutOrientation.Horizontal) { ItemSpacing = SidewaysItemSpacing };
		}
		else if (wasSideways || StoryList.ItemsLayout is not GridItemsLayout)
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

		SetResourceIfChanged(TileWidthKey, sideways ? LandscapeLayout.CoverWidth(shelf.CoverHeight) : -1.0);
		SetResourceIfChanged(TileTitleLinesKey, sideways ? 1 : 2);
		ApplyShelfMargin(sideways, shelf, rail, tabletShelf);

		if (wasSideways != sideways)
		{
			ReattachItems();
		}

		KeepFirstBookOnScreen();
	}

	// Slice 235 part C (frame 154): on Windows the 1600 dp cap is this margin, a pure function of the PAGE width; the list is never given a maximum width or asked its own size (a layout cycle on the VM).
	private void ApplyShelfMargin(bool sideways, ShelfShape shelf, bool rail, bool tabletShelf)
	{
		bool roomy = tabletShelf || (DesktopTips.IsWindows && !sideways && !rail);
		double side = roomy ? (DesktopTips.IsWindows ? WindowShapeSeam.ShelfSideMargin(_windowSize.Width, shelf.SidePadding) : shelf.SidePadding) : LandscapeLayout.PhoneShelfSidePadding;
		Thickness margin = rail ? new Thickness(16, 14, 26, 0) : sideways ? new Thickness(LandscapeLayout.SidewaysInset, 0, 0, 0) : new Thickness(side, 0);
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

	// Slice 235 part B: the shelf answers the Windows window's keyboard. Esc closes the book's options sheet while it is open and does nothing on the shelf itself.
	DinoScreen IDinoKeyTarget.Screen => _reorder.IsVisible ? DinoScreen.Reorder : BookInfo.IsVisible ? DinoScreen.Sheet : DinoScreen.Shelf;

	bool IDinoKeyTarget.Handle(DinoKeyAction action, DinoKey key)
	{
		if (_reorder.IsVisible)
		{
			return HandleReorderKey(action); // Slice 264: Esc cancels the reorder mode, Enter is Done (Space lifts and drops a book)
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
		_reorder.Open(tiles, liftedId, _reorderColumns, _reorderSidePadding, _reorderGap, (double)Resources[TileTitleHeightKey]);
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
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[BookSheet] {line}"); // tells a sheet that never opened from one that opened and was hidden
#endif
	}

	private static void ReorderLog(string line)
	{
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Reorder] {line}"); // the shelf-reorder flow's expect-log markers (RTST-TEST tag)
#endif
	}

#if DEBUG
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
			await OpenTileAsync(tile);
		}
	}

	// A tap on a cover (kids' mode: read it; grown-up mode: its options sheet). The Windows keyboard (Enter or Space on a focused book) takes the same path.
	private Task OpenTileAsync(StoryTile tile) =>
		_busy.RunAsync(() => _mode.IsGrownUp ? OpenBookSheetAsync(tile.Story) : _actions.ReadAsync(Navigation, tile.Story));

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
		string title = _services.GetRequiredService<StoryTranslator>().ShelfTitle(story);
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
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[GrownUps] read-now leaves the corner and opens {story.Id}"); // the corner-read-now flow's expect-log marker (RTST-TEST tag)
#endif
		FirstRunState.HandoverPending = false;
		_mode.Leave();
		await _actions.ReadAsync(Navigation, story);
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
			DeleteOutcome outcome = await _actions.ConfirmDeleteAsync(this, story, RemovePrompt(story, _services.GetRequiredService<StoryTranslator>().ShelfTitle(story)));
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

	// The store (Slice 92, "Store" since Slice 172): a grown-up page, so it is only reachable in grown-up mode and closes with it.
	private async void OnStoreTapped(object? sender, TappedEventArgs e) => await RunGrownUpAsync(OpenStoreAsync);

	private async Task OpenStoreAsync()
	{
		var store = _services.GetRequiredService<StorePage>();
		if (_mode.IsGrownUp)
		{
			await Navigation.PushAsync(store);
		}
	}

	private async void OnOpenRequested(object? sender, EventArgs e)
	{
		if (_reorder.IsVisible || Environment.TickCount64 < _suppressTapUntil)
		{
			return;
		}

		if (TileOf(sender) is { } tile)
		{
			await RunGrownUpAsync(() => OpenBookSheetAsync(tile.Story));
		}
	}

	// Seven taps on the title in grown-up mode flip the developer switch (Slice 91); the tiles are rebuilt, and the book sheet shows or hides Export.
	private async void OnTitleTapped(object? sender, TappedEventArgs e)
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
				await RefreshAsync();
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// no-fallback: logged only; the shelf stays as it was until the next refresh
			Debug.WriteLine($"Dino Reader gallery refresh failed: {ex}");
		}
	}

	// The book-and-plus button (Slice 189): the gate first, then grown-up mode. A failed gate leaves the shelf as it was.
	private async void OnGrownUpsTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(EnterGrownUpModeAsync);

	private async Task EnterGrownUpModeAsync()
	{
		if (await _gate.RequestAsync(Navigation))
		{
			_mode.Enter();
		}
	}

	// The gear in grown-up mode: Settings with no second gate. Back from it (and from its sub-pages, one step each) ends here, then on the kids' shelf (Decision 169, mock 94).
	private async void OnSettingsTapped(object? sender, TappedEventArgs e) => await RunGrownUpAsync(OpenSettingsAsync);

	private async Task OpenSettingsAsync() => await Navigation.PushAsync(_services.GetRequiredService<SettingsPage>());

	// Back from grown-up mode lands on the kids' shelf, not out of the app (Slice 172, Decision 169, mock 94).
	protected override bool OnBackButtonPressed()
	{
		if (_languageButton.Parent?.Parent is Grid menuHost && StoryLanguageMenu.DismissOpen(menuHost))
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

		CompleteHandover();
		return true;
	}

	// The kids' shelf is home (Slice 174, Decision 169, mock 94): Android back asks "Close Dino Book Reader?" instead of leaving. iOS has no back button and
	// Windows closes from its window, so nothing changes there.
	private bool AskToClose()
	{
		if (!OperatingSystem.IsAndroid())
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

	// The sticky coral button (Slice 228): "Ready for my child" during the first hand-over (Slice 93), "Back to my child's shelf" after it. Both leave grown-up mode.
	private void OnHandoverClicked(object? sender, EventArgs e) => CompleteHandover();

	// Leaves grown-up mode; Back during the first hand-over counts as "Ready for my child", so the flag never outlives the mode.
	private void CompleteHandover()
	{
		FirstRunState.HandoverPending = false;
		_mode.Leave();
	}
}
