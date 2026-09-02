namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Pure eligibility rule for the audio-chunk startup sweep (Decision 49), extracted
/// so it's unit-testable without touching disk. A read folder is orphaned — left
/// behind by a read that never reached a terminal state, e.g. the app was killed
/// mid-read — once its last write is at least <c>maxAge</c> old.
/// </summary>
public static class AudioChunkSweep
{
    public static bool IsEligible(DateTime folderLastWriteTime, DateTime now, TimeSpan maxAge) =>
        now - folderLastWriteTime >= maxAge;
}
