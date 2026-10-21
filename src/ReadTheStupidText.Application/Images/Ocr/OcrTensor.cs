namespace ReadTheStupidText.Application.Images;

/// <summary>A float tensor in channel-first order (1, 3, Height, Width) ready for the model.</summary>
public sealed record OcrTensor(float[] Data, int Height, int Width);
