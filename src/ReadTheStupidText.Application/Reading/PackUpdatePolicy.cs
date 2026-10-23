namespace ReadTheStupidText.Application.Reading;

/// <summary>The kinds of network connection a device can report, as the platform layer maps them.</summary>
public enum NetworkProfile
{
    Other,
    WiFi,
    Ethernet,
    Cellular,
}

/// <summary>Whether the device may download a pack update without asking (a metered link must not).</summary>
public interface IPackUpdateNetwork
{
    bool AllowsAutomaticUpdate { get; }
}

/// <summary>
/// An automatic update of an already-installed pack runs only on an unmetered connection: Wi-Fi or
/// Ethernet. On cellular (or any connection that is not clearly unmetered) the installed pack stays
/// Ready and the check happens again next launch.
/// </summary>
public static class PackUpdatePolicy
{
    public static bool AllowsAutomaticUpdate(IEnumerable<NetworkProfile> active)
    {
        // Read once: the platform reports the live connections, which can change between two passes.
        NetworkProfile[] profiles = [.. active];
        return profiles.Any(p => p is NetworkProfile.WiFi or NetworkProfile.Ethernet) && !profiles.Contains(NetworkProfile.Cellular);
    }
}
