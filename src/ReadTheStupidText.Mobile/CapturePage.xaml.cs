using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Mobile.Resources.Strings;
using ReadTheStupidText.Mobile.Shared;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Wizard page 1, "Take a photo of each page" (Slice 100, Decision 104): our own multi-page capture so
/// "add another page" cannot hide inside Google's scanner. Each Add captures one page (the ML Kit scanner
/// limited to one page, or a single photo without Play services) and returns here; every page is read in
/// the background as it arrives, so Done is quick. Done joins the pages' text and opens the review page
/// (Slice 101); pictures are deleted when the wizard ends.
/// </summary>
public partial class CapturePage : ContentPage, IScanPackUi
{
    private const int Columns = 3;
    private const double ColumnGap = 10;
    private const double RowGap = 12;
    private const double TileAspect = 4.0 / 3.0;
    private const int MaxPhotosPerPick = 20;
    private const int MaxPages = 30;
    private const float ThumbDecodeWidth = 360;
    private const float ThumbJpegQuality = 0.8f;
    private const double DisabledOpacity = 0.45;
    private const uint SpinnerMillis = 900;
    private const string SpinnerAnimation = "capture-spin";

    private readonly IImageTextExtractor _imageExtractor;
    private readonly IOcrLineExtractor? _lineExtractor;
    private readonly OcrPackInstaller? _ocrPacks;
    private readonly IDocumentScanner? _scanner;
    private readonly ISettingsStore _settings;
    private readonly ReviewHandOff _handOff;
    private readonly CapturedPageList _pages = new();
    private readonly Dictionary<Guid, View> _spinners = [];
    private readonly Dictionary<Guid, View> _cells = [];
    private readonly Dictionary<Guid, byte[]> _thumbs = [];

    private Task _readQueue = Task.CompletedTask;
    private Action? _cancelDownload;
    private CapturedPage? _viewing;
    private double _gridWidth;
    private double _tileWidth;
    private bool _entered;
    private bool _capturing;
    private bool _finishing;
    private bool _leaving;
    private bool _scannerUnavailableShown;
    private bool _ocrUnavailable;
    private string? _pickedLanguage;

    public CapturePage(IImageTextExtractor imageExtractor, ISettingsStore settings, ReviewHandOff handOff, IDocumentScanner? scanner = null, IOcrLineExtractor? lineExtractor = null, OcrPackInstaller? ocrPacks = null)
    {
        InitializeComponent();
        FlowDirection = FlowDirection.LeftToRight;
        _imageExtractor = imageExtractor;
        _settings = settings;
        _handOff = handOff;
        _scanner = scanner;
        _lineExtractor = lineExtractor;
        _ocrPacks = ocrPacks;
        ApplyStaticTexts();
        RenderPages();
    }

    private void ApplyStaticTexts()
    {
        TextFlow.ForUi(TitleLabel, AppResources.Capture_Title);
        TextFlow.ForUi(AddLabel, AppResources.Capture_AddPage);
        TextFlow.ForUi(DoneLabel, AppResources.Capture_Done);
        TextFlow.ForUi(ChoosePhotosLabel, AppResources.Capture_ChoosePhotos);
        TextFlow.ForUi(DiscardTitle, AppResources.Capture_DiscardTitle);
        TextFlow.ForUi(KeepTakingLabel, AppResources.Capture_KeepTaking);
        TextFlow.ForUi(DiscardLabel, AppResources.Capture_Discard);
        TextFlow.ForUi(RemovePageLabel, AppResources.Capture_RemovePage);
        TextFlow.ForUi(CloseViewerLabel, AppResources.Capture_ClosePreview);
        TextFlow.ForUi(HintLabel);
        HintLabel.FormattedText = BuildHint();
        SemanticProperties.SetDescription(BackButton, AppResources.Common_Back);
        SemanticProperties.SetDescription(AddButton, AppResources.Capture_AddPage);
        SemanticProperties.SetDescription(DoneButton, AppResources.Capture_Done);
    }

    // The hint carries the Done button's word in bold: the resource holds a {0} for it.
    private static FormattedString BuildHint()
    {
        string[] parts = AppResources.Capture_Hint.Split("{0}");
        var text = new FormattedString();
        text.Spans.Add(new Span { Text = parts[0] });
        if (parts.Length > 1)
        {
            var done = new Span { Text = AppResources.Capture_HintDone, FontAttributes = FontAttributes.Bold };
            done.SetAppThemeColor(Span.TextColorProperty, Color.FromArgb("#1F2027"), Colors.White);
            text.Spans.Add(done);
            text.Spans.Add(new Span { Text = parts[1] });
        }

        return text;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_entered)
        {
            return;
        }

        // The camera is the first thing they see.
        _entered = true;
        Dispatcher.Dispatch(async () => await CaptureAsync(CapturePagesAsync));
    }

    // Any pop that bypasses our Back/Discard/Done paths (iOS swipe-back) still cleans up. OnNavigatedFrom
    // fires only when the shell really leaves the page; OnDisappearing also fires when the app goes to
    // the background (Home, a call), which must keep the pages.
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (!_leaving)
        {
            EndWizard();
        }
    }

    private bool DialogOpen => DiscardOverlay.IsVisible || ViewerOverlay.IsVisible;

    protected override bool OnBackButtonPressed()
    {
        if (ViewerOverlay.IsVisible)
        {
            CloseViewer();
        }
        else if (!DiscardOverlay.IsVisible)
        {
            _ = RequestLeaveAsync();
        }
        else
        {
            DiscardOverlay.IsVisible = false;
            RenderChrome();
        }

        return true;
    }

    private Task AlertAsync(string title, string message)
    {
        return _leaving ? Task.CompletedTask : DisplayAlertAsync(title, message, AppResources.Common_Ok, FlowDirection.LeftToRight);
    }

    // ---- capturing -------------------------------------------------------------------------

    private async void OnAddTapped(object? sender, TappedEventArgs e)
    {
        if (!DialogOpen)
        {
            await CaptureAsync(CapturePagesAsync);
        }
    }

    private async void OnChoosePhotosTapped(object? sender, TappedEventArgs e)
    {
        if (!DialogOpen)
        {
            await CaptureAsync(PickPhotosAsync);
        }
    }

    // One capture at a time; the pages that come back join the list and start reading at once.
    private async Task CaptureAsync(Func<Task<IReadOnlyList<string>>> source)
    {
        if (_capturing || _finishing || _leaving)
        {
            return;
        }

        _capturing = true;
        try
        {
            await AddPagesAsync(await source());
        }
        catch (Exception)
        {
            await AlertAsync(AppResources.Reader_ScanFailedTitle, AppResources.Reader_ScanFailedMessage);
        }
        finally
        {
            _capturing = false;
        }
    }

    // A session holds at most MaxPages pictures; the rest are deleted and the person is told.
    private async Task AddPagesAsync(IReadOnlyList<string> locations)
    {
        bool overLimit = false;
        foreach (string location in locations)
        {
            if (_leaving || _pages.Count >= MaxPages)
            {
                overLimit |= !_leaving;
                ScannedImageFiles.TryDelete(location);
                continue;
            }

            CapturedPage page = _pages.Add(location);
            _readQueue = ReadAfterAsync(_readQueue, page);
            AppendPageCell(page);
        }

        RenderChrome();
        if (overLimit)
        {
            await AlertAsync(AppResources.Capture_Title, AppResources.Format(AppResources.Capture_PageLimit, MaxPages));
        }
    }

    private async Task<IReadOnlyList<string>> CapturePagesAsync()
    {
        if (_scanner is null)
        {
            return await CapturePhotoAsync();
        }

        try
        {
            return await _scanner.ScanAsync(1);
        }
        catch (DocumentScannerStartFailedException)
        {
            await AlertAsync(AppResources.Reader_ScannerStartFailedTitle, AppResources.Reader_ScannerStartFailedMessage);
            return [];
        }
        catch (DocumentScannerPreparingException)
        {
            await AlertAsync(AppResources.Reader_ScannerPreparingTitle, AppResources.Reader_ScannerPreparingMessage);
            return [];
        }
        catch (DocumentScannerUnavailableException)
        {
            return await PhotoWithoutScannerAsync();
        }
    }

    // Without the scanner every Add is one photo; the explanation is shown once, not on every page.
    private async Task<IReadOnlyList<string>> PhotoWithoutScannerAsync()
    {
        if (!_scannerUnavailableShown)
        {
            _scannerUnavailableShown = true;
            await AlertAsync(AppResources.Reader_ScannerUnavailableTitle, AppResources.Reader_ScannerUnavailableMessage);
        }

        return await CapturePhotoAsync();
    }

    private async Task<IReadOnlyList<string>> CapturePhotoAsync()
    {
        try
        {
            if (MediaPicker.Default.IsCaptureSupported)
            {
                FileResult? photo = await MediaPicker.Default.CapturePhotoAsync();
                return photo is null ? [] : [photo.FullPath];
            }
        }
        catch (PermissionException)
        {
            await AlertAsync(AppResources.Reader_CameraAccessTitle, AppResources.Reader_CameraAccessMessage);
            return [];
        }
        catch (FeatureNotSupportedException)
        {
        }

        await AlertAsync(AppResources.Reader_NoCameraTitle, AppResources.Reader_NoCameraMessage);
        return [];
    }

    private static async Task<IReadOnlyList<string>> PickPhotosAsync()
    {
        List<FileResult>? picked = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = MaxPhotosPerPick });

        return picked?.Select(photo => photo.FullPath).ToList() ?? [];
    }

    // ---- background OCR --------------------------------------------------------------------

    // Pages are read one after another in the order they arrived; a failure never stops the next page.
    // Nothing in here may fault the chain: one failed page (or a UI refresh that throws) would otherwise
    // block every later page and Done.
    private async Task ReadAfterAsync(Task previous, CapturedPage page)
    {
        await previous;
        try
        {
            await ReadPageAsync(page);
        }
        catch (Exception)
        {
            _pages.Settle(page.Id, null);
        }
        finally
        {
            DeleteIfRemoved(page);
        }
    }

    private async Task ReadPageAsync(CapturedPage page)
    {
        if (_leaving || !IsKept(page))
        {
            return;
        }

        string? text = await ReadTextAsync(page.Location);
        if (!_leaving)
        {
            _pages.Settle(page.Id, text);
            RefreshPageCell(page);
        }
    }

    private bool IsKept(CapturedPage page) => _pages.Pages.Contains(page);

    // A page removed while queued or being read keeps its picture until the reader is done with it.
    private void DeleteIfRemoved(CapturedPage page)
    {
        if (!IsKept(page))
        {
            ScannedImageFiles.TryDelete(page.Location);
        }
    }

    // Null when the page could not be read (its OCR threw, no engine, or the language pack was declined).
    private async Task<string?> ReadTextAsync(string location)
    {
        try
        {
            ScanPageRead first = await ExtractPageAsync(location, AppLanguages.Code(AppLanguageSetting.Current(_settings)));
            return await ResolveLanguageAsync(location, first);
        }
        catch (OcrUnavailableException)
        {
            _ocrUnavailable = true;
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // A page in a script no installed reader covers asks its language and downloads that pack (Slice 87),
    // never while a camera is in front.
    private async Task<string?> ResolveLanguageAsync(string location, ScanPageRead first)
    {
        if (_ocrPacks is null || PageLanguagePrompt.IsResolved(first.Source) || !_pages.Pages.Any(page => page.Location == location))
        {
            return first.Text;
        }

        while (_capturing && !_leaving)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }

        IReadOnlyList<ScanPageRead>? reads = await new ScanPackFlow(_ocrPacks, this).ResolveAsync([location], [first], ReadAgainAsync);

        return reads?[0].Text;
    }

    private async Task<IReadOnlyList<ScanPageRead>> ReadAgainAsync(IReadOnlyList<string> pages, string language)
    {
        var reads = new List<ScanPageRead>();
        foreach (string page in pages)
        {
            reads.Add(await ExtractPageAsync(page, language));
        }

        return reads;
    }

    // A null text marks a page whose OCR threw; OcrUnavailableException is not per-page.
    private async Task<ScanPageRead> ExtractPageAsync(string page, string language)
    {
        try
        {
            if (_lineExtractor is null)
            {
                return new ScanPageRead(await _imageExtractor.ExtractTextAsync(page), OcrSource.MlKit, ScriptShare.Empty);
            }

            ImageText image = await _lineExtractor.ExtractLinesAsync(page, language, CancellationToken.None);
            return new ScanPageRead(ScanReadingText.From(image), image.Source, ScriptDetector.Analyze(image.Lines.Select(l => l.Text)));
        }
        catch (OcrUnavailableException)
        {
            throw;
        }
        catch (Exception)
        {
            return new ScanPageRead(null, OcrSource.MlKit, ScriptShare.Empty);
        }
    }

    // ---- page-language pack flow (IScanPackUi) ---------------------------------------------

    bool IScanPackUi.IsRetired => _leaving;

    // A picked language is remembered for the session; a Cancel applies to that page only, so the next
    // unresolved page asks again.
    async Task<int> IScanPackUi.PickLanguageAsync(IReadOnlyList<string> names)
    {
        int remembered = _pickedLanguage is null ? -1 : names.ToList().IndexOf(_pickedLanguage);
        if (remembered >= 0)
        {
            return remembered;
        }

        string? picked = await DisplayActionSheetAsync(AppResources.Reader_PageLanguageTitle, AppResources.Common_Cancel, null, FlowDirection.LeftToRight, names.ToArray());

        _pickedLanguage = picked ?? _pickedLanguage;

        return picked is null ? -1 : names.ToList().IndexOf(picked);
    }

    // Ticks arrive from the download thread; one that lands after the flow ended (Cancel hidden) is dropped.
    void IScanPackUi.ShowProgress(string? text) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_cancelDownload is not null)
            {
                TextFlow.ForUi(StatusLabel, text);
            }
        });

    void IScanPackUi.SetCancel(Action? cancel)
    {
        _cancelDownload = cancel;
        StatusRow.IsVisible = cancel is not null;
    }

    Task<bool> IScanPackUi.AskRetryAsync(string title, string message) =>
        _leaving ? Task.FromResult(false) : DisplayAlertAsync(title, message, AppResources.Language_Retry, AppResources.Common_Cancel, FlowDirection.LeftToRight);

    Task IScanPackUi.NotifyAsync(string title, string message) => AlertAsync(title, message);

    private void OnCancelDownloadTapped(object? sender, TappedEventArgs e)
    {
        _cancelDownload?.Invoke();
    }

    // ---- Done ------------------------------------------------------------------------------

    private async void OnDoneTapped(object? sender, TappedEventArgs e)
    {
        if (_pages.Count == 0 || _finishing || _capturing || _leaving || DialogOpen)
        {
            return;
        }

        _finishing = true;
        RenderChrome();
        try
        {
            await _readQueue;
            await FinishAsync();
        }
        finally
        {
            _finishing = false;
            RenderChrome();
        }
    }

    // Done is quick because the pages were read as they arrived; it only waits for the last one. Nothing
    // readable keeps the pages here so the person can retake them.
    private async Task FinishAsync()
    {
        IReadOnlyList<string?> texts = _pages.Texts();
        if (texts.Count == 0)
        {
            return; // every page was removed meanwhile: nothing failed, nothing to hand over
        }

        IReadOnlyList<int> failed = ScannedText.FailedPageNumbers(texts);
        string joined = ScannedText.JoinPages(texts);
        if (failed.Count == texts.Count)
        {
            await ReportUnreadableAsync();
        }
        else if (joined.Length == 0)
        {
            await AlertAsync(AppResources.Reader_NoTextFoundTitle, AppResources.Reader_NoTextFoundMessage);
        }
        else
        {
            await OpenReviewAsync(new ReviewRequest(joined, null, failed, ScannedText.ContributingPageNumbers(texts)));
        }
    }

    private Task ReportUnreadableAsync() =>
        _ocrUnavailable
            ? AlertAsync(AppResources.Reader_ScanUnavailableTitle, AppResources.Reader_ScanUnavailableMessage)
            : AlertAsync(AppResources.Reader_ScanUnreadableTitle, AppResources.Reader_ScanUnreadableMessage);

    // The wizard ends and page 2 takes its place, so Back from it returns to the main page.
    // The request is queued only once we know the page really opens (not while leaving).
    private async Task OpenReviewAsync(ReviewRequest request)
    {
        if (_leaving)
        {
            return;
        }

        _handOff.Open(request);
        EndWizard();
        await Shell.Current.GoToAsync($"../{AppShell.ReviewRoute}");
    }

    // ---- leaving ---------------------------------------------------------------------------

    private async void OnBackTapped(object? sender, TappedEventArgs e)
    {
        if (!DialogOpen)
        {
            await RequestLeaveAsync();
        }
    }

    // Asked only when at least one page has been taken.
    private async Task RequestLeaveAsync()
    {
        if (_leaving || _finishing)
        {
            return;
        }

        if (_pages.Count == 0)
        {
            await LeaveAsync();
            return;
        }

        string message = _pages.Count == 1
            ? AppResources.Capture_DiscardMessageOne
            : AppResources.Format(AppResources.Capture_DiscardMessage, _pages.Count);
        TextFlow.ForUi(DiscardMessage, message);
        DiscardOverlay.IsVisible = true;
        RenderChrome();
    }

    private void OnKeepTakingTapped(object? sender, TappedEventArgs e)
    {
        DiscardOverlay.IsVisible = false;
        RenderChrome();
    }

    private async void OnDiscardTapped(object? sender, TappedEventArgs e)
    {
        await LeaveAsync();
    }

    // The wizard ends: its pictures go whether or not they were read.
    private async Task LeaveAsync()
    {
        if (_leaving)
        {
            return;
        }

        EndWizard();
        await Shell.Current.GoToAsync("..");
    }

    // Cancels the work and deletes the pictures; runs once, from our own leave or from OnDisappearing.
    private void EndWizard()
    {
        _leaving = true;
        _cancelDownload?.Invoke();
        foreach (CapturedPage page in _pages.Pages)
        {
            ScannedImageFiles.TryDelete(page.Location);
        }

        StopSpinners();
    }

    // ---- viewing and removing a page -------------------------------------------------------

    private void OpenViewer(CapturedPage page)
    {
        if (_finishing || _leaving || DialogOpen)
        {
            return;
        }

        _viewing = page;
        TextFlow.ForUi(ViewerTitle, AppResources.Format(AppResources.Capture_PageLabel, _pages.Pages.ToList().IndexOf(page) + 1));
        ViewerImage.Source = ImageFor(page);
        ViewerOverlay.IsVisible = true;
        RenderChrome();
    }

    private void CloseViewer()
    {
        _viewing = null;
        ViewerOverlay.IsVisible = false;
        ViewerImage.Source = null;
        RenderChrome();
    }

    private void OnCloseViewerTapped(object? sender, TappedEventArgs e)
    {
        CloseViewer();
    }

    private void OnRemovePageTapped(object? sender, TappedEventArgs e)
    {
        if (_viewing is { } page && _pages.Remove(page.Id) is { } removed)
        {
            _thumbs.Remove(removed.Id);
            if (removed.State != CapturedPageState.Reading)
            {
                ScannedImageFiles.TryDelete(removed.Location);
            }
        }

        CloseViewer();
        RenderPages();
    }

    private static ImageSource? ImageFor(CapturedPage page)
    {
        return ScannedText.ToLocalPath(page.Location) is { } path ? ImageSource.FromFile(path) : null;
    }

    // ---- drawing ---------------------------------------------------------------------------

    private void OnThumbGridSizeChanged(object? sender, EventArgs e)
    {
        if (Math.Abs(ThumbGrid.Width - _gridWidth) > 0.5)
        {
            RenderPages();
        }
    }

    // The back arrow hides behind a dialog (an empty 48x48 slot), and Done waits for pages to exist.
    private void RenderChrome()
    {
        bool dialog = DiscardOverlay.IsVisible || ViewerOverlay.IsVisible;
        BackGlyph.IsVisible = !dialog;
        DoneButton.Opacity = _pages.Count == 0 || _finishing ? DisabledOpacity : 1;
        AddButton.Opacity = _finishing ? DisabledOpacity : 1;
    }

    private void RenderPages()
    {
        RenderChrome();
        _gridWidth = ThumbGrid.Width;
        if (_gridWidth <= 0)
        {
            return; // SizeChanged draws them once the grid has a width
        }

        StopSpinners();
        _cells.Clear();
        ThumbGrid.Children.Clear();
        ThumbGrid.RowDefinitions.Clear();
        _tileWidth = (_gridWidth - ColumnGap * (Columns - 1)) / Columns;
        for (int i = 0; i < _pages.Count; i++)
        {
            AddCell(_pages.Pages[i], i);
        }
    }

    private void AddCell(CapturedPage page, int index)
    {
        if (index % Columns == 0)
        {
            ThumbGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        View cell = BuildCell(page, index + 1, _tileWidth);
        _cells[page.Id] = cell;
        ThumbGrid.Add(cell, index % Columns, index / Columns);
    }

    // A new page adds one tile; the others are left alone.
    private void AppendPageCell(CapturedPage page)
    {
        if (_gridWidth > 0)
        {
            AddCell(page, _pages.Pages.ToList().IndexOf(page));
        }
    }

    // A page that settled redraws only its own tile.
    private void RefreshPageCell(CapturedPage page)
    {
        if (!_cells.TryGetValue(page.Id, out View? old))
        {
            return;
        }

        StopSpinner(page.Id);
        int index = _pages.Pages.ToList().IndexOf(page);
        ThumbGrid.Remove(old);
        View cell = BuildCell(page, index + 1, _tileWidth);
        _cells[page.Id] = cell;
        ThumbGrid.Add(cell, index % Columns, index / Columns);
    }

    private View BuildCell(CapturedPage page, int number, double tileWidth)
    {
        string label = AppResources.Format(AppResources.Capture_PageLabel, number);
        var name = new Label { FontSize = 14, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
        bool failed = page.State == CapturedPageState.Failed;
        TextFlow.ForUi(name, failed ? $"{label} · {AppResources.Capture_CouldntRead}" : label);
        var cell = new VerticalStackLayout { Spacing = 6, Children = { BuildTile(page, tileWidth), name } };
        SemanticProperties.SetDescription(cell, label);
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => OpenViewer(page);
        cell.GestureRecognizers.Add(tap);
        return cell;
    }

    private View BuildTile(CapturedPage page, double tileWidth)
    {
        var tile = new Grid { HeightRequest = tileWidth * TileAspect };
        var frame = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(10) },
            StrokeThickness = 1,
            Content = BuildThumbnail(page),
        };
        frame.SetAppTheme(Border.StrokeProperty, (Brush)new SolidColorBrush(Color.FromArgb("#14000000")), new SolidColorBrush(Color.FromArgb("#1FFFFFFF")));
        frame.SetAppThemeColor(VisualElement.BackgroundColorProperty, Color.FromArgb("#DFE2EA"), Color.FromArgb("#1F1F1F"));
        tile.Children.Add(frame);
        if (page.State == CapturedPageState.Reading)
        {
            tile.Children.Add(BuildReadingPill(page.Id));
        }
        else if (page.State == CapturedPageState.Failed)
        {
            tile.Children.Add(BuildFailedBadge());
        }

        return tile;
    }

    // A small red "!" in the tile's corner for a page that could not be read.
    private static Border BuildFailedBadge() => new()
    {
        WidthRequest = 24,
        HeightRequest = 24,
        Margin = new Thickness(6),
        HorizontalOptions = LayoutOptions.End,
        VerticalOptions = LayoutOptions.Start,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(12) },
        StrokeThickness = 0,
        BackgroundColor = Color.FromArgb("#C62828"),
        Content = new Label { Text = "!", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
    };

    // The pill at the bottom of a page still being read: a spinner and "reading…".
    private Border BuildReadingPill(Guid pageId)
    {
        var content = new HorizontalStackLayout { Spacing = 5, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        content.Children.Add(BuildSpinner(pageId));
        var text = new Label { FontSize = 11.5, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center };
        TextFlow.ForUi(text, AppResources.Capture_Reading);
        content.Children.Add(text);
        return new Border
        {
            HeightRequest = 26,
            Margin = new Thickness(6),
            VerticalOptions = LayoutOptions.End,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(13) },
            StrokeThickness = 0,
            BackgroundColor = Color.FromArgb("#BF1F2027"),
            Content = content,
        };
    }

    // The mock's spinner: a faint r9 ring and a white quarter arc on a 24 grid, drawn at half size.
    private View BuildSpinner(Guid pageId)
    {
        var spinner = new Grid { WidthRequest = 12, HeightRequest = 12, VerticalOptions = LayoutOptions.Center };
        spinner.Children.Add(SpinnerPath((string)Resources["IconSpinnerRing"], Color.FromArgb("#4DFFFFFF")));
        spinner.Children.Add(SpinnerPath((string)Resources["IconSpinnerArc"], Colors.White));
        spinner.Animate(SpinnerAnimation, new Animation(angle => spinner.Rotation = angle, 0, 360), length: SpinnerMillis, repeat: () => true);
        _spinners[pageId] = spinner;
        return spinner;
    }

    private static Microsoft.Maui.Controls.Shapes.Path SpinnerPath(string data, Color stroke) => new()
    {
        Data = (Microsoft.Maui.Controls.Shapes.Geometry?)new Microsoft.Maui.Controls.Shapes.PathGeometryConverter().ConvertFromInvariantString(data),
        Aspect = Stretch.None,
        WidthRequest = 24,
        HeightRequest = 24,
        Scale = 0.5,
        Stroke = new SolidColorBrush(stroke),
        StrokeThickness = 3,
        StrokeLineCap = Microsoft.Maui.Controls.Shapes.PenLineCap.Round,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
    };

    private void StopSpinners()
    {
        foreach (View spinner in _spinners.Values)
        {
            spinner.AbortAnimation(SpinnerAnimation);
        }

        _spinners.Clear();
    }

    private void StopSpinner(Guid pageId)
    {
        if (_spinners.Remove(pageId, out View? spinner))
        {
            spinner.AbortAnimation(SpinnerAnimation);
        }
    }

    // ---- thumbnails ------------------------------------------------------------------------

    // The tile shows a small decode of the picture, made once per page off the UI thread; the viewer
    // loads the full picture only when it is opened.
    private Image BuildThumbnail(CapturedPage page)
    {
        var image = new Image { Aspect = Aspect.AspectFill };
        _ = LoadThumbnailAsync(image, page);
        return image;
    }

    private async Task LoadThumbnailAsync(Image image, CapturedPage page)
    {
        try
        {
            if (!_thumbs.TryGetValue(page.Id, out byte[]? bytes))
            {
                string? path = ScannedText.ToLocalPath(page.Location);
                if (path is null)
                {
                    return;
                }

                bytes = await Task.Run(() => DecodeThumbnail(path));
                if (!IsKept(page) || _leaving)
                {
                    return;
                }

                _thumbs[page.Id] = bytes;
            }

            image.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            // No thumbnail: the tile keeps its plain background.
        }
    }

    private static byte[] DecodeThumbnail(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Microsoft.Maui.Graphics.IImage original = Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(stream);
        float height = (float)(ThumbDecodeWidth * TileAspect);
        using Microsoft.Maui.Graphics.IImage small = original.Downsize(ThumbDecodeWidth, height, true);
        using var output = new MemoryStream();
        small.Save(output, Microsoft.Maui.Graphics.ImageFormat.Jpeg, ThumbJpegQuality);
        return output.ToArray();
    }
}
