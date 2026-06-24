namespace ReadTheStupidText.Domain.Reading;

/// <summary>
/// A narrator playback rate as a decimal multiplier (e.g. 1.35x). The user may
/// pick any value from <see cref="Minimum"/> to <see cref="Maximum"/> in
/// <see cref="Step"/> increments. Constructing one snaps to the nearest step and
/// clamps to range, so an out-of-range or off-step rate can never exist — the
/// value object owns the rule rather than scattering it across the UI.
/// </summary>
public readonly record struct PlaybackRate
{
    public const double Minimum = 0.5;
    public const double Maximum = 2.0;
    public const double Step = 0.05;

    public PlaybackRate(double value) => Value = Normalize(value);

    /// <summary>The default rate when nothing has been chosen yet (1x).</summary>
    public static PlaybackRate Default => new(1.0);

    /// <summary>The multiplier applied to playback, within [Minimum, Maximum].</summary>
    public double Value { get; }

    /// <summary>The short label shown beside the speed control (e.g. "1.35x").</summary>
    public string ToDisplayLabel() => $"{Value:0.##}x";

    private static double Normalize(double value)
    {
        double snapped = Math.Round(value / Step) * Step;
        return Math.Round(Math.Clamp(snapped, Minimum, Maximum), 2);
    }
}
