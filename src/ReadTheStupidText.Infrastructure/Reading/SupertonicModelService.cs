using System.Formats.Tar;
using ICSharpCode.SharpZipLib.BZip2;
using ReadTheStupidText.Application.Reading;
using Windows.Storage;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Downloads and unpacks the Supertonic-3 neural voice model on first run, into
/// the app's local storage. The model ships as a <c>.tar.bz2</c> release asset;
/// .NET supplies the tar reader, SharpZipLib the bzip2 layer. Subsequent launches
/// find the unpacked files already present and become ready immediately.
/// </summary>
public sealed class SupertonicModelService : IVoiceModelService
{
    private const string ModelName = "sherpa-onnx-supertonic-3-tts-int8-2026-05-11";
    private const string DownloadUrl =
        "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/sherpa-onnx-supertonic-3-tts-int8-2026-05-11.tar.bz2";

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

    private static readonly HttpClient Http = new();

    public bool IsReady { get; private set; }

    public VoiceModelPaths? Paths { get; private set; }

    public event EventHandler? ReadyChanged;

    public async Task InitializeAsync(IProgress<double>? progress = null)
    {
        try
        {
            string root = ApplicationData.Current.LocalFolder.Path;
            string modelDir = Path.Combine(root, ModelName);

            if (!AllFilesPresent(modelDir))
            {
                await DownloadAndExtractAsync(root, progress);
            }

            if (AllFilesPresent(modelDir))
            {
                MarkReady(new VoiceModelPaths(modelDir));
            }
        }
        catch
        {
            // Leave IsReady false; the reader falls back to the system voice and
            // the picker stays in its "preparing" state. A later launch retries.
        }
    }

    private void MarkReady(VoiceModelPaths paths)
    {
        Paths = paths;
        IsReady = true;
        ReadyChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool AllFilesPresent(string modelDir) =>
        RequiredFiles.All(f => File.Exists(Path.Combine(modelDir, f)));

    private static async Task DownloadAndExtractAsync(string root, IProgress<double>? progress)
    {
        string archive = Path.Combine(root, ModelName + ".tar.bz2");
        await DownloadAsync(archive, progress);
        await Task.Run(() => Extract(archive, root));
        File.Delete(archive);
    }

    private static async Task DownloadAsync(string destination, IProgress<double>? progress)
    {
        using HttpResponseMessage response =
            await Http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength;
        await using Stream source = await response.Content.ReadAsStreamAsync();
        await using FileStream file = File.Create(destination);

        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read));
            received += read;
            if (total is > 0)
            {
                progress?.Report((double)received / total.Value);
            }
        }
    }

    // The archive expands to "<ModelName>/..." entries, landing under <root>.
    private static void Extract(string archivePath, string root)
    {
        using FileStream compressed = File.OpenRead(archivePath);
        using var bzip2 = new BZip2InputStream(compressed);
        using var tar = new TarReader(bzip2);

        while (tar.GetNextEntry() is { } entry)
        {
            string destination = Path.Combine(root, entry.Name);
            if (entry.EntryType is TarEntryType.Directory)
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
        }
    }
}
