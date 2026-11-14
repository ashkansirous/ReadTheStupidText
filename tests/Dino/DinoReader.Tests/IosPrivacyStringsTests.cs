using Xunit;

namespace DinoReader.Tests;

/// <summary>Dino iOS declares no camera or photo-library purpose strings: its build excludes the scanner (Slice 349 item 4).</summary>
public class IosPrivacyStringsTests
{
    [Theory]
    [InlineData("NSCameraUsageDescription")]
    [InlineData("NSPhotoLibraryUsageDescription")]
    [InlineData("NSPhotoLibraryAddUsageDescription")]
    public void Info_plist_has_no_camera_or_photo_key(string key)
    {
        string plist = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Dino", "DinoReader.App", "Platforms", "iOS", "Info.plist"));

        Assert.DoesNotContain($"<key>{key}</key>", plist);
    }

    [Fact]
    public void Info_plist_keeps_the_microphone_key()
    {
        string plist = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Dino", "DinoReader.App", "Platforms", "iOS", "Info.plist"));

        Assert.Contains("<key>NSMicrophoneUsageDescription</key>", plist);
    }

    [Fact]
    public void Dino_build_drops_the_scanner_source()
    {
        string csproj = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Shared", "Sirous.Reading.Mobile.Shared", "Sirous.Reading.Mobile.Shared.csproj"));
        int target = csproj.IndexOf("<Target Name=\"DropSpeechEngineSources\"", StringComparison.Ordinal);
        Assert.True(target >= 0);
        int end = csproj.IndexOf("</Target>", target, StringComparison.Ordinal);

        Assert.True(end > target);
        Assert.Contains(@"Compile Remove=""Platforms\iOS\VisionKitDocumentScanner.cs""", csproj[target..end]);
    }

    [Fact]
    public void Scanner_registration_stays_under_NO_SPEECH_ENGINES_guard()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Shared", "Sirous.Reading.Mobile.Shared", "Platforms", "iOS", "MobileSharedServices.iOS.cs"));
        int registration = source.IndexOf("VisionKitDocumentScanner>", StringComparison.Ordinal);
        Assert.True(registration >= 0);
        int guardStart = source.LastIndexOf("#if !NO_SPEECH_ENGINES", registration, StringComparison.Ordinal);
        int guardEnd = source.LastIndexOf("#endif", registration, StringComparison.Ordinal);

        Assert.True(guardStart >= 0 && guardStart > guardEnd);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo root not found.");
    }
}
