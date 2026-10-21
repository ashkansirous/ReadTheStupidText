namespace ReadTheStupidText.Application.Images;

/// <summary>
/// The whole read, independent of the model runner: detect text boxes, order them into reading
/// order, recognize each crop, decode, and (for Arabic script) restore logical order. The lines
/// keep their boxes so a later feature can use the geometry (Slice 63).
/// </summary>
public sealed class OcrPipeline
{
    public const float MinConfidence = 0.5f;
    private readonly IOcrInference _inference;
    private readonly CtcDecoder _decoder;
    private readonly bool _rightToLeft;

    public OcrPipeline(IOcrInference inference, IReadOnlyList<string> dictionary, bool rightToLeft)
    {
        _inference = inference;
        _decoder = new CtcDecoder(dictionary);
        _rightToLeft = rightToLeft;
    }

    /// <summary>Reads an in-memory image (the tests; a real page goes through <see cref="IOcrPage"/>).</summary>
    public IReadOnlyList<OcrLine> Read(PixelImage image, CancellationToken cancellationToken)
    {
        using var page = new PixelOcrPage(image);
        return Read(page, cancellationToken);
    }

    public IReadOnlyList<OcrLine> Read(IOcrPage page, CancellationToken cancellationToken)
    {
        IReadOnlyList<OcrBox> boxes = LineOrdering.Order(DetectBoxes(page.Detection), _rightToLeft);
        var lines = new List<OcrLine>();
        foreach (OcrBox box in boxes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Recognize(page, box) is { } text)
            {
                lines.Add(new OcrLine(text, box));
            }
        }

        return lines;
    }

    private IReadOnlyList<OcrBox> DetectBoxes(PixelImage image)
    {
        (OcrTensor tensor, float scaleX, float scaleY) = DetectionPreprocessor.Prepare(image);
        DetectionMap map = _inference.Detect(tensor);
        return DbPostProcessor.Boxes(map.Probabilities, map.Width, map.Height, scaleX, scaleY, image.Width, image.Height);
    }

    // Null when the crop is empty, the decoded text is blank or the recognizer is not confident
    // (PaddleOCR's own drop score), so detector noise never becomes spoken text. A tall crop is
    // turned on its side first, and a very wide line is recognized in pieces (their text is joined
    // in visual order, so the right-to-left conversion still runs once over the whole line).
    private string? Recognize(IOcrPage page, OcrBox box)
    {
        PixelImage? crop = page.CropLine(box, _inference.RecognitionHeight);
        if (crop is null)
        {
            return null;
        }

        IEnumerable<string> texts = LineCrops.Split(LineCrops.ToHorizontal(crop), _inference.RecognitionHeight)
            .Select(RecognizePiece)
            .OfType<string>();
        string text = string.Join(' ', texts);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return _rightToLeft ? RightToLeftText.ToLogicalOrder(text) : text;
    }

    private string? RecognizePiece(PixelImage piece)
    {
        OcrTensor input = RecognitionPreprocessor.Prepare(piece, _inference.RecognitionHeight);
        RecognitionScores output = _inference.Recognize(input);
        CtcResult result = _decoder.Decode(output.Scores, output.Steps, output.Classes);
        bool confident = !string.IsNullOrWhiteSpace(result.Text) && result.Confidence >= MinConfidence;
        return confident ? result.Text.Trim() : null;
    }
}
