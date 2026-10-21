namespace ReadTheStupidText.Domain.Reading;

/// <summary>
/// The file layout of a Piper (VITS) voice pack, normalised by the packs workflow so
/// every platform's reader and the manifest agree. <see cref="EspeakDataDir"/> holds
/// the GPL-3.0 espeak-ng data and exists only inside the downloaded pack (Decision 63).
/// </summary>
public static class VitsFiles
{
    public const string Model = "model.onnx";
    public const string Tokens = "tokens.txt";
    public const string EspeakDataDir = "espeak-ng-data";
}
