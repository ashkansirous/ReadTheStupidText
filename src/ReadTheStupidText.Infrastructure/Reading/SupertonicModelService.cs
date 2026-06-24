using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Locates the Supertonic-3 neural voice model that ships inside the package
/// (copied next to the app under <c>VoiceModel/</c>). No download is needed — the
/// model is always present, so the service is ready as soon as it is initialized.
/// </summary>
public sealed class SupertonicModelService : IVoiceModelService
{
    private const string ModelFolder = "VoiceModel";

    // The files the engine needs; presence of all of them means "ready".
    private static readonly string[] RequiredFiles =
    [
        SupertonicFiles.DurationPredictor,
        SupertonicFiles.TextEncoder,
        SupertonicFiles.VectorEstimator,
        SupertonicFiles.Vocoder,
        SupertonicFiles.TtsJson,
        SupertonicFiles.UnicodeIndexer,
        SupertonicFiles.VoiceStyle,
    ];

    public bool IsReady { get; private set; }

    public VoiceModelPaths? Paths { get; private set; }

    public event EventHandler? ReadyChanged;

    public Task InitializeAsync(IProgress<double>? progress = null)
    {
        string modelDir = Path.Combine(AppContext.BaseDirectory, ModelFolder);
        if (AllFilesPresent(modelDir))
        {
            Paths = new VoiceModelPaths(modelDir);
            IsReady = true;
            ReadyChanged?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }

    private static bool AllFilesPresent(string modelDir) =>
        RequiredFiles.All(f => File.Exists(Path.Combine(modelDir, f)));
}
