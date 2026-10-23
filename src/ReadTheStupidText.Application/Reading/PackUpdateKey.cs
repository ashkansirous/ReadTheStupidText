namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Names one version of one pack, so an update that failed for good is remembered and not repeated until the
/// app restarts. Only a download that was fetched and then rejected counts; a missing connection or a
/// transient failure is retried on the next check.
/// </summary>
public static class PackUpdateKey
{
    private const char Separator = '@';

    public static string For(PackInfo pack) => pack.Id + Separator + pack.Version;

    /// <summary>True for a result a retry of the same version cannot fix: the bytes or the pack content are wrong.</summary>
    public static bool IsPermanentFailure(PackDownloadResult result) =>
        result is PackDownloadResult.HashMismatch or PackDownloadResult.InvalidPack;
}
