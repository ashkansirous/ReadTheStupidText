namespace ReadTheStupidText.Application.Images;

/// <summary>Lines read from one image plus the pixel size of the image their boxes are in (Slice 63).</summary>
public sealed record OcrReading(IReadOnlyList<OcrLine> Lines, int Width, int Height);
