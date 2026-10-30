using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared.Network;

namespace ReadTheStupidText.Mobile.Shared.ModelDownload;

/// <summary>
/// The phone's real network for a <see cref="TranslationModelDownloadSession"/> (Slice 187): MAUI <c>Connectivity</c> decides whether there is internet,
/// and the app's one <see cref="PackResumeWatcher"/> says when the network changed, so no second connectivity subscription exists.
/// </summary>
public sealed class ConnectivityTranslationNetwork(PackResumeWatcher watcher) : ITranslationNetwork
{
    public bool IsOnline => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public IReadOnlyCollection<NetworkProfile> Profiles =>
        [.. Connectivity.Current.ConnectionProfiles.Select(profile => profile switch
        {
            ConnectionProfile.WiFi => NetworkProfile.WiFi,
            ConnectionProfile.Ethernet => NetworkProfile.Ethernet,
            ConnectionProfile.Cellular => NetworkProfile.Cellular,
            _ => NetworkProfile.Other,
        })];

    public event EventHandler? Changed
    {
        add => watcher.NetworkChanged += value;
        remove => watcher.NetworkChanged -= value;
    }

    /// <summary>A download session on the phone's real network: the one place the pack key, the network and the mobile-data choice are wired together.</summary>
    public static TranslationModelDownloadSession NewSession(string packKey, Func<IProgress<double>, CancellationToken, Task> attempt, PackResumeWatcher watcher) =>
        new(packKey, attempt, new ConnectivityTranslationNetwork(watcher), BeganOnMobileData());

    /// <summary>True when the phone is on mobile data alone right now: a download begun then is one the person accepted on mobile data.</summary>
    public static bool BeganOnMobileData() => PackResumeWatcher.CellularOnlyKnown();
}
