using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using SherpaOnnx;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Builds the sherpa-onnx engine for a pack's model family (Slice 56, the Windows twin of the Android
/// reader's engine setup from Slice 51) and runs one generation. Supertonic chooses its language per
/// generation; the VITS-family models (Piper, Melo) are single-language and take only a speaker id.
/// </summary>
internal static class SherpaEngineFactory
{
    // OfflineTtsGenerationConfig.Extra key that selects the Supertonic language.
    private const string SupertonicLanguageKey = "lang";

    private const string CpuProvider = "cpu";

    public static OfflineTts Build(VoiceModelPaths paths, int threads)
    {
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

        config.Model.NumThreads = threads;
        config.Model.Provider = CpuProvider;
        RequireModelFiles(paths, config);
        return new OfflineTts(config);
    }

    /// <summary>Generates speech at 1x (the player applies the rate) for the model the read captured.</summary>
    public static OfflineTtsGeneratedAudio Generate(BuiltEngine engine, string text, int speakerId)
    {
        VoiceModelPaths model = engine.Model;
        if (model.Kind != VoiceEngineKind.Supertonic)
        {
            return engine.Tts.Tts.Generate(text, 1.0f, speakerId);
        }

        var config = new OfflineTtsGenerationConfig { Sid = speakerId, Speed = 1.0f };
        config.Extra[SupertonicLanguageKey] = model.Language;
        return engine.Tts.Tts.GenerateWithConfig(text, config, KeepGenerating);
    }

    // Returning 1 tells sherpa-onnx to keep generating (0 would stop early).
    private static int KeepGenerating(IntPtr samples, int count, float progress, IntPtr arg) => 1;

    // OfflineTtsConfig and its nested model configs are structs, so a Configure* helper must take the
    // config by ref: a by-value copy silently drops every path. The native constructor reports a
    // rejected config only by returning a null handle, which the C# wrapper never checks (the first
    // Generate then logs "tts is nullptr"), so a missing model file is turned into an exception here.
    private static void RequireModelFiles(VoiceModelPaths paths, OfflineTtsConfig config)
    {
        string[] required = paths.Kind switch
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

        foreach (string file in required)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
            {
                throw new FileNotFoundException($"Voice model file missing for {paths.Kind}: '{file}'.", file);
            }
        }
    }

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

    // MeloTTS zh_en: lexicon + jieba dictionary, with the phone/date/number rule FSTs the upstream
    // example uses so digits and dates are spoken as words.
    private static void ConfigureMelo(ref OfflineTtsConfig config, string dir)
    {
        config.Model.Vits.Model = Path.Combine(dir, MeloFiles.Model);
        config.Model.Vits.Lexicon = Path.Combine(dir, MeloFiles.Lexicon);
        config.Model.Vits.Tokens = Path.Combine(dir, MeloFiles.Tokens);
        config.Model.Vits.DictDir = Path.Combine(dir, MeloFiles.DictDir);
        config.RuleFsts = string.Join(',', MeloFiles.RuleFsts.Select(file => Path.Combine(dir, file)));
    }
}
