using ReadTheStupidText.Domain.Reading;
using SherpaOnnx;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The sherpa-onnx configuration of the Kokoro storyteller pack (Slice 114), shared by the Android and iOS engines so a
/// file change is made once. <see cref="OfflineTtsConfig"/> is a struct, so <see cref="Configure"/> takes it by ref.
/// </summary>
internal static class KokoroEngineConfig
{
    /// <summary>
    /// One model with every voice in voices.bin (the speaker id picks one), the English lexicon, and the espeak-ng data that lives only in the pack.
    /// The engine is configured for one <paramref name="language"/> (Slice 118): <c>lang</c> picks the espeak-ng voice (empty for English), and Chinese
    /// adds its lexicon, the jieba dictionary and the rule FSTs.
    /// </summary>
    public static void Configure(ref OfflineTtsConfig config, string dir, string language)
    {
        config.Model.Kokoro.Model = Path.Combine(dir, KokoroFiles.Model);
        config.Model.Kokoro.Voices = Path.Combine(dir, KokoroFiles.Voices);
        config.Model.Kokoro.Tokens = Path.Combine(dir, KokoroFiles.Tokens);
        config.Model.Kokoro.Lexicon = string.Join(',', KokoroFiles.LexiconsFor(language).Select(file => Path.Combine(dir, file)));
        config.Model.Kokoro.DataDir = Path.Combine(dir, KokoroFiles.EspeakDataDir);
        config.Model.Kokoro.Lang = KokoroVoiceTable.EngineLang(language);
        if (KokoroFiles.UsesChineseFiles(language))
        {
            config.Model.Kokoro.DictDir = Path.Combine(dir, KokoroFiles.DictDir);
            config.RuleFsts = string.Join(',', KokoroFiles.RuleFsts.Select(file => Path.Combine(dir, file)));
        }
    }

    /// <summary>Every file and folder the engine needs; a pack missing one fails the build here, not natively inside sherpa-onnx.</summary>
    public static string[] RequiredPaths(OfflineTtsConfig config) =>
    [
        config.Model.Kokoro.Model, config.Model.Kokoro.Voices, config.Model.Kokoro.Tokens,
        .. config.Model.Kokoro.Lexicon.Split(','), config.Model.Kokoro.DataDir,
        .. (string.IsNullOrEmpty(config.Model.Kokoro.DictDir) ? [] : new[] { config.Model.Kokoro.DictDir }),
        .. config.RuleFsts.Split(',', StringSplitOptions.RemoveEmptyEntries),
    ];
}
