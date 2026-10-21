namespace ReadTheStupidText.Application.Images;

/// <summary>An axis-aligned box in the pixel coordinates of the image that was read.</summary>
public readonly record struct OcrBox(float Left, float Top, float Right, float Bottom)
{
    public float Width => Right - Left;

    public float Height => Bottom - Top;

    public float CenterY => (Top + Bottom) / 2;
}
