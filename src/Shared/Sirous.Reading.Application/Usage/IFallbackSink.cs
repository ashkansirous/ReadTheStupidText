namespace Sirous.Reading.Application.Usage;

/// <summary>
/// Where shared code reports that a feature fell back to something else (Slice 273 part B). Shared code (Application, Infrastructure, Mobile.Shared) takes one and never knows which app it runs in:
/// Read The Stupid Text answers with <see cref="FallbackReporter"/>, Dino Reader with its own sink over <c>UsageCounter</c>, and a test or a caller with no usage with <see cref="NoFallbackSink"/>.
/// Reporting never throws and never changes what the fallback does.
/// </summary>
public interface IFallbackSink
{
    /// <summary>Reports a fallback; <paramref name="area"/> and <paramref name="reason"/> are <see cref="FallbackArea"/> and <see cref="FallbackReason"/> values, <paramref name="language"/> the language it concerns when known.</summary>
    void Report(string area, string reason, string? language = null);
}

/// <summary>The sink that does nothing: the default of every constructor that takes an optional <see cref="IFallbackSink"/>.</summary>
public sealed class NoFallbackSink : IFallbackSink
{
    public static NoFallbackSink Instance { get; } = new();

    public void Report(string area, string reason, string? language = null)
    {
    }
}
