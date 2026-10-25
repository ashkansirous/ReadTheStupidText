using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using SherpaOnnx;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// An <see cref="OfflineTts"/> disposed when its last holder releases it (Slice 55, the iOS
/// counterpart of the Android reader's private type). The reader holds one reference while the
/// engine is current; each read and each in-flight generation holds another.
/// </summary>
internal sealed class IosRefCountedTts(OfflineTts tts)
{
    private int _references = 1;

    public OfflineTts Tts { get; } = tts;

    public bool IsAlive => Volatile.Read(ref _references) > 0;

    public bool TryAcquire()
    {
        int current;
        do
        {
            current = Volatile.Read(ref _references);
            if (current <= 0)
            {
                return false;
            }
        }
        while (Interlocked.CompareExchange(ref _references, current + 1, current) != current);

        return true;
    }

    public void Release()
    {
        if (Interlocked.Decrement(ref _references) == 0)
        {
            Tts.Dispose();
        }
    }
}

/// <summary>
/// An engine and the model it was built from, never changed after creation. A language change
/// within one pack shares the <see cref="IosRefCountedTts"/>.
/// </summary>
internal sealed record IosBuiltEngine(IosRefCountedTts Tts, VoiceModelPaths Model) : IEngineHandle
{
    public bool TryAcquire() => Tts.TryAcquire();

    public void Release() => Tts.Release();
}

/// <summary>
/// Builds the sherpa-onnx engine for an installed voice pack on iOS and runs one synthesis
/// (Slice 55): Supertonic, Piper (the VITS family, phonemized with the pack's espeak-ng data)
/// and MeloTTS, as on Android. Which engine a pack needs comes from its
/// <see cref="VoiceModelPaths.Kind"/>, never from the platform.
/// </summary>
internal static class IosTtsEngine
{
    // Same reasoning as the Android reader: intra-op ONNX threads per synthesis, scaled to the
    // device but capped so one Generate does not over-claim cores the concurrent chunks need.
    private static readonly int SynthesisThreads = Math.Clamp(Environment.ProcessorCount / 2, 2, 4);

    public static readonly int MaxSynthesisConcurrency =
        Math.Clamp(Environment.ProcessorCount / SynthesisThreads, 2, 4);

    // OfflineTtsGenerationConfig.Extra key that selects the Supertonic language.
    private const string SupertonicLanguageKey = "lang";

    private const string CpuProvider = "cpu";
    private const float NormalSpeed = 1.0f;

    /// <summary>
    /// The engine for <paramref name="paths"/>: the live <paramref name="previous"/> of the same model folder
    /// re-labelled for the new language (it shares the loaded model), otherwise a freshly built one.
    /// </summary>
    public static IosBuiltEngine Create(IosBuiltEngine? previous, VoiceModelPaths paths) =>
        previous is not null && previous.Tts.TryAcquire()
            ? new IosBuiltEngine(previous.Tts, paths)
            : new IosBuiltEngine(new IosRefCountedTts(Build(paths)), paths);

    // Supertonic chooses its language per generation (Extra "lang"); the VITS-family models are
    // single-language and take only the speaker id. Both come from the model the read captured,
    // never from whatever is current now.
    public static OfflineTtsGeneratedAudio Generate(
        IosBuiltEngine engine, string text, int speakerId, float pace = NormalSpeed)
    {
        VoiceModelPaths model = engine.Model;
        if (model.Kind != VoiceEngineKind.Supertonic)
        {
            return engine.Tts.Tts.Generate(text, pace, speakerId);
        }

        var config = new OfflineTtsGenerationConfig { Sid = speakerId, Speed = pace };
        config.Extra[SupertonicLanguageKey] = model.Language;
        return engine.Tts.Tts.GenerateWithConfig(text, config, KeepGenerating);
    }

    // Returning 1 tells sherpa-onnx to keep generating (0 would stop early).
    private static int KeepGenerating(IntPtr samples, int count, float progress, IntPtr arg) => 1;

    private static OfflineTts Build(VoiceModelPaths paths)
    {
        IosSherpaLibrary.EnsureResolver();
        var config = new OfflineTtsConfig();
        switch (paths.Kind)
        {
            case VoiceEngineKind.Supertonic:
                ConfigureSupertonic(ref config, paths.RootDir);
                break;
            case VoiceEngineKind.Vits:
                ConfigureVits(ref config, paths);
                break;
            case VoiceEngineKind.Melo:
                ConfigureMelo(ref config, paths.RootDir);
                break;
            default:
                throw new NotSupportedException($"No engine for voice model kind {paths.Kind}.");
        }

        config.Model.NumThreads = SynthesisThreads;
        config.Model.Provider = CpuProvider;
        RequireModelFiles(paths, config);
        return new OfflineTts(config);
    }

    // OfflineTtsConfig and its nested model configs are structs, so a Configure* helper must
    // take the config by ref: a by-value copy silently drops every path. The native constructor
    // reports a rejected config only by returning a null handle, which the C# wrapper never
    // checks (the first Generate then logs "tts is nullptr"), so a missing model file is turned
    // into an exception here, with the path in it.
    private static void RequireModelFiles(VoiceModelPaths paths, OfflineTtsConfig config)
    {
        foreach (string file in RequiredFiles(paths.Kind, config))
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                string message = $"Voice model file missing for {paths.Kind}: '{file}'.";
                System.Diagnostics.Debug.WriteLine(message);
                throw new FileNotFoundException(message, file);
            }
        }
    }

    private static string[] RequiredFiles(VoiceEngineKind kind, OfflineTtsConfig config) =>
        kind switch
        {
            VoiceEngineKind.Supertonic =>
            [
                config.Model.Supertonic.DurationPredictor, config.Model.Supertonic.TextEncoder,
                config.Model.Supertonic.VectorEstimator, config.Model.Supertonic.Vocoder,
                config.Model.Supertonic.TtsJson, config.Model.Supertonic.UnicodeIndexer,
                config.Model.Supertonic.VoiceStyle,
            ],
            _ => [config.Model.Vits.Model, config.Model.Vits.Tokens],
        };

    private static void ConfigureSupertonic(ref OfflineTtsConfig config, string dir)
    {
        config.Model.Supertonic.DurationPredictor = Path.Combine(dir, SupertonicFiles.DurationPredictor);
        config.Model.Supertonic.TextEncoder = Path.Combine(dir, SupertonicFiles.TextEncoder);
        config.Model.Supertonic.VectorEstimator = Path.Combine(dir, SupertonicFiles.VectorEstimator);
        config.Model.Supertonic.Vocoder = Path.Combine(dir, SupertonicFiles.Vocoder);
        config.Model.Supertonic.TtsJson = Path.Combine(dir, SupertonicFiles.TtsJson);
        config.Model.Supertonic.UnicodeIndexer = Path.Combine(dir, SupertonicFiles.UnicodeIndexer);
        config.Model.Supertonic.VoiceStyle = Path.Combine(dir, SupertonicFiles.VoiceStyle);
    }

    // Piper: the espeak-ng data directory phonemizes the text (it lives only in the pack).
    // The voice's own model + tokens may sit in a folder of the pack (Slice 89); the data directory
    // is shared and stays at the pack root.
    private static void ConfigureVits(ref OfflineTtsConfig config, VoiceModelPaths paths)
    {
        config.Model.Vits.Model = Path.Combine(paths.ModelFolder, VitsFiles.Model);
        config.Model.Vits.Tokens = Path.Combine(paths.ModelFolder, VitsFiles.Tokens);
        config.Model.Vits.DataDir = Path.Combine(paths.RootDir, VitsFiles.EspeakDataDir);
    }

    // MeloTTS zh_en: lexicon + jieba dictionary, with the phone/date/number rule FSTs the
    // upstream example uses so digits and dates are spoken as words.
    private static void ConfigureMelo(ref OfflineTtsConfig config, string dir)
    {
        config.Model.Vits.Model = Path.Combine(dir, MeloFiles.Model);
        config.Model.Vits.Lexicon = Path.Combine(dir, MeloFiles.Lexicon);
        config.Model.Vits.Tokens = Path.Combine(dir, MeloFiles.Tokens);
        config.Model.Vits.DictDir = Path.Combine(dir, MeloFiles.DictDir);
        config.RuleFsts = string.Join(',', MeloFiles.RuleFsts.Select(file => Path.Combine(dir, file)));
    }
}
