using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// One loaded OCR pack: its two onnxruntime sessions and the pipeline around them (Slice 52).
/// Loading reads both models, so the composite keeps the engine of the last pack and reuses it.
/// </summary>
public sealed class PaddleOcrEngine : IDisposable
{
    private readonly OnnxOcrInference _inference;
    private readonly OcrPipeline _pipeline;
    private readonly IPixelImageLoader _loader;

    public PaddleOcrEngine(OcrModelPaths paths, IPixelImageLoader loader)
    {
        _loader = loader;
        _inference = new OnnxOcrInference(paths.Detection, paths.Recognition);
        IReadOnlyList<string> dictionary = CtcDecoder.ParseDictionary(File.ReadAllText(paths.Dictionary));
        _pipeline = new OcrPipeline(_inference, dictionary, paths.RightToLeft);
        Paths = paths;
    }

    public OcrModelPaths Paths { get; }

    public IReadOnlyList<OcrLine> Read(string imagePath, CancellationToken cancellationToken)
    {
        using IOcrPage page = _loader.Open(imagePath, DetectionPreprocessor.MaxSide);
        return _pipeline.Read(page, cancellationToken);
    }

    public void Dispose() => _inference.Dispose();
}
