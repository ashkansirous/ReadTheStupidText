using System.Collections.ObjectModel;
using System.ComponentModel;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Documents;
using ReadTheStupidText.Mobile;

namespace DinoReader;

/// <summary>What the page grid binds to for one page (Slice 78): its picture or text preview, its number and whether the OCR found nothing on it.</summary>
public sealed class PageTile(PageDraft page, int number) : INotifyPropertyChanged
{
	private const int PreviewLength = 120;

	private int _number = number;

	public event PropertyChangedEventHandler? PropertyChanged;

	public string Id { get; } = page.Id;

	public string? Picture { get; } = page.PictureFile;

	public bool HasPicture => Picture is not null;

	public bool IsTextOnly => !page.HasPicture;

	/// <summary>A page with a picture that was read and has no text: kept as a picture-only page, but flagged.</summary>
	public bool IsBlank { get; } = page.HasPicture && page.Ocr is not null && string.IsNullOrWhiteSpace(page.Text);

	public string Preview { get; } = page.Text.Length <= PreviewLength ? page.Text : page.Text[..PreviewLength].TrimEnd() + "…";

	public string Description => AppResources.Format(AppResources.Wizard_PageN, Number);

	public int Number
	{
		get => _number;
		set
		{
			_number = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Number)));
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
		}
	}
}

/// <summary>
/// Step 1 of the wizard (Slice 78): the thumbnail grid. Drag to reorder (<c>CanReorderItems</c>), "•••" on a tile to rotate, crop or
/// delete, and "Add pages" for a scan (Android), photos, a PDF or file, or typed text. Every picture is copied into the wizard's
/// work folder first, so rotating and cropping never touch the user's own files.
/// </summary>
public partial class PagesStep : ContentView
{
	private const string PdfExtension = ".pdf";
	private const double TileAspect = 4.0 / 3.0;
	private const double MinimumTileWidth = 150;
	private const int MinimumColumns = 3;
	private const double SlotThumbWidth = 58;
	private const double ThumbAspect = 3.0 / 4.0;
	private const string SlotDashColour = "#c9c0f5";
	private const string SlotMenuGlyph = "•••";
	private const string TextExtension = ".txt";

	private static readonly FilePickerFileType SupportedFileTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
	{
		{ DevicePlatform.Android, ["text/plain", "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"] },
		{ DevicePlatform.iOS, ["public.plain-text", "com.adobe.pdf", "org.openxmlformats.wordprocessingml.document"] },
		{ DevicePlatform.WinUI, [".txt", ".pdf", ".docx"] },
	});

	private readonly ObservableCollection<PageTile> _tiles = [];
	private readonly BusyGuard _busy = new();
	private WizardSession? _session;
	private Action? _edited;

	public PagesStep()
	{
		InitializeComponent();
		PageGrid.ItemsSource = _tiles;
		SizeChanged += (_, _) => FitColumns();
	}

	/// <summary>Connects the step to the wizard's session. Called once.</summary>
	public void Attach(WizardSession session, Action edited)
	{
		_session = session;
		_edited = edited;
		AddSourceTile(IconKind.Camera, AppResources.Add_Scan, AppResources.Add_ScanSub, 0, 0, HasScanner ? ScanAsync : null);
		AddSourceTile(IconKind.Photos, AppResources.Add_Photos, AppResources.Add_PhotosSub, 1, 0, PhotosAsync);
		AddSourceTile(IconKind.File, AppResources.Add_File, AppResources.Add_FileSub, 0, 1, FileAsync);
		AddSourceTile(IconKind.Type, AppResources.Add_Type, AppResources.Add_TypeSub, 1, 1, TypeAsync);
	}

	// Windows has no document scanner (Slice 67): its Scan tile stays, dimmed and inert; Photos reads pictures there.
	private bool HasScanner => _session!.Services.GetService<IDocumentScanner>() is not null;

	private StoryDraft Draft => _session!.Draft;

	/// <summary>Shows the draft's pages (or the empty state).</summary>
	public void Refresh()
	{
		if (_session is null)
		{
			return;
		}

		_tiles.Clear();
		for (int i = 0; i < Draft.PageDrafts.Count; i++)
		{
			_tiles.Add(new PageTile(Draft.PageDrafts[i], i + 1));
		}

		EmptyState.IsVisible = _tiles.Count == 0;
		PagesState.IsVisible = _tiles.Count > 0;
		ShowPageList();
		_edited?.Invoke();
	}

	// Slice 98: a text-only (or picture + text) story shows one optional picture slot per page instead of the thumbnail grid.
	// The wizard's own record of which pages are picture + text pages is what counts: a rotate or crop clears a page's reading
	// (so it no longer looks like one), and a scanned page the grown-up typed text on never was one.
	private bool UsesSlots =>
		Draft.PageDrafts.Count > 0 && Draft.PageDrafts.All(p => !p.HasPicture || _session!.PictureTextPages.Contains(p.Id));

	private void ShowPageList()
	{
		bool slots = UsesSlots;
		SlotList.IsVisible = slots;
		PageGrid.IsVisible = !slots;
		FoundLine.IsVisible = slots && Draft.PageDrafts.Count > 0 && Draft.PageDrafts.All(p => p.HasPicture);
		if (slots)
		{
			NoPicturesCard.IsVisible = Draft.PageDrafts.Any(p => !p.HasPicture);
			SlotRows.Children.Clear();
			for (int i = 0; i < Draft.PageDrafts.Count; i++)
			{
				SlotRows.Children.Add(SlotRow(Draft.PageDrafts[i], i));
			}
		}
	}

	private Border SlotRow(PageDraft page, int index)
	{
		var grid = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)], ColumnSpacing = 10 };
		grid.Add(SlotThumbnail(page, index), 0);
		grid.Add(SlotText(page, index), 1);
		grid.Add(SlotAction(page, index), 2);
		grid.Add(SlotMenu(page, index), 3);
		var row = new Border
		{
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			Padding = 10,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
			Content = grid,
		};
		var edit = new TapGestureRecognizer();
		edit.Tapped += async (_, _) => await _busy.RunAsync(() => OpenPageAsync(new PageTile(page, index + 1)));
		row.GestureRecognizers.Add(edit);
		return row;
	}

	// The picture is decoded at the thumbnail's own size (and kept), not at full resolution, so a long book stays light.
	private static readonly Dictionary<string, byte[]> ThumbnailCache = [];
	private const int ThumbDecodeScale = 3;

	private static Image ThumbnailImage(string file)
	{
		var image = new Image { Aspect = Aspect.AspectFill };
		_ = LoadThumbnailAsync(image, file);
		return image;
	}

	private static async Task LoadThumbnailAsync(Image image, string file)
	{
		try
		{
			byte[] bytes = ThumbnailCache.TryGetValue(file, out byte[]? cached) ? cached : await Task.Run(() => DecodeThumbnail(file));
			ThumbnailCache[file] = bytes;
			image.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
		{
			image.Source = file;
		}
	}

	private static byte[] DecodeThumbnail(string file)
	{
		using var stream = File.OpenRead(file);
		Microsoft.Maui.Graphics.IImage original = Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(stream);
		using Microsoft.Maui.Graphics.IImage small = original.Downsize((float)(SlotThumbWidth * ThumbDecodeScale), (float)(SlotThumbWidth * ThumbDecodeScale / ThumbAspect), true);
		using var output = new MemoryStream();
		small.Save(output, Microsoft.Maui.Graphics.ImageFormat.Jpeg, 0.8f);
		return output.ToArray();
	}

	private View SlotThumbnail(PageDraft page, int index)
	{
		View content = page.PictureFile is { } file
			? ThumbnailImage(file)
			: new IconView { Kind = IconKind.Plus, Color = DinoTokens.Get(DinoTokens.Brand), Size = 18, StrokeWidth = 2.6, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		var thumb = new Border
		{
			WidthRequest = SlotThumbWidth,
			HeightRequest = SlotThumbWidth / ThumbAspect,
			StrokeThickness = page.HasPicture ? 0 : 2,
			Stroke = Color.FromArgb(SlotDashColour),
			StrokeDashArray = [3, 2],
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 9 },
			Content = content,
			VerticalOptions = LayoutOptions.Center,
		};
		SemanticProperties.SetDescription(thumb, AppResources.Format(AppResources.Wizard_PageN, index + 1));
		thumb.GestureRecognizers.Add(PickTap(index));
		return thumb;
	}

	private static View SlotText(PageDraft page, int index) => new VerticalStackLayout
	{
		VerticalOptions = LayoutOptions.Center,
		Spacing = 2,
		Children =
		{
			new Label { Text = AppResources.Format(AppResources.Pictures_PageN, index + 1), FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Muted) },
			new Label { Text = page.Text.Trim(), FontFamily = DinoFonts.ReadingForText(page.Text), FlowDirection = UiFlow.ForText(page.Text), FontSize = 13.5, LineHeight = 1.35, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation, TextColor = DinoTokens.Get(DinoTokens.Ink) },
		},
	};

	private View SlotAction(PageDraft page, int index)
	{
		var action = new Label
		{
			Text = page.HasPicture ? AppResources.Pictures_Change : AppResources.Pictures_Add,
			FontSize = 13.5,
			FontAttributes = FontAttributes.Bold,
			TextColor = DinoTokens.Get(DinoTokens.Brand),
			VerticalOptions = LayoutOptions.Center,
			VerticalTextAlignment = TextAlignment.Center,
			HeightRequest = 44,
		};
		action.GestureRecognizers.Add(PickTap(index));
		return action;
	}

	// The page options a slot row keeps from the grid: edit the text, rotate and crop a picture, and delete the page (reordering is not offered here).
	private View SlotMenu(PageDraft page, int index)
	{
		var menu = new Border
		{
			WidthRequest = 44,
			HeightRequest = 44,
			StrokeThickness = 0,
			BackgroundColor = Colors.Transparent,
			VerticalOptions = LayoutOptions.Center,
			Content = new Label { Text = SlotMenuGlyph, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		SemanticProperties.SetDescription(menu, AppResources.Wizard_PageMenu);
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) => await _busy.RunAsync(() => ShowSlotMenuAsync(new PageTile(page, index + 1)));
		menu.GestureRecognizers.Add(tap);
		return menu;
	}

	private async Task ShowSlotMenuAsync(PageTile tile)
	{
		int index = IndexOf(tile);
		if (index < 0)
		{
			return;
		}

		// Rotate and crop come from the grid: the page keeps its text and stays picture + text (KeepPictureText on the next read).
		string[] options = Draft.PageDrafts[index].HasPicture
			? [AppResources.Wizard_MenuEditText, AppResources.Wizard_MenuRotate, AppResources.Wizard_MenuCrop]
			: [AppResources.Wizard_MenuEditText];
		string picked = await _session!.Host.DisplayActionSheetAsync(tile.Description, AppResources.Common_Cancel, AppResources.Common_Delete, options);
		if (picked == AppResources.Common_Delete)
		{
			Draft.RemovePageDraft(index);
			Refresh();
		}
		else if (picked == AppResources.Wizard_MenuEditText)
		{
			await _session.Host.Navigation.PushAsync(new PageTextPage(Draft, index));
		}
		else if (picked == AppResources.Wizard_MenuRotate)
		{
			await RotateAsync(index);
		}
		else if (picked == AppResources.Wizard_MenuCrop)
		{
			await _session.Host.Navigation.PushAsync(new PageEditorPage(_session, Draft.PageDrafts[index]));
		}
	}

	private TapGestureRecognizer PickTap(int index)
	{
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) => await _busy.RunAsync(() => PickAndAssignAsync([index]));
		return tap;
	}

	private async void OnPickAllTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(PickAllAsync);

	private Task PickAllAsync() =>
		PickAndAssignAsync(Enumerable.Range(0, Draft.PageDrafts.Count).Where(i => !Draft.PageDrafts[i].HasPicture).ToList());

	// One picker call for the given pages; the pictures go to them in natural file-name order (1, 2 ... 10).
	private async Task PickAndAssignAsync(IReadOnlyList<int> pages)
	{
		if (pages.Count == 0)
		{
			return;
		}

		// The pages are remembered by id: one may be deleted or moved while the system picker is open.
		var ids = pages.Where(i => i >= 0 && i < Draft.PageDrafts.Count).Select(i => Draft.PageDrafts[i].Id).ToList();
		try
		{
			var external = _session!.Services.GetRequiredService<ExternalActivityGuard>();
			List<FileResult>? picked = await external.RunAsync(() => MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = pages.Count }));
			if (picked is { Count: > 0 } && _session.Host.Parent is not null)
			{
				await AssignPicturesAsync(ids, picked.Select(p => p.FullPath).ToList());
			}
		}
		catch (Exception ex) when (ex is PermissionException or FeatureNotSupportedException)
		{
			await ShowPermissionAsync();
		}
	}

	private async Task AssignPicturesAsync(IReadOnlyList<string> pageIds, IReadOnlyList<string> locations)
	{
		var import = _session!.Services.GetRequiredService<StoryImport>();
		var targets = pageIds.Select(id => Draft.PageDrafts.FirstOrDefault(p => p.Id == id)).OfType<PageDraft>().ToList();
		IReadOnlyList<string?> assigned = PictureTextBook.FillEmptySlots(targets.Select(_ => (string?)null).ToList(), locations);
		var used = new HashSet<string>();
		int failed = 0;
		for (int i = 0; i < targets.Count && assigned[i] is { } location; i++)
		{
			used.Add(location);
			if (await TryCopyAsync(import, location) is { } copy)
			{
				DeleteWorkCopies(targets[i]);
				Draft.ReplacePageDraft(PictureTextBook.WithPicture(targets[i], copy));
				_session.PictureTextPages.Add(targets[i].Id);
			}
			else
			{
				failed++;
			}
		}

		// A picker that ignores SelectionLimit hands back more than asked for: the extras are not used, so their cache files go.
		foreach (string extra in locations.Where(l => !used.Contains(l)))
		{
			PickedPhotoCache.Delete(extra);
		}

		Refresh();
		if (failed > 0)
		{
			await ShowReadFailedAsync();
		}
	}

	// A replaced picture's copies in the wizard's work folder are of no use any more (only files in that folder, never the user's own).
	private void DeleteWorkCopies(PageDraft replaced)
	{
		string[] own = new[] { replaced.SourceFile, replaced.TransformedFile }
			.OfType<string>()
			.Where(f => f.StartsWith(Path.TrimEndingDirectorySeparator(_session!.WorkFolder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
			.ToArray();
		StoryImport.DeleteLocations(own);
	}

	private async Task<string?> TryCopyAsync(StoryImport import, string location)
	{
		try
		{
			return await import.CopyIntoAsync(location, _session!.WorkFolder);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			DinoLog.Error(_session!.Services, "could not copy a picked picture", ex);
			return null;
		}
		finally
		{
			PickedPhotoCache.Delete(location);
		}
	}

	// Phones show three columns; a wide window (Windows) shows as many as fit.
	private void FitColumns()
	{
		if (Width > 0)
		{
			GridLayout.Span = Math.Max(MinimumColumns, (int)(Width / MinimumTileWidth));
		}
	}

	private void OnTileSizeChanged(object? sender, EventArgs e)
	{
		var tile = (VisualElement)sender!;
		double height = tile.Width * TileAspect;
		if (tile.Width > 0 && Math.Abs(tile.HeightRequest - height) > 0.5)
		{
			tile.HeightRequest = height;
		}
	}

	private void AddSourceTile(IconKind icon, string title, string subtitle, int column, int row, Func<Task>? action)
	{
		var tile = new Border
		{
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			Padding = 14,
			MinimumHeightRequest = 104,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
			Content = SourceTileContent(icon, title, subtitle),
			Opacity = action is null ? 0.45 : 1,
		};
		SemanticProperties.SetDescription(tile, title);
		if (action is not null)
		{
			var tap = new TapGestureRecognizer();
			tap.Tapped += async (_, _) => await _busy.RunAsync(action);
			tile.GestureRecognizers.Add(tap);
		}

		SourceGrid.Add(tile, column, row);
	}

	private static VerticalStackLayout SourceTileContent(IconKind icon, string title, string subtitle)
	{
		var chip = new Border
		{
			WidthRequest = 42,
			HeightRequest = 42,
			BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
			StrokeThickness = 0,
			HorizontalOptions = LayoutOptions.Start,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
			Content = new IconView { Kind = icon, Color = DinoTokens.Get(DinoTokens.Brand), Size = 22 },
		};
		return new VerticalStackLayout
		{
			Spacing = 8,
			Children =
			{
				chip,
				new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold },
				new Label { Text = subtitle, FontSize = 11.5, TextColor = DinoTokens.Get(DinoTokens.Muted) },
			},
		};
	}

	private async void OnAddTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(ChooseSourceAsync);

	private async Task ChooseSourceAsync()
	{
		var choices = new List<(string Label, Func<Task> Action)>();
		if (HasScanner)
		{
			choices.Add((AppResources.Add_Scan, ScanAsync));
		}

		choices.Add((AppResources.Add_Photos, PhotosAsync));
		choices.Add((AppResources.Add_File, FileAsync));
		choices.Add((AppResources.Add_Type, TypeAsync));
		string picked = await _session!.Host.DisplayActionSheetAsync(AppResources.Wizard_AddPages, AppResources.Common_Cancel, null, choices.Select(c => c.Label).ToArray());
		if (choices.FirstOrDefault(c => c.Label == picked).Action is { } action)
		{
			await action();
		}
	}

	private async Task ScanAsync()
	{
		if (_session!.Services.GetService<IDocumentScanner>() is not { } scanner)
		{
			await ShowReadFailedAsync();
			return;
		}

		try
		{
			var external = _session.Services.GetRequiredService<ExternalActivityGuard>();
			await AddPicturesAsync(await external.RunAsync(scanner.ScanAsync), scannerOutput: true);
		}
		catch (Exception ex) when (ex is DocumentScannerPreparingException or DocumentScannerStartFailedException
			or DocumentScannerUnavailableException)
		{
			await ShowReadFailedAsync();
		}
	}

	private async Task PhotosAsync()
	{
		try
		{
			var external = _session!.Services.GetRequiredService<ExternalActivityGuard>();
			List<FileResult> photos = await external.RunAsync(() => MediaPicker.Default.PickPhotosAsync());
			await AddPicturesAsync(photos.Select(p => p.FullPath).ToList(), scannerOutput: false);
		}
		catch (Exception ex) when (ex is PermissionException or FeatureNotSupportedException)
		{
			await ShowPermissionAsync();
		}
	}

	// Each pick is copied (upright, JPEG) into the work folder; the scanner's or picker's own file is then removed if it was only a
	// temporary one (a photo outside the app cache is the user's and stays).
	private async Task AddPicturesAsync(IReadOnlyList<string> locations, bool scannerOutput)
	{
		if (locations.Count == 0)
		{
			return;
		}

		var import = _session!.Services.GetRequiredService<StoryImport>();
		int failed = 0;
		foreach (string location in locations)
		{
			failed += await TryAddPictureAsync(import, location, scannerOutput) ? 0 : 1;
		}

		Refresh();
		if (failed > 0)
		{
			await ShowReadFailedAsync();
		}
	}

	private async Task<bool> TryAddPictureAsync(StoryImport import, string location, bool scannerOutput)
	{
		try
		{
			string copy = await import.CopyIntoAsync(location, _session!.WorkFolder);
			Draft.AddPageDraft(PageDraft.ForPicture(copy));
			return true;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			DinoLog.Error(_session!.Services, "could not copy a picked picture", ex);
			return false;
		}
		finally
		{
			DeleteTemporary(location, scannerOutput);
		}
	}

	private static void DeleteTemporary(string location, bool scannerOutput)
	{
		if (scannerOutput)
		{
			ScannedImageFiles.TryDelete(location);
		}
		else
		{
			PickedPhotoCache.Delete(location);
		}
	}

	private async Task FileAsync()
	{
		FileResult? file = await PickFileAsync();
		// Parent is null once the app reset to the gallery during a long pick: discard the pick.
		if (file is null || _session!.Host.Parent is null)
		{
			return;
		}

		string name = Path.GetFileNameWithoutExtension(file.FileName);
		if (file.FileName.EndsWith(PdfExtension, StringComparison.OrdinalIgnoreCase) && await TryAddPdfAsync(file.FullPath, name))
		{
			return;
		}

		string? text = await ExtractAsync(file.FullPath);
		if (text is not null)
		{
			UseTitleIfBlank(name);
			_session.FileName = file.FileName;
			// Only a plain .txt is cut at "---" lines: a PDF or DOCX page may hold one as a rule or a table border.
			bool plain = file.FileName.EndsWith(TextExtension, StringComparison.OrdinalIgnoreCase);
			IReadOnlyList<string> parts = plain ? PictureTextBook.ImportPages(text) : [text];
			Draft.AddPageDrafts(parts.Select(PageDraft.ForText));
			Refresh();
		}
	}

	private void UseTitleIfBlank(string title)
	{
		if (string.IsNullOrWhiteSpace(Draft.Title))
		{
			Draft.Title = title;
		}
	}

	// Every PDF page becomes a picture page like a photo; a page the PDF already gave text for (its text layer) carries that
	// reading, so it is not read again. The document's own paragraphs (cut per page) are the page text when they match its lines.
	private async Task<bool> TryAddPdfAsync(string path, string title)
	{
		var rendered = new List<string>();
		_session!.ShowProgress(string.Empty);
		try
		{
			var pdf = _session!.Services.GetRequiredService<RenderedPdfTextExtractor>();
			IReadOnlyList<PositionedPage> pages = await pdf.ExtractPagesAsync(path);
			if (pages.Count == 0)
			{
				return false;
			}

			IReadOnlyList<string>? pageTexts = await FlowingPageTextsAsync(path, pages);
			IReadOnlyDictionary<int, string> renders = await pdf.RenderPagesAsync(path, pages.Where(p => p.ImagePath is null).Select(p => p.Index).ToList());
			List<PageDraft> drafts = await PdfDraftsAsync(pages, renders, pageTexts, rendered);
			UseTitleIfBlank(title);
			_session.FileName = Path.GetFileName(path);
			Draft.AddPageDrafts(drafts);
			Refresh();
			return true;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			DinoLog.Error(_session!.Services, "import failed", ex);
			return false;
		}
		finally
		{
			_session.ShowProgress(null);
			StoryImport.DeleteLocations(rendered);
		}
	}

	private async Task<List<PageDraft>> PdfDraftsAsync(
		IReadOnlyList<PositionedPage> pages, IReadOnlyDictionary<int, string> renders, IReadOnlyList<string>? pageTexts, List<string> rendered)
	{
		var import = _session!.Services.GetRequiredService<StoryImport>();
		var drafts = new List<PageDraft>();
		for (int i = 0; i < pages.Count; i++)
		{
			string? image = pages[i].ImagePath ?? renders.GetValueOrDefault(pages[i].Index);
			if (image is null)
			{
				drafts.Add(PageDraft.ForText(pageTexts?[i] ?? ImportedText.PageText(pages[i].Lines.Select(l => l.Text))));
				continue;
			}

			rendered.Add(image);
			_session.ShowProgress(AppResources.Format(AppResources.Wizard_Reading, i + 1, pages.Count));
			string copy = await import.CopyIntoAsync(image, _session.WorkFolder);
			int at = i;
			drafts.Add(await Task.Run(() => StoryImport.DraftFromPositioned(pages[at], copy, pageTexts?[at])));
		}

		return drafts;
	}

	// A text-layer PDF reads as the document's own paragraphs, cut per page by where each page's lines sit in it. A PDF with any
	// scanned page, or text that does not match its lines, reads as the recognised lines instead.
	private async Task<IReadOnlyList<string>?> FlowingPageTextsAsync(string path, IReadOnlyList<PositionedPage> pages)
	{
		if (pages.Any(p => p.ImagePath is not null))
		{
			return null;
		}

		try
		{
			string flowing = await _session!.Services.GetRequiredService<IDocumentTextExtractor>().ExtractTextAsync(path);
			var lines = pages.Select(p => (IReadOnlyList<string>)p.Lines.Select(l => l.Text).ToList()).ToList();
			return ImportedText.SplitByPages(flowing, lines);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			DinoLog.Error(_session!.Services, "could not read the PDF text", ex);
			return null;
		}
	}

	private async Task<FileResult?> PickFileAsync()
	{
		try
		{
			// The picker stops the window; the guard keeps the grown-up pages open meanwhile.
			var external = _session!.Services.GetRequiredService<ExternalActivityGuard>();
			return await external.RunAsync(() => FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = AppResources.Add_PickerTitle,
				FileTypes = SupportedFileTypes,
			}));
		}
		catch (PermissionException)
		{
			await ShowPermissionAsync();
			return null;
		}
	}

	private async Task<string?> ExtractAsync(string path)
	{
		try
		{
			return await _session!.Services.GetRequiredService<IDocumentTextExtractor>().ExtractTextAsync(path);
		}
		catch (DocumentTooLargeException ex)
		{
			await _session!.Host.DisplayAlertAsync(AppResources.Add_FileTooLargeTitle, AppResources.Format(AppResources.Add_FileTooLargeBody, ex.Actual, ex.Limit), AppResources.Common_Ok);
		}
		catch (Exception)
		{
			await ShowReadFailedAsync();
		}

		return null;
	}

	private Task TypeAsync() => _session!.Host.Navigation.PushAsync(new PageTextPage(Draft, PageTextPage.NewPage));

	private Task ShowReadFailedAsync() =>
		_session!.Host.DisplayAlertAsync(AppResources.Add_ReadFailedTitle, AppResources.Add_ReadFailedBody, AppResources.Common_Ok);

	private Task ShowPermissionAsync() =>
		_session!.Host.DisplayAlertAsync(AppResources.Add_PermissionTitle, AppResources.Add_PermissionBody, AppResources.Common_Ok);

	private async void OnReordered(object? sender, EventArgs e)
	{
		await Task.Yield();
		Draft.ReorderTo(_tiles.Select(t => t.Id).ToList());
		for (int i = 0; i < _tiles.Count; i++)
		{
			_tiles[i].Number = i + 1;
		}
	}

	private static PageTile? TileOf(object? sender) => (sender as BindableObject)?.BindingContext as PageTile;

	private async void OnTileTapped(object? sender, TappedEventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await _busy.RunAsync(() => OpenPageAsync(tile));
		}
	}

	private async void OnMenuTapped(object? sender, TappedEventArgs e)
	{
		if (TileOf(sender) is { } tile)
		{
			await _busy.RunAsync(() => ShowMenuAsync(tile));
		}
	}

	private Task OpenPageAsync(PageTile tile)
	{
		int index = IndexOf(tile);
		if (index < 0)
		{
			return Task.CompletedTask;
		}

		INavigation navigation = _session!.Host.Navigation;
		return Draft.PageDrafts[index].HasPicture
			? navigation.PushAsync(new PageEditorPage(_session, Draft.PageDrafts[index]))
			: navigation.PushAsync(new PageTextPage(Draft, index));
	}

	private async Task ShowMenuAsync(PageTile tile)
	{
		int index = IndexOf(tile);
		if (index < 0)
		{
			return;
		}

		bool picture = Draft.PageDrafts[index].HasPicture;
		string[] options = picture
			? [AppResources.Wizard_MenuRotate, AppResources.Wizard_MenuCrop]
			: [AppResources.Wizard_MenuEditText];
		string picked = await _session!.Host.DisplayActionSheetAsync(tile.Description, AppResources.Common_Cancel, AppResources.Common_Delete, options);
		if (picked == AppResources.Common_Delete)
		{
			Draft.RemovePageDraft(index);
			Refresh();
		}
		else if (picked == AppResources.Wizard_MenuRotate)
		{
			await RotateAsync(index);
		}
		else if (picked == AppResources.Wizard_MenuCrop || picked == AppResources.Wizard_MenuEditText)
		{
			await OpenPageAsync(tile);
		}
	}

	private int IndexOf(PageTile tile) => Draft.PageDrafts.FindIndex(p => p.Id == tile.Id);

	private async Task RotateAsync(int index)
	{
		try
		{
			PageDraft turned = await _session!.RenderAsync(Draft.PageDrafts[index].Rotated(1));
			Draft.ReplacePageDraft(turned);
			Refresh();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			DinoLog.Error(_session!.Services, "could not rotate a page", ex);
			await ShowReadFailedAsync();
		}
	}
}
