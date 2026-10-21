using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class OcrPackTests
{
    private static InstalledVoicePack Installed(string id, PackKind kind, string languages, string version = "1") =>
        new($"/packs/{id}", new PackManifest(id, kind, languages, version, "onnxruntime", ["det.onnx", "rec.onnx", "dict.txt"]));

    private static PackInfo Entry(string id, PackKind kind, string languages) =>
        new(id, languages.Split(','), kind, "1", 1000, "abc", "Apache-2.0", "https://example.test/x.zip");

    [Fact]
    public void Resolve_PicksTheOcrPackServingTheLanguage()
    {
        InstalledVoicePack[] installed =
        [
            Installed("supertonic-3", PackKind.Voice, "ar,en"),
            Installed("ocr-arabic", PackKind.Ocr, "ar,fa"),
        ];

        OcrModelPaths? paths = InstalledOcrCatalog.Resolve(installed, "fa");

        Assert.NotNull(paths);
        Assert.Equal("/packs/ocr-arabic", paths.Folder);
        Assert.Equal("fa", paths.Language);
        Assert.True(paths.RightToLeft);
        Assert.EndsWith("det.onnx", paths.Detection);
        Assert.EndsWith("rec.onnx", paths.Recognition);
        Assert.EndsWith("dict.txt", paths.Dictionary);
    }

    [Fact]
    public void Resolve_IgnoresVoicePacks_AndUnservedLanguages()
    {
        InstalledVoicePack[] installed = [Installed("supertonic-3", PackKind.Voice, "ru")];

        Assert.Null(InstalledOcrCatalog.Resolve(installed, "ru"));
        Assert.Null(InstalledOcrCatalog.Resolve([Installed("ocr-latin", PackKind.Ocr, "en,fr")], "ru"));
    }

    [Fact]
    public void Resolve_PrefersTheMostSpecificPack()
    {
        InstalledVoicePack[] installed =
        [
            Installed("ocr-wide", PackKind.Ocr, "en,fr,es"),
            Installed("ocr-fr", PackKind.Ocr, "fr"),
        ];

        Assert.Equal("/packs/ocr-fr", InstalledOcrCatalog.Resolve(installed, "fr")!.Folder);
    }

    [Theory]
    [InlineData("ar", true)]
    [InlineData("fa", true)]
    [InlineData("ru", false)]
    [InlineData("en", false)]
    public void RightToLeft_IsArabicScriptOnly(string language, bool expected)
    {
        Assert.Equal(expected, OcrLanguages.IsRightToLeft(language));
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("pt", true)]
    [InlineData("ru", false)]
    [InlineData("ja", false)]
    public void LatinScript_IsWhatMlKitReads(string language, bool expected)
    {
        Assert.Equal(expected, OcrLanguages.IsLatinScript(language));
    }

    [Fact]
    public void Select_FindsTheOcrPackOfTheCatalog_NotTheVoicePack()
    {
        PackInfo[] catalog =
        [
            Entry("supertonic-3", PackKind.Voice, "ru,en"),
            Entry("ocr-cyrillic", PackKind.Ocr, "ru"),
        ];

        Assert.Equal("ocr-cyrillic", OcrPackSelector.Select(catalog, "ru")!.Id);
        Assert.Null(OcrPackSelector.Select(catalog, "en"));
    }

    [Fact]
    public void ImageText_DerivesTheTextFromTheLines_AndKeepsTheBoxes()
    {
        var box = new OcrBox(1, 2, 3, 4);
        var result = new ImageText([new OcrLine("one", box), new OcrLine("two", box)], OcrSource.MlKit);

        Assert.Equal("one\ntwo", result.Text);
        Assert.Equal(box, result.Lines[0].Box);
    }

    [Fact]
    public void ManifestOfAnOcrPack_ParsesWithoutVoiceFields()
    {
        const string json = """
            { "id": "ocr-arabic", "kind": "ocr", "language": "ar,fa", "version": "1", "engine": "onnxruntime",
              "files": ["det.onnx", "rec.onnx", "dict.txt"] }
            """;

        PackManifest manifest = PackManifest.Parse(json);

        Assert.Equal(PackKind.Ocr, manifest.Kind);
        Assert.Empty(manifest.Voices ?? []);
        Assert.Empty(manifest.MissingFiles(_ => true));
    }
}
