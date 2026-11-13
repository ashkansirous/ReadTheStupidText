namespace DinoReader.Core;

/// <summary>How the last try to get one book's audio pack in one language ended (Slice 360).</summary>
public enum PackOutcome
{
    Ok,
    Failed,
    Missing404,
    Offline,
}

/// <summary>What the app remembers, in memory only, about the last try for one (book, language) pack (Slice 360). <see cref="ListedVersion"/> is the pack version that try asked for.</summary>
public sealed record PackRepairEntry(DateTimeOffset LastAttemptUtc, PackOutcome LastOutcome, int? ListedVersion = null);

/// <summary>The one rule for when a missing pack is tried again (Slice 360, Decision 1): at once the first time in a launch, then at most once an hour, and at once for a version it has not tried.</summary>
public static class PackRepairDue
{
    /// <summary>The wait between two tries of the same pack.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(60);

    /// <summary>
    /// Whether the pack is due. Never tried in this launch: due. A newer (or different) listed version than the one last asked for is a new attempt: due. Otherwise due once <see cref="Interval"/> has passed;
    /// an <see cref="PackOutcome.Offline"/> try is also due when <paramref name="networkBack"/> says the phone just got its connection back. A pack that is installed is never asked about.
    /// </summary>
    public static bool IsDue(PackRepairEntry? entry, DateTimeOffset now, int? listedVersion = null, bool networkBack = false)
    {
        if (entry is null || entry.LastOutcome == PackOutcome.Ok)
        {
            return true; // an Ok entry is a pack that arrived; if it is asked about again it was removed since, so it is a first try (AnyDue never counts an Ok entry)
        }

        if (listedVersion is { } version && entry.ListedVersion is { } tried && version != tried)
        {
            return true;
        }

        if (networkBack && entry.LastOutcome == PackOutcome.Offline)
        {
            return true;
        }

        return now - entry.LastAttemptUtc >= Interval;
    }
}
