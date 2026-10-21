using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class OcrLayoutTests
{
    private static OcrBox Box(float left, float top, float right, float bottom) => new(left, top, right, bottom);

    // A two-column page: left column at x 10-200, right column at x 400-600, three lines each.
    private static (OcrBox[] Left, OcrBox[] Right) TwoColumns() =>
        ([Box(10, 10, 200, 30), Box(10, 40, 200, 60), Box(10, 70, 200, 90)],
         [Box(400, 12, 600, 32), Box(400, 42, 600, 62), Box(400, 72, 600, 92)]);

    [Fact]
    public void Order_TwoColumns_ReadsTheLeftColumnThenTheRight()
    {
        (OcrBox[] left, OcrBox[] right) = TwoColumns();
        OcrBox[] shuffled = [right[1], left[2], right[0], left[0], right[2], left[1]];

        IReadOnlyList<OcrBox> ordered = LineOrdering.Order(shuffled, rightToLeft: false);

        Assert.Equal([.. left, .. right], ordered);
    }

    [Fact]
    public void Order_TwoColumns_RightToLeft_ReadsTheRightColumnFirst()
    {
        (OcrBox[] left, OcrBox[] right) = TwoColumns();

        IReadOnlyList<OcrBox> ordered = LineOrdering.Order([.. left, .. right], rightToLeft: true);

        Assert.Equal([.. right, .. left], ordered);
    }

    [Fact]
    public void Order_ABoxSplitLineWithASmallGap_StaysOneRow()
    {
        OcrBox a = Box(10, 10, 100, 30);
        OcrBox b = Box(120, 10, 220, 30);
        OcrBox below = Box(10, 40, 220, 60);

        Assert.Equal([a, b, below], LineOrdering.Order([below, b, a], rightToLeft: false));
    }

    [Fact]
    public void RotateCounterClockwise_MovesTheTopRightPixelToTheTopLeft()
    {
        // 2 wide, 3 tall; pixel value = row * 2 + column.
        byte[] bgr = new byte[2 * 3 * 3];
        for (int i = 0; i < 6; i++)
        {
            bgr[i * 3] = (byte)i;
        }

        PixelImage rotated = ImageResizer.RotateCounterClockwise(new PixelImage(2, 3, bgr));

        Assert.Equal(3, rotated.Width);
        Assert.Equal(2, rotated.Height);
        Assert.Equal(1, rotated.Bgr[0]);       // was top-right
        Assert.Equal(0, rotated.Bgr[(1 * 3 + 0) * 3]); // was top-left, now bottom-left
    }

    [Fact]
    public void ToHorizontal_TurnsOnlyTallCrops()
    {
        var tall = new PixelImage(10, 16, new byte[10 * 16 * 3]);
        var square = new PixelImage(10, 14, new byte[10 * 14 * 3]);

        Assert.Equal(16, LineCrops.ToHorizontal(tall).Width);
        Assert.Same(square, LineCrops.ToHorizontal(square));
    }

    [Fact]
    public void Split_KeepsANormalLineWhole_AndCutsAnOverWideOneIntoPieces()
    {
        var normal = new PixelImage(480, 48, new byte[480 * 48 * 3]);
        var wide = new PixelImage(4000, 48, new byte[4000 * 48 * 3]);

        Assert.Single(LineCrops.Split(normal, 48));
        IReadOnlyList<PixelImage> pieces = LineCrops.Split(wide, 48);
        Assert.Equal(3, pieces.Count);
        Assert.InRange(pieces.Sum(p => p.Width), 4000, 4000 + pieces.Count); // neighbours share at most a rounding pixel
        Assert.All(pieces, p => Assert.True(p.Width <= 48 * RecognitionPreprocessor.MaxWidthInHeights));
    }

    [Theory]
    [InlineData(40, 1)]
    [InlineData(96, 1)]
    [InlineData(192, 2)]
    [InlineData(400, 4)]
    [InlineData(1000, 8)]
    public void CropSampling_KeepsTheLineAtLeastTwiceTheRecognizerHeight(float lineHeight, int expected)
    {
        Assert.Equal(expected, CropSampling.SampleSize(lineHeight, 48));
    }

    [Theory]
    [InlineData("2", "1", true)]
    [InlineData("10", "9", true)]
    [InlineData("1.1", "1", true)]
    [InlineData("1", "1.0", false)]
    [InlineData("1", "2", false)]
    [InlineData("1.2.1", "1.2", true)]
    [InlineData("1", "1", false)]
    public void PackVersions_IsNewer_ComparesNumerically(string candidate, string installed, bool expected)
    {
        Assert.Equal(expected, PackVersions.IsNewer(candidate, installed));
    }

    private static PackInfo Entry(string id, string version) =>
        new(id, ["fr"], PackKind.Ocr, version, 1000, "abc", "Apache-2.0", "https://example.test/x.zip");

    private static InstalledVoicePack Installed(string id, string version) =>
        new($"/packs/{id}", new PackManifest(id, PackKind.Ocr, "fr", version, "onnxruntime", ["det.onnx", "rec.onnx", "dict.txt"]));

    [Fact]
    public void ShouldInstall_WhenNothingIsInstalled_OrTheCatalogIsNewer()
    {
        Assert.True(PackVersions.ShouldInstall(Entry("ocr-fr", "1"), null));
        Assert.True(PackVersions.ShouldInstall(Entry("ocr-fr", "2"), Installed("ocr-fr", "1")));
    }

    [Fact]
    public void ShouldInstall_NotWhenCurrent_OlderOrADifferentPack()
    {
        Assert.False(PackVersions.ShouldInstall(Entry("ocr-fr", "1"), Installed("ocr-fr", "1")));
        Assert.False(PackVersions.ShouldInstall(Entry("ocr-fr", "1"), Installed("ocr-fr", "2")));
        Assert.False(PackVersions.ShouldInstall(Entry("ocr-fr", "9"), Installed("ocr-wide", "1")));
    }

    [Fact]
    public void FromCatalog_BreaksTiesOnPackId()
    {
        PackInfo[] catalog = [Entry("ocr-b", "1"), Entry("ocr-a", "1")];

        Assert.Equal("ocr-a", PackSelection.FromCatalog(catalog, PackKind.Ocr, "fr")!.Id);
        Assert.Equal("ocr-a", PackSelection.FromCatalog(catalog.Reverse(), PackKind.Ocr, "fr")!.Id);
    }
}
