using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>Reads the active connection profiles from MAUI Connectivity (Connectivity.Current.ConnectionProfiles).</summary>
public sealed class MauiPackUpdateNetwork : IPackUpdateNetwork
{
    public bool AllowsAutomaticUpdate =>
        PackUpdatePolicy.AllowsAutomaticUpdate(Connectivity.Current.ConnectionProfiles.Select(ToProfile));

    private static NetworkProfile ToProfile(ConnectionProfile profile) => profile switch
    {
        ConnectionProfile.WiFi => NetworkProfile.WiFi,
        ConnectionProfile.Ethernet => NetworkProfile.Ethernet,
        ConnectionProfile.Cellular => NetworkProfile.Cellular,
        _ => NetworkProfile.Other,
    };
}
