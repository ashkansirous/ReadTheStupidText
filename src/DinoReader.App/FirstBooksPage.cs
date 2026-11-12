using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared.Waiting;
using UsageBuckets = ReadTheStupidText.Application.Usage.UsageBuckets;
using UsageWaitStage = ReadTheStupidText.Application.Usage.UsageWaitStage;

namespace DinoReader;

/// <summary>
/// Where a fresh install is in the first-run book step (Slice 261): set when the language is picked, cleared when the shelf holds <see cref="FirstBooks.Minimum"/> books. The library on disk is
/// the one owner of the real state (<see cref="FirstBooks.IsSatisfied"/>); this flag only says that the step has begun, so an existing install that later empties its shelf never sees the picker.
/// </summary>
public static class FirstBooksGate
{
	private const string PendingKey = "dino.firstbooks.pending";
	private const string FirstPickKey = "dino.firstbooks.first";

	/// <summary>True from the language pick until the shelf holds enough books: the app opens on the picker (language page, then picker, then the rest).</summary>
	public static bool Pending
	{
		get => Preferences.Default.Get(PendingKey, false);
		set => Preferences.Default.Set(PendingKey, value);
	}

	/// <summary>The store id of the book the grown-up ticked first: the intro's "Try it" opens it. Null when none was picked.</summary>
	public static string? FirstPick
	{
		get => Preferences.Default.ContainsKey(FirstPickKey) ? Preferences.Default.Get(FirstPickKey, string.Empty) : null;
		set
		{
			if (value is null)
			{
				Preferences.Default.Remove(FirstPickKey);
				return;
			}

			Preferences.Default.Set(FirstPickKey, value);
		}
	}
}

/// <summary>
/// The first-run book picker (Slice 261, mock Batch 47 frames 163-166): a fresh install has an empty shelf, and the grown-up ticks at least two of the store's books before the app goes on. There is
/// no skip. The list shows tagged books first (Slice 260); Continue is enabled from two ticks and downloads them behind the shared wait overlay (Cancel keeps the ticks and the books that landed).
/// Offline, a message with Try again takes its place and the list loads by itself when the connection returns. After a kill the books that landed come back ticked, and a shelf that already
/// holds two books ends the step at once. All words come from <see cref="AppResources"/>; the layout never mirrors (Slice 96).
/// </summary>
public partial class FirstBooksPage : ContentPage, IDinoKeyTarget
{
	private const int CoverParallelism = 4;
	private const int PercentScale = 100;
	private const int WaitShownMilliseconds = 500; // the shared wait counts only a wait the grown-up could see (Slice 148); the getting page keeps that rule for its count

	private readonly StoreClient _store;
	private readonly IStoryLibrary _library;
	private readonly UsageCounter _usage;
	private readonly BusyGuard _busy = new();
	private readonly WaitOverlay _waitOverlay = DinoWait.OverPage();
	private readonly WaitController _wait;
	private readonly HashSet<string> _ticked = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _onShelf = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<string> _tickOrder = [];
	private CancellationTokenSource? _downloadCancel;
	private CancellationTokenSource? _loadCancel;
	private readonly List<FirstBookCard> _cards = [];
	private IReadOnlyList<StoreItem> _items = [];
	private CancellationTokenSource _lifetime = new();
	private bool _loaded;
	private bool _finished;
	private bool _handedOver;
	private bool _offline;
	private Size _windowSize;
	private OnboardingShape? _shape;

	public FirstBooksPage(StoreClient store, IStoryLibrary library, UsageCounter usage)
	{
		_store = store;
		_library = library;
		_usage = usage;
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		BackgroundColor = DinoTokens.Get(DinoTokens.Cream);
		_wait = new WaitController(_waitOverlay);
		BuildViews();
	}

	/// <summary>Called when the step is done (the shelf holds enough books): the first run goes on to "Ready to read". Set by whoever shows the page.</summary>
	public Func<Task>? Completed { get; set; }

	/// <summary>Called when the grown-up goes back to the language page. Null (the page is the root after a restart) hides the arrow.</summary>
	public Func<Task>? BackRequested
	{
		get => _backRequested;
		set
		{
			_backRequested = value;
			SetBackVisible(value is not null);
		}
	}

	private Func<Task>? _backRequested;

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_lifetime.IsCancellationRequested)
		{
			_lifetime = new CancellationTokenSource();
		}

		Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged; // OnAppearing can fire twice without OnDisappearing
		Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
		if (!_loaded)
		{
			await _busy.RunAsync(LoadAsync);
			return;
		}

		if (_gettingLayer.IsVisible)
		{
			StartHop(); // the getting page stayed up after success; OnDisappearing stopped its hop
		}

		_ = LoadCoversAsync(_lifetime.Token); // covers cut short when the page was left come back
	}

	// Android Back / iOS swipe-back: a download wait behaves like its Cancel (ticks and landed books stay); otherwise it is the arrow; the restart root has nowhere to go back to.
	protected override bool OnBackButtonPressed()
	{
		if (_finished)
		{
			return true; // the page is handing over to the ready page: Back must not pop the language page under it
		}

		if (_wait.IsActive || _downloadCancel is not null)
		{
			(_downloadCancel ?? _loadCancel)?.Cancel(); // the getting page or the list-load wait
			return true;
		}

		if (_backRequested is not null)
		{
			if (!_busy.IsBusy)
			{
				_ = _busy.RunAsync(GoBackAsync);
			}

			return true;
		}

		return _busy.IsBusy || base.OnBackButtonPressed(); // the restart root with no wait: the platform default (Android leaves the app)
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
		_lifetime.Cancel();
		StopHop(); // the getting page can stay up after success; its endless hop must not outlive the page
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		(width, height) = WindowShapeSeam.Effective(DesktopTips.IsWindows, width, height);
		if (width <= 0 || height <= 0 || (width == _windowSize.Width && height == _windowSize.Height))
		{
			return;
		}

		_windowSize = new Size(width, height);
		OnboardingShape shape = OnboardingLayout.Shape(width, height, DesktopTips.IsWindows, intro: false);
		if (shape != _shape)
		{
			_shape = shape;
			ApplyShape(shape);
		}
	}

	// A list that could not load comes back by itself when the connection does; a download in progress only re-words its page.
	private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
	{
		if (_wait.IsActive || _downloadCancel is not null)
		{
			MainThread.BeginInvokeOnMainThread(RefreshWaitLook);
			return;
		}

		if (_offline && e.NetworkAccess == NetworkAccess.Internet)
		{
			await MainThread.InvokeOnMainThreadAsync(() => _busy.RunAsync(LoadAsync));
		}
	}

	private async Task LoadAsync()
	{
		try
		{
			await ReadShelfAsync();
			if (FirstBooks.IsSatisfied(_shelfCount))
			{
				await FinishAsync(); // the shelf already holds enough books: no need for the store
				return;
			}

			StoreListing listing = await ListWithWaitAsync();
			if (listing.Status == StoreLoadStatus.Offline)
			{
				ShowOffline();
				return;
			}

			await ShowListAsync(listing.Items);
		}
		catch (OperationCanceledException)
		{
			// no-fallback: the grown-up cancelled or left the page; the load-failed state is shown, nothing was swapped in
			ShowLoadFailed(); // Cancel on the list wait, or the page was left: Try again takes over, but nothing reloads by itself
		}
		catch (Exception ex)
		{
			DinoFallbacks.Report(ReadTheStupidText.Application.Usage.FallbackArea.Store, ReadTheStupidText.Application.Usage.FallbackReason.StoreFailed); // Slice 275: the page shows its load-failed state with Try again
			Debug.WriteLine($"Dino Reader first-books load failed: {ex}");
			ShowLoadFailed();
		}
	}

	// The list-load wait ends on its own Cancel or on Back (through _loadCancel).
	private async Task<StoreListing> ListWithWaitAsync()
	{
		using CancellationTokenSource backCancel = new();
		_loadCancel = backCancel;
		try
		{
			return await _wait.RunAsync(WaitStage.GettingFile, AppResources.Wait_GettingBook, canCancel: true, async cancel =>
			{
				using CancellationTokenSource both = CancellationTokenSource.CreateLinkedTokenSource(cancel, backCancel.Token);
				return await _store.ListAsync(both.Token);
			});
		}
		finally
		{
			_loadCancel = null;
		}
	}

	private async Task ShowListAsync(IReadOnlyList<StoreItem> items)
	{
		_items = FirstBooks.Order(BookAccess.FirstRunOffer(_store.Sales.EnabledForOffer, items)); // Slice 346: a fresh install is offered only books that are not sold; while the switch is unread, only those the listing says are free
		await ReadShelfAsync();
		if (FirstBooks.IsSatisfied(_shelfCount))
		{
			await FinishAsync(); // killed after the last book landed: nothing left to choose
			return;
		}

		_ticked.Clear();
		_tickOrder.Clear();
		foreach (string id in FirstBooks.AlreadyOnShelf(_items, _onShelf))
		{
			_ticked.Add(id);
			_tickOrder.Add(id);
		}

		_loaded = true;
		_offline = false;
		BuildCards();
		ShowLayer(_listLayer);
		RefreshBar();
		_ = LoadCoversAsync(_lifetime.Token);
		LogState("list");
	}

	private int _shelfCount;

	private async Task ReadShelfAsync()
	{
		IReadOnlyList<Story> shelf = await _library.ListAsync();
		_shelfCount = shelf.Count;
		_onShelf.Clear();
		foreach (string id in shelf.Select(s => s.SourceId).OfType<string>())
		{
			_onShelf.Add(id);
		}
	}

	private void ShowOffline()
	{
		_offline = true;
		ShowLayer(_offlineLayer);
		LogState("offline");
	}

	// The list could not load for a reason other than having no connection (cancelled, timed out, failed): the same layer with Try again, but only a connection coming back reloads the offline case.
	private void ShowLoadFailed()
	{
		_offline = false;
		ShowLayer(_offlineLayer);
		LogState("failed-load");
	}

	private void Toggle(FirstBookCard card)
	{
		if (_busy.IsBusy || _wait.IsActive || _downloadCancel is not null)
		{
			return;
		}

		string id = card.Item.Id;
		bool picked = _ticked.Add(id);
		if (picked)
		{
			_tickOrder.Add(id);
		}
		else
		{
			_ticked.Remove(id);
			_tickOrder.Remove(id);
		}

		card.SetPicked(picked);
		RefreshBar();
		LogState("picked");
	}

	private async Task ContinueAsync()
	{
		if (!FirstBooks.CanContinue(_ticked.Count))
		{
			return;
		}

		IReadOnlyList<StoreItem> toFetch = FirstBooks.ToDownload(_items, _ticked, _onShelf);
		try
		{
			if (await DownloadAsync(toFetch))
			{
				await ReadShelfAsync();
			}
			else
			{
				return;
			}

			if (FirstBooks.IsSatisfied(_shelfCount))
			{
				FirstBooksGate.FirstPick = _tickOrder.FirstOrDefault(); // set only once the books are on the shelf: the first book still ticked, in tick order, is the intro's "Try it"
				await FinishAsync();
			}
		}
		finally
		{
			// the getting page stays only once FinishAsync handed over (the page is leaving), the offline page stays if it came up meanwhile, the picker returns otherwise (also when Completed threw)
			if (FirstBooks.EndOfContinue(_handedOver, _offlineLayer.IsVisible) == ContinueEnd.ShowPicker)
			{
				ShowLayer(_listLayer);
			}
		}
	}

	// False when the grown-up cancelled or a book failed: the ticks stay, a book that landed stays on the shelf (and ticked), and Continue fetches what is missing.
	private async Task<bool> DownloadAsync(IReadOnlyList<StoreItem> toFetch)
	{
		_waitCovers = new FirstBooksWaitCovers(toFetch.Select(CoverOf));
		using CancellationTokenSource linked = new();
		_downloadCancel = linked; // set before the page shows, so a connectivity event at once re-words it; Back and the page's Cancel both cancel through this
		long started = Stopwatch.GetTimestamp();
		ShowGetting(_waitCovers);
		try
		{
			for (int i = 0; i < toFetch.Count; i++)
			{
				if (!await FetchOneAsync(toFetch, i, linked.Token))
				{
					return false;
				}
			}

			return true;
		}
		finally
		{
			_downloadCancel = null;
			CountGettingWait(Stopwatch.GetElapsedTime(started), linked.IsCancellationRequested);
		}
	}

	// Slice 148 counted every wait that showed and every Cancel on one; the getting page is a page, not the shared overlay, so it reports the same two counts itself (only when it stayed 500 ms or more).
	private void CountGettingWait(TimeSpan shown, bool cancelled)
	{
		if (shown.TotalMilliseconds < WaitShownMilliseconds)
		{
			return;
		}

		string detail = UsageDetail.Wait(UsageWaitStage.Of(WaitStage.GettingFile), UsageBuckets.Duration(shown));
		_usage.RecordGeneral(UsageEventName.WaitShown, UsageEvent.NoLanguage, detail);
		if (cancelled)
		{
			_usage.RecordGeneral(UsageEventName.WaitCancelled, UsageEvent.NoLanguage, detail);
		}
	}

	private async Task<bool> FetchOneAsync(IReadOnlyList<StoreItem> toFetch, int index, CancellationToken cancel)
	{
		ShowProgress(index, toFetch.Count, 0);
		DownloadOutcome outcome = await _store.DownloadAsync(toFetch[index], new Progress<double>(p => ShowProgress(index, toFetch.Count, p)), cancel);
		if (outcome == DownloadOutcome.Done)
		{
			_onShelf.Add(toFetch[index].Id);
			_waitCovers?.SetLanded(index + 1);
			_usage.RecordStoreDownload(toFetch[index], ageFilterSet: false, authorFilterSet: false);
			LogState("landed");
			return true;
		}

		LogState(outcome == DownloadOutcome.Cancelled ? "cancelled" : "failed");
		if (outcome == DownloadOutcome.Failed)
		{
			await DisplayAlertAsync(AppResources.Store_OfflineTitle, AppResources.Store_OfflineBody, AppResources.Common_Ok);
		}

		return false;
	}

	private double _progress;
	private string _bookOfText = string.Empty;
	private string _percentText = string.Empty;
	private FirstBooksWaitCovers? _waitCovers;

	// The cover file the picker already loaded for this book, or null (the getting page then draws the tinted block).
	private string? CoverOf(StoreItem item) => _cards.FirstOrDefault(c => string.Equals(c.Item.Id, item.Id, StringComparison.OrdinalIgnoreCase))?.CoverPath;

	// The getting page (frame O3): "Getting your books…", "Book 2 of 2" and "72%" under a bar over all the books; with no connection the line reads "Waiting for the internet" and the bar holds.
	private void ShowProgress(int index, int total, double fraction)
	{
		_progress = Math.Clamp((index + fraction) / Math.Max(total, 1), 0, 1);
		_percentText = string.Create(AppResources.Culture, $"{(int)(_progress * PercentScale)}%");
		_bookOfText = AppResources.Format(AppResources.FirstBooks_BookOf, FirstBooks.BookNumber(index, total), total);
		RefreshWaitLook();
	}

	private void RefreshWaitLook()
	{
		if (_downloadCancel is null)
		{
			return; // a late progress report or connectivity callback must not re-word a page that is gone
		}

		bool offline = Connectivity.Current.NetworkAccess != NetworkAccess.Internet;
		string line = offline ? AppResources.Wait_WaitingInternet : AppResources.FirstBooks_Getting;
		SetGettingText(line, _bookOfText, _percentText, _progress);
	}

	// Frames O2 / O2-8 / OL2 / OT2 / OW2: Enter is Continue (once two are picked), Space ticks the focused cover; Esc goes back to the language page (or cancels the download).
	DinoScreen IDinoKeyTarget.Screen => DinoScreen.FirstBooks;

	bool IDinoKeyTarget.Handle(DinoKeyAction action, DinoKey key)
	{
		switch (action)
		{
			case DinoKeyAction.PickToggle when _cards.FirstOrDefault(c => c.TickFocused) is { } card && _listLayer.IsVisible:
				Toggle(card);
				return true;
			case DinoKeyAction.PickContinue when _listLayer.IsVisible:
				// Enter is Continue here: when it cannot continue yet it is still ours, or WinUI would click the focused tick button and untick that cover; on the Back or Continue button it is theirs (WinUI activates it)
				if (_backButtons.Any(b => b.IsFocused) || _continue.IsFocused)
				{
					return false;
				}

				if (FirstBooks.CanContinue(_ticked.Count) && !_busy.IsBusy && !_wait.IsActive && _downloadCancel is null)
				{
					_ = _busy.RunAsync(ContinueAsync);
				}

				return true;
			case DinoKeyAction.Back:
				return OnBackButtonPressed();
			default:
				return false;
		}
	}

	private async Task FinishAsync()
	{
		if (_finished)
		{
			return;
		}

		_finished = true;
		FirstBooksGate.Pending = false;
		LogState("done");
		try
		{
			if (Completed is { } completed)
			{
				await completed();
			}

			_handedOver = true; // only now has the page really handed over: EndOfContinue keeps the getting page up for this and nothing else
		}
		catch
		{
			// no-fallback: restores the gate and rethrows to ContinueAsync's caller
			FirstBooksGate.Pending = true; // the hand-over failed: the picker comes back and Continue can try again
			_finished = false;
			throw;
		}
	}

	private async Task LoadCoversAsync(CancellationToken cancel)
	{
		using var gate = new SemaphoreSlim(CoverParallelism);
		await Task.WhenAll(_cards.Select(card => LoadCoverAsync(card, gate, cancel)));
	}

	private async Task LoadCoverAsync(FirstBookCard card, SemaphoreSlim gate, CancellationToken cancel)
	{
		try
		{
			await gate.WaitAsync(cancel);
			try
			{
				card.SetCover(await _store.CoverPathAsync(card.Item, cancel));
			}
			finally
			{
				gate.Release();
			}
		}
		catch (OperationCanceledException)
		{
			// no-fallback: the page was left; the covers stay as the tinted blocks.
		}
		catch (Exception ex)
		{
			DinoFallbacks.Report(ReadTheStupidText.Application.Usage.FallbackArea.Store, ReadTheStupidText.Application.Usage.FallbackReason.StoreFailed); // Slice 275: the cover stays a tinted block; the book itself is unaffected
			Debug.WriteLine($"Dino Reader first-books cover failed: {ex.Message}");
		}
	}

	// Debug-only marker a flow reads: the page state, how many books are ticked and how many are on the shelf.
	private void LogState(string what)
	{
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[FirstBooks] {what} listed={_items.Count} ticked={_ticked.Count} onShelf={_onShelf.Count}");
#endif
	}
}
