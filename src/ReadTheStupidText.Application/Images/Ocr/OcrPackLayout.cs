namespace ReadTheStupidText.Application.Images;

/// <summary>The files an OCR pack holds, next to <c>pack.json</c> (docs/voice-packs.md).</summary>
public static class OcrPackLayout
{
    public const string DetectionModel = "det.onnx";
    public const string RecognitionModel = "rec.onnx";
    public const string Dictionary = "dict.txt";
    public const string Engine = "onnxruntime";
}
