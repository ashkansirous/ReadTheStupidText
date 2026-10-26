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
    private const double CancelSidePadding = 22;

    private readonly WaitOverlayStyle _style;
    private readonly Label _line;
    private readonly Label _detail;
    private readonly Button _cancel;
    private readonly IWaitAnimation? _animation;

    public WaitOverlay(WaitOverlayStyle style, string cancelText)
    {
        _style = style;
        IsVisible = false;
        BackgroundColor = style.Fill;
        _line = new Label
        {
            FontSize = style.LineSize, FontAttributes = FontAttributes.Bold, TextColor = style.LineColor,
            HorizontalTextAlignment = TextAlignment.Center,
        };
        _detail = new Label
        {
            FontSize = style.DetailSize, TextColor = style.DetailColor, HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, DetailPull, 0, 0), IsVisible = false,
        };
        _cancel = BuildCancel(cancelText);
        View motion = style.CreateAnimation?.Invoke() ?? new ActivityIndicator { IsRunning = true, Color = style.SpinnerColor };
        _animation = motion as IWaitAnimation;
        Unloaded += (_, _) => _animation?.Stop(); // a page popped while the wait shows must not leave the hop ticking
        Loaded += (_, _) => RestartIfShowing(); // and one that comes back while still waiting must hop again
        Children.Add(new VerticalStackLayout
        {
            Spacing = style.Spacing, Padding = style.Padding, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center,
            Children = { Centred(motion), _line, _detail, Centred(_cancel) },
        });
    }

    /// <summary>The Cancel button was tapped.</summary>
    public event EventHandler? CancelTapped;

    /// <summary>Shows the overlay with <paramref name="line"/>, a second line when <paramref name="detail"/> is given, and Cancel when asked.</summary>
    public void Show(string line, string? detail, bool canCancel)
    {
        _line.Text = line;
        _detail.Text = detail;
        _detail.IsVisible = !string.IsNullOrEmpty(detail);
        _cancel.IsVisible = canCancel;
        SemanticProperties.SetDescription(this, string.IsNullOrEmpty(detail) ? line : $"{line} {detail}");
        if (!IsVisible)
        {
            _cancel.IsEnabled = true; // only a fresh show re-arms Cancel; re-wording a wait must not undo a tap
            IsVisible = true;
            _animation?.Start();
        }
    }

    private void RestartIfShowing()
    {
        if (IsVisible)
        {
            _animation?.Start();
        }
    }

    public void Hide()
    {
        _animation?.Stop();
        IsVisible = false;
    }

    private Button BuildCancel(string text)
    {
        var button = new Button
        {
            Text = text, HeightRequest = _style.CancelHeight, CornerRadius = (int)(_style.CancelHeight / 2), FontSize = _style.CancelSize,
            FontAttributes = FontAttributes.Bold, BackgroundColor = _style.CancelFill, BorderColor = _style.CancelBorder,
            BorderWidth = CancelBorderWidth, TextColor = _style.CancelText, Padding = new Thickness(CancelSidePadding, 0), IsVisible = false,
        };
        button.Clicked += (_, _) => OnCancelClicked(button);
        return button;
    }

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
