namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Renders a <see cref="ReadTiming"/> as <c>mm:ss/mm:ss</c> (Decision 33): minutes are
/// padded to at least two digits but never capped (a read past 99 minutes grows to
/// three digits rather than wrapping), and an unknown total renders as <c>--:--</c>.
/// </summary>
public static class ReadTimingFormatter
{
    private const string UnknownDuration = "--:--";

    public static string Format(ReadTiming timing) =>
        $"{FormatDuration(timing.Elapsed)}/{FormatDuration(timing.Total)}";

    /// <summary>One side of the player clock for the phone: <c>m:ss</c> (minutes unpadded), or <c>--:--</c> when unknown.</summary>
    public static string FormatClock(TimeSpan? duration) =>
        duration is { } value ? $"{(int)value.TotalMinutes}:{value.Seconds:D2}" : UnknownDuration;

    private static string FormatDuration(TimeSpan? duration)
    {
        if (duration is not { } value)
        {
            return UnknownDuration;
        }

        int minutes = (int)value.TotalMinutes;
        int seconds = value.Seconds;
        return $"{minutes:D2}:{seconds:D2}";
    }
}
