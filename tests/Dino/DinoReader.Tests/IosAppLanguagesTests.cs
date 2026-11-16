using System.Text.RegularExpressions;
using Xunit;

namespace DinoReader.Tests;

/// <summary>Slice 401: Dino iOS declares the app's languages (CFBundleLocalizations) so the App Store lists them, one InfoPlist.strings per language.</summary>
public class IosAppLanguagesTests
{
    private const string DevelopmentLanguage = "en";
    private const string ResxChineseSuffix = "zh";
    private const string AppleChineseCode = "zh-Hans";
    private const string MicrophoneKey = "NSMicrophoneUsageDescription";

    [Fact]
    public void Localizations_match_the_app_resx_languages()
    {
        Assert.Equal(ResxLanguages().OrderBy(c => c), PlistLocalizations().OrderBy(c => c));
    }

    [Fact]
    public void Every_language_has_its_strings_file_listed_in_the_csproj()
    {
        string iosGroup = IosItemGroup();

        foreach (string code in PlistLocalizations())
            Assert.Contains($@"<BundleResource Include=""Platforms\iOS\Resources\{code}.lproj\InfoPlist.strings"" LogicalName=""{code}.lproj/InfoPlist.strings"" />", iosGroup);
    }

    private static string IosItemGroup()
    {
        string csproj = File.ReadAllText(Path.Combine(AppDir(), "DinoReader.App.csproj"));
        Match group = Regex.Match(csproj, @"<ItemGroup Condition=""[^""]*== 'ios'"">(.*?)</ItemGroup>", RegexOptions.Singleline);
        Assert.True(group.Success, "The iOS ItemGroup is missing from DinoReader.App.csproj.");
        return group.Groups[1].Value;
    }

    [Fact]
    public void Every_strings_file_exists_and_holds_the_microphone_prompt()
    {
        foreach (string code in PlistLocalizations())
        {
            string path = Path.Combine(AppDir(), "Platforms", "iOS", "Resources", $"{code}.lproj", "InfoPlist.strings");

            Assert.Matches($@"(?m)^""{MicrophoneKey}"" = ""(?:[^""\\]|\\.)+"";\s*$", File.ReadAllText(path));
        }
    }

    [Fact]
    public void English_strings_file_equals_the_Info_plist_microphone_text()
    {
        string plist = File.ReadAllText(Path.Combine(AppDir(), "Platforms", "iOS", "Info.plist"));
        Match key = Regex.Match(plist, $@"<key>{MicrophoneKey}</key>\s*<string>([^<]+)</string>");
        Assert.True(key.Success, "NSMicrophoneUsageDescription is missing from Info.plist.");

        string strings = File.ReadAllText(Path.Combine(AppDir(), "Platforms", "iOS", "Resources", $"{DevelopmentLanguage}.lproj", "InfoPlist.strings"));

        Assert.Contains($"\"{MicrophoneKey}\" = \"{key.Groups[1].Value}\";", strings);
    }

    [Fact]
    public void Development_region_is_english()
    {
        string plist = File.ReadAllText(Path.Combine(AppDir(), "Platforms", "iOS", "Info.plist"));

        Assert.Matches($@"<key>CFBundleDevelopmentRegion</key>\s*<string>{DevelopmentLanguage}</string>", plist);
    }

    private static IEnumerable<string> PlistLocalizations()
    {
        string plist = File.ReadAllText(Path.Combine(AppDir(), "Platforms", "iOS", "Info.plist"));
        Match block = Regex.Match(plist, @"<key>CFBundleLocalizations</key>\s*<array>(.*?)</array>", RegexOptions.Singleline);
        Assert.True(block.Success, "CFBundleLocalizations is missing from Info.plist.");

        return Regex.Matches(block.Groups[1].Value, "<string>([^<]+)</string>").Select(m => m.Groups[1].Value).ToList();
    }

    private static IEnumerable<string> ResxLanguages()
    {
        string dir = Path.Combine(AppDir(), "Resources", "Strings");
        var codes = Directory.GetFiles(dir, "AppResources.*.resx")
            .Select(f => Path.GetFileNameWithoutExtension(f).Substring("AppResources.".Length))
            .Select(c => c == ResxChineseSuffix ? AppleChineseCode : c)
            .ToList();
        codes.Add(DevelopmentLanguage);
        return codes;
    }

    private static string AppDir() => Path.Combine(RepoRoot(), "src", "Dino", "DinoReader.App");

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo root not found.");
    }
}
