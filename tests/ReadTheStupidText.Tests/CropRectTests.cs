using ReadTheStupidText.Mobile;
using ReadTheStupidText.Mobile.Shared;

namespace ReadTheStupidText.Tests;

public class CropRectTests
{
    [Fact]
    public void Normalised_KeepsARectInsideThePicture_WithAMinimumSide()
    {
        CropRect moved = new CropRect(0.8, -0.2, 0.5, 0.01).Normalised();

        Assert.Equal(0.5, moved.Left, 6);
        Assert.Equal(0, moved.Top, 6);
        Assert.Equal(0.5, moved.Width, 6);
        Assert.Equal(CropRect.MinimumSide, moved.Height, 6);

        CropRect broken = new CropRect(double.NaN, double.NaN, double.NaN, double.NaN).Normalised();
        Assert.True(broken.IsFull);
    }

    [Fact]
    public void IsFull_OnlyForTheWholePicture()
    {
        Assert.True(CropRect.Full.IsFull);
        Assert.False(new CropRect(0, 0, 0.9, 1).IsFull);
        Assert.False(new CropRect(0.1, 0, 0.9, 1).IsFull);
    }

    [Fact]
    public void ToPixels_RoundsIntoThePicture_AndNeverGivesAnEmptyRect()
    {
        Assert.Equal((100, 50, 400, 200), new CropRect(0.1, 0.1, 0.4, 0.4).ToPixels(1000, 500));
        Assert.Equal((0, 0, 1000, 500), CropRect.Full.ToPixels(1000, 500));

        (int x, int y, int w, int h) = new CropRect(0.97, 0.97, 0.5, 0.5).ToPixels(100, 100);
        Assert.InRange(x + w, 1, 100);
        Assert.InRange(y + h, 1, 100);
        Assert.True(w >= 1 && h >= 1);

        (_, _, int tinyW, int tinyH) = new CropRect(0, 0, 0.3, 0.3).ToPixels(1, 1);
        Assert.Equal((1, 1), (tinyW, tinyH));
    }
}
