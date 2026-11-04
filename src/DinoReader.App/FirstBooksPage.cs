using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared.Waiting;

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
public partial class FirstBooksPage : ContentPage
{
	private const double SideMargin = 16;
	private const double ColumnGap = 12;
	private const double RowGap = 14;
	private const double MinCardWidth = 150;
	private const int MinColumns = 2;
	private const int MaxColumns = 4;
	private const int CoverParallelism = 4;
	private const int PercentScale = 100;

	private readonly StoreClient _store;
	private readonly IStoryLibrary _library;
	private readonly UsageCounter _usage;
	private readonly BusyGuard _busy = new();
	private readonly WaitOverlay _waitOverlay = DinoWait.OverPage();
	private readonly WaitController _wait;
	private readonly HashSet<string> _ticked = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _onShelf = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<FirstBookCard> _cards = [];
	private IReadOnlyList<StoreItem> _items = [];
	private CancellationTokenSource _lifetime = new();
	private bool _loaded;
	private bool _finished;
	private bool _offline;
	private double _width;

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

	/// <summary>Called when the step is done (the shelf holds enough books): the first run goes on to the voice download. Set by whoever shows the page.</summary>
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

		Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
		if (!_loaded)
		{
			await _busy.RunAsync(LoadAsync);
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
		_lifetime.Cancel();
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		if (width > 0 && width != _width)
		{
			_width = width;
			PlaceCards();
		}
	}

	// A list that could not load comes back by itself when the connection does; a download in progress only re-words its wait.
	private async void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
	{
		if (_wait.IsActive)
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
			StoreListing listing = await _wait.RunAsync(WaitStage.GettingFile, AppResources.Wait_GettingBook, canCancel: true, cancel => _store.ListAsync(cancel));
			if (listing.Status == StoreLoadStatus.Offline)
			{
				ShowOffline();
				return;
			}

			await ShowListAsync(listing.Items);
		}
		catch (OperationCanceledException)
		{
			ShowOffline(); // Cancel on the list wait, or the page was left: the message with Try again takes over
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader first-books load failed: {ex}");
			ShowOffline();
		}
	}

	private async Task ShowListAsync(IReadOnlyList<StoreItem> items)
	{
		_items = FirstBooks.Order(items);
		await ReadShelfAsync();
		if (FirstBooks.IsSatisfied(_shelfCount))
		{
			await FinishAsync(); // killed after the last book landed: nothing left to choose
			return;
		}

		_ticked.Clear();
		foreach (string id in FirstBooks.AlreadyOnShelf(_items, _onShelf))
		{
			_ticked.Add(id);
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

	private void Toggle(FirstBookCard card)
	{
		if (_busy.IsBusy || _wait.IsActive)
		{
			return;
		}

		string id = card.Item.Id;
		bool picked = _ticked.Add(id);
		if (!picked)
		{
			_ticked.Remove(id);
		}

		if (picked && FirstBooksGate.FirstPick is null)
		{
			FirstBooksGate.FirstPick = id;
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
		if (!await DownloadAsync(toFetch))
		{
			return;
		}

		await ReadShelfAsync();
		if (FirstBooks.IsSatisfied(_shelfCount))
		{
			await FinishAsync();
		}
	}

	// False when the grown-up cancelled or a book failed: the ticks stay, a book that landed stays on the shelf (and ticked), and Continue fetches what is missing.
	private async Task<bool> DownloadAsync(IReadOnlyList<StoreItem> toFetch)
	{
		_waitCovers = new FirstBooksWaitCovers(toFetch.Select(CoverOf));
		_waitOverlay.SetContent(_waitCovers);
		CancellationToken cancel = _wait.BeginCancellable(WaitStage.GettingFile, AppResources.FirstBooks_Getting);
		try
		{
			for (int i = 0; i < toFetch.Count; i++)
			{
				if (!await FetchOneAsync(toFetch, i, cancel))
				{
					return false;
				}
			}

			return true;
		}
		finally
		{
			_wait.End();
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
	private string _progressDetail = string.Empty;
	private FirstBooksWaitCovers? _waitCovers;

	// The cover file the picker already loaded for this book, or null (the wait then draws the tinted block).
	private string? CoverOf(StoreItem item) => _cards.FirstOrDefault(c => string.Equals(c.Item.Id, item.Id, StringComparison.OrdinalIgnoreCase))?.CoverPath;

	// The shared wait: "Getting your books…", "Book 2 of 2 · 72%" and a bar over all the books; with no connection the line reads "Waiting for the internet" and the bar holds.
	private void ShowProgress(int index, int total, double fraction)
	{
		_progress = Math.Clamp((index + fraction) / Math.Max(total, 1), 0, 1);
		string percent = string.Create(AppResources.Culture, $"{(int)(_progress * PercentScale)}%");
		_progressDetail = $"{AppResources.Format(AppResources.FirstBooks_BookOf, FirstBooks.BookNumber(index, total), total)} · {percent}";
		RefreshWaitLook();
	}

	private void RefreshWaitLook()
	{
		bool offline = Connectivity.Current.NetworkAccess != NetworkAccess.Internet;
		string line = offline ? AppResources.Wait_WaitingInternet : AppResources.FirstBooks_Getting;
		_wait.Begin(WaitStage.GettingFile, line, canCancel: true, _progressDetail, new WaitLook(offline, _progress, null));
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
		if (Completed is { } completed)
		{
			await completed();
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
			// The page was left: the covers stay as the tinted blocks.
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader first-books cover failed: {ex.Message}");
		}
	}

	// How many columns fit: at least two, as many 150 dp cards as the width allows, at most four.
	private int Columns() => Math.Clamp((int)((_width - 2 * SideMargin + ColumnGap) / (MinCardWidth + ColumnGap)), MinColumns, MaxColumns);

	private double CardWidth(int columns) => (_width - 2 * SideMargin - ColumnGap * (columns - 1)) / columns;

	// Debug-only marker a flow reads: the page state, how many books are ticked and how many are on the shelf.
	private void LogState(string what)
	{
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[FirstBooks] {what} listed={_items.Count} ticked={_ticked.Count} onShelf={_onShelf.Count}");
#endif
	}
}
