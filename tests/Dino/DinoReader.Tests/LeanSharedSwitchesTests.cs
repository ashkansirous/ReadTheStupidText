using System.Xml.Linq;

namespace DinoReader.Tests;

/// <summary>
/// Slice 305: Dino Reader links no speech engine and no ML Kit. Its project sets both Mobile.Shared switches false and passes them to the Mobile.Shared reference;
/// without them the sherpa-onnx, onnxruntime and ML Kit natives return to the Android and iOS packages.
/// </summary>
public class LeanSharedSwitchesTests
{
    [Theory]
    [InlineData("IncludeSpeechEngines")]
    [InlineData("IncludeMlKit")]
    public void The_Dino_project_switches_the_engine_off_and_passes_it_to_Mobile_Shared(string switchName)
    {
        XElement project = XDocument.Load(Path.Combine(RepoRoot(), "src", "Dino", "DinoReader.App", "DinoReader.App.csproj")).Root!;

        string?[] values = project.Descendants(switchName).Select(e => e.Value).ToArray();
        Assert.NotEmpty(values);
        Assert.All(values, v => Assert.Equal("false", v));

        string additional = project.Descendants("ProjectReference")
            .Single(r => ((string?)r.Attribute("Include"))!.Contains("Mobile.Shared", StringComparison.Ordinal))
            .Attribute("AdditionalProperties")!.Value;
        Assert.Contains($"{switchName}=$({switchName})", additional);
    }

    [Theory]
    [InlineData("IncludeSpeechEngines")]
    [InlineData("IncludeMlKit")]
    public void The_RTST_project_keeps_both_switches_true_and_passes_them_to_Mobile_Shared(string switchName)
    {
        XElement project = LoadRtstMobile();

        string?[] values = project.Descendants(switchName).Select(e => e.Value).ToArray();
        Assert.NotEmpty(values);
        Assert.All(values, v => Assert.Equal("true", v));

        string additional = project.Descendants("ProjectReference")
            .Single(r => ((string?)r.Attribute("Include"))!.Contains("Mobile.Shared", StringComparison.Ordinal))
            .Attribute("AdditionalProperties")!.Value;
        Assert.Contains($"{switchName}=$({switchName})", additional);
    }

    [Theory]
    [InlineData("Xamarin.GooglePlayServices.MLKit.DocumentScanner")]
    [InlineData("Xamarin.Google.MLKit.TextRecognition")]
    [InlineData("Xamarin.GooglePlayServices.Tasks")]
    [InlineData("Xamarin.Google.MLKit.Translate")]
    [InlineData("Microsoft.ML.OnnxRuntime.Managed")]
    [InlineData("EngageSolutionsGroup.Apple.MLKitTranslate")]
    public void The_RTST_project_re_declares_every_package_Mobile_Shared_keeps_private(string package)
    {
        // Mobile.Shared marks these PrivateAssets="all" (Slice 305), so RTST ships them only because it asks for them itself.
        string?[] included = LoadRtstMobile().Descendants("PackageReference")
            .Select(e => (string?)e.Attribute("Include")).ToArray();

        Assert.Contains(package, included);
    }

    private static XElement LoadRtstMobile()
    {
        return XDocument.Load(Path.Combine(RepoRoot(), "src", "Rtst", "ReadTheStupidText.Mobile", "ReadTheStupidText.Mobile.csproj")).Root!;
    }

    private static string RepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "src", "Dino", "DinoReader.App")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new DirectoryNotFoundException("Repo root not found.");
    }
}
