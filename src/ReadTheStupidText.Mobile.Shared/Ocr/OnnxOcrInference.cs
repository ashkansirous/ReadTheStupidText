using Microsoft.ML.OnnxRuntime;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Runs PaddleOCR's detection and recognition models on onnxruntime (Slice 52). The native
/// library is the one sherpa-onnx already bundles (see the csproj), so only the managed binding
/// is referenced. Each session is created once per loaded pack; <see cref="Run"/> calls are
/// serialized because a recognizer call per line shares the one session.
/// </summary>
public sealed class OnnxOcrInference : IOcrInference, IDisposable
{
    private const int InputChannelRank = 4;
    private const int HeightAxis = 2;
    private const int MaxIntraOpThreads = 4;
    private const int DetectionRank = 4;
    private const int RecognitionRank = 3;

    private readonly InferenceSession _detector;
    private readonly InferenceSession _recognizer;
    private readonly object _gate = new();

    public OnnxOcrInference(string detectionModelPath, string recognitionModelPath)
    {
        using var options = new SessionOptions { IntraOpNumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, MaxIntraOpThreads) };
        _detector = new InferenceSession(detectionModelPath, options);
        try
        {
            _recognizer = new InferenceSession(recognitionModelPath, options);
        }
        catch
        {
            _detector.Dispose();
            throw;
        }

        RecognitionHeight = StaticInputHeight(_recognizer) ?? RecognitionPreprocessor.DefaultHeight;
    }

    public int RecognitionHeight { get; }

    public DetectionMap Detect(OcrTensor input)
    {
        (float[] data, long[] shape) = Run(_detector, input);
        if (shape.Length != DetectionRank)
        {
            throw new InvalidDataException("The detector output is not a probability map.");
        }

        return new DetectionMap(data, (int)shape[^1], (int)shape[^2]);
    }

    public RecognitionScores Recognize(OcrTensor input)
    {
        (float[] data, long[] shape) = Run(_recognizer, input);
        if (shape.Length != RecognitionRank)
        {
            throw new InvalidDataException("The recognizer output is not a sequence of class scores.");
        }

        return new RecognitionScores(data, (int)shape[1], (int)shape[2]);
    }

    public void Dispose()
    {
        _detector.Dispose();
        _recognizer.Dispose();
    }

    private (float[] Data, long[] Shape) Run(InferenceSession session, OcrTensor input)
    {
        long[] dimensions = [1, PixelImage.BytesPerPixel, input.Height, input.Width];
        using OrtValue value = OrtValue.CreateTensorValueFromMemory(input.Data, dimensions);
        var inputs = new Dictionary<string, OrtValue> { [session.InputNames[0]] = value };
        using var runOptions = new RunOptions();
        lock (_gate)
        {
            using IDisposableReadOnlyCollection<OrtValue> outputs = session.Run(runOptions, inputs, [session.OutputNames[0]]);
            return (outputs[0].GetTensorDataAsSpan<float>().ToArray(), outputs[0].GetTensorTypeAndShape().Shape);
        }
    }

    // The recognizer's height axis is fixed in the model (48 for PP-OCRv3/v5); a dynamic axis
    // reports -1 and the default is used.
    private static int? StaticInputHeight(InferenceSession session)
    {
        int[] dimensions = session.InputMetadata[session.InputNames[0]].Dimensions;
        bool known = dimensions.Length == InputChannelRank && dimensions[HeightAxis] > 0;
        return known ? dimensions[HeightAxis] : null;
    }
}
