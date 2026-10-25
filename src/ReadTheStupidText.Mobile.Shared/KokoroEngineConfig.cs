using ReadTheStupidText.Domain.Reading;
using SherpaOnnx;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The sherpa-onnx configuration of the Kokoro storyteller pack (Slice 114), shared by the Android and iOS engines so a
/// file change is made once. <see cref="OfflineTtsConfig"/> is a struct, so <see cref="Configure"/> takes it by ref.
/// </summary>
internal static class KokoroEngineConfig
{
    /// <summary>One model with every voice in voices.bin (the speaker id picks one), the English lexicon, and the espeak-ng data that lives only in the pack.</summary>
    public static void Configure(ref OfflineTtsConfig config, string dir)
    {
        config.Model.Kokoro.Model = Path.Combine(dir, KokoroFiles.Model);
        config.Model.Kokoro.Voices = Path.Combine(dir, KokoroFiles.Voices);
        config.Model.Kokoro.Tokens = Path.Combine(dir, KokoroFiles.Tokens);
        config.Model.Kokoro.Lexicon = Path.Combine(dir, KokoroFiles.Lexicon);
        config.Model.Kokoro.DataDir = Path.Combine(dir, KokoroFiles.EspeakDataDir);
    }

    /// <summary>Every file and folder the engine needs; a pack missing one fails the build here, not natively inside sherpa-onnx.</summary>
    public static string[] RequiredPaths(OfflineTtsConfig config) =>
    [
        config.Model.Kokoro.Model, config.Model.Kokoro.Voices, config.Model.Kokoro.Tokens,
        config.Model.Kokoro.Lexicon, config.Model.Kokoro.DataDir,
    ];
}
