using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>Where one store entry stands: on offer, being fetched, or already on the shelf.</summary>
public enum StoreRowState
{
	Get,
	Downloading,
	Downloaded,
	Failed,
}

/// <summary>What one store row binds to (mock screen 21): the entry's labels and its Get / progress / Downloaded state.</summary>
public sealed class StoreRow(StoreItem item, bool onShelf) : INotifyPropertyChanged
{
	private const double PillWidth = 88;
	private const int PercentScale = 100;

	private StoreRowState _state = onShelf ? StoreRowState.Downloaded : StoreRowState.Get;
	private double _progress;
	private ImageSource? _cover;

	public event PropertyChangedEventHandler? PropertyChanged;

	public StoreItem Item { get; } = item;

	public string Title => Item.Title;

	/// <summary>Titles in Farsi and Arabic are set in Vazirmatn and run right to left, as the mock's Farsi row does.</summary>
	public string TitleFont => DinoFonts.IsRightToLeft(Item.Language) ? DinoFonts.Persian : DinoFonts.Ui;

	/// <summary>The language in its own name, then the length on the next line (omitted when the server gave none).</summary>
	public string Detail => StoreText.Detail(Item);

	public string GetHint => StoreText.GetHint(Item.Title);

	public string ProgressHint => StoreText.ProgressHint(Item.Title, ProgressText);

	/// <summary>The Get pill also stands for a failed download, labelled "Try again".</summary>
	public bool IsGet => _state is StoreRowState.Get or StoreRowState.Failed;

	public string GetLabel => _state == StoreRowState.Failed ? AppResources.Store_TryAgain : AppResources.Store_Get;

	public bool IsDownloading => _state == StoreRowState.Downloading;

	public bool IsDownloaded => _state == StoreRowState.Downloaded;

	public double FillWidth => PillWidth * _progress;

	public string ProgressText => string.Create(CultureInfo.CurrentCulture, $"{(int)(_progress * PercentScale)}%");

	public ImageSource? Cover
	{
		get => _cover;
		set => Set(ref _cover, value, nameof(Cover));
	}

	public StoreRowState State => _state;

	public void SetState(StoreRowState state)
	{
		_state = state;
		_progress = 0;
		Raise(nameof(IsGet), nameof(GetLabel), nameof(IsDownloading), nameof(IsDownloaded), nameof(FillWidth), nameof(ProgressText), nameof(ProgressHint));
	}

	public void SetProgress(double progress)
	{
		_progress = Math.Clamp(progress, 0, 1);
		Raise(nameof(FillWidth), nameof(ProgressText), nameof(ProgressHint));
	}

	private void Set<T>(ref T field, T value, string name)
	{
		field = value;
		Raise(name);
	}

	private void Raise(params string[] names)
	{
		foreach (string name in names)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}
}

/// <summary>The store's copy that is composed from numbers or names, in the UI language.</summary>
public static class StoreText
{
	public static string Detail(StoreItem item)
	{
		string language = LanguageNames.All.Any(l => l.Code == item.Language) ? LanguageNames.NativeName(item.Language) : item.Language;
		return item.PageCount is { } pages && item.Minutes is { } minutes
			? language + Environment.NewLine + AppResources.Format(AppResources.Store_Length, pages, minutes)
			: language;
	}

	public static string GetHint(string title) => AppResources.Format(AppResources.Store_GetStory, title);

	public static string ProgressHint(string title, string percent) => title + " " + percent;
}

/// <summary>
/// Free stories (Slice 92, mock screens 21-23), behind the grown-up gate: lists what <see cref="StoreClient"/> finds, and a
/// Get downloads one into the library through Slice 91's importer. Offline and empty are calm states of their own. Leaving the
/// page does not stop a download: it finishes into the library, and only a real failure ends it (the row then offers Try again).
/// </summary>
public partial class StorePage : ContentPage, IGrownUpPage
{
	private readonly StoreClient _client;
	private readonly GrownUpMode _mode;
	private const int CoverParallelism = 4;

	private readonly BusyGuard _busy = new();
	private readonly ObservableCollection<StoreRow> _rows = [];
	private CancellationTokenSource _lifetime = new();
	private bool _loaded;

	public StorePage(StoreClient client, GrownUpMode mode)
	{
		InitializeComponent();
		_client = client;
		_mode = mode;
		StoryList.ItemsSource = _rows;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_lifetime.IsCancellationRequested)
		{
			_lifetime = new CancellationTokenSource();
		}

		if (!_loaded)
		{
			_loaded = await LoadAsync();
		}
		else
		{
			// Leaving the page may have cut the cover fetches short: pick the missing ones up again.
			_ = LoadCoversAsync(_rows.Where(r => r.Cover is null).ToList(), _lifetime.Token);
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_lifetime.Cancel();
	}

	/// <summary>Loads and shows the list; false when the page was left first, so the next appearance loads again. Runs from async void
	/// handlers, so any other failure ends on the calm offline screen instead of crashing the app.</summary>
	private async Task<bool> LoadAsync()
	{
		CancellationToken cancel = _lifetime.Token;
		Show(LoadingView);
		try
		{
			StoreListing listing = await _client.ListAsync(cancel);
			if (listing.Status == StoreLoadStatus.Offline)
			{
				Show(OfflineView);
				return true;
			}

			await ShowItemsAsync(listing.Items, cancel);
			return true;
		}
		catch (OperationCanceledException)
		{
			return false;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader store load failed: {ex}");
			Show(OfflineView);
			return true;
		}
	}

	private async Task ShowItemsAsync(IReadOnlyList<StoreItem> items, CancellationToken cancel)
	{
		HashSet<string> onShelf = await _client.InstalledIdsAsync();
		_rows.Clear();
		foreach (StoreItem item in items)
		{
			_rows.Add(new StoreRow(item, onShelf.Contains(item.Id)));
		}

		Show(_rows.Count == 0 ? EmptyView : ListPanel);
		_ = LoadCoversAsync(_rows.ToList(), cancel);
	}

	// Covers arrive a few at a time after the list is on screen; a cover that cannot be fetched leaves the plain placeholder.
	private async Task LoadCoversAsync(IReadOnlyList<StoreRow> rows, CancellationToken cancel)
	{
		using var gate = new SemaphoreSlim(CoverParallelism);
		await Task.WhenAll(rows.Select(row => LoadCoverAsync(row, gate, cancel)));
	}

	private async Task LoadCoverAsync(StoreRow row, SemaphoreSlim gate, CancellationToken cancel)
	{
		try
		{
			await gate.WaitAsync(cancel);
			try
			{
				string? path = await _client.CoverPathAsync(row.Item, cancel);
				row.Cover = path is null ? null : ImageSource.FromFile(path);
			}
			finally
			{
				gate.Release();
			}
		}
		catch (OperationCanceledException)
		{
			// The page was left: the next appearance picks up the rest.
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader store cover failed: {ex.Message}");
		}
	}

	private void Show(View visible)
	{
		foreach (View view in new View[] { LoadingView, ListPanel, OfflineView, EmptyView })
		{
			view.IsVisible = view == visible;
		}
	}

	private async void OnGetTapped(object? sender, TappedEventArgs e)
	{
		if ((sender as BindableObject)?.BindingContext is StoreRow { State: StoreRowState.Get or StoreRowState.Failed } row && _mode.IsGrownUp)
		{
			await GetAsync(row);
		}
	}

	private async Task GetAsync(StoreRow row)
	{
		row.SetState(StoreRowState.Downloading);
		DownloadOutcome outcome = DownloadOutcome.Failed;
		try
		{
			outcome = await _client.DownloadAsync(row.Item, new Progress<double>(row.SetProgress), CancellationToken.None);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader store download failed: {ex}");
		}
		finally
		{
			// Only a finished download shows Downloaded; a failed one offers Try again.
			row.SetState(outcome == DownloadOutcome.Done ? StoreRowState.Downloaded : StoreRowState.Failed);
		}
	}

	private async void OnTryAgainClicked(object? sender, EventArgs e) => await _busy.RunAsync(LoadAsync);

	private async void OnBackTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(() => Navigation.PopAsync());

	private async void OnBackToStoriesClicked(object? sender, EventArgs e) => await _busy.RunAsync(() => Navigation.PopAsync());
}
