namespace ReadTheStupidText.Mobile.Shared.Waiting;

/// <summary>
/// The wait overlay both MAUI apps share (Slice 143, Decision 149): a veil over whatever it is placed on (a picture box or the
/// whole page), the app's animation (or a spinner), one line for what is awaited, an optional second line (a count) and an
/// optional Cancel. It only draws; <see cref="WaitController"/> decides when. Put it last in a <see cref="Grid"/> so it covers the
/// cells it should; while shown it takes the touches, so what is under it cannot be tapped.
/// </summary>
public sealed class WaitOverlay : Grid
{
    private const double DetailPull = -8;
    private const double CancelBorderWidth = 2;
    private const double BarWidth = 220;
    private const double ButtonGap = 10;

    /// <summary>The ids a scripted device check finds the overlay's parts by (Slice 187).</summary>
    public const string LineId = "WaitLine";
    public const string CancelId = "WaitCancel";
    public const string RetryId = "WaitRetry";

    private WaitOverlayStyle _style;
    private readonly Label _line;
    private readonly Label _detail;
    private readonly Button _cancel;
    private readonly Button _retry;
    private readonly ProgressBar _bar;
    private readonly NoWifiIcon _noWifi;
    private readonly IWaitAnimation? _animation;
    private readonly View _motion;
    private bool _waitingLook;

    public WaitOverlay(WaitOverlayStyle style, string cancelText)
    {
        _style = style;
        IsVisible = false;
        BackgroundColor = style.Fill;
        _line = new Label
        {
            FontSize = style.LineSize, FontAttributes = FontAttributes.Bold, TextColor = style.LineColor,
            HorizontalTextAlignment = TextAlignment.Center, AutomationId = LineId,
        };
        _detail = new Label
        {
            FontSize = style.DetailSize, TextColor = style.DetailColor, HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, DetailPull, 0, 0), IsVisible = false,
        };
        _cancel = BuildCancel(cancelText);
        _retry = BuildRetry();
        _bar = new ProgressBar { WidthRequest = BarWidth, ProgressColor = style.SpinnerColor, IsVisible = false, HorizontalOptions = LayoutOptions.Center };
        _noWifi = new NoWifiIcon(style.DetailColor) { IsVisible = false, HorizontalOptions = LayoutOptions.Center };
        View motion = style.CreateAnimation?.Invoke() ?? new ActivityIndicator { IsRunning = true, Color = style.SpinnerColor };
        _motion = motion;
        _animation = motion as IWaitAnimation;
        Unloaded += (_, _) => _animation?.Stop(); // a page popped while the wait shows must not leave the hop ticking
        Loaded += (_, _) => RestartIfShowing(); // and one that comes back while still waiting must hop again
        Children.Add(new VerticalStackLayout
        {
            Spacing = style.Spacing, Padding = style.Padding, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Fill,
            Children = { Centred(motion), _noWifi, _line, _detail, _bar, BuildButtonRow() },
        });
    }

    /// <summary>The Cancel button was tapped.</summary>
    public event EventHandler? CancelTapped;

    /// <summary>The Retry button of the waiting look was tapped (Slice 187).</summary>
    public event EventHandler? RetryTapped;

    /// <summary>Shows the overlay with <paramref name="line"/>, a second line when <paramref name="detail"/> is given, and Cancel when asked.</summary>
    public void Show(string line, string? detail, bool canCancel, WaitLook? look = null)
    {
        bool wasVisible = IsVisible;
        bool wasWaiting = _waitingLook;
        ApplyLook(look, enteringWaiting: !(wasVisible && wasWaiting));
        _line.Text = line;
        _detail.Text = detail;
        _detail.IsVisible = !string.IsNullOrEmpty(detail);
        _cancel.IsVisible = canCancel;
        SemanticProperties.SetDescription(this, string.IsNullOrEmpty(detail) ? line : $"{line} {detail}");
        if (!wasVisible)
        {
            _cancel.IsEnabled = true; // only a fresh show re-arms Cancel; re-wording a wait must not undo a tap
            IsVisible = true;
        }

        SyncAnimation(wasVisible, wasWaiting);
    }

    // The hop runs only in the normal look, and only starts or stops on a change: a progress tick re-showing the same wait must not restart it.
    private void SyncAnimation(bool wasVisible, bool wasWaiting)
    {
        if (wasVisible && wasWaiting == _waitingLook)
        {
            return;
        }

        if (_waitingLook)
        {
            _animation?.Stop();
        }
        else
        {
            _animation?.Start();
        }
    }

    // The waiting look swaps the spinner for the no-Wi-Fi icon and greys the bar; any other wait shows exactly what it always did.
    private void ApplyLook(WaitLook? look, bool enteringWaiting)
    {
        bool waiting = look is { Waiting: true };
        _waitingLook = waiting;
        _motion.IsVisible = !waiting;
        _noWifi.IsVisible = waiting;
        _bar.IsVisible = look?.Progress is not null;
        _bar.Progress = look?.Progress ?? 0;
        _bar.ProgressColor = waiting ? _style.DetailColor : _style.SpinnerColor;
        _retry.IsVisible = waiting && look?.RetryText is not null;
        if (_retry.IsVisible)
        {
            _retry.Text = look!.RetryText;
            if (enteringWaiting)
            {
                _retry.IsEnabled = true; // armed once per waiting look; a tick re-showing it must not re-arm a Retry already tapped
            }
        }
    }

    /// <summary>Makes Retry tappable again after a tap that changed nothing (the download had already moved on).</summary>
    public void RearmRetry() => _retry.IsEnabled = true;

    /// <summary>Applies new colours (the app's light or dark theme changed while the page lives); sizes stay as built.</summary>
    public void Restyle(WaitOverlayStyle style)
    {
        _style = style;
        BackgroundColor = style.Fill;
        _line.TextColor = style.LineColor;
        _detail.TextColor = style.DetailColor;
        _noWifi.Recolor(style.DetailColor);
        _bar.ProgressColor = _waitingLook ? style.DetailColor : style.SpinnerColor;
        _retry.FontSize = style.CancelSize;
        _retry.Padding = new Thickness(style.CancelSidePadding, 0);
        _cancel.FontSize = style.CancelSize;
        _cancel.Padding = new Thickness(style.CancelSidePadding, 0);
        _retry.BackgroundColor = style.CancelFill;
        _retry.BorderColor = style.CancelBorder;
        _retry.TextColor = style.CancelText;
        _retry.HeightRequest = style.CancelHeight;
        _retry.CornerRadius = (int)(style.CancelCornerRadius ?? style.CancelHeight / 2);
        _cancel.BackgroundColor = style.CancelFill;
        _cancel.BorderColor = style.CancelBorder;
        _cancel.TextColor = style.CancelText;
        _cancel.HeightRequest = style.CancelHeight;
        _cancel.CornerRadius = (int)(style.CancelCornerRadius ?? style.CancelHeight / 2);
        if (_motion is ActivityIndicator spinner)
        {
            spinner.Color = style.SpinnerColor; // an app's own animation keeps its art; only the platform spinner has a tint
        }
    }

    private void RestartIfShowing()
    {
        if (IsVisible && !_waitingLook)
        {
            _animation?.Start();
        }
    }

    public void Hide()
    {
        _animation?.Stop();
        IsVisible = false;
    }

    // Cancel and Retry share a row that wraps: long translations or a large font scale put one under the other instead of clipping one.
    private FlexLayout BuildButtonRow()
    {
        _cancel.Margin = new Thickness(ButtonGap / 2);
        _retry.Margin = new Thickness(ButtonGap / 2);
        return new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Center, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center, Children = { _cancel, _retry } };
    }

    private Button BuildCancel(string text)
    {
        Button button = BuildPill(text);
        button.AutomationId = CancelId;
        button.Clicked += (_, _) => OnCancelClicked(button);
        return button;
    }

    // Retry looks like Cancel and is dead once tapped, until the next waiting look re-arms it.
    private Button BuildRetry()
    {
        Button button = BuildPill(string.Empty);
        button.AutomationId = RetryId;
        button.Clicked += (_, _) =>
        {
            button.IsEnabled = false;
            RetryTapped?.Invoke(this, EventArgs.Empty);
        };
        return button;
    }

    private Button BuildPill(string text) => new()
    {
        Text = text, HeightRequest = _style.CancelHeight, CornerRadius = (int)(_style.CancelCornerRadius ?? _style.CancelHeight / 2), FontSize = _style.CancelSize,
        FontAttributes = FontAttributes.Bold, BackgroundColor = _style.CancelFill, BorderColor = _style.CancelBorder,
        BorderWidth = CancelBorderWidth, TextColor = _style.CancelText, Padding = new Thickness(_style.CancelSidePadding, 0), IsVisible = false,
    };

    // Cancel is acknowledged at once: the button goes dead, so a slow stage ending is not mistaken for a broken button.
    private void OnCancelClicked(Button button)
    {
        button.IsEnabled = false;
        CancelTapped?.Invoke(this, EventArgs.Empty);
    }

    private static View Centred(View view)
    {
        view.HorizontalOptions = LayoutOptions.Center;
        return view;
    }
}
