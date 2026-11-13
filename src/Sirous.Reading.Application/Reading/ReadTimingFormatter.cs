using Sirous.Reading.Domain.Reading;

namespace Sirous.Reading.Application.Reading;

/// <summary>
/// Renders a <see cref="ReadTiming"/> as <c>mm:ss/mm:ss</c> (Decision 33): minutes are
/// padded to at least two digits but never capped (a read past 99 minutes grows to
/// three digits rather than wrapping), and an unknown total renders as <c>--:--</c>.
/// </summary>
public static class ReadTimingFormatter
{
    private const string UnknownDuration = "--:--";

    /// <summary>The timing to show: elapsed never runs past a known total (a position can overshoot the estimate at the end of a chunk).</summary>
    public static ReadTiming ClampToTotal(ReadTiming timing) =>
        timing.Total is { } total && timing.Elapsed > total ? timing with { Elapsed = total } : timing;

    /// <summary>The clock of a player with nothing playing: zero elapsed, unknown total.</summary>
    public static ReadTiming Idle { get; } = new(TimeSpan.Zero, null);

    public static string Format(ReadTiming timing)
    {
        ReadTiming shown = ClampToTotal(timing);
        return $"{FormatDuration(shown.Elapsed)}/{FormatDuration(shown.Total)}";
    }

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
