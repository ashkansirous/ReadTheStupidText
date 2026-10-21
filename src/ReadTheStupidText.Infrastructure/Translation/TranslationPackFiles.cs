namespace ReadTheStupidText.Infrastructure.Translation;

/// <summary>
/// The file names inside a translation pack (Slice 75, <c>kind: translation</c>). One pack is one
/// translation leg: a Marian encoder and decoder (int8 ONNX), the source SentencePiece model, the
/// shared vocabulary and <c>translation.json</c> (<see cref="TranslationModelSpec"/>). Keep in sync
/// with the translation pack build step in <c>.github/workflows/packs.yml</c>.
/// </summary>
public static class TranslationPackFiles
{
    public const string Encoder = "encoder.onnx";
    public const string Decoder = "decoder.onnx";
    public const string SourceModel = "source.spm";
    public const string Vocabulary = "vocab.json";
    public const string Spec = "translation.json";
}
