namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The unpacked model's root directory, once it has been downloaded. The
/// synthesis engine knows its own file layout under this directory; keeping the
/// contract to a single folder makes it engine-agnostic.
/// </summary>
public sealed record VoiceModelPaths(string RootDir);

/// <summary>
/// Owns the local neural voice model: ensures it is present (downloading it on
/// first run), reports readiness, and exposes its file locations. The synthesis
/// engine stays silent on the neural path until <see cref="IsReady"/> is true.
/// </summary>
public interface IVoiceModelService
{
    /// <summary>Whether the model has been downloaded and is ready to synthesize.</summary>
    bool IsReady { get; }

    /// <summary>The model file locations once <see cref="IsReady"/> is true; otherwise null.</summary>
    VoiceModelPaths? Paths { get; }

    /// <summary>Raised once, on the transition to ready.</summary>
    event EventHandler? ReadyChanged;

    /// <summary>
    /// Ensures the model is available, downloading and unpacking it on first run.
    /// Safe to call once at startup; returns when the model is ready (or on failure,
    /// leaving <see cref="IsReady"/> false). Progress is reported as 0.0–1.0.
    /// </summary>
    Task InitializeAsync(IProgress<double>? progress = null);
}
