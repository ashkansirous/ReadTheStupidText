namespace ReadTheStupidText.Domain.Reading;

/// <summary>
/// The file layout of the Kokoro storyteller pack (Slice 114, Decision 118), normalised by the packs workflow
/// (the upstream int8 model is stored as <see cref="Model"/>). <see cref="EspeakDataDir"/> holds the GPL-3.0
/// espeak-ng data and exists only inside the downloaded pack (Decision 119).
/// </summary>
public static class KokoroFiles
{
    public const string Model = "model.onnx";
    public const string Voices = "voices.bin";
    public const string Tokens = "tokens.txt";
    public const string Lexicon = "lexicon-us-en.txt";
    public const string EspeakDataDir = "espeak-ng-data";

    // Chinese (Slice 118) runs through its own lexicon, the jieba dictionary and the rule FSTs that say digits, dates and phone numbers as words;
    // every other language is phonemised by espeak-ng, whose voice the engine's lang picks.
    public const string LexiconZh = "lexicon-zh.txt";
    public const string DictDir = "dict";

    public static IReadOnlyList<string> RuleFsts { get; } = ["date-zh.fst", "phone-zh.fst", "number-zh.fst"];

    /// <summary>The lexicon files an engine of <paramref name="language"/> loads, English first (a multi-language engine takes them comma separated).</summary>
    public static IReadOnlyList<string> LexiconsFor(string language) =>
        UsesChineseFiles(language) ? [Lexicon, LexiconZh] : [Lexicon];

    /// <summary>Whether an engine of <paramref name="language"/> needs the Chinese files (<see cref="LexiconZh"/>, <see cref="DictDir"/> and <see cref="RuleFsts"/>).</summary>
    public static bool UsesChineseFiles(string language) => language == KokoroVoiceTable.ChineseCode;
}
