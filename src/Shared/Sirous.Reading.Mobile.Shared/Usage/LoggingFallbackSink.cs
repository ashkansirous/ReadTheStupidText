using Sirous.Reading.Application.Usage;

namespace Sirous.Reading.Mobile.Shared.Usage;

/// <summary>
/// Read The Stupid Text's phone <see cref="IFallbackSink"/> (Slice 273 part B): hands each fallback to the shared <see cref="FallbackReporter"/> and, in a Debug build only, writes one
/// <c>[Usage] fallback &lt;area&gt;:&lt;reason&gt;</c> line to the test-hook log, so a device flow can prove which fallback fired. Never throws.
/// </summary>
internal sealed class LoggingFallbackSink(FallbackReporter reporter, ConfigFallbackHold? hold = null) : IFallbackSink
{
    internal FallbackReporter Reporter { get; } = reporter;

    public void Report(string area, string reason, string? language = null)
    {
#if DEBUG
        Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Usage] fallback {area}:{reason}");
#endif
        if (hold?.TryHold(area, reason, language) == true)
        {
            return; // no real config yet: kept until one arrives, since the default has telemetry off and would drop it
        }

        Reporter.Report(area, reason, language);
    }
}
