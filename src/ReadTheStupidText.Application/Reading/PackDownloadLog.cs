using System.Diagnostics;
using System.Globalization;

namespace ReadTheStupidText.Application.Reading;

/// <summary>Which pack a guarded download is for, and the file position it started at (the resume offset).</summary>
public sealed record DownloadTag(string PackId, long Offset);

/// <summary>
/// Diagnostic lines about voice pack and story downloads: start, resume, stall, retry, done, failed. Written to the
/// debug output, and also to <see cref="Sink"/> when a Debug build sets one (the mobile apps point it at logcat tag
/// <c>RTST-TEST</c>). Ids, byte counts and result names only: never a URL, a path or user text.
/// </summary>
public static class PackDownloadLog
{
    public const string Prefix = "PACKDL";
    public const string StartEvent = "start";
    public const string RangeEvent = "range";
    public const string StallEvent = "stall";
    public const string RetryEvent = "retry";
    public const string DoneEvent = "done";
    public const string FailedEvent = "failed";
    public const string ProgressEvent = "progress";
    public const string ResumeEvent = "resume";
    public const string WaitingEvent = "waiting";
    public const string RetryReason = "retry";
    public const string LanguageFailedEvent = "language-failed";
    public const string HeadersPhase = "headers";
    public const string BodyPhase = "body";

    /// <summary>Set once at start-up by a Debug build; null in Release.</summary>
    public static Action<string>? Sink
    {
        get => Volatile.Read(ref _sink);
        set => Volatile.Write(ref _sink, value);
    }

    private static Action<string>? _sink;

    public static void Start(string packId, long offset) => Write($"{StartEvent} pack={packId} offset={offset}");

    public static void Range(string packId, long offset) => Write($"{RangeEvent} pack={packId} offset={offset}");

    /// <summary>Slice 241: the bytes in the partial file so far, at most every <see cref="PackStallWatch.ProgressInterval"/> while the body moves.</summary>
    public static void Progress(string packId, long bytes) => Write($"{ProgressEvent} pack={packId} bytes={bytes}");

    public static void Stall(string phase, long bytes, string? packId = null) =>
        Write(packId is null ? $"{StallEvent} phase={phase} bytes={bytes}" : $"{StallEvent} pack={packId} phase={phase} bytes={bytes}");

    public static void Retry(string packId, int attempt, PackDownloadResult result, TimeSpan backoff) =>
        Write($"{RetryEvent} pack={packId} attempt={attempt} result={result} backoffSec={backoff.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)}");

    public static void Resume(PackResumeReason reason, string packId, long offset) =>
        Write($"{ResumeEvent} reason={reason.ToString().ToLowerInvariant()} pack={packId} offset={offset}");

    /// <summary>The person tapped Retry on a waiting translation model download (Slice 187): not a foreground return.</summary>
    public static void ResumeByRetry(string packId) => Write($"{ResumeEvent} reason={RetryReason} pack={packId} offset=0");

    public static void ResumeSkipped(PackResumeReason reason, bool online, string profiles) =>
        Write($"{ResumeEvent} reason={reason.ToString().ToLowerInvariant()} skipped online={online} network={profiles}");

    /// <summary>A translation model download has no internet and waits (Slice 187).</summary>
    public static void Waiting(string packId) => Write($"{WaitingEvent} pack={packId}");

    public static void Done(string packId, long bytes, TimeSpan elapsed) =>
        Write($"{DoneEvent} pack={packId} bytes={bytes} seconds={elapsed.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)}");

    public static void Failed(string packId, int attempt, PackDownloadResult result) =>
        Write($"{FailedEvent} pack={packId} attempts={attempt} result={result}");

    public static void LanguageFailure(string language, PackDownloadResult result, PackFailureAction action) =>
        Write($"{LanguageFailedEvent} language={language} result={result} action={action}");

    private static void Write(string line)
    {
        string text = $"{Prefix} {line}";
        Debug.WriteLine(text);
        try
        {
            Volatile.Read(ref _sink)?.Invoke(text);
        }
        catch (Exception)
        {
            // A diagnostic sink must never replace the download's own exception.
        }
    }
}
