namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The pure decisions behind a resumable pack download: where to resume a
/// <c>.partial</c> file, how long to back off, and which outcomes are worth
/// another attempt.
/// </summary>
public static class PackRetryPolicy
{
    public const int MaxAttempts = 4;

    /// <summary>A corrupted download gets one clean re-download, no more.</summary>
    public const int MaxHashMismatchAttempts = 2;

    private const int BaseDelaySeconds = 2;
    private const int MaxDelaySeconds = 30;
    private const int MaxBackoffExponent = 16;

    /// <summary>
    /// The byte offset to resume from given the <c>.partial</c> length: 0 (start
    /// over) when there is none or it is larger than the pack, otherwise its length.
    /// A length equal to the pack size means the body is complete and only needs verifying.
    /// </summary>
    public static long ResumeOffset(long partialLength, long expectedSize) =>
        partialLength > 0 && partialLength <= expectedSize ? partialLength : 0;

    /// <summary>Exponential backoff (2s, 4s, 8s ...) capped at 30s; <paramref name="attempt"/> is 1-based.</summary>
    public static TimeSpan Backoff(int attempt)
    {
        int exponent = Math.Clamp(attempt, 1, MaxBackoffExponent);
        double seconds = Math.Min(BaseDelaySeconds * Math.Pow(2, exponent - 1), MaxDelaySeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Whether to try again after <paramref name="result"/> on the 1-based <paramref name="attempt"/>.
    /// Offline stops at once (the UI offers Retry); a transient failure retries with backoff;
    /// a hash mismatch re-downloads once; an invalid pack never retries (it would fail the same way).
    /// </summary>
    public static bool ShouldRetry(PackDownloadResult result, int attempt) => result switch
    {
        PackDownloadResult.Failed => attempt < MaxAttempts,
        PackDownloadResult.HashMismatch => attempt < MaxHashMismatchAttempts,
        _ => false,
    };
}
