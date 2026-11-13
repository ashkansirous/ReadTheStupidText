using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Net;
using ReadTheStupidText.Application.Usage;
using WaitStage = ReadTheStupidText.Application.Reading.WaitStage;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>Where one store entry stands: on offer, being fetched, or already on the shelf.</summary>
public enum StoreRowState
{
	Get,
	Downloading,
	Downloaded,
	Failed,

	/// <summary>Slice 346: a sold book the store account does not own; the pill is Buy with the store's price.</summary>
	Buy,

	/// <summary>Slice 346: a sold book whose price is not known because the store has not answered (offline): a calm sand strip in the card, no button.</summary>
	ConnectToBuy,

	/// <summary>Slice 358: a sold book the store answered about without a price: a red strip in the card, no button.</summary>
	NotForSaleNow,

	/// <summary>Slice 346: a purchase waiting for approval.</summary>
	Waiting,
}

/// <summary>What one store row binds to (mock frame T2): the entry's labels and its Get / progress / On the shelf state.</summary>
public sealed class StoreRow(StoreItem item, bool onShelf) : INotifyPropertyChanged
{
	private const int PercentScale = 100;

	private StoreRowState _state = onShelf ? StoreRowState.Downloaded : StoreRowState.Get;
	private double _progress;
	private ImageSource? _cover;
	private string _price = string.Empty;
	private HttpError? _error;

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

	/// <summary>The tag badges above the title (Slice 260): at most two, in list order; none for an untagged book or an older Worker.</summary>
	public IReadOnlyList<StoreTag> Badges => Item.Badges;

	/// <summary>The line under the credits on a card (frame T2): the length, "12 pages · 6 min"; empty when the server gave none.</summary>
	public string Meta => StoreText.Length(Item);

	public bool HasDetail => Detail.Length > 0;
	public bool HasMeta => Meta.Length > 0;
	public bool HasCredits => Credits.Length > 0;

	public string GetHint => _state == StoreRowState.Buy ? AppResources.Format(AppResources.Store_BuyStory, Title, _price) : StoreText.GetHint(Title);

	public string ProgressHint => StoreText.ProgressHint(Title, ProgressText);

	/// <summary>Slice 364 (mock S1): the brand Get pill, for a book that can be fetched now.</summary>
	public bool IsGet => _state == StoreRowState.Get;

	/// <summary>The sold book's button: the store's own price with a cart glyph (mock S1b); it stays under a 402 strip.</summary>
	public bool IsBuy => _state == StoreRowState.Buy;

	/// <summary>The tinted Try again button at the end of a failed row (mock S2); a book the store no longer has gets none (mock S3).</summary>
	public bool IsTryAgain => _state == StoreRowState.Failed && _error?.Kind != HttpErrorKind.NotFound;

	/// <summary>The green Free tag after the length (mock S1): a book that is not sold, whether or not it is on the shelf yet.</summary>
	public bool IsFree => Item.Sale == BookSale.Free;

	public string GetLabel => AppResources.Store_Get;

	public string TryAgainLabel => AppResources.Store_TryAgain;

	/// <summary>A sold book with nothing to press because its purchase waits for approval: one calm line at the end of the row.</summary>
	public bool HasNote => _state == StoreRowState.Waiting;

	public string NoteText => AppResources.Store_Waiting;

	/// <summary>Slice 358 (mock S3): the store did not answer, so a sold book has no price; the sand strip with the cloud glyph. A server error strip, when there is one, says it instead.</summary>
	public bool HasSandNote => _state == StoreRowState.ConnectToBuy && !HasError;

	/// <summary>Slice 358 (mock S2): the store answered without a price for a sold book; the red strip with the alert glyph, no button.</summary>
	public bool HasRedNote => _state == StoreRowState.NotForSaleNow && !HasError;

	public bool IsDownloading => _state == StoreRowState.Downloading;

	public bool IsOnShelf => _state == StoreRowState.Downloaded;

	/// <summary>0 to 1, for the ring (mock S1c).</summary>
	public double Progress => _progress;

	public string ProgressText => string.Create(AppResources.Culture, $"{(int)(_progress * PercentScale)}%");

	public ImageSource? Cover
	{
		get => _cover;
		set => Set(ref _cover, value, nameof(Cover));
	}

	public StoreRowState State => _state;

	/// <summary>Slice 357: why the last Get failed, shown under the row while the row offers a button again; cleared when a download starts or the book is on the shelf.</summary>
	public HttpError? Error => _error;

	public bool HasError => _error is not null && _state is StoreRowState.Get or StoreRowState.Failed or StoreRowState.Buy or StoreRowState.ConnectToBuy or StoreRowState.NotForSaleNow;

	public string ErrorText => _error is null ? string.Empty : StoreText.ErrorMessage(_error);

	/// <summary>Mock S3: no answer at all is the calm sand strip; every server answer is the red one.</summary>
	public bool HasCalmError => HasError && _error?.Kind == HttpErrorKind.Offline;

	public bool HasRedError => HasError && _error?.Kind != HttpErrorKind.Offline;

	/// <summary>The error is part of the state: a call that ends the row in a failed or error-carrying state passes it, every other call passes none and so clears it (Slice 357 review).</summary>
	public void SetState(StoreRowState state, HttpError? error = null)
	{
		_state = state;
		_progress = 0;
		_error = error;

		Raise(nameof(Error), nameof(HasError), nameof(HasCalmError), nameof(HasRedError), nameof(ErrorText), nameof(IsGet), nameof(IsBuy), nameof(IsTryAgain), nameof(IsFree), nameof(GetLabel), nameof(Price), nameof(GetHint), nameof(HasNote), nameof(NoteText), nameof(HasSandNote), nameof(HasRedNote), nameof(IsDownloading), nameof(IsOnShelf), nameof(Progress), nameof(ProgressText), nameof(ProgressHint));
	}

	/// <summary>Completes when the row is no longer downloading (at once when it is not), or when <paramref name="cancel"/> is cancelled; never throws, the caller reads the state afterwards (Slice 364 restore).</summary>
	public Task WhenNotDownloadingAsync(CancellationToken cancel)
	{
		if (!IsDownloading || cancel.IsCancellationRequested)
		{
			return Task.CompletedTask;
		}

		var done = new TaskCompletionSource();
		PropertyChangedEventHandler? handler = null;
		CancellationTokenRegistration registration = cancel.Register(() =>
		{
			PropertyChanged -= handler;
			done.TrySetResult();
		});
		handler = (_, _) =>
		{
			if (IsDownloading)
			{
				return;
			}

			PropertyChanged -= handler;
			registration.Dispose();
			done.TrySetResult();
		};
		PropertyChanged += handler;
		return done.Task;
	}

	/// <summary>The store's own formatted price for a sold book (Slice 346); empty otherwise.</summary>
	public string Price => _price;

	/// <summary>Shows what <see cref="BookAccess"/> decided for this row. A download that is running or has just failed is never overwritten.</summary>
	public void Apply(StoreCardPlan plan)
	{
		if (_state == StoreRowState.Downloading || (_state == StoreRowState.Failed && plan.Action != StoreAction.OnShelf))
		{
			return;
		}

		_price = plan.Price ?? string.Empty;
		SetState(plan.Action switch
		{
			StoreAction.OnShelf => StoreRowState.Downloaded,
			StoreAction.Buy => StoreRowState.Buy,
			StoreAction.ConnectToBuy => StoreRowState.ConnectToBuy,
			StoreAction.NotForSaleNow => StoreRowState.NotForSaleNow,
			StoreAction.Waiting => StoreRowState.Waiting,
			_ => StoreRowState.Get,
		});
#if DEBUG
		if (_state is StoreRowState.ConnectToBuy or StoreRowState.NotForSaleNow)
		{
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[StoreSale] row state={_state}");
		}
#endif
	}

	public void SetProgress(double progress)
	{
		_progress = Math.Clamp(progress, 0, 1);
		Raise(nameof(Progress), nameof(ProgressText), nameof(ProgressHint));
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

	/// <summary>
	/// The text for a failed server call (Slice 357), in the UI language: one translated line per status kind, "You're offline" for no answer, and for a 400 the server's own English
	/// sentence as it is (the general server text when it sent none). Every 5xx and any other 4xx show the general text.
	/// </summary>
	public static string ErrorMessage(HttpError error) => error.Kind switch
	{
		HttpErrorKind.Offline => AppResources.Store_OfflineTitle,
		HttpErrorKind.BadRequest => error.Message ?? AppResources.Error_Server,
		HttpErrorKind.NotFound => AppResources.Error_BookGone,
		HttpErrorKind.PaymentRequired => AppResources.Error_PaymentRequired,
		HttpErrorKind.TooManyRequests => AppResources.Error_TooManyTries,
		_ => AppResources.Error_Server, // Slice 364 (mock S2): every 5xx and any other 4xx (401, 403 too) say the server had a problem
	};

	public static string ProgressHint(string title, string percent) => title + " " + percent;

	/// <summary>The "Bought books before?" card's account line (Slice 364, mock S3): which account brings the books back, in the words for a phone, a tablet or a computer.</summary>
	public static string BoughtBeforeLine(DevicePlatform platform, DeviceIdiom idiom)
	{
		bool phone = idiom == DeviceIdiom.Phone;
		if (platform == DevicePlatform.iOS || platform == DevicePlatform.MacCatalyst)
		{
			return phone ? AppResources.Store_BoughtBefore_Apple : AppResources.Store_BoughtBefore_Apple_Tablet;
		}

		if (platform == DevicePlatform.WinUI)
		{
			return AppResources.Store_BoughtBefore_Microsoft;
		}

		return phone ? AppResources.Store_BoughtBefore_Google : AppResources.Store_BoughtBefore_Google_Tablet;
	}
}

/// <summary>
/// The corner's Store tab (Slice 92, mock screens 21-23; a tab of the grown-ups' corner since Slice 325 part B, frame T2): lists what <see cref="StoreClient"/> finds, and a
/// Get downloads one into the library through Slice 91's importer. Offline and empty are calm states of their own. Leaving the
/// tab does not stop a download: it finishes into the library, and only a real failure ends it (the row then offers Try again). The gallery keeps this one view for the corner's life, so
/// the rows, the filter and a running download survive a switch to another tab.
/// </summary>
public partial class StorePage : ContentView, ICornerTabView
{
	private readonly StoreClient _client;
	private readonly GrownUpMode _mode;
	private readonly UsageCounter _usage;
	private const int CoverParallelism = 4;
	private const string WifiOffGlyphKey = "WifiOffGlyph";
	private const string ServerErrorGlyphKey = "ServerErrorGlyph";
	private int _shownColumns;
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
	private CancellationTokenSource? _restoreRun; // the one restore run of this visit: a new run or Disappearing cancels it, and a cancelled run never shows a result line (Slice 364 review)
	private bool _purchaseAreaInFooter;
	private bool _loaded;
	private bool _loading;

	public StorePage(StoreClient client, GrownUpMode mode, UsageCounter usage, ExternalActivityGuard external)
	{
		InitializeComponent();
		_external = external;
		_client = client;
		_mode = mode;
		_usage = usage;
		StoryList.ItemsSource = _rows;
		BuildChips();
		ShowAuthor();
		WaitOverlay overlay = DinoWait.OverPage(); // Slice 346: a purchase or a restore waits for the store; over 500 ms shows the shared wait overlay
		StoreRoot.Children.Add(overlay);
		_wait = new WaitController(overlay);
		RestoreButton.AutomationId = "StoreRestorePurchases";
		RedeemButton.AutomationId = "StoreRedeemCode";
		HandlerChanged += OnHandlerChanged;
	}

	// Check2 #7: the page is destroyed (its handler goes away): the long-lived BookPurchases must not keep it alive. Appearing subscribes again if it is ever shown again.
	private void OnHandlerChanged(object? sender, EventArgs e)
	{
		if (Handler is null)
		{
			_onScreen = false;
			_client.Sales.Purchases.Changed -= OnPurchasesChanged;
		}
	}

	private readonly WaitController _wait;
	private readonly ExternalActivityGuard _external;

	// The store said something new (a book bought or redeemed outside the app, a pending purchase approved): every row follows it.
	// Check8 #7: a change that comes while the tab is hidden (also after a failed first load) is only remembered; the next Appearing replans, so no row keeps a stale Buy pill for an owned book.
	private void OnPurchasesChanged(object? sender, EventArgs e)
	{
		if (!_onScreen)
		{
			_purchasesChangedWhileHidden = true;
			return;
		}

		MainThread.BeginInvokeOnMainThread(() => _ = ReplanRowsAsync());
	}

	private bool _onScreen;
	private bool _purchasesChangedWhileHidden;

	private async Task ReplanRowsAsync()
	{
		try
		{
			HashSet<string> onShelf = await _client.InstalledIdsAsync();
			foreach (StoreRow row in _allRows.ToList())
			{
				row.Apply(_client.Sales.PlanFor(row.Item, onShelf.Contains(row.Item.Id)));
			}

			ApplyPurchaseChrome();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader store replan failed: {ex}");
			_client.Fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed); // the rows keep what they show until the next visit
		}
	}

	// Restore and Redeem show only while books are sold; the footer line then says purchases go through the store account.
	private void ApplyPurchaseChrome()
	{
		bool sold = _client.Sales.SwitchKnownOn;
		BoughtBeforeCard.IsVisible = sold;
		BoughtBeforeTitle.Text = AppResources.Store_BoughtBeforeTitle;
		BoughtBeforeLine.Text = StoreText.BoughtBeforeLine(DeviceInfo.Platform, DeviceInfo.Idiom);
		FooterLabel.Text = sold ? AppResources.Store_FooterPaid : AppResources.Store_Footer;
		PlacePurchaseArea();
	}

	/// <summary>The Store tab is on screen: the list loads the first time; later it only picks up the covers a tab switch cut short. A download that was running is not touched.</summary>
	public async void Appearing()
	{
		_onScreen = true;
		_client.Sales.Purchases.Changed -= OnPurchasesChanged; // never twice, whatever the host raises
		_client.Sales.Purchases.Changed += OnPurchasesChanged;
		RefreshChips();
		ShowAuthor();
		_client.Sales.RefreshConfigOnStoreTab(); // quiet, never awaited, at most once per 10 minutes
		if (_lifetime.IsCancellationRequested)
		{
			_lifetime = new CancellationTokenSource();
		}

		if (_loading)
		{
			return; // the first load is still running: a second appearance must not start another one over the same rows (check2 #6: the hidden-change flag stays set for the next appearance)
		}

		bool changedWhileHidden = _purchasesChangedWhileHidden;
		_purchasesChangedWhileHidden = false;
		if (!_loaded)
		{
			_loading = true;
			try
			{
				_loaded = await LoadAsync();
			}
			finally
			{
				_loading = false;
			}

			if (_loaded && (_client.Sales.SwitchKnownOn || changedWhileHidden))
			{
				await ReplanRowsAsync(); // a purchase change that came while the list loaded (or while the tab was hidden) shows on the first rows too
			}
		}
		else
		{
			// Leaving the page may have cut the cover fetches short: pick the missing ones up again.
			_ = LoadCoversAsync(_allRows.Where(r => r.Cover is null).ToList(), _lifetime.Token);
			await RefreshOnShelfAsync();
			if (_client.Sales.SwitchKnownOn)
			{
				await RefreshPurchasesAsync(_lifetime.Token); // a book bought or redeemed elsewhere shows as owned without Restore (raises Changed)
			}

			await ReplanRowsAsync();
		}
	}

	// The tab view outlives a visit, so a book removed in Books meanwhile must show its Get pill again: every row that is not downloading follows the library.
	private async Task RefreshOnShelfAsync()
	{
		Dictionary<StoreRow, StoreRowState> before = _allRows.ToDictionary(r => r, r => r.State);
		HashSet<string> onShelf;
		try
		{
			onShelf = await _client.InstalledIdsAsync();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader store shelf refresh failed: {ex}");
			_client.Fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed); // the rows keep their last state until the next visit
			return;
		}

		foreach (StoreRow row in _allRows.ToList())
		{
			// A download that ran or finished while the shelf was read is newer than the snapshot: leave it be.
			if (row.IsDownloading || !before.TryGetValue(row, out StoreRowState was) || was != row.State)
			{
				continue;
			}

			row.Apply(_client.Sales.PlanFor(row.Item, onShelf.Contains(row.Item.Id))); // Get, On the shelf, Buy or a note, by the shelf and the store's answer (a failed download keeps Try again)
		}
	}

	/// <summary>The tab went off screen (another tab, the shelf): cover fetches stop. A download keeps running into the library and only a real failure ends it.</summary>
	public void Disappearing()
	{
		RestoreResultView.IsVisible = false; // a restore line is for the visit it ran in
		_restoreRun?.Cancel(); // a running restore stops fetching and shows no line (its downloads may still finish, as a row's do)
		_onScreen = false; // the subscription stays: a change while hidden is remembered (check8 #7)
		_lifetime.Cancel();
	}

	// Decision 4 (Slice 325 part B): one column of cards, two from 800 dp of content. The gallery passes the column's width (the window less the rail), a pure function of the window, so a
	// rotation changes the grid's span only: the rows, the filter, the open download and the scroll position are never rebuilt.
	public void ApplyShape(double contentWidth)
	{
		int columns = SettingsLayout.StoreColumns(contentWidth);
		if (columns == _shownColumns)
		{
			return;
		}

		_shownColumns = columns;
		((GridItemsLayout)StoryList.ItemsLayout).Span = columns;
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] store columns={columns} rows={_rows.Count}");
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Redesign] store columns={columns} content={contentWidth:0} rows={_rows.Count}");
#endif
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
				ShowOffline(listing.Error);
				return true;
			}

			await ShowItemsAsync(listing.Items, cancel);
			return true;
		}
		catch (OperationCanceledException)
		{
			// no-fallback: the page was left or the load was replaced by a newer one
			return false;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader store load failed: {ex}");
			_client.Fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed); // Slice 275: an unexpected failure (not the network) shows the offline view
			ShowOffline(null);
			return true;
		}
	}

	// Slice 357: the offline panel for no answer, or for no known reason; when the server did answer with an error, the same panel carries that error's own text instead of "You're offline" (Try again stays).
	private void ShowOffline(HttpError? error)
	{
		ApplyPurchaseChrome(); // Restore and Redeem stay reachable on the offline screen too
		bool serverAnswered = error is { Kind: not HttpErrorKind.Offline };
		// A server answer is not "you're offline": it has its own icon and title, and the error's text (the translated line, or a 400's own sentence) is the body.
		OfflineTitleLabel.Text = serverAnswered ? AppResources.Store_ServerErrorTitle : AppResources.Store_OfflineTitle;
		OfflineBodyLabel.Text = serverAnswered ? StoreText.ErrorMessage(error!) : AppResources.Store_OfflineBody;
		string glyphKey = serverAnswered ? ServerErrorGlyphKey : WifiOffGlyphKey;
		OfflineIcon.Data = (Microsoft.Maui.Controls.Shapes.Geometry)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString((string)Resources[glyphKey])!;
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[StoreError] list kind={error?.Kind.ToString() ?? "none"}");
#endif
		Show(OfflineView);
	}

	private async Task ShowItemsAsync(IReadOnlyList<StoreItem> items, CancellationToken cancel)
	{
		HashSet<string> onShelf = await _client.InstalledIdsAsync();
		if (_client.Sales.SwitchKnownOn)
		{
			await _client.Sales.Purchases.RefreshAsync(items, cancel, interactive: true); // the grown-up opened the store page: the store may be asked in full (iOS replays the account's purchases once per run)
		}

		_allRows.Clear();
		_allRows.AddRange(items.Select(item => NewRow(item, onShelf)));
		ApplyPurchaseChrome();
		if (_author is not null && !AuthorsInBand().Any(a => StoreFilter.SameAuthor(a, _author)))
		{
			_author = null;
		}

		ApplyFilter();
		ShowAuthor();
		Show(_allRows.Count == 0 ? EmptyView : ListPanel);
		_ = LoadCoversAsync(_allRows.ToList(), cancel);
	}

	private StoreRow NewRow(StoreItem item, HashSet<string> onShelf)
	{
		var row = new StoreRow(item, onShelf.Contains(item.Id));
		row.Apply(_client.Sales.PlanFor(item, onShelf.Contains(item.Id)));
		return row;
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
		PlacePurchaseArea();
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
		tap.Tapped += (_, _) =>
		{
			UiUsage.Tap(UiControls.Filter); // Slice 352: an age chip (which age is never counted)
			SelectBand(band);
		};
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
		UiUsage.Tap(UiControls.Filter); // Slice 352: the author filter (which author is never counted)
		// Each button label maps to its author (null for "Anyone"); an author named like a fixed button gets a visible number so the pick is never misread.
		var choices = new Dictionary<string, string?> { [AppResources.Store_Anyone] = null };
		foreach (string author in AuthorsInBand())
		{
			choices[UniqueLabel(author, choices)] = author;
		}

		if (Window?.Page is not { } page)
		{
			return; // the tab is not on a window (it was torn down)
		}

		string? picked = await page.DisplayActionSheetAsync(AppResources.Store_Author, AppResources.Common_Cancel, null, [.. choices.Keys]);
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
			// no-fallback: the page was left; the next appearance picks up the rest.
		}
		catch (Exception ex)
		{
			_client.Fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed); // Slice 275: the cover stays a tinted block; the book itself is unaffected
			Debug.WriteLine($"Dino Reader store cover failed: {ex.Message}");
		}
	}

	private void Show(View visible)
	{
		foreach (View view in new View[] { LoadingView, ListPanel, OfflineView, EmptyView })
		{
			view.IsVisible = view == visible;
		}

		PlacePurchaseArea();
	}

	// Slice 364 review: the "Bought books before?" card is the foot of the list while the list shows books, and sits at the bottom of the page in every other state (empty, filtered to nothing, offline),
	// so Restore and Redeem are always reachable; it stays out of the way while the first load runs.
	private void PlacePurchaseArea()
	{
		bool inList = ListPanel.IsVisible && _rows.Count > 0;
		PurchaseArea.IsVisible = _client.Sales.SwitchKnownOn && !LoadingView.IsVisible;
		if (inList == _purchaseAreaInFooter)
		{
			return;
		}

		_purchaseAreaInFooter = inList;
		if (inList)
		{
			StoreRoot.Children.Remove(PurchaseArea);
			StoryList.Footer = PurchaseArea;
		}
		else
		{
			StoryList.Footer = null;
			StoreRoot.Add(PurchaseArea, column: 0, row: 1);
		}
	}

	private async void OnGetTapped(object? sender, TappedEventArgs e)
	{
		UiUsage.Tap(UiControls.Download); // Slice 352: the Get button of a store row
		if ((sender as BindableObject)?.BindingContext is not StoreRow row || !_mode.IsGrownUp)
		{
			return;
		}

		if (row.State == StoreRowState.Buy)
		{
			await _busy.RunAsync(() => BuyAsync(row));
		}
		else if (row.State is StoreRowState.Get or StoreRowState.Failed)
		{
			await GetAsync(row);
		}
	}

	// Slice 346: Buy opens the store's own sheet (the grown-up corner is behind the gate; mode is re-checked after each wait). Cancelled and failed change nothing; a bought book downloads as any other.
	private async Task BuyAsync(StoreRow row)
	{
		string? product = row.Item.Products.FirstOrDefault();
		if (product is null)
		{
			return;
		}

		RestoreResultView.IsVisible = false; // a buy changes what the last restore line counted
		BuyOutcome outcome = await _wait.RunAsync(WaitStage.GettingFile, AppResources.Wait_BuyingBook, canCancel: false,
			_ => _client.Sales.Purchases.BuyAsync(product, _allRows.Select(r => r.Item), CancellationToken.None));
		await ReplanRowsAsync();
		if (!_mode.IsGrownUp)
		{
			return;
		}

		if (outcome == BuyOutcome.Failed)
		{
			await AlertAsync(AppResources.Store_BuyFailed);
		}
		else if (outcome == BuyOutcome.Bought && row.State == StoreRowState.Get)
		{
			await GetAsync(row);
		}
	}

	private Task AlertAsync(string message) =>
		Window?.Page is { } page ? page.DisplayAlertAsync(AppResources.Store_Title, message, AppResources.Common_Ok) : Task.CompletedTask;

	private async void OnRestorePurchasesClicked(object? sender, EventArgs e)
	{
		UiUsage.Tap(UiControls.Restore); // Slice 352
		RestoreResultView.IsVisible = false; // the last restore's line never outlives the next try, even when the grown-up gate ends it early
		await _busy.RunAsync(RestorePurchasesAsync);
	}

	// One owner of a restore: the run's token is linked to this visit, a new run cancels the old one and Disappearing cancels it. The line appears only when the run finishes un-cancelled.
	private async Task RestorePurchasesAsync()
	{
		_restoreRun?.Cancel();
		_restoreRun?.Dispose();
		_restoreRun = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
		CancellationToken cancel = _restoreRun.Token;
		PurchaseSnapshot snapshot = await _wait.RunAsync(WaitStage.GettingFile, AppResources.Wait_CheckingPurchases, canCancel: false,
			_ => _client.Sales.Purchases.RestoreAsync(_allRows.Select(r => r.Item), CancellationToken.None));
		await ReplanRowsAsync();
		if (!_mode.IsGrownUp || cancel.IsCancellationRequested)
		{
			return;
		}

		if (!snapshot.Reachable)
		{
			ShowRestoreText(AppResources.Store_OfflineTitle, found: false); // the store could not be asked: the sand strip says so rather than "nothing found"
			return;
		}

		await PutRestoredBooksOnShelfAsync(cancel);
	}

	// Slice 364 (mock S3, "They're on the shelf"): every bought book that is not on the shelf is fetched, one at a time with its own progress (a Failed row too); a bought book already arriving is awaited
	// and counted like the fetched ones. A Get tap that lands while the run is going is awaited, never started twice.
	private async Task PutRestoredBooksOnShelfAsync(CancellationToken cancel)
	{
		List<StoreRow> bought = _allRows.Where(r => StoreRowRestore.NeedsFetch(_client.Sales.PlanFor(r.Item, onShelf: false))).ToList();
		List<StoreRow> arriving = bought.Where(r => r.State is StoreRowState.Get or StoreRowState.Failed or StoreRowState.Downloading).ToList();
		int alreadyDownloading = arriving.Count(r => r.IsDownloading);
		foreach (StoreRow row in arriving)
		{
			if (row.IsDownloading)
			{
				await row.WhenNotDownloadingAsync(cancel);
			}
			else if (row.State is StoreRowState.Get or StoreRowState.Failed)
			{
				await GetAsync(row); // re-checked now: a row fetched by hand meanwhile (Downloaded) is not fetched again
			}

			if (cancel.IsCancellationRequested || !_mode.IsGrownUp)
			{
				return; // the grown-up left the tab or the corner: no line for a run nobody is watching
			}
		}

		int arrived = arriving.Count(r => r.State == StoreRowState.Downloaded);
		RestoreLine line = StoreRowRestore.LineFor(arriving.Count - alreadyDownloading, arrived, alreadyDownloading);
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[StoreRestore] toFetch={arriving.Count - alreadyDownloading} downloading={alreadyDownloading} arrived={arrived} line={line}");
#endif
		ShowRestoreLine(line, arrived);
	}

	private void ShowRestoreLine(RestoreLine line, int arrived)
	{
		if (line == RestoreLine.None)
		{
			RestoreResultView.IsVisible = false; // every book failed: the rows' own strips say why
			return;
		}

		string text = line switch
		{
			RestoreLine.RestoredOne => AppResources.Store_Restored_One,
			RestoreLine.RestoredMany => AppResources.Format(AppResources.Store_Restored_Other, arrived),
			_ => AppResources.Store_NothingToRestore,
		};
		ShowRestoreText(text, found: line is RestoreLine.RestoredOne or RestoreLine.RestoredMany);
	}

	private void ShowRestoreText(string text, bool found)
	{
		RestoreResultLabel.Text = text;
		RestoreResultView.BackgroundColor = (Color)Resources[found ? "ResultGreenFill" : "StripSandFill"];
		RestoreResultLabel.TextColor = (Color)Resources[found ? "ResultGreenText" : "StripSandText"];
		RestoreResultView.IsVisible = true;
	}

	private async void OnRedeemCodeClicked(object? sender, EventArgs e) => await _busy.RunAsync(async () =>
	{
		UiUsage.Tap(UiControls.Pick); // Slice 352: the redeem-code button (the code itself is typed on the store's own screen, never seen here)
		RestoreResultView.IsVisible = false; // a redeemed code changes what the last restore line counted
		await _external.RunAsync(_client.Sales.Purchases.RedeemCodeAsync); // the store's own redeem screen
		await RefreshPurchasesAsync(CancellationToken.None); // a code redeemed there shows as owned without pressing Restore
		await ReplanRowsAsync();
	});

	// The store can take up to 15 s (Play) or 20 s (StoreKit) to answer: past 500 ms the shared wait overlay shows (Slice 346).
	private Task<PurchaseSnapshot> RefreshPurchasesAsync(CancellationToken cancel) =>
		_wait.RunAsync(WaitStage.GettingFile, AppResources.Wait_CheckingPurchases, canCancel: false,
			_ => _client.Sales.Purchases.RefreshAsync(_allRows.Select(r => r.Item), cancel, interactive: true));

	private async Task GetAsync(StoreRow row)
	{
		row.SetState(StoreRowState.Downloading);
		// The filters in use when Get was tapped, not when the download ends.
		bool ageFilterSet = _band != StoreAgeBand.All;
		bool authorFilterSet = _author is not null;
		DownloadOutcome outcome = DownloadOutcome.Failed;
		try
		{
			await SettlePurchasesBehindWaitAsync();
			outcome = await _client.DownloadAsync(row.Item, new Progress<double>(row.SetProgress), CancellationToken.None);
		}
		catch (Exception ex)
		{
			_client.Fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed); // Slice 275: StoreClient reports the failures it catches; this is anything else, and the row offers Try again
			Debug.WriteLine($"Dino Reader store download failed: {ex}");
		}
		finally
		{
			// Only a finished download shows Downloaded and is counted (Slice 134: which filters were set, not their values); a failed one offers Try again.
			if (outcome == DownloadOutcome.Done)
			{
				_usage.RecordStoreDownload(row.Item, ageFilterSet, authorFilterSet);
			}

			await ShowDownloadResultAsync(row, outcome);
		}
	}

	// Slice 357: a failed Get says why under the row (the server's answer, in the app's language) and offers Try again. A 402 ("You need to buy this book first") goes back to the row's plan, so the Buy button shows where the book can be bought.
	private async Task ShowDownloadResultAsync(StoreRow row, DownloadOutcome outcome)
	{
		if (outcome == DownloadOutcome.Done)
		{
			row.SetState(StoreRowState.Downloaded);
			return;
		}

		HttpError? error = outcome == DownloadOutcome.Failed ? _client.DownloadErrorFor(row.Item.Id) : null;
		Debug.WriteLine($"Dino Reader store row {row.Item.Id} failed: {error?.Kind.ToString() ?? "no server answer"}");
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[StoreError] row kind={error?.Kind.ToString() ?? "none"}");
#endif
		if (error?.Kind == HttpErrorKind.PaymentRequired)
		{
			await ShowPaymentRequiredAsync(row, error);
			return;
		}

		row.SetState(StoreRowState.Failed, error);
	}

	// The plan decides what the row offers now: where the book can be bought it shows Buy (or Connect to buy) with the message under it. Where the plan still says Get or Waiting
	// there is no way to buy from here, so the row keeps Try again with the message rather than a Get pill that can only fail the same way.
	private async Task ShowPaymentRequiredAsync(StoreRow row, HttpError error)
	{
		row.SetState(StoreRowState.Get);
		await ReplanRowsAsync();
		if (row.State == StoreRowState.Downloaded)
		{
			return; // the replan found the book on the shelf after all
		}

		if (row.State is StoreRowState.Buy or StoreRowState.ConnectToBuy or StoreRowState.NotForSaleNow)
		{
			row.SetState(row.State, error);
			return;
		}

		row.SetState(StoreRowState.Failed, error);
	}

	// Slice 346 (review): with the switch on, a download first waits for the store's purchase read (up to 25 s); past 500 ms the shared wait overlay says what is awaited. With the switch off this returns at once.
	private async Task SettlePurchasesBehindWaitAsync()
	{
		if (!_client.Sales.SwitchKnownOn)
		{
			return;
		}

		await _wait.RunAsync(WaitStage.GettingFile, AppResources.Wait_CheckingPurchases, canCancel: false, async _ =>
		{
			await _client.SettlePurchasesAsync(CancellationToken.None);
			return true;
		});
	}

	private async void OnTryAgainClicked(object? sender, EventArgs e)
	{
		UiUsage.Tap(UiControls.Retry); // Slice 352
		await _busy.RunAsync(LoadAsync);
	}
}
