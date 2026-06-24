namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The neural model's root directory. The synthesis engine knows its own file
/// layout under this directory; keeping the contract to a single folder makes it
/// engine-agnostic.
/// </summary>
public sealed record VoiceModelPaths(string RootDir);

/// <summary>
/// Locates the local neural voice model (shipped in the package), reports
/// readiness, and exposes its directory. The synthesis engine stays silent on the
/// neural path until <see cref="IsReady"/> is true.
/// </summary>
public interface IVoiceModelService
{
    /// <summary>Whether the model is present and ready to synthesize.</summary>
    bool IsReady { get; }

    /// <summary>The model directory once <see cref="IsReady"/> is true; otherwise null.</summary>
    VoiceModelPaths? Paths { get; }

    /// <summary>Raised once, on the transition to ready.</summary>
    event EventHandler? ReadyChanged;

    /// <summary>
    /// Locates the model and marks the service ready. Safe to call once at startup;
    /// leaves <see cref="IsReady"/> false if the model files are missing. The
    /// <paramref name="progress"/> parameter is unused for a packaged model.
    /// </summary>
    Task InitializeAsync(IProgress<double>? progress = null);
}
