namespace ReadTheStupidText.Application.Images;

/// <summary>The detector's probability map, one float per pixel, row-major.</summary>
public sealed record DetectionMap(float[] Probabilities, int Width, int Height);

/// <summary>The recognizer's per-step class scores, <c>Steps * Classes</c> floats, row-major.</summary>
public sealed record RecognitionScores(float[] Scores, int Steps, int Classes);

/// <summary>
/// The model runner behind <see cref="OcrPipeline"/>: PaddleOCR detection and recognition on
/// onnxruntime in the app, a fake in the tests.
/// </summary>
public interface IOcrInference
{
    /// <summary>The recognizer's fixed input height in pixels.</summary>
    int RecognitionHeight { get; }

    DetectionMap Detect(OcrTensor input);

    RecognitionScores Recognize(OcrTensor input);
}
