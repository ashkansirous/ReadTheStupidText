using ReadTheStupidText.Application.Reading;
using Windows.Networking.Connectivity;

namespace ReadTheStupidText.Infrastructure.Translation;

/// <summary>
/// Whether the active Windows connection is unmetered (Slice 142): the early start downloads a missing translation pack
/// only then. No connection, an unknown cost, a metered or a roaming link all say no.
/// </summary>
public sealed class WindowsPackUpdateNetwork : IPackUpdateNetwork
{
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
                return false; // cannot tell: treat as metered
            }
        }
    }
}
