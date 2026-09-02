using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;

namespace ReadTheStupidText_App;

/// <summary>
/// The reading text box (Slice 37, Decision 45): a toggleable window that shows the
/// text of the current read with the currently-playing chunk highlighted whole
/// (Decision 46). A single instance is kept for the app's lifetime and shown/hidden
/// (never recreated), so it keeps tracking the read's events while hidden and
/// reopening always reflects the current chunk immediately — no separate re-sync
/// step needed.
/// </summary>
public sealed partial class ReadingTextBoxWindow : Window
{
    private const string NothingReadingStatus = "Nothing reading yet";

    private readonly ReadAloudService _readAloud;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly TextHighlighter _highlighter = new()
    {
        Background = new SolidColorBrush(ColorHelper.FromArgb(0x55, 0x5B, 0x57, 0xE8)),
    };

    private string _fullText = string.Empty;

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
            ReadingText.Text = text;
            _highlighter.Ranges.Clear();
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

    private void ApplyHighlight(ReadChunk chunk)
    {
        if (chunk.SourceEnd > _fullText.Length)
        {
            return; // stale event for a read this window hasn't caught up to yet
        }

        StatusText.Text = $"Reading — chunk {chunk.Index + 1} of {chunk.ChunkCount}";

        _highlighter.Ranges.Clear();
        if (chunk.SourceLength > 0)
        {
            _highlighter.Ranges.Add(new TextRange { StartIndex = chunk.SourceStart, Length = chunk.SourceLength });
        }

        ScrollToChunk(chunk);
    }

    // Approximate auto-follow for this first pass (Decision 45): scrolls to the
    // chunk's fractional position in the full text. Slice 38's real pagination
    // (page-based, chunk-aware) replaces this with an exact page turn.
    private void ScrollToChunk(ReadChunk chunk)
    {
        if (_fullText.Length == 0 || TextScroller.ExtentHeight <= TextScroller.ViewportHeight)
        {
            return;
        }

        double fraction = (double)chunk.SourceStart / _fullText.Length;
        double target = fraction * (TextScroller.ExtentHeight - TextScroller.ViewportHeight);
        TextScroller.ChangeView(null, target, null);
    }
}
