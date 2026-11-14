namespace ReadTheStupidText_App;

/// <summary>
/// Formats the local timing diagnostics (time-to-first-audio, synthesis duration)
/// for the activity log's columns: sub-second values in milliseconds, longer ones
/// in seconds. A null (not yet measured) renders as an empty cell.
/// </summary>
internal static class TimingText
{
    private const int MillisecondCutoff = 1000;

    public static string ForDisplay(System.TimeSpan? duration)
    {
        if (duration is not { } value)
        {
            return string.Empty;
        }

        return value.TotalMilliseconds < MillisecondCutoff
            ? $"{value.TotalMilliseconds:0} ms"
            : $"{value.TotalSeconds:0.0} s";
    }
}
