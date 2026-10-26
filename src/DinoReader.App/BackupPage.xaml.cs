using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared.Waiting;
using RoundRectangle = Microsoft.Maui.Controls.Shapes.RoundRectangle;

namespace DinoReader;

/// <summary>
/// Backup (Slice 93, mock screens 24 and 25), behind the grown-up gate. "Save a backup" writes every story into one zip of <c>.dino</c>
/// packages and hands it to the share sheet (a save dialog on Windows); "Restore" imports the stories this phone lacks, keeping each
/// one's own id so restoring twice adds nothing. Nothing goes anywhere the grown-up does not send it.
/// </summary>
public partial class BackupPage : ContentPage, IGrownUpPage
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
	private readonly BusyGuard _busy = new();
	private readonly WaitController _wait;
	private int _count;

	public BackupPage(IStoryLibrary library, ExternalActivityGuard external, GrownUpMode mode)
	{
		InitializeComponent();
		_library = library;
		_external = external;
		_mode = mode;
		WaitOverlay overlay = DinoWait.OverPage();
		Grid.SetRowSpan(overlay, BackupRoot.RowDefinitions.Count);
		BackupRoot.Children.Add(overlay);
		_wait = new WaitController(overlay);
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		ExportFolders.DeleteStale();
		await TryAsync(CountStoriesAsync);
	}

	private async Task CountStoriesAsync()
	{
		_count = (await _library.ListAsync()).Count;
		SaveBody.Text = _count == 1 ? AppResources.Backup_SaveBody_One : AppResources.Format(AppResources.Backup_SaveBody_Other, _count);
		SaveButton.IsEnabled = _count > 0;
	}

	private async void OnBackTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(() => Navigation.PopAsync());

	private async void OnSeeStoriesClicked(object? sender, EventArgs e) => await _busy.RunAsync(() => Navigation.PopAsync());

	private async void OnSaveClicked(object? sender, EventArgs e) => await _busy.RunAsync(() => GuardedAsync(SaveAsync));

	private async void OnRestoreClicked(object? sender, EventArgs e) => await _busy.RunAsync(() => GuardedAsync(RestoreAsync));

	// A failure the code below did not name ends in the calm "didn't work" alert.
	private async Task GuardedAsync(Func<Task> work)
	{
		try
		{
			await work();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Debug.WriteLine($"Dino Reader backup failed: {ex}");
			await DisplayAlertAsync(AppResources.Backup_Title, AppResources.Backup_Failed, AppResources.Common_Ok);
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
			Debug.WriteLine($"Dino Reader backup page failed: {ex}");
		}
	}

	private async Task SaveAsync()
	{
		IReadOnlyList<Story> stories = await _library.ListAsync();
		if (stories.Count == 0 || !_mode.IsGrownUp)
		{
			return;
		}

		(string path, int skipped) = await WaitingAsync(AppResources.Wait_BackingUp, () => WriteBackupAsync(stories));
		await _external.RunAsync(() => DeliverAsync(path));
		if (skipped > 0)
		{
			await DisplayAlertAsync(AppResources.Backup_Title, AppResources.Format(AppResources.Backup_SaveSkipped, skipped), AppResources.Common_Ok);
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
			return (path, await StoryBackupWriter.WriteAsync(stories, _library, output));
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
		await using FileStream file = File.OpenRead(path);
		return await StoryBackupReader.RestoreAsync(file, _library);
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
			await DisplayAlertAsync(AppResources.Backup_Title, AppResources.Backup_Newer, AppResources.Common_Ok);
		}
		catch (StoryBackupException ex)
		{
			Debug.WriteLine($"Dino Reader backup refused: {ex.Message}");
			await DisplayAlertAsync(AppResources.Backup_Title, AppResources.Backup_NotABackup, AppResources.Common_Ok);
		}
	}

	private void ShowResult(StoryRestoreResult result)
	{
		ResultTitle.Text = result.Added == 1
			? AppResources.Format(AppResources.Backup_Restored_One, result.Added)
			: AppResources.Format(AppResources.Backup_Restored_Other, result.Added);
		ResultRows.Children.Clear();
		AddResultRow(AppResources.Backup_Added, result.Added, (Color)Resources["AddedDot"]);
		AddResultRow(AppResources.Backup_Skipped, result.Skipped, (Color)Resources["SkippedDot"]);
		if (result.Damaged > 0)
		{
			AddResultRow(AppResources.Backup_Damaged, result.Damaged, DinoTokens.Get(DinoTokens.Danger));
		}

		if (result.TooLarge > 0)
		{
			AddResultRow(AppResources.Backup_TooLarge, result.TooLarge, DinoTokens.Get(DinoTokens.Danger));
		}

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
