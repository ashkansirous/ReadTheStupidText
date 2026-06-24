namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The on-disk locations of the neural voice model's files, once it has been
/// downloaded and unpacked. Passed to the synthesis engine to construct itself.
/// </summary>
public sealed record VoiceModelPaths(string Model, string Voices, string Tokens, string DataDir);

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
