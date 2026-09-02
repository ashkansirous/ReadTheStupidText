using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;

namespace ReadTheStupidText_App;

/// <summary>
/// The reading text box (Slice 37/38, Decisions 45-48): a toggleable window that
/// shows the text of the current read, paginated to fit the box (Decision 47), with
/// the currently-playing chunk highlighted whole (Decision 46). A single instance is
/// kept for the app's lifetime and shown/hidden (never recreated), so it keeps
/// tracking the read's events while hidden and reopening always reflects the current
/// chunk immediately — no separate re-sync step needed.
/// </summary>
public sealed partial class ReadingTextBoxWindow : Window
{
    private const string NothingReadingStatus = "Nothing reading yet";

    // Matches the ScrollViewer's XAML Padding — subtracted to get the width/height
    // actually available to the text (both the real content and the measure probe).
    private const double PaddingX = 24;
    private const double PaddingY = 20;

    private const double ZoomStep = 2;
    private const double ZoomFloor = 10;
    private const double ZoomHardCeiling = 32;

    // The dynamic zoom-in limit's stand-in "typical sentence" (Decision 48) — not
    // real read content, just a fixed-length stress test for whether a sentence of
    // that rough length still fits the box at a candidate font size.
    private static readonly string ThirtyWordProbe = string.Join(' ', System.Linq.Enumerable.Repeat("reading", 30));

    private readonly ReadAloudService _readAloud;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly TextHighlighter _highlighter = new()
    {
        Background = new SolidColorBrush(ColorHelper.FromArgb(0x55, 0x5B, 0x57, 0xE8)),
    };

    private string _fullText = string.Empty;
    private IReadOnlyList<TextPage> _pages = [];
    private int _currentPageIndex = -1;
    private ReadChunk? _lastChunk;
    private double _fontSize = 16;

    // The system X is intercepted to hide instead of destroy (below) so playback
    // survives it; Shutdown() lifts that guard for the one path that must really
    // close the window — app quit.
    private bool _allowClose;

    public ReadingTextBoxWindow(ReadAloudService readAloud)
    {
        _readAloud = readAloud;
        InitializeComponent();
        Title = "Read The Stupid Text — Reading";
        AppWindow.Resize(new SizeInt32(640, 520));
        ConfigureWindowChrome();
        ReadingText.TextHighlighters.Add(_highlighter);

        AppWindow.Closing += OnAppWindowClosing;
        _readAloud.ReadTextChanged += OnReadTextChanged;
        _readAloud.ChunkChanged += OnChunkChanged;
        _readAloud.StateChanged += OnPlaybackStateChanged;
    }

    /// <summary>Raised whenever the window is actually shown or hidden — by
    /// <see cref="Toggle"/> or by the system ✕ — so the control panel's toggle can
    /// stay in sync even when the change didn't come from that toggle.</summary>
    public event EventHandler<bool>? ReadingVisibilityChanged;

    /// <summary>Shows the window if hidden, hides it if shown (the control panel's
    /// 4th CONTROLS toggle). Reopening needs no extra work: content already tracks
    /// the read live, whether the window is visible or not.</summary>
    public void Toggle()
    {
        if (AppWindow.IsVisible)
        {
            HideWindow();
        }
        else
        {
            ShowWindow();
        }
    }

    public bool IsOpen => AppWindow.IsVisible;

    /// <summary>Genuinely closes the window — app quit only. Everywhere else, the
    /// system ✕ hides instead (below), so closing this window never stops
    /// playback.</summary>
    public void Shutdown()
    {
        _allowClose = true;
        Close();
    }

    private void ShowWindow()
    {
        AppWindow.Show();
        Activate();
        ReadingVisibilityChanged?.Invoke(this, true);
    }

    private void HideWindow()
    {
        AppWindow.Hide();
        ReadingVisibilityChanged?.Invoke(this, false);
    }

    // The system ✕ hides rather than destroys the window (mirrors the control
    // panel, Decision 12) — closing this window must not stop playback, and the
    // single instance keeps following the read while hidden.
    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;
        HideWindow();
    }

    private void ConfigureWindowChrome()
    {
        AppWindow.SetIcon("Assets/AppIcon.ico");

        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        Color brand = ColorHelper.FromArgb(255, 0x5B, 0x57, 0xE8);
        Color brandHover = ColorHelper.FromArgb(255, 0x6E, 0x6A, 0xEE);
        Color brandPressed = ColorHelper.FromArgb(255, 0x4C, 0x48, 0xD0);
        Color inactiveText = ColorHelper.FromArgb(0xB3, 0xFF, 0xFF, 0xFF);

        AppWindowTitleBar bar = AppWindow.TitleBar;
        bar.BackgroundColor = brand;
        bar.InactiveBackgroundColor = brand;
        bar.ForegroundColor = Colors.White;
        bar.InactiveForegroundColor = inactiveText;
        bar.ButtonBackgroundColor = brand;
        bar.ButtonInactiveBackgroundColor = brand;
        bar.ButtonForegroundColor = Colors.White;
        bar.ButtonInactiveForegroundColor = inactiveText;
        bar.ButtonHoverBackgroundColor = brandHover;
        bar.ButtonHoverForegroundColor = Colors.White;
        bar.ButtonPressedBackgroundColor = brandPressed;
        bar.ButtonPressedForegroundColor = Colors.White;
    }

    private void OnReadTextChanged(object? sender, string text) =>
        _dispatcher.TryEnqueue(() =>
        {
            _fullText = text;
            _lastChunk = null;
            Repaginate();
        });

    private void OnChunkChanged(object? sender, ReadChunk chunk) =>
        _dispatcher.TryEnqueue(() => ApplyHighlight(chunk));

    private void OnPlaybackStateChanged(object? sender, PlaybackState state) =>
        _dispatcher.TryEnqueue(() =>
        {
            if (state == PlaybackState.Idle)
            {
                StatusText.Text = NothingReadingStatus;
            }
        });

    private void OnTextScrollerSizeChanged(object sender, SizeChangedEventArgs e) => Repaginate();

    private void OnZoomInClick(object sender, RoutedEventArgs e)
    {
        double candidate = Math.Min(_fontSize + ZoomStep, ZoomHardCeiling);
        // Dynamic limit (Decision 48): stop the moment a typical sentence would no
        // longer fit the box at the candidate size, even below the hard ceiling.
        if (candidate <= _fontSize || !MeasureFits(ThirtyWordProbe, candidate))
        {
            return;
        }

        _fontSize = candidate;
        ApplyFontSize();
    }

    private void OnZoomOutClick(object sender, RoutedEventArgs e)
    {
        _fontSize = Math.Max(_fontSize - ZoomStep, ZoomFloor);
        ApplyFontSize();
    }

    private void ApplyFontSize()
    {
        ReadingText.FontSize = _fontSize;
        Repaginate();
    }

    // Re-fills pages for the current text/font/box size (Decision 47) and restores
    // whatever was on screen — the currently-playing chunk's page if known, else
    // the first page — so a zoom or a window resize never loses the reader's place.
    private void Repaginate()
    {
        if (_fullText.Length == 0)
        {
            _pages = [];
            _currentPageIndex = -1;
            ReadingText.Text = string.Empty;
            UpdatePageIndicator();
            return;
        }

        _pages = TextBoxPaginator.Paginate(_fullText, Fits);
        _currentPageIndex = -1; // force the next ShowPage to actually reassign Text
        if (_lastChunk is { } chunk)
        {
            ApplyHighlight(chunk);
        }
        else
        {
            ShowPage(0);
        }
    }

    private void ApplyHighlight(ReadChunk chunk)
    {
        _lastChunk = chunk;
        if (chunk.SourceEnd > _fullText.Length || _pages.Count == 0)
        {
            return; // stale event, or not paginated yet
        }

        StatusText.Text = $"Reading — chunk {chunk.Index + 1} of {chunk.ChunkCount}";

        int pageIndex = TextBoxPaginator.PageIndexContaining(_pages, chunk.SourceStart);
        if (pageIndex != _currentPageIndex)
        {
            ShowPage(pageIndex);
        }

        TextPage page = _pages[_currentPageIndex];
        int localStart = Math.Clamp(chunk.SourceStart - page.SourceStart, 0, page.Text.Length);
        int localEnd = Math.Clamp(chunk.SourceEnd - page.SourceStart, 0, page.Text.Length);

        _highlighter.Ranges.Clear();
        if (localEnd > localStart)
        {
            _highlighter.Ranges.Add(new TextRange { StartIndex = localStart, Length = localEnd - localStart });
        }
    }

    private void ShowPage(int index)
    {
        if (_pages.Count == 0)
        {
            return;
        }

        _currentPageIndex = Math.Clamp(index, 0, _pages.Count - 1);
        ReadingText.Text = _pages[_currentPageIndex].Text;
        _highlighter.Ranges.Clear();
        TextScroller.ChangeView(null, 0, null, disableAnimation: true);
        UpdatePageIndicator();
    }

    private void UpdatePageIndicator() =>
        PageIndicator.Text = _pages.Count == 0 ? string.Empty : $"Page {_currentPageIndex + 1} of {_pages.Count}";

    private bool Fits(string candidate) => MeasureFits(candidate, _fontSize);

    // Real text measurement (Decisions 47, 48) against the off-screen MeasureProbe:
    // does `candidate`, wrapped at the box's current width, fit within its current
    // height at `fontSize`? Accepts everything before the window has ever been
    // sized (ActualWidth/Height still 0) — the SizeChanged handler repaginates for
    // real the moment real dimensions are known.
    private bool MeasureFits(string candidate, double fontSize)
    {
        double width = Math.Max(TextScroller.ActualWidth - PaddingX * 2, 0);
        double height = Math.Max(TextScroller.ActualHeight - PaddingY * 2, 0);
        if (width <= 0 || height <= 0)
        {
            return true;
        }

        MeasureProbe.FontSize = fontSize;
        MeasureProbe.Text = candidate;
        MeasureProbe.Measure(new Size(width, double.PositiveInfinity));
        return MeasureProbe.DesiredSize.Height <= height;
    }
}
