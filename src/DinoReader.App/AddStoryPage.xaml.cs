using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Documents;
using ReadTheStupidText.Mobile;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>
/// Adds a story from typed or pasted text, or from a .txt/.pdf/.docx file. The text is cast once
/// here (who speaks, which voice); the grown-up sees the result and nothing is stored until Save.
/// </summary>
public partial class AddStoryPage : ContentPage, IGrownUpPage
{
	private const int DebounceMilliseconds = 500;
	private const string PdfExtension = ".pdf";

	private static readonly FilePickerFileType SupportedFileTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
	{
		{ DevicePlatform.Android, ["text/plain", "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"] },
		{ DevicePlatform.iOS, ["public.plain-text", "com.adobe.pdf", "org.openxmlformats.wordprocessingml.document"] },
		{ DevicePlatform.WinUI, [".txt", ".pdf", ".docx"] },
	});

	private readonly IStoryLibrary _library;
	private readonly StoryVoicing _voicing;
	private readonly IDocumentTextExtractor _documents;
	private readonly ExternalActivityGuard _external;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly StoryTranslator _translator;
	private readonly LanguageCard _languageCard;
	private string _language = StoryTranslator.MyLanguage;
	private bool _languageDetected;
	private bool _languageChosen;
	private string _narratorVoiceId = SupertonicVoiceTable.Default.Id;
	private IReadOnlyList<CastMember> _cast = [];
	private IReadOnlyDictionary<string, string> _merges = Story.NoMerges;
	private CancellationTokenSource? _analysis;

	// Pages of a scan, photo set or PDF, and the text they gave: kept with the story while the text is unedited.
	private IReadOnlyList<PositionedPage>? _imported;
	private string _importedText = string.Empty;
	private string? _pdfPath;
	private IReadOnlyList<string>? _importedPageTexts;

	public AddStoryPage(IStoryLibrary library, StoryVoicing voicing, IDocumentTextExtractor documents, ExternalActivityGuard external, IServiceProvider services, StoryTranslator translator)
	{
		InitializeComponent();
		Header.Padding = UiFlow.Mirror(Header.Padding);
		_translator = translator;
		_library = library;
		_voicing = voicing;
		_documents = documents;
		_external = external;
		_services = services;
		// Windows has no document scanner (Slice 67): its Scan tile stays, dimmed and inert; Photos reads pictures there.
		AddSourceTile(IconKind.Camera, AppResources.Add_Scan, AppResources.Add_ScanSub, 0, 0, services.GetService<IDocumentScanner>() is null ? null : OnScanTapped);
		AddSourceTile(IconKind.Photos, AppResources.Add_Photos, AppResources.Add_PhotosSub, 1, 0, OnPhotosTapped);
		AddSourceTile(IconKind.File, AppResources.Add_File, AppResources.Add_FileSub, 0, 1, OnFileTapped);
		AddSourceTile(IconKind.Type, AppResources.Add_Type, AppResources.Add_TypeSub, 1, 1, OnTypeTapped);
		_languageCard = new LanguageCard(_translator.IsAvailable, () => _busy.RunAsync(ChooseLanguageAsync), _busy);
		_languageCard.Show(_language, detected: false);
		LanguageHost.Children.Add(_languageCard);
	}

	private void AddSourceTile(IconKind icon, string title, string subtitle, int column, int row, EventHandler<TappedEventArgs>? tapped)
	{
		var tile = new Border
		{
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			Padding = 14,
			MinimumHeightRequest = 104,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
			Content = SourceTileContent(icon, title, subtitle),
			Opacity = tapped is null ? 0.45 : 1,
		};
		SemanticProperties.SetDescription(tile, title);
		if (tapped is not null)
		{
			var tap = new TapGestureRecognizer();
			tap.Tapped += tapped;
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

	private async void OnBackTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(LeaveAsync);

	private async Task LeaveAsync()
	{
		DiscardImported();
		await Navigation.PopAsync();
	}

	// The page is gone for good (popped, system back), not just covered by the cast page: its pictures go too.
	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		if (!Navigation.NavigationStack.Contains(this))
		{
			DiscardImported();
		}
	}

	private void OnTypeTapped(object? sender, TappedEventArgs e)
	{
		TextCard.IsVisible = true;
		TextEditor.Focus();
	}

	private async void OnFileTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(ImportFileAsync);

	private async Task ImportFileAsync()
	{
		FileResult? file = await PickFileAsync();
		// Parent is null once the app reset to the gallery during a long pick: discard the pick.
		if (file is null || Parent is null)
		{
			return;
		}

		// The previous import goes before the new one is extracted: both render into the same shared page folder.
		DiscardImported();
		if (file.FileName.EndsWith(PdfExtension, StringComparison.OrdinalIgnoreCase) && await TryImportPdfAsync(file.FullPath))
		{
			return;
		}

		string? text = await ExtractAsync(file.FullPath);
		if (text is not null)
		{
			TitleEntry.Text = Path.GetFileNameWithoutExtension(file.FileName);
			TextEditor.Text = text;
			TextCard.IsVisible = true;
		}
	}

	private async void OnScanTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(ScanAsync);

	private async void OnPhotosTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(PickPhotosAsync);

	private async Task ScanAsync()
	{
		if (_services.GetService<IDocumentScanner>() is not { } scanner)
		{
			await ShowReadFailedAsync();
			return;
		}

		try
		{
			await ReadPicturesAsync(await _external.RunAsync(scanner.ScanAsync));
		}
		catch (Exception ex) when (ex is DocumentScannerPreparingException or DocumentScannerStartFailedException
			or DocumentScannerUnavailableException)
		{
			await ShowReadFailedAsync();
		}
	}

	private async Task PickPhotosAsync()
	{
		try
		{
			List<FileResult> photos = await _external.RunAsync(() => MediaPicker.Default.PickPhotosAsync());
			await ReadPicturesAsync(photos.Select(p => p.FullPath).ToList());
		}
		catch (Exception ex) when (ex is PermissionException or FeatureNotSupportedException)
		{
			await DisplayAlertAsync(AppResources.Add_PermissionTitle, AppResources.Add_PermissionBody, AppResources.Common_Ok);
		}
	}

	private async Task ReadPicturesAsync(IReadOnlyList<string> locations)
	{
		if (locations.Count == 0)
		{
			return;
		}

		if (Parent is null)
		{
			StoryImport.DeleteLocations(locations);
			return;
		}

		DiscardImported();
		try
		{
			var ocr = _services.GetRequiredService<OcrPositionedTextExtractor>();
			IReadOnlyList<PositionedPage> read = await StoryImport.ReadPicturesAsync(ocr, locations);
			IReadOnlyList<PositionedPage> withText = StoryImport.WithText(read);
			StoryImport.DeleteSources(read.Except(withText));
			ShowImported(withText, null, null, null);
		}
		catch (Exception ex)
		{
			LogReadFailure("import failed", ex);
			await ShowReadFailedAsync();
		}
	}

	// A Windows text box hands its line breaks back as a lone CR; Core only knows LF, so normalise where the text is read.
	private string EditorText() => LineBreaks.ToLf(TextEditor.Text);

	// Exception only, never the picked text: a swallowed failure must still leave a trace in the system log.
	private void LogReadFailure(string message, Exception error) => DinoLog.Error(_services, message, error);

	// A PDF keeps its page pictures; they are rendered when the story is saved, so a pick that is never saved costs nothing.
	private async Task<bool> TryImportPdfAsync(string path)
	{
		IReadOnlyList<PositionedPage> moved = [];
		try
		{
			var pdf = _services.GetRequiredService<RenderedPdfTextExtractor>();
			IReadOnlyList<PositionedPage> pages = await pdf.ExtractPagesAsync(path);
			IReadOnlyList<PositionedPage> withText = StoryImport.WithText(pages);
			StoryImport.DeleteSources(pages.Except(withText));
			// Own folder at once: the next import renders into the shared folder under the same names.
			withText = moved = StoryImport.MoveToOwnFolder(withText);
			(string? flowing, IReadOnlyList<string>? pageTexts) = await FlowingTextAsync(path, withText);
			ShowImported(withText, Path.GetFileNameWithoutExtension(path), flowing, pageTexts);
			_pdfPath = _imported is null ? null : path;
			if (_imported is null)
			{
				StoryImport.DeleteSources(moved);
			}

			return _imported is not null;
		}
		catch (Exception ex)
		{
			LogReadFailure("import failed", ex);
			StoryImport.DeleteSources(moved);
			return false;
		}
	}

	// A text-layer PDF reads as the document's own paragraphs (the normal text path), cut per page by where each page's
	// lines sit in it; the lines only place the boxes. A PDF with any scanned page, or text that does not match its
	// lines, reads as the recognised lines instead.
	private async Task<(string? Text, IReadOnlyList<string>? PageTexts)> FlowingTextAsync(string path, IReadOnlyList<PositionedPage> pages)
	{
		if (pages.Count == 0 || pages.Any(p => p.ImagePath is not null))
		{
			return (null, null);
		}

		try
		{
			string flowing = await _documents.ExtractTextAsync(path);
			var lines = pages.Select(p => (IReadOnlyList<string>)p.Lines.Select(l => l.Text).ToList()).ToList();
			IReadOnlyList<string>? cut = ImportedText.SplitByPages(flowing, lines);
			return cut is null ? (null, null) : (flowing, cut);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogReadFailure("could not read the PDF text", ex);
			return (null, null);
		}
	}

	// Renders the pages that have no picture yet (the PDF's text-layer pages); a failure leaves them as text only.
	private async Task<IReadOnlyList<PositionedPage>> WithPicturesAsync(IReadOnlyList<PositionedPage> pages)
	{
		var missing = pages.Where(p => p.ImagePath is null).Select(p => p.Index).ToList();
		if (_pdfPath is null || missing.Count == 0)
		{
			return pages;
		}

		try
		{
			var pdf = _services.GetRequiredService<RenderedPdfTextExtractor>();
			IReadOnlyDictionary<int, string> images = await pdf.RenderPagesAsync(_pdfPath, missing);
			return pages.Select(p => p.ImagePath is null && images.TryGetValue(p.Index, out string? image)
				? p with { ImagePath = image } : p).ToList();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			LogReadFailure("could not render the PDF pages", ex);
			return pages;
		}
	}

	// Forgets the import and deletes its temporary pictures (replaced, abandoned or saved).
	private void DiscardImported()
	{
		if (_imported is not null)
		{
			StoryImport.DeleteSources(_imported);
		}

		_imported = null;
		_pdfPath = null;
		_importedPageTexts = null;
	}

	private void ShowImported(IReadOnlyList<PositionedPage> pages, string? title, string? flowingText, IReadOnlyList<string>? pageTexts)
	{
		DiscardImported();
		if (pages.Count == 0)
		{
			return;
		}

		_imported = pages;
		_importedPageTexts = pageTexts;
		_importedText = flowingText ?? StoryImport.TextOf(pages);
		if (title is not null)
		{
			TitleEntry.Text = title;
		}

		TextEditor.Text = _importedText;
		TextCard.IsVisible = true;
	}

	private Task ShowReadFailedAsync() =>
		DisplayAlertAsync(AppResources.Add_ReadFailedTitle, AppResources.Add_ReadFailedBody, AppResources.Common_Ok);

	private async Task<FileResult?> PickFileAsync()
	{
		try
		{
			// The picker stops the window; the guard keeps the grown-up pages open meanwhile.
			return await _external.RunAsync(() => FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = AppResources.Add_PickerTitle,
				FileTypes = SupportedFileTypes,
			}));
		}
		catch (PermissionException)
		{
			await DisplayAlertAsync(AppResources.Add_PermissionTitle, AppResources.Add_PermissionBody, AppResources.Common_Ok);
			return null;
		}
	}

	private async Task<string?> ExtractAsync(string path)
	{
		try
		{
			return await _documents.ExtractTextAsync(path);
		}
		catch (DocumentTooLargeException ex)
		{
			await DisplayAlertAsync(AppResources.Add_FileTooLargeTitle, AppResources.Format(AppResources.Add_FileTooLargeBody, ex.Actual, ex.Limit), AppResources.Common_Ok);
		}
		catch (Exception)
		{
			await DisplayAlertAsync(AppResources.Add_ReadFailedTitle, AppResources.Add_ReadFailedBody, AppResources.Common_Ok);
		}

		return null;
	}

	// Casting waits for a pause in typing, so a long paste is analysed once, off the UI thread.
	private async void OnTextChanged(object? sender, TextChangedEventArgs e)
	{
		_analysis?.Cancel();
		var analysis = _analysis = new CancellationTokenSource();
		try
		{
			await Task.Delay(DebounceMilliseconds, analysis.Token);
			await AnalyseAsync(EditorText(), analysis.Token);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			// The preview is a convenience: a failed analysis hides the card, it never crashes the app.
			LogReadFailure("cast preview failed", ex);
			ResultCard.IsVisible = false;
		}
	}

	private async Task AnalyseAsync(string text, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		if (string.IsNullOrWhiteSpace(text))
		{
			ResultCard.IsVisible = false;
			return;
		}

		DetectLanguage(text);
		IReadOnlyList<CastMember> cast = await _voicing.CastAsync(_language, text, _narratorVoiceId, _cast, _merges);
		token.ThrowIfCancellationRequested();
		_cast = cast;
		ShowResult(cast);
	}

	// The pages the import read while the text is still the imported one; typed or edited text is one page.
	private int ImportedPageCount() =>
		_imported is not null && ImportedText.Unchanged(EditorText(), _importedText) ? _imported.Count : 1;

	private void ShowResult(IReadOnlyList<CastMember> cast)
	{
		SummaryLabel.Text = GalleryText.ImportSummary(ImportedPageCount(), cast.Count);
		NameChips.Children.Clear();
		foreach (CastMember member in cast)
		{
			NameChips.Children.Add(NameChip(member.Name));
		}

		ResultCard.IsVisible = true;
	}

	private static Border NameChip(string name) => new()
	{
		BackgroundColor = Color.FromArgb("#1FFFFFFF"),
		StrokeThickness = 0,
		Padding = new Thickness(10, 4),
		Margin = new Thickness(0, 0, 6, 6),
		StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
		Content = new Label { Text = name, FontSize = 12.5, TextColor = Colors.White },
	};

	// The language is guessed from the text until the grown-up picks one; "· detected" shows while the guess stands.
	private void DetectLanguage(string text)
	{
		if (_languageChosen)
		{
			return;
		}

		if (ScriptLanguageGuesser.Guess(text) is not { } guess)
		{
			ClearDetected();
			return;
		}

		if (guess == _language && _languageDetected)
		{
			return;
		}

		_language = guess;
		_languageDetected = true;
		UseLanguage();
	}

	// No guess any more: the language stays as it was, but it no longer claims to be detected.
	private void ClearDetected()
	{
		if (!_languageDetected)
		{
			return;
		}

		_languageDetected = false;
		_languageCard.Show(_language, detected: false);
	}

	private async Task ChooseLanguageAsync()
	{
		if (await LanguageChooser.ChooseAsync(this) is not { } picked)
		{
			return;
		}

		_language = picked;
		_languageChosen = true;
		_languageDetected = false;
		_analysis?.Cancel();
		UseLanguage();
		string text = EditorText();
		if (!string.IsNullOrWhiteSpace(text))
		{
			_cast = await _voicing.CastAsync(_language, text, _narratorVoiceId, _cast, _merges);
			ShowResult(_cast);
		}
	}

	// Shows the language, and moves the narrator to a voice of that language's pack when it has no such voice.
	private void UseLanguage()
	{
		_languageCard.Show(_language, _languageDetected);
		if (_voicing.VoicesFor(_language).All(v => v.Id != _narratorVoiceId))
		{
			_narratorVoiceId = _voicing.NarratorFor(_language);
		}
	}

	private async void OnSaveClicked(object? sender, EventArgs e) => await _busy.RunAsync(SaveAsync);

	private async Task SaveAsync()
	{
		string text = EditorText();
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		string title = TitleOrDefault(text);
		int existing = (await _library.ListAsync()).Count;
		string id = StoryIds.New();
		try
		{
			var story = new Story(id, title, _language, DateTimeOffset.UtcNow,
				await PagesForAsync(id, text), await CastNowAsync(text), CoverStyle.Colour, existing, _narratorVoiceId, null, _merges, _languageCard.Translate);
			await _library.AddAsync(story);
		}
		catch
		{
			await TryDeleteStoryAsync(id);
			DiscardImported();
			throw;
		}

		DiscardImported();
		await Navigation.PopAsync();
	}

	// Clearing a half-saved story must never hide the error that made the save fail.
	private async Task TryDeleteStoryAsync(string id)
	{
		try
		{
			await _library.DeleteAsync(id);
		}
		catch (Exception ex)
		{
			LogReadFailure("could not remove a half-saved story", ex);
		}
	}

	// Pictures are kept only while the text is the one the import read: an edited text no longer matches the lines.
	private async Task<IReadOnlyList<StoryPage>> PagesForAsync(string id, string text)
	{
		if (_imported is null || !ImportedText.Unchanged(text, _importedText))
		{
			return [new StoryPage(text)];
		}

		_imported = await WithPicturesAsync(_imported);
		return await _services.GetRequiredService<StoryImport>().KeepAsync(_imported, id, _library, _importedPageTexts);
	}

	// The cast shown on the result card is for the text as it was when typing paused; recast if it moved on.
	private async Task<IReadOnlyList<CastMember>> CastNowAsync(string text) =>
		await _voicing.CastAsync(_language, text, _narratorVoiceId, _cast, _merges);

	private async void OnCheckCastClicked(object? sender, EventArgs e) => await _busy.RunAsync(OpenCastAsync);

	// The cast page works on the story as typed so far, in memory: nothing is saved until Save story.
	private async Task OpenCastAsync()
	{
		string text = EditorText();
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		// A pending debounced analysis would land after the cast page's edits and overwrite them.
		_analysis?.Cancel();
		_cast = await CastNowAsync(text);
		// The id here is provisional (the cast page only edits it in memory); Save story mints a fresh one per attempt.
		var story = new Story(StoryIds.New(), TitleOrDefault(text), _language, DateTimeOffset.UtcNow,
			[new StoryPage(text)], _cast, CoverStyle.Colour, 0, _narratorVoiceId, null, _merges, _languageCard.Translate);
		var page = _services.GetRequiredService<CastPage>();
		page.Load(story, KeepCast);
		await Navigation.PushAsync(page);
	}

	private Task KeepCast(Story story)
	{
		_cast = story.Cast;
		_merges = story.Merges;
		_narratorVoiceId = story.NarratorVoiceId;
		ShowResult(_cast);
		return Task.CompletedTask;
	}

	private string TitleOrDefault(string text) =>
		string.IsNullOrWhiteSpace(TitleEntry.Text) ? StoryTitles.FromText(text) : TitleEntry.Text.Trim();
}
