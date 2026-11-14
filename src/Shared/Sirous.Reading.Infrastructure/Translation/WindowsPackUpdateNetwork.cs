using Sirous.Reading.Application.Reading;
using Sirous.Reading.Application.Usage;
using Windows.Networking.Connectivity;

namespace Sirous.Reading.Infrastructure.Translation;

/// <summary>
/// Whether the active Windows connection is unmetered (Slice 142): the early start downloads a missing translation pack
/// only then. No connection, an unknown cost, a metered or a roaming link all say no.
/// </summary>
public sealed class WindowsPackUpdateNetwork : IPackUpdateNetwork
{
    private readonly IFallbackSink _fallbacks; // Slice 275

    public WindowsPackUpdateNetwork(IFallbackSink? fallbacks = null) => _fallbacks = fallbacks ?? NoFallbackSink.Instance;

    public bool AllowsAutomaticUpdate
    {
        get
        {
            try
            {
                ConnectionCost? cost = NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost();
                return cost is { NetworkCostType: NetworkCostType.Unrestricted, Roaming: false, BackgroundDataUsageRestricted: false };
            }
            catch (Exception)
            {
                _fallbacks.Report(FallbackArea.Pack, FallbackReason.UpdateCheckFailed); // Slice 275: an unreadable connection cost defers an automatic pack update (treated as metered)
                return false; // cannot tell: treat as metered
            }
        }
    }
}
