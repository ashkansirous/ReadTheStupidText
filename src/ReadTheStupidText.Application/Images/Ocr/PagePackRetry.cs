using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Images;

/// <summary>Why a pack download failed, as far as the person can act on it (Slice 87).</summary>
public enum PackFailureKind
{
    /// <summary>No connection: say so.</summary>
    Offline,

    /// <summary>A failure a second try may fix (a corrupt or interrupted download).</summary>
    Retryable,

    /// <summary>The pack itself is wrong; trying again cannot help.</summary>
    Permanent,
}

/// <summary>When a failed page-language pack download offers Retry (Slice 87). Pure.</summary>
public static class PagePackRetry
{
    /// <summary>The most tries a person gets for one pack (the first included); then Retry is no longer offered.</summary>
    public const int MaxAttempts = 3;

    public static PackFailureKind Classify(PackDownloadResult result) => result switch
    {
        PackDownloadResult.Offline => PackFailureKind.Offline,
        PackDownloadResult.InvalidPack => PackFailureKind.Permanent,
        _ => PackFailureKind.Retryable,
    };

    /// <summary>True when attempt number <paramref name="attempt"/> (1-based) failed with <paramref name="result"/> and another try is offered.</summary>
    public static bool OffersRetry(PackDownloadResult result, int attempt) =>
        Classify(result) != PackFailureKind.Permanent && attempt < MaxAttempts;
}
