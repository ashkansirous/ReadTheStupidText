using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using Sirous.Reading.Application.Net;
using Sirous.Reading.Application.Usage;
using WaitStage = Sirous.Reading.Application.Reading.WaitStage;
using Sirous.Reading.Mobile.Shared.Waiting;

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

	/// <summary>The detail line, "Ages 5–8 · 7 min" (Slice 397, mock SB1): the age range leads the length (each part is left out when the server gave none).</summary>
	public string Detail => StoreText.Detail(Item);

	/// <summary>The sub-line under the title: the credit line in the app language (Slice 192: <c>creditsByLanguage</c>, else <c>credits</c>), or the language in its own name when the story has none.</summary>
	public string Credits => Item.CreditsIn(LanguageChoice.Current()) ?? StoreText.Language(Item);

	/// <summary>The tag badges above the title (Slice 260): at most two, in list order; none for an untagged book or an older Worker.</summary>
	public IReadOnlyList<StoreTag> Badges => Item.Badges;

	/// <summary>The line under the credits on a card (frame T2, Slice 397 mock): "Ages 5–8 · 6 min"; empty when the server gave neither.</summary>
	public string Meta => StoreText.Detail(Item);

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

	private bool _included;

	/// <summary>Slice 397 (mock SB3, SB4): the tint "Included" tag after the length of a paid book the subscription opens (the brand Get stays instead of a price).</summary>
	public bool IsIncluded => _included && _state is StoreRowState.Get or StoreRowState.Downloading or StoreRowState.Downloaded;

	public string IncludedLabel => AppResources.Store_Included;

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

	// Slice 346 E3: while the download waits for the store to catch up with the purchase the row says so (the shared "checking your purchases" text) instead of a stuck 0%.
	public string ProgressText => _cancelling ? CancellingText : _waitingForStore ? AppResources.Wait_CheckingPurchases : string.Create(AppResources.Culture, $"{(int)(_progress * PercentScale)}%");

	// Slice 346 review pass 6: after Cancel the row stays Downloading, disabled, showing the ring and a bare ellipsis (no new copy string) until the cancelled download has really ended; only then does it replan.
	private const string CancellingText = "…";
	private bool _waitingForStore;
	private bool _cancelling;
	private readonly DownloadCancelSlot _downloadCancel = new();

	/// <summary>Slice 346 review #3: shown only while the download waits for the store to catch up with the purchase, so the long wait can be stopped; the row then waits for the download to end and returns to its state before the tap, with no error.</summary>
	public bool IsWaitingForStore => _waitingForStore && IsDownloading && !_cancelling;

	/// <summary>Starts a download of this row and returns the token its Cancel (<see cref="CancelDownload"/>) trips.</summary>
	public CancellationToken BeginDownload()
	{
		Run++;
		return _downloadCancel.Begin();
	}

	/// <summary>The number of the row's current download (Slice 346 review pass 7 #5): a wait report carries the number of the run that sent it, so a late report of an earlier run is ignored.</summary>
	public int Run { get; private set; }

	/// <summary>Slice 346 review (check2 #2, pass 5 #7): the end of this row's download (called from its <c>finally</c>, after the download's task ended) disposes only its own source. Both run on the UI thread.</summary>
	public void EndDownload(CancellationToken own) => _downloadCancel.End(own);

	/// <summary>Trips the running download and shows the disabled "cancelling" state; the row is freed by the download's own end, never here.</summary>
	public void CancelDownload()
	{
		if (!IsDownloading || _cancelling)
		{
			return;
		}

		_cancelling = true;
		_downloadCancel.Cancel();
		Raise(nameof(ProgressText), nameof(ProgressHint), nameof(IsWaitingForStore));
	}

	/// <summary>A wait report that arrives after the row left Downloading (a late report of a finished download) is ignored, so it cannot bring the wait label back over a final state.</summary>
	public void SetWaitingForStore(bool waiting, int run)
	{
		if (!IsDownloading || run != Run)
		{
			return;
		}

		_waitingForStore = waiting;
		Raise(nameof(ProgressText), nameof(ProgressHint), nameof(IsWaitingForStore));
	}

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

	/// <summary>Slice 396 (mock RR4): the 402 strip ("You need to buy this book first.") carries the Restore link after the sentence, for a family that bought it already.</summary>
	public bool HasBoughtItAlready => _error?.Kind == HttpErrorKind.PaymentRequired;

	/// <summary>The strip's sentence with the gap before the link (the red strip's first span).</summary>
	public string ErrorLead => HasBoughtItAlready ? ErrorText + " " : ErrorText;

	public string BoughtItAlreadyText => HasBoughtItAlready ? AppResources.Store_BoughtItAlready : string.Empty;

	/// <summary>Mock S3: no answer at all is the calm sand strip; every server answer is the red one.</summary>
	public bool HasCalmError => HasError && _error?.Kind == HttpErrorKind.Offline;

	public bool HasRedError => HasError && _error?.Kind != HttpErrorKind.Offline;

	/// <summary>The error is part of the state: a call that ends the row in a failed or error-carrying state passes it, every other call passes none and so clears it (Slice 357 review).</summary>
	public void SetState(StoreRowState state, HttpError? error = null)
	{
		_state = state;
		_progress = 0;
		_waitingForStore = false;
		_cancelling = false;
		_error = error;

		Raise(nameof(Error), nameof(HasError), nameof(HasCalmError), nameof(HasRedError), nameof(ErrorText), nameof(ErrorLead), nameof(HasBoughtItAlready), nameof(BoughtItAlreadyText), nameof(IsGet), nameof(IsBuy), nameof(IsTryAgain), nameof(IsFree), nameof(IsIncluded), nameof(GetLabel), nameof(Price), nameof(GetHint), nameof(HasNote), nameof(NoteText), nameof(HasSandNote), nameof(HasRedNote), nameof(IsDownloading), nameof(IsOnShelf), nameof(Progress), nameof(ProgressText), nameof(ProgressHint), nameof(IsWaitingForStore));
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
	public void Apply(StoreCardPlan plan, bool included = false)
	{
		if (_state == StoreRowState.Downloading || (_state == StoreRowState.Failed && plan.Action != StoreAction.OnShelf))
		{
			return;
		}

		_included = included;
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
			Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[StoreSale] row state={_state}");
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
		item.Minutes is { } minutes ? AppResources.Format(AppResources.Store_LengthMinutes, minutes) : string.Empty;

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

	/// <summary>The restore's "nothing found" message (Slice 396, mock RR3): names the store account the platform buys with (Apple ID, Microsoft account, else Google account).</summary>
	public static string NothingBoughtLine(DevicePlatform platform)
	{
		if (platform == DevicePlatform.iOS || platform == DevicePlatform.MacCatalyst)
		{
			return AppResources.Store_NothingBought_Apple;
		}

		return platform == DevicePlatform.WinUI ? AppResources.Store_NothingBought_Microsoft : AppResources.Store_NothingBought_Google;
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
	private CancellationTokenSource? _restoreRun; // the one restore run of this visit: a new run or Disappearing cancels it, and a cancelled run never shows a result message (Slice 364 review)
	private CancellationTokenSource? _messageTimer; // the 5 second life of the result message (Slice 396); a new message or leaving the tab ends it
	private const int MessageSeconds = 5;
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
		StoreRoot.Children.Remove(SubscriptionCard); // Slice 384: the card is the head of the book list, so it scrolls away with the books
		StoryList.Header = SubscriptionCard;
		BuildChips();
		ShowAuthor();
		WaitOverlay overlay = DinoWait.OverPage(); // Slice 346: a purchase or a restore waits for the store; over 500 ms shows the shared wait overlay
		Grid.SetRowSpan(overlay, 2);
		StoreRoot.Children.Insert(StoreRoot.Children.IndexOf(RestoreResultView), overlay); // under the result message, which stays pinned above everything
		_wait = new WaitController(overlay);
		HandlerChanged += OnHandlerChanged;
	}

	// Check2 #7: the page is destroyed (its handler goes away): the long-lived BookPurchases must not keep it alive. Appearing subscribes again if it is ever shown again.
	private void OnHandlerChanged(object? sender, EventArgs e)
	{
		if (Handler is null)
		{
			_onScreen = false;
			ClearOnScreen();
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
				row.Apply(_client.Sales.PlanFor(row.Item, onShelf.Contains(row.Item.Id)), _client.Sales.IsIncluded(row.Item));
			}

			ApplyPurchaseChrome();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader store replan failed: {ex}");
			_client.Fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed); // the rows keep what they show until the next visit
		}
	}

	// The Restore link on the Store's first line shows only while books are sold; the footer line then says purchases go through the store account.
	private void ApplyPurchaseChrome()
	{
		bool sold = _client.Sales.SwitchKnownOn;
		RestoreLinkRow.IsVisible = sold;
		FooterLabel.Text = sold ? AppResources.Store_FooterPaid : AppResources.Store_Footer;
		ApplySubscriptionCard();
	}

	// Slice 384: the card shows while books are sold and the store priced a plan (both plans with their own recurring price and the trial line), or while the subscription runs (then it says so, and keeps Manage).
	private void ApplySubscriptionCard()
	{
		bool sold = _client.Sales.SwitchKnownOn;
		IReadOnlyDictionary<string, string> prices = _client.Sales.Purchases.Snapshot.Prices;
		string? monthly = prices.GetValueOrDefault(SubscriptionProducts.Monthly);
		string? yearly = prices.GetValueOrDefault(SubscriptionProducts.Yearly);
		bool active = sold && _client.Sales.SubscriptionActive;
		BookPurchases purchases = _client.Sales.Purchases;
		int locked = _client.Sales.LapsedCount();
		SubscriptionCardPlan plan = DinoReader.Core.SubscriptionCard.Choose(sold, purchases.Subscription.Record, active, purchases.Snapshot.TrialAvailable, monthly, yearly, locked, DateTimeOffset.UtcNow);
		SubscriptionCard.IsVisible = plan.Visible;
		ApplyCardTexts(plan, monthly, yearly);
		ApplyCardLook(plan.Kind);
		ApplyCardPlans(plan, monthly, yearly);
		_cardShowsPlans = plan.ShowPlans;
		_planShowsTrial = plan.PlansHaveTrial;
		ApplyCardLayout();
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Subscription] card visible={SubscriptionCard.IsVisible} active={active} monthly={BookAccess.HasPrice(monthly)} yearly={BookAccess.HasPrice(yearly)} kind={plan.Kind} save={plan.SaveTag?.ToString() ?? "none"} trial={plan.PlansHaveTrial} locked={plan.LockedCount} wide={_cardWide}");
#endif
	}

	private bool _cardWide;
	private bool _cardShowsPlans;
	private bool _planShowsTrial = true;

	private static readonly Color TileGreen = Color.FromArgb("#DFF5E3");
	private static readonly Color TileGreenIcon = Color.FromArgb("#2E7D32");
	private static readonly Color TileRed = Color.FromArgb("#FFF0EA");
	private static readonly Color TileRedIcon = Color.FromArgb("#A4462A");
	private static readonly Color GraceRing = Color.FromArgb("#F5C9B8");
	private const double GraceRingThickness = 2;
	private const double CardColumnGap = 20;
	private const double CardRowGap = 12;

	// Title and line by state; the line is hidden when there is nothing to say (subscribed with no usable date). Grace and ended get their own copy (SB4, SB5).
	private void ApplyCardTexts(SubscriptionCardPlan plan, string? monthly, string? yearly)
	{
		string? product = _client.Sales.Purchases.Subscription.Record?.ProductId;
		SubscriptionTitle.Text = SubscriptionCardText.Title(plan);
		SubscriptionLine.Text = SubscriptionCardText.Line(plan, _client.Sales.Purchases.StoreName, monthly, yearly, product);
		SubscriptionLine.IsVisible = SubscriptionLine.Text.Length > 0;
	}

	// The tile and ring: layers on the brand tint for the offer and the ended card, a green check while subscribed, a red card with a ring for a failed payment.
	private void ApplyCardLook(SubscriptionCardKind kind)
	{
		bool grace = kind == SubscriptionCardKind.Grace;
		bool subscribed = kind is SubscriptionCardKind.Trial or SubscriptionCardKind.Active;
		SubscriptionTile.BackgroundColor = grace ? TileRed : subscribed ? TileGreen : (Color)Application.Current!.Resources["BrandTint"];
		SubscriptionTileIcon.Stroke = new SolidColorBrush(grace ? TileRedIcon : subscribed ? TileGreenIcon : (Color)Application.Current!.Resources["Brand"]);
		SubscriptionTileIcon.Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString((string)Resources[grace ? "PaymentGlyph" : subscribed ? "CheckGlyph" : "LayersGlyph"])!;
		SubscriptionCard.Stroke = grace ? GraceRing : Colors.Transparent;
		SubscriptionCard.StrokeThickness = grace ? GraceRingThickness : 0;
	}

	// The two plan buttons (yearly first, monthly second), the Save tag, the terms, and Manage or Fix payment.
	private void ApplyCardPlans(SubscriptionCardPlan plan, string? monthly, string? yearly)
	{
		bool hasMonthly = BookAccess.HasPrice(monthly);
		bool hasYearly = BookAccess.HasPrice(yearly);
		SubscriptionPlans.IsVisible = plan.ShowPlans;
		SubscriptionTermsBlock.IsVisible = plan.ShowTerms;
		SubscribeMonthlyButton.IsVisible = hasMonthly;
		SubscribeYearlyButton.IsVisible = hasYearly;
		SubscribeMonthlyLabel.Text = hasMonthly ? SubscriptionCardText.Monthly(plan, monthly!) : string.Empty;
		SubscribeYearlyLabel.Text = hasYearly ? SubscriptionCardText.Yearly(plan, yearly!) : string.Empty;
		SemanticProperties.SetDescription(SubscribeMonthlyPress, SubscribeMonthlyLabel.Text);
		SemanticProperties.SetDescription(SubscribeYearlyPress, plan.SaveTag is { } tag && hasYearly ? $"{SubscribeYearlyLabel.Text}. {SubscriptionCardText.Save(tag)}" : SubscribeYearlyLabel.Text); // the face (label and Save tag) is out of the accessibility tree: the Button says it all
		SubscribeSaveTag.IsVisible = hasYearly && plan.SaveTag is not null;
		SubscribeSaveLabel.Text = plan.SaveTag is { } save ? SubscriptionCardText.Save(save) : string.Empty;
		SubscriptionTerms.Text = SubscriptionCardText.Terms(plan);
		SubscriptionManageRow.IsVisible = plan.ShowManage && !plan.ShowFixPayment;
		SubscriptionFixPayment.IsVisible = plan.ShowFixPayment;
	}

	// Mock SB10: from 600 dp the words stay at the left and the plans sit in a 300-wide column at the right; narrower, header, plans and terms stack. No plans (subscribed) leaves no empty column.
	private void ApplyCardLayout()
	{
		bool split = _cardWide && _cardShowsPlans;
		SubscriptionGrid.ColumnDefinitions[1].Width = split ? DinoReader.Core.SubscriptionCard.PlansColumnWidth : 0;
		SubscriptionGrid.ColumnSpacing = split ? CardColumnGap : 0; // spacing only where a column or row shows: the hidden parts add no blank space (review fix 6)
		SubscriptionPlans.Margin = split ? new Thickness(0) : new Thickness(0, CardRowGap, 0, 0);
		Grid.SetColumn(SubscriptionPlans, split ? 1 : 0);
		Grid.SetRow(SubscriptionPlans, split ? 0 : 1);
		Grid.SetRowSpan(SubscriptionPlans, split ? 2 : 1);
		Grid.SetRow(SubscriptionTermsBlock, split ? 1 : 2);
	}

	// no-tap-count (all five subscription presses): the closed UiControls list (shared with the Worker's CONTROLS) has no subscription control yet, and a new one is outside Slice 384's files; the store sheet's outcome is the fact that matters and the store holds it
	private async void OnSubscribeMonthlyClicked(object? sender, EventArgs e) => await _busy.RunAsync(() => SubscribeAsync(SubscriptionProducts.Monthly)); // no-tap-count: see above

	private async void OnSubscribeYearlyClicked(object? sender, EventArgs e) => await _busy.RunAsync(() => SubscribeAsync(SubscriptionProducts.Yearly)); // no-tap-count: see above

	// Slice 384: opens the store's own purchase sheet for the plan (behind the grown-ups gate like a book); the wait overlay shows past 500 ms. Cancelled changes nothing; a failure says so plainly. Whatever the sheet
	// did, the store is read again and every row and the card follow it (a subscription that started opens the paid books for download at once).
	private async Task SubscribeAsync(string plan)
	{
		HideRestoreMessage();
		bool withTrial = _planShowsTrial; // the buy follows what the card promised (review 4): an Ended card shows no trial, so the sheet starts none
		BuyOutcome outcome = await _wait.RunAsync(WaitStage.GettingFile, AppResources.Wait_BuyingBook, canCancel: false,
			_ => _client.Sales.Purchases.BuyAsync(plan, _allRows.Select(r => r.Item), CancellationToken.None, withTrial));
		await ReplanRowsAsync();
		if (_mode.IsGrownUp && outcome == BuyOutcome.Failed)
		{
			await AlertAsync(AppResources.Store_BuyFailed);
		}
	}

	private async void OnSubscriptionTermsTapped(object? sender, TappedEventArgs e) => // no-tap-count: a link to the store's terms page, see above
		await _external.RunAsync(() => Launcher.Default.OpenAsync(new Uri(SubscriptionLinks.TermsFor(_client.Sales.Purchases.StoreName))));

	private async void OnSubscriptionPrivacyTapped(object? sender, TappedEventArgs e) => // no-tap-count: a link to the privacy page, see above
		await _external.RunAsync(() => Launcher.Default.OpenAsync(new Uri(SubscriptionLinks.Privacy)));

	// Slice 384: false means the store's sheet or page failed (BookPurchases has reported it): the grown-up is told. A tap while the store's sheet is already open does nothing.
	private async void OnSubscriptionManageTapped(object? sender, EventArgs e) // no-tap-count: opens the store's own subscription page, see above
	{
		if (_external.IsActive)
		{
			return;
		}

		bool opened = await _external.RunAsync(_client.Sales.Purchases.ManageSubscriptionAsync);
		if (!opened)
		{
			await AlertAsync(AppResources.Store_ManageFailed);
		}
	}

	/// <summary>The Store tab is on screen: the list loads the first time; later it only picks up the covers a tab switch cut short. A download that was running is not touched.</summary>
	public async void Appearing()
	{
		_onScreen = true;
		_onScreenStore = this;
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

			row.Apply(_client.Sales.PlanFor(row.Item, onShelf.Contains(row.Item.Id)), _client.Sales.IsIncluded(row.Item)); // Get, On the shelf, Buy or a note, by the shelf and the store's answer (a failed download keeps Try again)
		}
	}

	/// <summary>The tab went off screen (another tab, the shelf): cover fetches stop. A download keeps running into the library and only a real failure ends it.</summary>
	public void Disappearing()
	{
		HideRestoreMessage(); // a restore message is for the visit it ran in
		ClearOnScreen();
		_restoreRun?.Cancel(); // a running restore stops fetching and shows no line (its downloads may still finish, as a row's do)
		_onScreen = false; // the subscription stays: a change while hidden is remembered (check8 #7)
		_lifetime.Cancel();
	}

	// Decision 4 (Slice 325 part B): one column of cards, two from 800 dp of content. The gallery passes the column's width (the window less the rail), a pure function of the window, so a
	// rotation changes the grid's span only: the rows, the filter, the open download and the scroll position are never rebuilt.
	public void ApplyShape(double contentWidth)
	{
		_cardWide = DinoReader.Core.SubscriptionCard.IsWide(contentWidth);
		ApplyCardLayout(); // Slice 397: the subscription card's own split (SB10), independent of the book columns below
		int columns = SettingsLayout.StoreColumns(contentWidth);
		if (columns == _shownColumns)
		{
			return;
		}

		_shownColumns = columns;
		((GridItemsLayout)StoryList.ItemsLayout).Span = columns;
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] store columns={columns} rows={_rows.Count}");
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Redesign] store columns={columns} content={contentWidth:0} rows={_rows.Count}");
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
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[StoreError] list kind={error?.Kind.ToString() ?? "none"}");
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
		row.Apply(_client.Sales.PlanFor(item, onShelf.Contains(item.Id)), _client.Sales.IsIncluded(item));
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
	}

	private void OnCancelWaitTapped(object? sender, TappedEventArgs e)
	{
		if ((sender as BindableObject)?.BindingContext is StoreRow row)
		{
			row.CancelDownload();
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
		string? product = row.Item.Products.FirstOrDefault(p => !SubscriptionProducts.Is(p)); // the book's own product (or a bundle), never a subscription plan
		if (product is null)
		{
			return;
		}

		HideRestoreMessage(); // a buy changes what the last restore counted
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
		await RestoreNowAsync();
	}

	// Slice 396: the 402 strip's "Bought it already? Restore purchases" and Ctrl+R run the very restore the first line runs.
	// The tap lands on the whole strip (a bigger target, one accessibility action); only a strip that carries the link (a 402) acts.
	private async void OnBoughtItAlreadyTapped(object? sender, TappedEventArgs e)
	{
		if ((sender as BindableObject)?.BindingContext is not StoreRow { HasBoughtItAlready: true })
		{
			return;
		}

		UiUsage.Tap(UiControls.Restore);
		await RestoreNowAsync();
	}

	private bool _restoring; // a restore is going: the first line already says "Restoring…", so a second tap (or Ctrl+R) is the same restore and is not started again

	// Slice 396 review: one restore at a time, visible from the tap (spinner) and queued behind a buy or a redeem that holds the guard, never dropped. Only the ask-the-store part holds the guard;
	// the book downloads that follow run outside it, so Buy, Subscribe and Redeem keep working while they arrive.
	private async Task RestoreNowAsync()
	{
		if (_restoring)
		{
			return;
		}

		_restoring = true;
		HideRestoreMessage(); // the last restore's message never outlives the next try, even when the grown-up gate ends it early
		_restoreRun?.Dispose();
		_restoreRun = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
		ShowRestoring(true);
		try
		{
			await RestorePurchasesAsync(_restoreRun.Token);
		}
		catch (OperationCanceledException) when (_restoreRun is { IsCancellationRequested: true })
		{
			// no-fallback: the grown-up left the tab or started a newer restore; nobody is waiting for a message. Any other cancel (a timeout) goes to the catch-all below. (Slice 396 review check2 #2)
		}
		catch (Exception ex)
		{
			_client.Fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed); // Slice 396: a restore that throws shows the "could not ask the store" notice, never a crash from the async-void handlers
			Debug.WriteLine($"Dino Reader store restore failed: {ex}");
			if (_restoreRun is { IsCancellationRequested: false })
			{
				ShowRestoreMessage(ex is HttpRequestException ? AppResources.Store_OfflineTitle : AppResources.Error_Server, RestoreMessageKind.Notice); // Slice 396 review #3: only a network error says "offline"; nothing shows once the tab is gone
			}
		}
		finally
		{
			_restoring = false;
			ShowRestoring(false);
		}
	}

	private static StorePage? _onScreenStore; // the Store tab while it shows (Appearing to Disappearing), so the Windows head's Ctrl+R finds it without the gallery

	private void ClearOnScreen()
	{
		if (ReferenceEquals(_onScreenStore, this))
		{
			_onScreenStore = null;
		}
	}

	/// <summary>
	/// Ctrl+R on the Windows head (Slice 396): runs Restore from the Store tab, as the first line's link does. Returns whether the key was used: false when the Store tab is not showing or books are not sold
	/// (the Restore link is then not shown either).
	/// </summary>
	public static bool TryRestoreFromKeyboard()
	{
		if (_onScreenStore is not { RestoreLinkRow.IsVisible: true } store || !store._mode.IsGrownUp || DinoKeyTargets.TopPage(store.Window) is not GalleryPage)
		{
			return false; // not showing, not the grown-ups' corner, or a gate, a book or another page is on top of the tab
		}

		store.RestoreFromKeyboard();
		return true;
	}

	private async void RestoreFromKeyboard()
	{
		UiUsage.Tap(UiControls.Restore);
		await RestoreNowAsync();
	}

	// Slice 396 (mock RR1): the first line's icon turns into a spinner and its text into "Restoring…" while the run goes; the rest of the Store stays usable.
	private void ShowRestoring(bool running)
	{
		RestoreLineIcon.IsVisible = !running;
		RestoreSpinner.IsVisible = running;
		RestoreSpinner.IsRunning = running;
		RestoreLineLabel.Text = running ? AppResources.Store_Restoring : AppResources.Store_RestorePurchases;
	}

	// One owner of a restore: the run's token is linked to this visit, a new run cancels the old one and Disappearing cancels it. The message appears only when the run finishes un-cancelled.
	// Slice 396 (mock RR1): the first line shows "Restoring…" with a spinner for the run and the Store stays usable, so the wait overlay is not used.
	private async Task RestorePurchasesAsync(CancellationToken cancel)
	{
		PurchaseSnapshot? snapshot = null;
		await _busy.RunWhenFreeAsync(async () =>
		{
			snapshot = await _client.Sales.Purchases.RestoreAsync(_allRows.Select(r => r.Item), CancellationToken.None);
			await ReplanRowsAsync();
		}, cancel); // Slice 396 review #1: leaving the tab ends a restore that is still queued behind a buy
		if (snapshot is null || !_mode.IsGrownUp || cancel.IsCancellationRequested)
		{
			return;
		}

		if (!snapshot.Reachable)
		{
			ShowRestoreMessage(AppResources.Store_OfflineTitle, RestoreMessageKind.Notice); // the store could not be asked: the message says so rather than "nothing found"
			return;
		}

		await PutRestoredBooksOnShelfAsync(cancel);
	}

	// Slice 364 (mock S3, "They're on the shelf"): every bought book that is not on the shelf is fetched, one at a time with its own progress (a Failed row too); a bought book already arriving is awaited
	// and counted like the fetched ones. A Get tap that lands while the run is going is awaited, never started twice.
	private async Task PutRestoredBooksOnShelfAsync(CancellationToken cancel)
	{
		IReadOnlySet<string> ownedNow = _client.Sales.Purchases.Snapshot.Owned;
		List<StoreRow> bought = _allRows.Where(r => BookAccess.BoughtProduct(r.Item, ownedNow) is not null && StoreRowRestore.NeedsFetch(_client.Sales.PlanFor(r.Item, onShelf: false))).ToList(); // Slice 384: Restore brings back bought books only; a subscription opens the rest on demand, never all at once
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
		if (_allRows.Count == 0 && (ownedNow.Count > 0 || _client.Sales.SubscriptionActive))
		{
			HideRestoreMessage(); // the catalog is not loaded, so what the store says it owns cannot be matched to books: say nothing rather than guess
			return;
		}

		bool ownsBooks = ownedNow.Count > 0 || _allRows.Any(r => BookAccess.BoughtProduct(r.Item, ownedNow) is not null); // the store's own owned list counts even for a book the catalog does not show; all on the shelf already is "restored", not "nothing bought"
		RestoreLine line = StoreRowRestore.LineFor(arriving.Count - alreadyDownloading, arrived, alreadyDownloading, _client.Sales.SubscriptionActive, ownsBooks); // Slice 384: Restore also brings back a running subscription
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[StoreRestore] toFetch={arriving.Count - alreadyDownloading} downloading={alreadyDownloading} arrived={arrived} line={line}");
#endif
		ShowRestoreLine(line, arrived);
	}

	private void ShowRestoreLine(RestoreLine line, int arrived)
	{
		switch (line)
		{
			case RestoreLine.RestoredOne:
				ShowRestoreMessage(AppResources.Store_Restored_One, RestoreMessageKind.Restored);
				break;
			case RestoreLine.RestoredMany:
				ShowRestoreMessage(AppResources.Format(AppResources.Store_Restored_Other, arrived), RestoreMessageKind.Restored);
				break;
			case RestoreLine.SubscriptionBack:
				ShowRestoreMessage(AppResources.Sub_Active, RestoreMessageKind.Restored);
				break;
			case RestoreLine.AllOnShelf:
				ShowRestoreMessage(AppResources.Store_OnShelf, RestoreMessageKind.Restored);
				break;
			case RestoreLine.NothingFound:
				ShowRestoreMessage(StoreText.NothingBoughtLine(DeviceInfo.Platform), RestoreMessageKind.NothingFound);
				break;
			default:
				HideRestoreMessage(); // every book failed: the rows' own strips say why
				break;
		}
	}

	/// <summary>The look of the dark message (Slice 396): a green check, an amber alert with the Redeem a code action, or an amber alert alone.</summary>
	private enum RestoreMessageKind
	{
		Restored,
		NothingFound,
		Notice,
	}

	// Slice 396 (mock RR2, RR3): a dark message pinned above the tab bar for MessageSeconds, then it goes by itself; a new message, a buy or leaving the tab ends it earlier.
	private void ShowRestoreMessage(string text, RestoreMessageKind kind)
	{
		_messageTimer?.Cancel();
		_messageTimer?.Dispose();
		_messageTimer = new CancellationTokenSource();
		bool restored = kind == RestoreMessageKind.Restored;
		RestoreResultLabel.Text = text;
		RestoreResultIcon.Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString((string)Resources[restored ? "CheckGlyph" : "AlertGlyph"])!;
		RestoreResultIcon.Stroke = (Color)Resources[restored ? "MessageGreen" : "MessageAmber"];
		RestoreResultIcon.StrokeThickness = restored ? 3 : 2.2;
		RestoreRedeemAction.IsVisible = kind == RestoreMessageKind.NothingFound;
		RestoreResultView.IsVisible = true;
		SemanticScreenReader.Announce(text); // a screen reader user hears the result that disappears after MessageSeconds
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[StoreRestore] message={kind} text={text}");
#endif
		_ = HideRestoreMessageLaterAsync(_messageTimer.Token);
	}

	private async Task HideRestoreMessageLaterAsync(CancellationToken token)
	{
		try
		{
			await Task.Delay(TimeSpan.FromSeconds(MessageSeconds), token);
			RestoreResultView.IsVisible = false;
		}
		catch (OperationCanceledException)
		{
			// no-fallback: a newer message or a hide replaced this one; the timer of that one hides it.
		}
	}

	private void HideRestoreMessage()
	{
		_messageTimer?.Cancel();
		RestoreResultView.IsVisible = false;
	}

	private async void OnRedeemCodeClicked(object? sender, EventArgs e) => await _busy.RunAsync(async () =>
	{
		UiUsage.Tap(UiControls.Pick); // Slice 352: the redeem-code button (the code itself is typed on the store's own screen, never seen here)
		HideRestoreMessage(); // a redeemed code changes what the last restore counted
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
		CancellationToken cancel = row.BeginDownload();
		int run = row.Run;
		try
		{
			await SettlePurchasesBehindWaitAsync();
			outcome = await _client.DownloadAsync(row.Item, new Progress<double>(row.SetProgress), cancel, new Progress<bool>(waiting => row.SetWaitingForStore(waiting, run)));
		}
		catch (Exception ex)
		{
			_client.Fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed); // Slice 275: StoreClient reports the failures it catches; this is anything else, and the row offers Try again
			Debug.WriteLine($"Dino Reader store download failed: {ex}");
		}
		finally
		{
			row.EndDownload(cancel);
			// Only a finished download shows Downloaded and is counted (Slice 134: which filters were set, not their values); a failed one offers Try again.
			if (outcome == DownloadOutcome.Done)
			{
				_usage.RecordStoreDownload(row.Item, ageFilterSet, authorFilterSet);
			}

			if (row.Run == run)
			{
				await ShowDownloadResultAsync(row, outcome); // an older run's end must not reset a row a newer run now owns (Slice 346 check4 #8)
			}
		}
	}

	// A storage error while asking counts as "not installed": the replan that follows corrects the row.
	private async Task<bool> IsOnShelfAsync(StoreRow row)
	{
		try
		{
			return (await _client.InstalledIdsAsync()).Contains(row.Item.Id);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not read the shelf after a cancel: {ex.Message}");
			_client.Fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed);
			return false;
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

		if (outcome == DownloadOutcome.Cancelled)
		{
			// Slice 346 review pass 5 #5, pass 6: this runs only after the cancelled download has ended (the row stayed Downloading, disabled, until now), so no Get can have joined it.
			// a cancel is not a failure; the row goes back to what is installed (Downloaded for a book already on the shelf, else Get), then the plan decides the rest, with no error
			row.SetState(await IsOnShelfAsync(row) ? StoreRowState.Downloaded : StoreRowState.Get);
			await ReplanRowsAsync();
			return;
		}

		HttpError? error = outcome == DownloadOutcome.Failed ? _client.DownloadErrorFor(row.Item.Id) : null;
		Debug.WriteLine($"Dino Reader store row {row.Item.Id} failed: {error?.Kind.ToString() ?? "no server answer"}");
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[StoreError] row kind={error?.Kind.ToString() ?? "none"}");
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
