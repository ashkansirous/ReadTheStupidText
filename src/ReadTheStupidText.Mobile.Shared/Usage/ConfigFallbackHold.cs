using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Mobile.Shared.Usage;

/// <summary>
/// Slice 273 part C: holds the fallbacks that happen before the app has a real remote config (a failed first config fetch, the first-launch UI language fallback) and hands them on once
/// one arrives. Until then the config is only the default, which has telemetry off, so a count queued now would be dropped at once. The owner calls <see cref="Release"/> when the config
/// changes (its first arrival); the queue then keeps the counts only if telemetry is on. It never fetches anything, so reporting through it cannot loop.
/// </summary>
public sealed class ConfigFallbackHold(Func<bool> configHeld, Action<string, string, string?> send)
{
    private readonly object _gate = new();
    private readonly List<(string Area, string Reason, string? Language)> _held = [];

    /// <summary>True for the areas that can fire before a config exists.</summary>
    public static bool Applies(string area) => area is FallbackArea.Config or FallbackArea.UiLanguage;

    /// <summary>True when the fallback was kept for later (no real config is held yet); false means send it now.</summary>
    public bool TryHold(string area, string reason, string? language)
    {
        if (!Applies(area))
        {
            return false;
        }

        lock (_gate)
        {
            if (configHeld())
            {
                return false;
            }

            if (!_held.Contains((area, reason, language)))
            {
                _held.Add((area, reason, language)); // one per kind: repeats of a failing fetch must not replay as a burst
            }

            return true;
        }
    }

    /// <summary>Sends what was held, in the order it happened. Call when the config has arrived.</summary>
    public void Release()
    {
        (string Area, string Reason, string? Language)[] held;
        lock (_gate)
        {
            held = [.. _held];
            _held.Clear();
        }

        foreach ((string area, string reason, string? language) in held)
        {
            try
            {
                send(area, reason, language);
            }
            catch (Exception)
            {
                // Counting is best effort: it never changes what the fallback does.
            }
        }
    }
}
