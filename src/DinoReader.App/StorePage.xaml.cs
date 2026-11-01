using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

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

	/// <summary>The title in the app language (Slice 159): the listing's <c>titles</c>, else the shipped-titles table, else the listing's own title.</summary>
	public string Title => ShippedTitles.ShownInStore(Item, LanguageChoice.Current());

	/// <summary>Titles in Farsi and Arabic are set in Vazirmatn and run right to left, as the mock's Farsi row does.</summary>
	public string TitleFont => TextDirection.StartsRightToLeft(Title) ? DinoFonts.Persian : DinoFonts.Ui;

	/// <summary>The detail line, "Ages 5–8 · 14 pages · 7 min": the age range leads the length (each part is left out when the server gave none).</summary>
	public string Detail => StoreText.Detail(Item);

	/// <summary>The sub-line under the title: the credit line in the app language (Slice 192: <c>creditsByLanguage</c>, else <c>credits</c>), or the language in its own name when the story has none.</summary>
	public string Credits => Item.CreditsIn(LanguageChoice.Current()) ?? StoreText.Language(Item);

	public bool HasDetail => Detail.Length > 0;
	public bool HasCredits => Credits.Length > 0;

	public string GetHint => StoreText.GetHint(Title);

	public string ProgressHint => StoreText.ProgressHint(Title, ProgressText);

	/// <summary>The Get pill also stands for a failed download, labelled "Try again".</summary>
	public bool IsGet => _state is StoreRowState.Get or StoreRowState.Failed;

	public string GetLabel => _state == StoreRowState.Failed ? AppResources.Store_TryAgain : AppResources.Store_Get;

	public bool IsDownloading => _state == StoreRowState.Downloading;

	public bool IsDownloaded => _state == StoreRowState.Downloaded;

	public double FillWidth => PillWidth * _progress;

	public string ProgressText => string.Create(AppResources.Culture, $"{(int)(_progress * PercentScale)}%");

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
	public static string Language(StoreItem item) =>
		LanguageNames.All.Any(l => l.Code == item.Language) ? LanguageNames.NativeName(item.Language) : item.Language;

	private const string DetailSeparator = " · ";

	public static string Detail(StoreItem item) =>
		string.Join(DetailSeparator, new[] { Ages(item), Length(item) }.Where(part => part.Length > 0));

	public static string Ages(StoreItem item) =>
		item.AgeMin is { } min && item.AgeMax is { } max ? AgesOf(min, max) : string.Empty;

	private static string AgesOf(int min, int max) =>
		min == max ? AppResources.Format(AppResources.Store_AgeSingle, min) : AppResources.Format(AppResources.Store_Ages, min, max);

	public static string Length(StoreItem item) =>
		item.PageCount is { } pages && item.Minutes is { } minutes
			? AppResources.Format(AppResources.Store_Length, pages, minutes)
			: string.Empty;

	public static string BandLabel(StoreAgeBand band) => band switch
	{
		StoreAgeBand.UpTo5 => AppResources.Store_AgeUpTo5,
		StoreAgeBand.From6To8 => AppResources.Store_Age6To8,
		StoreAgeBand.From9To12 => AppResources.Store_Age9To12,
		_ => AppResources.Store_AllAges,
	};

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
	private readonly UsageCounter _usage;
	private const int CoverParallelism = 4;
	private const double ChipHeight = 40;
	private const double ChipFontSize = 14;
	private const double ChipPaddingX = 16;
	private const string BrandColorKey = "Brand";
	private const string BodyColorKey = "Body";

	private readonly BusyGuard _busy = new();
	private readonly ObservableCollection<StoreRow> _rows = [];
	private readonly List<StoreRow> _allRows = [];
	private StoreAgeBand _band = StoreAgeBand.All;
	private string? _author;
	private readonly Dictionary<StoreAgeBand, (Border Chip, Label Label)> _chips = [];
	private CancellationTokenSource _lifetime = new();
	private bool _loaded;

	public StorePage(StoreClient client, GrownUpMode mode, UsageCounter usage)
	{
		InitializeComponent();
		_client = client;
		_mode = mode;
		_usage = usage;
		StoryList.ItemsSource = _rows;
		BuildChips();
		ShowAuthor();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		RefreshChips();
		ShowAuthor();
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
			_ = LoadCoversAsync(_allRows.Where(r => r.Cover is null).ToList(), _lifetime.Token);
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
		_allRows.Clear();
		_allRows.AddRange(items.Select(item => new StoreRow(item, onShelf.Contains(item.Id))));
		if (_author is not null && !AuthorsInBand().Any(a => StoreFilter.SameAuthor(a, _author)))
		{
			_author = null;
		}

		ApplyFilter();
		ShowAuthor();
		Show(_allRows.Count == 0 ? EmptyView : ListPanel);
		_ = LoadCoversAsync(_allRows.ToList(), cancel);
	}

	// Slice 112: filtering is on the phone, over the rows already fetched (so it works on the cached list offline); rows keep their download state.
	private void ApplyFilter()
	{
		_rows.Clear();
		foreach (StoreRow row in StoreFilter.Apply(_allRows, r => r.Item, _band, _author))
		{
			_rows.Add(row);
		}

		// A filter that matches nothing says so, rather than leaving a blank list.
		NoMatchLabel.IsVisible = _allRows.Count > 0 && _rows.Count == 0;
	}

	// The four chips are built once; a tap only recolours them, so the tapped chip is never torn down inside its own handler.
	private void BuildChips()
	{
		AgeChips.Children.Clear();
		foreach (StoreAgeBand band in Enum.GetValues<StoreAgeBand>())
		{
			AgeChips.Children.Add(MakeChip(band));
		}

		RefreshChips();
	}

	private Border MakeChip(StoreAgeBand band)
	{
		var label = new Label { FontSize = ChipFontSize, VerticalOptions = LayoutOptions.Center };
		var chip = new Border
		{
			HeightRequest = ChipHeight,
			Padding = new Thickness(ChipPaddingX, 0),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = ChipHeight / 2 },
			Content = label,
		};
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => SelectBand(band);
		chip.GestureRecognizers.Add(tap);
		_chips[band] = (chip, label);
		return chip;
	}

	private void RefreshChips()
	{
		foreach ((StoreAgeBand band, (Border chip, Label label)) in _chips)
		{
			bool selected = band == _band;
			string text = StoreText.BandLabel(band);
			chip.BackgroundColor = selected ? (Color)Application.Current!.Resources[BrandColorKey] : Colors.White;
			label.Text = text;
			label.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
			label.TextColor = selected ? Colors.White : (Color)Application.Current!.Resources[BodyColorKey];
			SemanticProperties.SetDescription(chip, selected ? AppResources.Format(AppResources.Store_ChipSelected, text) : text);
		}
	}

	private void SelectBand(StoreAgeBand band)
	{
		if (band == _band)
		{
			return;
		}

		_band = band;
		if (_author is not null && !AuthorsInBand().Any(a => StoreFilter.SameAuthor(a, _author)))
		{
			_author = null;
		}

		ShowAuthor();
		RefreshChips();
		ApplyFilter();
	}

	// The picker offers only authors with a story in the chosen age band, so a pick never empties the list.
	private IReadOnlyList<string> AuthorsInBand() =>
		StoreFilter.Authors(StoreFilter.Apply(_allRows, r => r.Item, _band, null).Select(r => r.Item));

	private void ShowAuthor()
	{
		AuthorValue.Text = _author ?? AppResources.Store_Anyone;
		AuthorRow.IsVisible = AuthorsInBand().Count > 0;
	}

	private async void OnAuthorTapped(object? sender, TappedEventArgs e)
	{
		// Each button label maps to its author (null for "Anyone"); an author named like a fixed button gets a visible number so the pick is never misread.
		var choices = new Dictionary<string, string?> { [AppResources.Store_Anyone] = null };
		foreach (string author in AuthorsInBand())
		{
			choices[UniqueLabel(author, choices)] = author;
		}

		string? picked = await DisplayActionSheetAsync(AppResources.Store_Author, AppResources.Common_Cancel, null, [.. choices.Keys]);
		if (picked is null || !choices.TryGetValue(picked, out string? chosen))
		{
			return;
		}

		_author = chosen;
		ShowAuthor();
		ApplyFilter();
	}

	private static string UniqueLabel(string author, Dictionary<string, string?> taken)
	{
		return ChoiceLabels.Unique(author, taken.Keys, AppResources.Common_Cancel);
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
		// The filters in use when Get was tapped, not when the download ends.
		bool ageFilterSet = _band != StoreAgeBand.All;
		bool authorFilterSet = _author is not null;
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
			// Only a finished download shows Downloaded and is counted (Slice 134: which filters were set, not their values); a failed one offers Try again.
			if (outcome == DownloadOutcome.Done)
			{
				_usage.RecordStoreDownload(row.Item, ageFilterSet, authorFilterSet);
			}

			row.SetState(outcome == DownloadOutcome.Done ? StoreRowState.Downloaded : StoreRowState.Failed);
		}
	}

	private async void OnTryAgainClicked(object? sender, EventArgs e) => await _busy.RunAsync(LoadAsync);

	/// <summary>Set by the intro wizard when it pushes this page: the header button then reads "Continue" (Slice 229). Back and Continue both pop; a download in progress keeps going.</summary>
	public bool LaunchedFromIntro
	{
		set => Header.Continue = value;
	}

	private async void OnBackTapped(object? sender, EventArgs e) => await _busy.RunAsync(() => Navigation.PopAsync());

	private async void OnBackToStoriesClicked(object? sender, EventArgs e) => await _busy.RunAsync(() => Navigation.PopAsync());
}
