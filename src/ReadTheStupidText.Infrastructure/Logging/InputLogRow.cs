using System.Globalization;
using ReadTheStupidText.Domain.Activity;

namespace ReadTheStupidText.Infrastructure.Logging;

/// <summary>
/// Formats one tab-separated input-log row from an activity entry: the same
/// columns the Activity-Log grid shows plus the id. Pure (no IO) so it can be
/// unit-tested. The redacted text is flattened to a single line so a row never
/// spans more than one physical line.
/// </summary>
public static class InputLogRow
{
    private const char Separator = '\t';

    /// <summary>The column header written once at the top of each day's file.</summary>
    public static readonly string Header = string.Join(
        Separator,
        "timestamp", "id", "trigger", "state", "reason", "source", "first_audio_ms", "synth_ms", "text");

    public static string Format(DateTimeOffset timestamp, ActivityEntry entry) =>
        string.Join(
            Separator,
            timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
            entry.Id.ToString(CultureInfo.InvariantCulture),
            entry.Trigger,
            entry.State,
            entry.Reason,
            Source(entry.Window),
            Millis(entry.TimeToFirstAudio),
            Millis(entry.SynthesisDuration),
            OneLine(entry.Text));

    private static string Source(WindowSource? window) =>
        window is null ? "-" : $"{OrDash(window.App)} | {OrDash(window.Title)}";

    private static string OrDash(string value) => string.IsNullOrEmpty(value) ? "-" : value;

    private static string Millis(TimeSpan? span) =>
        span is { } value ? ((long)value.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) : string.Empty;

    private static string OneLine(string text) =>
        text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
}
