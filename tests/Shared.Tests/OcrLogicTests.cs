using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class OcrLogicTests
{
    private static readonly string[] Letters = ["a", "b", "c"];

    // ---- CTC decoding ----

    private static float[] OneHot(int classes, params int[] winners)
    {
        float[] scores = new float[winners.Length * classes];
        for (int step = 0; step < winners.Length; step++)
        {
            scores[step * classes + winners[step]] = 0.9f;
        }

        return scores;
    }

    [Fact]
    public void Ctc_SkipsBlanksAndCollapsesRepeats()
    {
        // classes: blank, a, b, c, space
        var decoder = new CtcDecoder(Letters);
        CtcResult result = decoder.Decode(OneHot(5, 1, 1, 0, 1, 2, 2, 3), steps: 7, classes: 5);

        Assert.Equal("aabc", result.Text);
        Assert.Equal(0.9f, result.Confidence, 3);
    }

    [Fact]
    public void Ctc_LastClassIsASpace_WhenTheModelHasOneMoreClassThanTheDictionary()
    {
        var decoder = new CtcDecoder(Letters);

        Assert.Equal("a b", decoder.Decode(OneHot(5, 1, 4, 2), steps: 3, classes: 5).Text);
    }

    [Fact]
    public void Ctc_WithoutASpaceClass_UsesTheDictionaryOnly()
    {
        var decoder = new CtcDecoder(Letters);

        Assert.Equal("cb", decoder.Decode(OneHot(4, 3, 2), steps: 2, classes: 4).Text);
    }

    [Fact]
    public void Ctc_ThrowsWhenTheDictionaryDoesNotFitTheModel()
    {
        var decoder = new CtcDecoder(Letters);

        Assert.Throws<InvalidDataException>(() => decoder.Decode(OneHot(9, 1), steps: 1, classes: 9));
    }

    [Fact]
    public void Ctc_AllBlank_IsEmptyWithZeroConfidence()
    {
        CtcResult result = new CtcDecoder(Letters).Decode(OneHot(5, 0, 0), steps: 2, classes: 5);

        Assert.Equal(string.Empty, result.Text);
        Assert.Equal(0f, result.Confidence);
    }

    [Fact]
    public void ParseDictionary_DropsCarriageReturnsAndTheTrailingNewline_ButKeepsMultiCharEntries()
    {
        IReadOnlyList<string> entries = CtcDecoder.ParseDictionary("a\r\nب\r\n\U0001D7D8\n");

        Assert.Equal(["a", "ب", "\U0001D7D8"], entries);
    }

    // ---- right-to-left logical order ----

    [Fact]
    public void RightToLeft_ReversesArabicLetters()
    {
        // The recognizer emits the line left-to-right on screen; logical order is the reverse.
        Assert.Equal("سلام", RightToLeftText.ToLogicalOrder("مالس"));
    }

    [Fact]
    public void RightToLeft_KeepsADigitRunAndALatinWordIntact()
    {
        Assert.Equal("ب 123 ا", RightToLeftText.ToLogicalOrder("ا 123 ب"));
        Assert.Equal("ب abc ا", RightToLeftText.ToLogicalOrder("ا abc ب"));
    }

    [Fact]
    public void RightToLeft_KeepsArabicIndicAndPersianDigitRunsIntact()
    {
        Assert.Equal("ب ١٢٣ ا", RightToLeftText.ToLogicalOrder("ا ١٢٣ ب"));
        Assert.Equal("ب ۱۲۳ ا", RightToLeftText.ToLogicalOrder("ا ۱۲۳ ب"));
    }

    [Fact]
    public void Ctc_CachesTheTablePerClassCount_AndStillRefusesAMismatchEveryTime()
    {
        var decoder = new CtcDecoder(Letters);

        Assert.Equal("ab", decoder.Decode(OneHot(5, 1, 2), steps: 2, classes: 5).Text);
        Assert.Equal("ab", decoder.Decode(OneHot(5, 1, 2), steps: 2, classes: 5).Text);
        Assert.Throws<InvalidDataException>(() => decoder.Decode(OneHot(9, 1), steps: 1, classes: 9));
        Assert.Throws<InvalidDataException>(() => decoder.Decode(OneHot(9, 1), steps: 1, classes: 9));
    }

    [Fact]
    public void SharedNormalizer_MatchesTheOriginalDetectionAndRecognitionFormulas()
    {
        byte[] bgr = [0, 17, 255, 128, 64, 200];
        var image = new PixelImage(2, 1, bgr);
        float[] mean = [0.485f, 0.456f, 0.406f];
        float[] std = [0.229f, 0.224f, 0.225f];

        float[] detection = DetectionPreprocessor.ToNormalizedChw(image);
        float[] recognition = RecognitionPreprocessor.ToNormalizedChw(image);

        for (int pixel = 0; pixel < 2; pixel++)
        {
            for (int c = 0; c < 3; c++)
            {
                float value = bgr[pixel * 3 + c];
                Assert.Equal((value / 255f - mean[c]) / std[c], detection[c * 2 + pixel], 4);
                Assert.Equal(value / 255f * 2f - 1f, recognition[c * 2 + pixel], 4);
            }
        }
    }

    [Fact]
    public void RightToLeft_EmptyStaysEmpty()
    {
        Assert.Equal(string.Empty, RightToLeftText.ToLogicalOrder(string.Empty));
    }

    // ---- reading order ----

    private static OcrBox Box(float left, float top, float right, float bottom) => new(left, top, right, bottom);

    [Fact]
    public void Order_RowsTopToBottom_LeftToRightWithinARow()
    {
        OcrBox topRight = Box(120, 10, 220, 30);
        OcrBox topLeft = Box(10, 12, 100, 32);
        OcrBox second = Box(10, 60, 300, 80);

        IReadOnlyList<OcrBox> ordered = LineOrdering.Order([second, topRight, topLeft], rightToLeft: false);

        Assert.Equal([topLeft, topRight, second], ordered);
    }

    [Fact]
    public void Order_RightToLeft_ReadsTheRightmostBoxOfARowFirst()
    {
        OcrBox topRight = Box(120, 10, 220, 30);
        OcrBox topLeft = Box(10, 12, 100, 32);
        OcrBox second = Box(10, 60, 300, 80);

        IReadOnlyList<OcrBox> ordered = LineOrdering.Order([second, topLeft, topRight], rightToLeft: true);

        Assert.Equal([topRight, topLeft, second], ordered);
    }

    [Fact]
    public void Order_SlightlyStaggeredBoxesStayOneRow_ButANextLineDoesNot()
    {
        OcrBox a = Box(10, 10, 100, 30);
        OcrBox b = Box(120, 16, 220, 36);
        OcrBox nextLine = Box(10, 40, 100, 60);

        Assert.Equal([a, b, nextLine], LineOrdering.Order([nextLine, b, a], rightToLeft: false));
    }

    // ---- detection post-processing ----

    private static float[] MapWithBlock(int width, int height, int left, int top, int right, int bottom, float value)
    {
        float[] map = new float[width * height];
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
            {
                map[y * width + x] = value;
            }
        }

        return map;
    }

    [Fact]
    public void Db_FindsABlock_UnclipsIt_AndScalesItBack()
    {
        float[] map = MapWithBlock(64, 64, 10, 20, 30, 30, 0.9f); // 20 wide, 10 tall

        IReadOnlyList<OcrBox> boxes = DbPostProcessor.Boxes(map, 64, 64, scaleX: 0.5f, scaleY: 0.5f, imageWidth: 128, imageHeight: 128);

        OcrBox box = Assert.Single(boxes);
        // unclip distance = 20 * 10 * 1.5 / (2 * 30) = 5 map pixels, then x2 for the scale.
        Assert.Equal((10 - 5) / 0.5f, box.Left, 2);
        Assert.Equal((20 - 5) / 0.5f, box.Top, 2);
        Assert.Equal((30 + 5) / 0.5f, box.Right, 2);
        Assert.Equal((30 + 5) / 0.5f, box.Bottom, 2);
    }

    [Fact]
    public void Db_TwoSeparateBlocksAreTwoBoxes()
    {
        float[] map = MapWithBlock(64, 64, 4, 4, 20, 12, 0.9f);
        float[] second = MapWithBlock(64, 64, 4, 40, 20, 48, 0.9f);
        for (int i = 0; i < map.Length; i++)
        {
            map[i] = Math.Max(map[i], second[i]);
        }

        Assert.Equal(2, DbPostProcessor.Boxes(map, 64, 64, 1f, 1f, 64, 64).Count);
    }

    [Fact]
    public void Db_DropsWeakAndTinyRegions()
    {
        float[] weak = MapWithBlock(64, 64, 10, 10, 30, 20, 0.4f); // above the binary threshold, below the box score
        float[] tiny = MapWithBlock(64, 64, 50, 50, 52, 52, 0.95f);
        for (int i = 0; i < weak.Length; i++)
        {
            weak[i] = Math.Max(weak[i], tiny[i]);
        }

        Assert.Empty(DbPostProcessor.Boxes(weak, 64, 64, 1f, 1f, 64, 64));
    }

    [Fact]
    public void Db_ClampsTheBoxToTheImage()
    {
        float[] map = MapWithBlock(32, 32, 0, 0, 32, 10, 0.9f);

        OcrBox box = Assert.Single(DbPostProcessor.Boxes(map, 32, 32, 1f, 1f, 32, 32));

        Assert.Equal(0, box.Left);
        Assert.Equal(0, box.Top);
        Assert.Equal(32, box.Right);
    }

    // ---- preprocessing and resizing ----

    [Theory]
    [InlineData(100, 50, 96, 64)]
    [InlineData(1280, 640, 1280, 640)]
    [InlineData(2560, 1280, 1280, 640)]
    [InlineData(10, 4000, 32, 1280)]
    public void DetectionTargetSize_IsAMultipleOf32_AndCapsTheLongSide(int width, int height, int expectedWidth, int expectedHeight)
    {
        Assert.Equal((expectedWidth, expectedHeight), DetectionPreprocessor.TargetSize(width, height));
    }

    [Fact]
    public void DetectionPrepare_ReturnsScalesThatMapBackToTheImage()
    {
        PixelImage image = Solid(2560, 1280, 255);

        (OcrTensor tensor, float scaleX, float scaleY) = DetectionPreprocessor.Prepare(image);

        Assert.Equal(1280, tensor.Width);
        Assert.Equal(640, tensor.Height);
        Assert.Equal(0.5f, scaleX);
        Assert.Equal(0.5f, scaleY);
        Assert.Equal(3 * 1280 * 640, tensor.Data.Length);
    }

    [Fact]
    public void DetectionPrepare_NormalizesWithTheImageNetMeanAndStd()
    {
        (OcrTensor tensor, _, _) = DetectionPreprocessor.Prepare(Solid(32, 32, 255));

        // Channel 0 is blue in the stored order: (1 - 0.485) / 0.229.
        Assert.Equal((1f - 0.485f) / 0.229f, tensor.Data[0], 4);
        Assert.Equal((1f - 0.406f) / 0.225f, tensor.Data[2 * 32 * 32], 4);
    }

    [Fact]
    public void RecognitionPrepare_KeepsTheAspectRatioAtTheModelHeight_AndScalesToMinusOneOne()
    {
        PixelImage line = Solid(200, 20, 0);

        OcrTensor tensor = RecognitionPreprocessor.Prepare(line, 48);

        Assert.Equal(48, tensor.Height);
        Assert.Equal(480, tensor.Width);
        Assert.All(tensor.Data, v => Assert.Equal(-1f, v));
    }

    [Theory]
    [InlineData(1, 100, 8)]
    [InlineData(100000, 10, 1920)]
    public void RecognitionTargetWidth_IsClamped(int lineWidth, int lineHeight, int expected)
    {
        Assert.Equal(expected, RecognitionPreprocessor.TargetWidth(lineWidth, lineHeight, 48));
    }

    [Fact]
    public void Crop_CopiesTheRegionAndClampsToTheImage()
    {
        PixelImage image = Gradient(8, 4);

        PixelImage? crop = ImageResizer.Crop(image, new OcrBox(-3, 1, 3.2f, 99));

        Assert.NotNull(crop);
        Assert.Equal(4, crop.Width);
        Assert.Equal(3, crop.Height);
        Assert.Equal(image.Bgr[(1 * 8 + 0) * 3], crop.Bgr[0]);
    }

    [Fact]
    public void Crop_OutsideTheImage_IsNull()
    {
        Assert.Null(ImageResizer.Crop(Solid(4, 4, 0), new OcrBox(10, 10, 20, 20)));
    }

    [Fact]
    public void Resize_OfASolidImageStaysSolid()
    {
        PixelImage resized = ImageResizer.Resize(Solid(10, 10, 77), 25, 7);

        Assert.Equal(25 * 7 * 3, resized.Bgr.Length);
        Assert.All(resized.Bgr, b => Assert.Equal(77, b));
    }

    [Fact]
    public void PixelImage_RejectsAMismatchedBuffer()
    {
        Assert.Throws<ArgumentException>(() => new PixelImage(2, 2, new byte[5]).Validate());
    }

    // ---- the whole pipeline with a fake model runner ----

    [Fact]
    public void Pipeline_ReadsLinesInOrder_KeepsTheirBoxes_AndDerivesTheText()
    {
        var inference = new FakeInference();
        var pipeline = new OcrPipeline(inference, Letters, rightToLeft: false);

        IReadOnlyList<OcrLine> lines = pipeline.Read(Solid(128, 64, 200), CancellationToken.None);

        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.True(l.Box.Width > 0 && l.Box.Height > 0));
        Assert.True(lines[0].Box.Top < lines[1].Box.Top);
        Assert.Equal("ab\nab", new ImageText(lines, OcrSource.PaddleOcr).Text);
    }

    [Fact]
    public void Pipeline_RightToLeft_RestoresLogicalOrderPerLine()
    {
        var inference = new FakeInference();
        var pipeline = new OcrPipeline(inference, ["ا", "ب", "ج"], rightToLeft: true);

        IReadOnlyList<OcrLine> lines = pipeline.Read(Solid(128, 64, 200), CancellationToken.None);

        Assert.Equal("با", lines[0].Text);
    }

    [Fact]
    public void Pipeline_DropsLowConfidenceLines()
    {
        var inference = new FakeInference { Confidence = 0.2f };
        var pipeline = new OcrPipeline(inference, Letters, rightToLeft: false);

        Assert.Empty(pipeline.Read(Solid(128, 64, 200), CancellationToken.None));
    }

    [Fact]
    public void Pipeline_StopsWhenCancelled()
    {
        var pipeline = new OcrPipeline(new FakeInference(), Letters, rightToLeft: false);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => pipeline.Read(Solid(128, 64, 200), cts.Token));
    }

    // ---- OCR follows the page's script, not the app language (Slice 84) ----

    private static InstalledVoicePack OcrPack(string id, string languages) =>
        new($"/packs/{id}", new PackManifest(id, PackKind.Ocr, languages, "1", "onnxruntime", ["det.onnx", "rec.onnx", "dict.txt"]));

    private static readonly string[] NutkinLines = ["Squirrel Nutkin loves exploring", "with his friend, Peter Rabbit."];

    private static ScriptShare ShareOf(params string[] lines) => ScriptDetector.Analyze(lines);

    [Fact]
    public void EnglishPageWithTheAppInFarsi_KeepsTheBuiltInReading_AndConsultsNoPack()
    {
        ScriptShare share = ShareOf(NutkinLines);

        Assert.True(OcrPackChoice.BuiltInIsFinal(share));
        Assert.Equal(string.Join('\n', NutkinLines), new ImageText(NutkinLines.Select(l => new OcrLine(l, default)).ToList(), OcrSource.MlKit).Text);
    }

    [Fact]
    public void FarsiPageReadAsLatinNoise_IsNotFinal_AndTheFarsiPackWins()
    {
        ScriptShare noise = ShareOf("oll l ,l", "AI");
        Assert.False(OcrPackChoice.BuiltInIsFinal(noise));

        InstalledVoicePack[] installed = [OcrPack("ocr-cjk", "zh,ja"), OcrPack("ocr-arabic", "ar,fa")];
        IReadOnlyList<OcrModelPaths> candidates = OcrPackChoice.Candidates(installed, "en", noise);
        var choice = new OcrPackChoice(noise);

        OcrModelPaths arabic = candidates.Single(c => c.Language == "ar");
        Assert.True(choice.Offer(arabic, ShareOf("سنجاب ناتکین عاشق گشتن است")));
        Assert.Same(arabic, choice.Winner);
        Assert.True(choice.IsDecided);
    }

    [Fact]
    public void ABlankOrShortBuiltInReading_IsNotFinal()
    {
        Assert.False(OcrPackChoice.BuiltInIsFinal(ScriptShare.Empty));
        Assert.False(OcrPackChoice.BuiltInIsFinal(ShareOf("Page 3")));
    }

    [Fact]
    public void Offer_APackThatDoesNotBeatTheBuiltInScriptCount_Loses()
    {
        ScriptShare builtIn = ShareOf("سنجاب ناتکین عاشق گشتن است");
        var choice = new OcrPackChoice(builtIn);
        OcrModelPaths arabic = new("/packs/ocr-arabic", "fa", "1");

        Assert.False(choice.Offer(arabic, ShareOf("سنجاب")));
        Assert.Null(choice.Winner);
    }

    [Fact]
    public void Offer_AMixedReading_DoesNotStopTheScan_AndALaterBetterPackReplacesIt()
    {
        var choice = new OcrPackChoice(ShareOf("oll"));
        OcrModelPaths arabic = new("/packs/ocr-arabic", "ar", "1");
        OcrModelPaths cyrillic = new("/packs/ocr-cyrillic", "ru", "1");

        Assert.True(choice.Offer(arabic, ShareOf("ابتثجحخد", "abcdefgh")));
        Assert.False(choice.IsDecided);
        Assert.True(choice.Offer(cyrillic, ShareOf("Привет мир как дела")));
        Assert.Same(cyrillic, choice.Winner);
        Assert.True(choice.IsDecided);
    }

    [Fact]
    public void Offer_AHallucinatedPackReadingOverShortEnglish_DoesNotReplaceIt()
    {
        ScriptShare builtIn = ShareOf("Page 3");
        Assert.False(OcrPackChoice.BuiltInIsFinal(builtIn));
        var choice = new OcrPackChoice(builtIn);

        Assert.False(choice.Offer(new OcrModelPaths("/packs/ocr-arabic", "ar", "1"), ShareOf("ابت")));
        Assert.Null(choice.Winner);
    }

    [Fact]
    public void JunkLatinAroundARealFarsiPage_IsNotFinal_AndTheFarsiPackWins()
    {
        ScriptShare builtIn = ShareOf("abcdefghijkl mnopq", "سنجاب ناتکین عاشق");
        Assert.False(OcrPackChoice.BuiltInIsFinal(builtIn));
        var choice = new OcrPackChoice(builtIn);

        Assert.True(choice.Offer(new OcrModelPaths("/packs/ocr-arabic", "fa", "1"), ShareOf("سنجاب ناتکین عاشق گشتن است و دوست دارد", "با دوستش پیتر خرگوش در جنگل", "بزرگ بازی می کند")));
        Assert.NotNull(choice.Winner);
    }

    [Fact]
    public void FarsiHint_AllLatinJunkBuiltIn_StillFinal_ButTheFarsiPackIsReadAndWins()
    {
        ScriptShare junk = ShareOf("abcdefghijklmnop qrstuvwx yzabcdef");
        Assert.True(OcrPackChoice.BuiltInIsFinal(junk));
        InstalledVoicePack[] installed = [OcrPack("ocr-cjk", "zh,ja"), OcrPack("ocr-arabic", "ar,fa")];

        OcrModelPaths hintPack = Assert.Single(OcrPackChoice.HintPackOnly(installed, "fa"));
        var choice = new OcrPackChoice(junk);

        Assert.Equal("fa", hintPack.Language);
        Assert.True(choice.Offer(hintPack, ShareOf("سنجاب ناتکین عاشق گشتن است و دوست دارد", "با دوستش پیتر خرگوش در جنگل", "بزرگ بازی می کند")));
    }

    [Fact]
    public void FarsiHint_RealEnglishPage_StaysEnglish_WhenThePackHallucinates()
    {
        var choice = new OcrPackChoice(ShareOf(NutkinLines));
        var farsi = new OcrModelPaths("/packs/ocr-arabic", "fa", "1");

        Assert.False(choice.Offer(farsi, ShareOf("سنجاب ناتکین عاشق")));
        Assert.Null(choice.Winner);
        Assert.Empty(OcrPackChoice.HintPackOnly([OcrPack("ocr-arabic", "ar,fa")], "en"));
    }

    [Fact]
    public void Candidates_TryTheAlreadyLoadedPackFirst()
    {
        InstalledVoicePack[] installed = [OcrPack("ocr-arabic", "ar,fa"), OcrPack("ocr-cyrillic", "ru")];
        var loaded = new OcrModelPaths("/packs/ocr-cyrillic", "ru", "1");

        IReadOnlyList<OcrModelPaths> candidates = OcrPackChoice.Candidates(installed, "fa", ShareOf("abc"), loaded);

        Assert.Equal("ocr-cyrillic", Path.GetFileName(candidates[0].Folder));
        Assert.True(candidates[0].IsSamePack(loaded with { Language = "uk" }));
    }

    [Fact]
    public void NonLatinCandidates_AMixedLatinAndCyrillicPack_IsKeptWhenTheHintDoesNotMatch()
    {
        IReadOnlyList<OcrModelPaths> candidates = InstalledOcrCatalog.NonLatinCandidates([OcrPack("ocr-mixed", "en,ru")], "de");

        Assert.Equal("ru", Assert.Single(candidates).Language);
    }

    [Fact]
    public void Candidates_PutThePackOfTheBuiltInDominantScriptFirst()
    {
        InstalledVoicePack[] installed = [OcrPack("ocr-arabic", "ar,fa"), OcrPack("ocr-cyrillic", "ru")];

        IReadOnlyList<OcrModelPaths> candidates = OcrPackChoice.Candidates(installed, "fa", ShareOf("Привет мир"));

        Assert.Equal(["ocr-cyrillic", "ocr-arabic"], candidates.Select(c => Path.GetFileName(c.Folder)));
    }

    [Fact]
    public void NonLatinCandidates_SkipsLatinPacks_AndPutsTheHintFirst()
    {
        InstalledVoicePack[] installed = [OcrPack("ocr-cjk", "zh,ja"), OcrPack("ocr-latin", "en,fr"), OcrPack("ocr-arabic", "ar,fa")];

        IReadOnlyList<OcrModelPaths> candidates = InstalledOcrCatalog.NonLatinCandidates(installed, "fa");

        Assert.Equal(["ocr-arabic", "ocr-cjk"], candidates.Select(c => Path.GetFileName(c.Folder)));
        Assert.Equal("fa", candidates[0].Language);
        Assert.True(candidates[0].RightToLeft);
        Assert.Equal("zh", candidates[1].Language);
        Assert.False(candidates[1].RightToLeft);
    }

    [Fact]
    public void NonLatinCandidates_WithAnEnglishApp_StillOffersTheFarsiPack()
    {
        IReadOnlyList<OcrModelPaths> candidates = InstalledOcrCatalog.NonLatinCandidates([OcrPack("ocr-arabic", "ar,fa")], "en");

        OcrModelPaths only = Assert.Single(candidates);
        Assert.Equal("ar", only.Language);
        Assert.True(only.RightToLeft);
    }

    // Detects two text rows on a 128x64 page (map == page size) and always recognizes "ab".
    private sealed class FakeInference : IOcrInference
    {
        public float Confidence { get; init; } = 0.9f;

        public int RecognitionHeight => 48;

        public DetectionMap Detect(OcrTensor input)
        {
            float[] map = MapWithBlock(input.Width, input.Height, 8, 8, 120, 24, 0.9f);
            float[] second = MapWithBlock(input.Width, input.Height, 8, 36, 120, 52, 0.9f);
            for (int i = 0; i < map.Length; i++)
            {
                map[i] = Math.Max(map[i], second[i]);
            }

            return new DetectionMap(map, input.Width, input.Height);
        }

        public RecognitionScores Recognize(OcrTensor input)
        {
            // classes: blank, a, b, c, space; steps: a, b
            float[] scores = new float[2 * 5];
            scores[1 * 1] = Confidence;
            scores[1 * 5 + 2] = Confidence;
            return new RecognitionScores(scores, 2, 5);
        }
    }

    private static PixelImage Solid(int width, int height, byte value)
    {
        byte[] bgr = new byte[width * height * 3];
        Array.Fill(bgr, value);
        return new PixelImage(width, height, bgr);
    }

    private static PixelImage Gradient(int width, int height)
    {
        byte[] bgr = new byte[width * height * 3];
        for (int i = 0; i < bgr.Length; i++)
        {
            bgr[i] = (byte)(i % 251);
        }

        return new PixelImage(width, height, bgr);
    }
}
