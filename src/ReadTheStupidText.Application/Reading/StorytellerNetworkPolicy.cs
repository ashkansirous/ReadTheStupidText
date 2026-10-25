namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Dino Reader's first storyteller download: allowed whenever a Wi-Fi or Ethernet link is active, even when
/// mobile data is also listed (Android reports both). Only a cellular-only device waits for Wi-Fi.
/// Separate from <see cref="PackUpdatePolicy"/>, which stays strict for silent pack updates.
/// </summary>
public static class StorytellerNetworkPolicy
{
    public static bool AllowsAutomaticDownload(IEnumerable<NetworkProfile> active)
    {
        return active.Any(p => p is NetworkProfile.WiFi or NetworkProfile.Ethernet);
    }
}
