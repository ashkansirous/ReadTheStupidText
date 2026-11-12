using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Application.Reading;
using FallbackArea = ReadTheStupidText.Application.Usage.FallbackArea;
using FallbackReason = ReadTheStupidText.Application.Usage.FallbackReason;
using IFallbackSink = ReadTheStupidText.Application.Usage.IFallbackSink;
using ReadTheStupidText.Mobile.Shared.Waiting;
using RoundRectangle = Microsoft.Maui.Controls.Shapes.RoundRectangle;

namespace DinoReader;

/// <summary>
/// Backup (Slice 93, mock screens 24 and 25), behind the grown-up gate. "Save a backup" writes every story into one zip of <c>.dino</c>
/// packages and hands it to the share sheet (a save dialog on Windows); "Restore" imports the stories this phone lacks, keeping each
/// one's own id so restoring twice adds nothing. Nothing goes anywhere the grown-up does not send it.
/// </summary>
public partial class BackupPage : ContentPage, IGrownUpPage, ICornerSubPage
{
	private const string BackupFilePrefix = "DinoReader-backup-";
	private const string DateFormat = "yyyy-MM-dd";
	private const string ZipMime = "application/zip";
	private const string RestoreTempPrefix = "restore-";
	private const int CopyBufferBytes = 81920;

	private static readonly FilePickerFileType BackupFileTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
	{
		{ DevicePlatform.Android, [ZipMime, "application/x-zip-compressed"] },
		{ DevicePlatform.iOS, ["public.zip-archive"] },
		{ DevicePlatform.WinUI, [StoryBackupManifest.Extension] },
	});

	private readonly IStoryLibrary _library;
	private readonly ExternalActivityGuard _external;
	private readonly GrownUpMode _mode;
	private readonly UsageCounter _usage;
	private readonly IFallbackSink _sink;
	private readonly BusyGuard _busy = new();
	private readonly WaitController _wait;
	private readonly StoreClient _store;
	private int _count;
	private const double CardGap = 14;
	private Size _windowSize;
	private bool _sideBySide;
	private Page? _hostPage;
	private Func<Task>? _hostClose;

	public BackupPage(IStoryLibrary library, ExternalActivityGuard external, GrownUpMode mode, UsageCounter usage, IFallbackSink sink, StoreClient store)
	{
		_sink = sink;
		_store = store;
		InitializeComponent();
		_library = library;
		_external = external;
		_mode = mode;
		_usage = usage;
		StayNote.Text = RecordViews.StayNoteText();
		WaitOverlay overlay = DinoWait.OverPage();
		Grid.SetRowSpan(overlay, BackupRoot.RowDefinitions.Count);
		BackupRoot.Children.Add(overlay);
		_wait = new WaitController(overlay);
	}

	// Slice 232 part D (mock Batch 44 frame 124): held sideways the save and restore cards sit side by side, upright they stack; rotating only moves them in the grid,
	// so a restore under way or its result panel is never touched. Slice 325 part B: the Settings tab hosts this page and tells it the window's size (HostShape); the side margins are the tab's.
	void ICornerSubPage.HostShape(double width, double height)
	{
		(width, height) = WindowShapeSeam.Effective(DesktopTips.IsWindows, width, height); // Slice 235 part C: the Windows window picks its shape through the seam, phones and tablets pass through
		if (width <= 0 || height <= 0 || (width == _windowSize.Width && height == _windowSize.Height))
		{
			return;
		}

		_windowSize = new Size(width, height);
		// The page lives in the content column (right of the rail, inside its margins), so it is that width, not the window's, that decides.
		double column = CornerLayout.ContentWidth(CornerLayout.ChromeOf(width, height, DesktopTips.IsWindows), width);
		bool sideBySide = TabletPageLayout.BackupSideBySide(width, height) && SettingsLayout.BackupCardsFit(column);
		if (sideBySide != _sideBySide)
		{
			_sideBySide = sideBySide;
			PlaceCards(sideBySide);
		}
#if DEBUG
		if (TabletPageLayout.Applies(width, height))
		{
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[TabletUpright] backup sideBySide={sideBySide}");
		}
#endif
	}

	View ICornerSubPage.HostIn(Page owner, Func<Task> close)
	{
		_hostPage = owner;
		_hostClose = close;
		Content = null;
		return BackupRoot;
	}

	void ICornerSubPage.HostAppearing() => OnAppearing();

	void ICornerSubPage.HostDisappearing() => OnDisappearing();

	Task ICornerSubPage.HostBackAsync() => _busy.RunAsync(CloseAsync);

	private Task CloseAsync() => _hostClose is { } close ? close() : Task.CompletedTask;

	private Task AlertAsync(string title, string message, string ok) => (_hostPage ?? this).DisplayAlertAsync(title, message, ok);

	private void PlaceCards(bool sideBySide)
	{
		int span = sideBySide ? 1 : 2;
		CardsGrid.RowSpacing = sideBySide ? 0 : CardGap;
		Grid.SetRow(SaveCard, 0);
		Grid.SetColumn(SaveCard, 0);
		Grid.SetColumnSpan(SaveCard, span);
		Grid.SetRow(RestoreCard, sideBySide ? 0 : 1);
		Grid.SetColumn(RestoreCard, sideBySide ? 1 : 0);
		Grid.SetColumnSpan(RestoreCard, span);
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] backup sideBySide={sideBySide}");
#endif
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (!_busy.IsBusy)
		{
			ExportFolders.DeleteStale(); // never while a save or restore is using its export folder
		}

		await TryAsync(CountStoriesAsync);
	}

	private async Task CountStoriesAsync()
	{
		_count = (await _library.ListAsync()).Count;
		SaveBody.Text = SaveBodyText();
		RestoreBody.Text = StateOnlyBackups ? AppResources.Backup_RestoreBodyState : AppResources.Backup_RestoreBody;
		SaveButton.IsEnabled = _count > 0;
	}

	// Slice 346: only a known-on paid-books switch makes a backup without the book files; off or unknown keeps the backup of before (book files inside).
	private bool StateOnlyBackups => _store.Sales.ConfigKnown && _store.Sales.Enabled;

	private string SaveBodyText()
	{
		if (StateOnlyBackups)
		{
			return _count == 1 ? AppResources.Backup_SaveBodyState_One : AppResources.Format(AppResources.Backup_SaveBodyState_Other, _count);
		}

		return (_count == 1 ? AppResources.Backup_SaveBody_One : AppResources.Format(AppResources.Backup_SaveBody_Other, _count)) + " " + AppResources.Backup_SaveIncludes;
	}

	private async void OnBackTapped(object? sender, EventArgs e) => await _busy.RunAsync(CloseAsync);

	private async void OnSeeStoriesClicked(object? sender, EventArgs e)
	{
		UiUsage.Tap(UiControls.SeeStories); // Slice 352
		await _busy.RunAsync(CloseAsync);
	}

	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		UiUsage.Tap(UiControls.Save);
		await _busy.RunAsync(() => GuardedAsync(SaveAsync));
	}

	private async void OnRestoreClicked(object? sender, EventArgs e)
	{
		UiUsage.Tap(UiControls.Restore);
		await _busy.RunAsync(() => GuardedAsync(RestoreAsync));
	}

	// A failure the code below did not name ends in the calm "didn't work" alert.
	private async Task GuardedAsync(Func<Task> work)
	{
		try
		{
			await work();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// no-fallback: the grown-up is told the backup failed by an alert; nothing falls back
			Debug.WriteLine($"Dino Reader backup failed: {ex}");
			await AlertAsync(AppResources.Backup_Title, AppResources.Backup_Failed, AppResources.Common_Ok);
		}
	}

	// The wait overlay (Slice 143, no Cancel: a half-restored library is worse than waiting) covers only the file work, never the picker, the share sheet or an alert.
	private Task<T> WaitingAsync<T>(string line, Func<Task<T>> work) => _wait.RunAsync(WaitStage.GettingFile, line, canCancel: false, _ => work());

	private static async Task TryAsync(Func<Task> work)
	{
		try
		{
			await work();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// no-fallback: the count line is left as it was; the page itself works
			Debug.WriteLine($"Dino Reader backup page failed: {ex}");
		}
	}

	private async Task SaveAsync()
	{
		IReadOnlyList<Story> stories = (await _library.ListAsync()).OrderByDescending(s => s.Created).ThenBy(s => s.Id, StringComparer.Ordinal).ToList(); // newest first, whatever order the shelf has
		if (stories.Count == 0 || !_mode.IsGrownUp)
		{
			return;
		}

		(string path, int skipped) = await WaitingAsync(AppResources.Wait_BackingUp, () => WriteBackupAsync(stories));
		await _external.RunAsync(() => DeliverAsync(path));
		if (skipped > 0)
		{
			await AlertAsync(AppResources.Backup_Title, AppResources.Format(AppResources.Backup_SaveSkipped, skipped), AppResources.Common_Ok);
		}
	}

	// A failed write removes its folder, so no partial zip is left behind to be shared later.
	private async Task<(string Path, int Skipped)> WriteBackupAsync(IReadOnlyList<Story> stories)
	{
		string folder = ExportFolders.CreateNew();
		string path = Path.Combine(folder, BackupFilePrefix + DateTime.Now.ToString(DateFormat) + StoryBackupManifest.Extension);
		try
		{
			await using FileStream output = File.Create(path);
			BackupFormat format = StateOnlyBackups ? BackupFormat.StateOnly : BackupFormat.WithBookFiles;
			return (path, await StoryBackupWriter.WriteAsync(stories, _library, output, format, SettingsToSave()));
		}
		catch
		{
			ExportFolders.TryDelete(folder);
			throw;
		}
	}

#if WINDOWS
	// Windows has no share sheet worth the name for a file: a save dialog puts it where the grown-up chooses.
	private static async Task<bool> DeliverAsync(string path)
	{
		var picker = new Windows.Storage.Pickers.FileSavePicker
		{
			SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
			SuggestedFileName = Path.GetFileNameWithoutExtension(path),
		};
		picker.FileTypeChoices.Add(AppResources.Backup_Title, [StoryBackupManifest.Extension]);
		var window = (Microsoft.UI.Xaml.Window?)Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView;
		if (window is null)
		{
			throw new InvalidOperationException("There is no window to show the save dialog on.");
		}

		WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
		Windows.Storage.StorageFile? file = await picker.PickSaveFileAsync();
		if (file is null)
		{
			return false;
		}

		await using Stream target = await file.OpenStreamForWriteAsync();
		target.SetLength(0);
		await using FileStream source = File.OpenRead(path);
		await source.CopyToAsync(target);
		return true;
	}
#else
	private static async Task<bool> DeliverAsync(string path)
	{
		await Share.Default.RequestAsync(new ShareFileRequest(AppResources.Backup_Title, new ShareFile(path, ZipMime)));
		return true;
	}
#endif

	private async Task RestoreAsync()
	{
		FileResult? picked = await _external.RunAsync(() => FilePicker.Default.PickAsync(new PickOptions
		{
			PickerTitle = AppResources.Backup_RestoreButton,
			FileTypes = BackupFileTypes,
		}));
		if (picked is null || !_mode.IsGrownUp)
		{
			return;
		}

		string temp = Path.Combine(FileSystem.CacheDirectory, RestoreTempPrefix + Guid.NewGuid().ToString("N") + StoryBackupManifest.Extension);
		try
		{
			await RestoreFromAsync(picked, temp);
		}
		finally
		{
			File.Delete(temp);
		}
	}

	// The picked file may be a one-way content stream; the zip reader needs one it can seek in.
	private static async Task CopyPickedAsync(FileResult picked, string temp)
	{
		await using Stream source = await picked.OpenReadAsync();
		await using FileStream target = File.Create(temp);
		var buffer = new byte[CopyBufferBytes];
		long copied = 0;
		int read;
		while ((read = await source.ReadAsync(buffer)) > 0)
		{
			copied += read;
			if (copied > StoryBackupReader.MaxBackupBytes)
			{
				throw new StoryBackupException("The file is too large to be a backup.");
			}

			await target.WriteAsync(buffer.AsMemory(0, read));
		}
	}

	private async Task<StoryRestoreResult> CopyAndRestoreAsync(FileResult picked, string path)
	{
		await CopyPickedAsync(picked, path);
		_store.Sales.RefreshConfigInBackground(); // never awaited: the last known switch decides (while it was never known, an old backup's books are fetched only when the listing says free)
		LegacyBookFiles legacy = BookAccess.LegacyFilesMode(_store.Sales.ConfigKnown, _store.Sales.Enabled); // unknown counts as off: the backup's book files are imported, as before paid books
		var added = new List<Story>();
		StoryRestoreResult merged;
		await using (FileStream file = File.OpenRead(path))
		{
			merged = await StoryBackupReader.RestoreAsync(file, _library, legacy, added.Add);
		}

		await ApplySettingsAsync(path);
		StoryRestoreResult result = await GetBooksBackAsync(path, merged, added);
		int dropped = await DropOwnBooksAsync(added);
		CountRestoredBooks(added);
		return dropped == 0 ? result : result with { Added = result.Added - dropped, Skipped = result.Skipped + dropped };
	}

	// Slice 278 A3 (design 2): the app holds store books only, so a backed-up book with no source id (a family's own book) is removed again by the same cleanup the first start runs, and shown as skipped.
	// The summary and the usage count see only the kept books. A book that could not be removed stays hidden (the library never lists it) and the cleanup is re-armed for the next start.
	private async Task<int> DropOwnBooksAsync(List<Story> added)
	{
		OwnBooksCleanupResult cleaned = await OwnBooksCleanupRun.RunAsync(_library, _sink);
		if (cleaned.Failed > 0 || !cleaned.Complete)
		{
			OwnBooksCleanup.Rearm();
		}

		return added.RemoveAll(story => cleaned.Deleted.Contains(story.Id));
	}

	// One count per restored own book (kind "restore" plus its page-count bucket, nothing the book holds), all queued in one write off the UI thread (Slice 191).
	// A restored starter or store book has a source and is not counted. Counting never decides whether the restore worked.
	private void CountRestoredBooks(List<Story> added)
	{
		List<(Story Story, string Detail)> counted = added
			.Select(story => (Story: story, Detail: UsageDetail.RestoredBook(story)))
			.Where(c => c.Detail is not null)
			.Select(c => (c.Story, c.Detail!))
			.ToList();
		if (counted.Count > 0)
		{
			_ = Task.Run(() => _usage.RecordMany(UsageEventName.OwnBookAdded, counted));
		}
	}

	// Slice 346 part C: a restore brings no book files. The books the phone lacks come from the store again: a free book, or one the store account owns (Restore purchases is read first), is
	// downloaded and then given its recordings and reading view from the backup; the rest are listed as "books to get back" and a second restore, after buying or restoring, fetches them.
	// A schema 1 backup still holds the book files: when the store cannot supply a book (offline, no longer listed, or free or owned but the download failed) and the book is not sold and unowned, the
	// file is used, so a book the family has is never lost by restoring (Slice 346). `shelfAdded` collects the stories put on the shelf from files, for the same own-book cleanup and count as before.
	private async Task<StoryRestoreResult> GetBooksBackAsync(string path, StoryRestoreResult merged, List<Story> shelfAdded)
	{
		IReadOnlyList<MissingBook> missing = merged.Missing ?? [];
		if (missing.Count == 0)
		{
			return merged;
		}

		StoreListing listing = await _store.ListAsync(CancellationToken.None);
		if (listing.Status == StoreLoadStatus.Ok && _store.Sales.Enabled)
		{
			await _store.Sales.Purchases.RestoreAsync(listing.Items, CancellationToken.None);
		}

		var left = new List<MissingBook>();
		int added = 0;
		foreach (MissingBook book in missing)
		{
			StoreItem? item = listing.Items.FirstOrDefault(i => string.Equals(i.Id, book.Id, StringComparison.OrdinalIgnoreCase));
			if (await TryGetOneAsync(book, item, path, shelfAdded))
			{
				added++;
			}
			else
			{
				left.Add(book);
			}
		}

		return merged with { Added = merged.Added + added, Missing = left.Count > 0 ? left : null };
	}

	// One decision for both the store download and the file fallback: BookAccess.LegacyBookChoice, fed by the same "sold" reading the first-run picker uses (an unread switch counts as sold).
	private async Task<bool> TryGetOneAsync(MissingBook book, StoreItem? item, string path, List<Story> shelfAdded)
	{
		BookSale access = item is not null && BookAccess.IsSold(_store.Sales.EnabledForOffer, item) ? BookSale.Paid : BookSale.Free;
		bool owned = item is not null && BookAccess.OwnedProduct(item, _store.Sales.Purchases.Snapshot.Owned) is not null;
		LegacyBookSource first = BookAccess.LegacyBookChoice(access, owned, storeCanSupply: item is not null);
		if (first == LegacyBookSource.Store && await TryGetBookAsync(item!, path))
		{
			return true;
		}

		return BookAccess.LegacyBookChoice(access, owned, storeCanSupply: false) == LegacyBookSource.File && await TryBookFileAsync(path, book.Id, shelfAdded);
	}

	private async Task<bool> TryGetBookAsync(StoreItem item, string path)
	{
		if (await _store.DownloadAsync(item, new Progress<double>(), CancellationToken.None) != DownloadOutcome.Done)
		{
			return false;
		}

		_usage.RecordStoreDownload(item, ageFilterSet: false, authorFilterSet: false);
		await TryApplyBackupAsync(item.Id, path);
		return true;
	}

	// The book itself is on the shelf; a backup entry that cannot be read costs that one book its recordings and reading view, is reported, and the restore goes on with the rest.
	private async Task TryApplyBackupAsync(string bookId, string path)
	{
		try
		{
			await using FileStream file = File.OpenRead(path);
			await StoryBackupReader.ApplyToBookAsync(file, _library, bookId);
		}
		catch (Exception ex) when (ex is StoryBackupException or IOException)
		{
			Debug.WriteLine($"Dino Reader could not apply the backup to {bookId}: {ex.Message}");
			_sink.Report(FallbackArea.Library, FallbackReason.StoryUnreadable);
		}
	}

	private async Task<bool> TryBookFileAsync(string path, string bookId, List<Story> shelfAdded)
	{
		await using FileStream file = File.OpenRead(path);
		return await StoryBackupReader.ImportLegacyBookAsync(file, _library, bookId, shelfAdded.Add) is not null;
	}

	// The few settings a backup carries (reading speed, animal sounds), through Dino's own preference classes.
	private static Dictionary<string, string> SettingsToSave() => new()
	{
		[BackupSettings.ReadingSpeedKey] = BackupSettings.SpeedText(ReadingSpeedPreference.Load().Value),
		[BackupSettings.AnimalSoundsKey] = AnimalSoundsPreference.Load() ? "true" : "false",
	};

	private static async Task ApplySettingsAsync(string path)
	{
		await using FileStream file = File.OpenRead(path);
		IReadOnlyDictionary<string, string> settings = await StoryBackupReader.ReadSettingsAsync(file);
		if (settings.TryGetValue(BackupSettings.ReadingSpeedKey, out string? speed) && BackupSettings.TryParseSpeed(speed, out double value))
		{
			ReadingSpeedPreference.Save(ReadingSpeed.Clamp(value));
		}

		if (settings.TryGetValue(BackupSettings.AnimalSoundsKey, out string? sounds))
		{
			AnimalSoundsPreference.Save(sounds == "true");
		}
	}

	private async Task RestoreFromAsync(FileResult picked, string path)
	{
		try
		{
			ShowResult(await WaitingAsync(AppResources.Wait_Restoring, () => CopyAndRestoreAsync(picked, path)));
			await TryAsync(CountStoriesAsync);
		}
		catch (StoryBackupNewerException)
		{
			// no-fallback: the grown-up is told by an alert that the backup is from a newer app; nothing falls back
			await AlertAsync(AppResources.Backup_Title, AppResources.Backup_Newer, AppResources.Common_Ok);
		}
		catch (StoryBackupException ex)
		{
			// no-fallback: the grown-up is told by an alert that the file is not a backup; nothing falls back
			Debug.WriteLine($"Dino Reader backup refused: {ex.Message}");
			await AlertAsync(AppResources.Backup_Title, AppResources.Backup_NotABackup, AppResources.Common_Ok);
		}
	}

	private void ShowResult(StoryRestoreResult result)
	{
		int restored = result.Added + result.Merged; // a book fetched from the store, or one that got its recordings back, counts as restored
		ResultTitle.Text = restored == 1
			? AppResources.Format(AppResources.Backup_Restored_One, restored)
			: AppResources.Format(AppResources.Backup_Restored_Other, restored);
		ResultRows.Children.Clear();
		AddResultRow(AppResources.Backup_Added, result.Added, (Color)Resources["AddedDot"]);
		if (result.Merged > 0)
		{
			AddResultRow(AppResources.Backup_Merged, result.Merged, (Color)Resources["AddedDot"]);
		}

		AddResultRow(AppResources.Backup_Skipped, result.Skipped, (Color)Resources["SkippedDot"]);
		if (result.Damaged > 0)
		{
			AddResultRow(AppResources.Backup_Damaged, result.Damaged, DinoTokens.Get(DinoTokens.Danger));
		}

		if (result.TooLarge > 0)
		{
			AddResultRow(AppResources.Backup_TooLarge, result.TooLarge, DinoTokens.Get(DinoTokens.Danger));
		}

		if (result.Missing is { Count: > 0 } toGetBack)
		{
			AddResultRow(AppResources.Backup_Missing, toGetBack.Count, DinoTokens.Get(DinoTokens.Danger));
			ResultNote.Text = AppResources.Backup_MissingNote;
		}

		ResultNote.IsVisible = result.Missing is { Count: > 0 };

		MainPanel.IsVisible = false;
		ResultPanel.IsVisible = true;
	}

	private void AddResultRow(string label, int count, Color dotColour)
	{
		var dot = new Border
		{
			WidthRequest = 10,
			HeightRequest = 10,
			StrokeThickness = 0,
			VerticalOptions = LayoutOptions.Center,
			StrokeShape = new RoundRectangle { CornerRadius = 5 },
			BackgroundColor = dotColour,
		};
		var row = new Grid
		{
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
			ColumnSpacing = 12,
			MinimumHeightRequest = 48,
		};
		row.Add(dot, 0);
		row.Add(new Label { Text = label, FontSize = 15, VerticalOptions = LayoutOptions.Center }, 1);
		row.Add(new Label { Text = count.ToString(), FontSize = 15, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }, 2);
		ResultRows.Children.Add(row);
	}
}
