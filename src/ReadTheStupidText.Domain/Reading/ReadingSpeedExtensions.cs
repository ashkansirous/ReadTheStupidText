namespace ReadTheStupidText.Domain.Reading;

/// <summary>
/// Maps each <see cref="ReadingSpeed"/> to the playback rate the media engine
/// applies. Keeping the mapping here means the rate values live with the
/// closed set they belong to, not scattered across the UI or infrastructure.
/// </summary>
public static class ReadingSpeedExtensions
{
    /// <summary>The default speed when nothing has been chosen yet.</summary>
    public const ReadingSpeed Default = ReadingSpeed.OneX;

    public static double ToPlaybackRate(this ReadingSpeed speed) => speed switch
    {
        ReadingSpeed.OneX => 1.0,
        ReadingSpeed.OneAndQuarterX => 1.25,
        ReadingSpeed.OneAndHalfX => 1.5,
        ReadingSpeed.OneAndThreeQuarterX => 1.75,
        ReadingSpeed.TwoX => 2.0,
        _ => 1.0,
    };

    /// <summary>The short label shown on the speed buttons (e.g. "1.25x").</summary>
    public static string ToDisplayLabel(this ReadingSpeed speed) =>
        $"{speed.ToPlaybackRate():0.##}x";
}
