using DinoReader.Core;
using Xunit;

namespace DinoReader.Tests;

public class TourGeometryTests
{
    // Pixel 10 Pro, 495 dpi: status bar 149 px = about 30 dp. The layer starts at the screen's top (page under the status bar); the tab sits 30 dp lower on the screen than its layout Y suggests.
    private const double StatusBar = 30;

    [Fact]
    public void InLayerSubtractsTheLayersScreenOrigin()
    {
        TourRect target = new(24, 120 + StatusBar, 80, 56);

        TourRect inLayer = TourGeometry.InLayer(target, 0, 0);

        Assert.Equal(target, inLayer);
    }

    [Fact]
    public void InLayerWithTheLayerBelowTheStatusBarMovesTheTargetUp()
    {
        TourRect target = new(24, 150, 80, 56);

        TourRect inLayer = TourGeometry.InLayer(target, 10, StatusBar);

        Assert.Equal(new TourRect(14, 150 - StatusBar, 80, 56), inLayer);
    }

    [Fact]
    public void LiftedCopyGrowsOnEverySide()
    {
        TourRect copy = TourGeometry.LiftedCopy(new TourRect(100, 200, 80, 56));

        Assert.Equal(new TourRect(94, 194, 92, 68), copy);
    }

    [Fact]
    public void ACopyDrawnAtTheInLayerPlaceIsAligned()
    {
        TourRect target = new(24, 2600, 120, 90);
        TourRect inLayer = TourGeometry.InLayer(target, 0, StatusBar);
        TourRect copyInLayer = TourGeometry.LiftedCopy(inLayer);
        TourRect copyOnScreen = new(copyInLayer.X, copyInLayer.Y + StatusBar, copyInLayer.Width, copyInLayer.Height);

        Assert.True(TourGeometry.IsAligned(copyOnScreen, target));
    }

    [Fact]
    public void ACopyAStatusBarLowerThanItsTargetIsNotAligned()
    {
        TourRect target = new(24, 2600, 120, 90);
        TourRect copyOnScreen = TourGeometry.LiftedCopy(new TourRect(target.X, target.Y + StatusBar, target.Width, target.Height));

        (double dx, double dy) = TourGeometry.CentreOffset(copyOnScreen, target);

        Assert.Equal(0, dx);
        Assert.Equal(StatusBar, dy);
        Assert.False(TourGeometry.IsAligned(copyOnScreen, target));
    }

    [Fact]
    public void FourDpOffIsStillAlignedAndFiveIsNot()
    {
        TourRect target = new(10, 10, 100, 50);

        Assert.True(TourGeometry.IsAligned(TourGeometry.LiftedCopy(new TourRect(14, 6, 100, 50)), target));
        Assert.False(TourGeometry.IsAligned(TourGeometry.LiftedCopy(new TourRect(15, 10, 100, 50)), target));
    }
}
