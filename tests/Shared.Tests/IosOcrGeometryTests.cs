using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Tests;

/// <summary>The pure geometry the iOS OCR path leans on (Slice 55): fitting the detection image and
/// converting Apple Vision's bottom-left normalised boxes.</summary>
public class IosOcrGeometryTests
{
    [Fact]
    public void FromNormalizedBottomLeft_FlipsTheVerticalAxis()
    {
        // A line in the top tenth of a 1000 x 2000 page: Vision reports y from the bottom.
        OcrBox box = OcrBox.FromNormalizedBottomLeft(0.1, 0.9, 0.5, 0.1, 1000, 2000);

        Assert.Equal(100f, box.Left, 2);
        Assert.Equal(0f, box.Top, 2);
        Assert.Equal(600f, box.Right, 2);
        Assert.Equal(200f, box.Bottom, 2);
    }

    [Fact]
    public void FromNormalizedBottomLeft_KeepsALowBoxLow()
    {
        OcrBox box = OcrBox.FromNormalizedBottomLeft(0, 0, 1, 0.25, 400, 800);

        Assert.Equal(600f, box.Top, 2);
        Assert.Equal(800f, box.Bottom, 2);
        Assert.Equal(400f, box.Width, 2);
    }
}
