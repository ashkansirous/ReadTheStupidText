namespace ReadTheStupidText.Mobile.Shared.Waiting;

/// <summary>
/// How an app's wait overlay looks (Slice 143, Decision 149): one shared control, each app brings its fill, its text and
/// Cancel colours and, optionally, its own animation. The defaults are a plain spinner on a white veil.
/// </summary>
public sealed record WaitOverlayStyle
{
    public Color Fill { get; init; } = Color.FromRgba(1, 1, 1, 0.9);

    public Color LineColor { get; init; } = Colors.Black;

    public Color DetailColor { get; init; } = Color.FromRgba(0, 0, 0, 0.58);

    public Color SpinnerColor { get; init; } = Color.FromArgb("#5B57E8");

    public Color CancelFill { get; init; } = Color.FromArgb("#1A5B57E8");

    public Color CancelBorder { get; init; } = Colors.Transparent;

    public Color CancelText { get; init; } = Color.FromArgb("#4A46D6");

    public double Padding { get; init; } = 24;

    public double Spacing { get; init; } = 14;

    public double LineSize { get; init; } = 16;

    public double DetailSize { get; init; } = 13;

    public double CancelHeight { get; init; } = 44;

    public double CancelSize { get; init; } = 15;

    /// <summary>Makes the app's own animation; null shows the platform spinner. The view should implement <see cref="IWaitAnimation"/>.</summary>
    public Func<View>? CreateAnimation { get; init; }
}
