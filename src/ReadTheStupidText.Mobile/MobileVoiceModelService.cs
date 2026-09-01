using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Locates the Supertonic-3 neural voice model on Android (Slice 31, Decision 39).
/// The model ships as a <c>MauiAsset</c> (packaged under <c>assets/VoiceModel/</c>),
/// but sherpa-onnx's native loader needs real file paths — package assets are only
/// reachable as streams — so this copies each required file out to app-local
/// storage once, then reuses the extracted copy on every later launch.
/// </summary>
public sealed class MobileVoiceModelService : IVoiceModelService
{
    private const string ModelFolder = "VoiceModel";

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

    public async Task InitializeAsync(IProgress<double>? progress = null)
    {
        string modelDir = Path.Combine(FileSystem.Current.AppDataDirectory, ModelFolder);
        Directory.CreateDirectory(modelDir);

        for (int i = 0; i < RequiredFiles.Length; i++)
        {
            string file = RequiredFiles[i];
            string destination = Path.Combine(modelDir, file);
            if (!File.Exists(destination))
            {
                await ExtractAsync(file, destination);
            }

            progress?.Report((i + 1) / (double)RequiredFiles.Length);
        }

        Paths = new VoiceModelPaths(modelDir);
        IsReady = true;
        ReadyChanged?.Invoke(this, EventArgs.Empty);
    }

    private static async Task ExtractAsync(string assetFile, string destination)
    {
        // Extract to a temp path first and rename into place: a crash/kill mid-copy
        // then leaves no half-written file for File.Exists to wrongly trust next launch.
        string tempDestination = destination + ".partial";
        await using (Stream asset = await FileSystem.OpenAppPackageFileAsync($"{ModelFolder}/{assetFile}"))
        await using (FileStream output = File.Create(tempDestination))
        {
            await asset.CopyToAsync(output);
        }

        File.Move(tempDestination, destination, overwrite: true);
    }
}
