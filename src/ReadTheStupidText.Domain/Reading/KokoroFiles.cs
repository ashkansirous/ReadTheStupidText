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
}
