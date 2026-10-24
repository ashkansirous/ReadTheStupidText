using System.Text.RegularExpressions;
using System.Xml.Linq;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText_App;

namespace ReadTheStupidText.Tests;

public partial class WindowsLocalizedUiTests
{
    private const string AppFolder = @"src\ReadTheStupidText.App";
    private const string EnglishFolder = "en-US";
    private const string ResourceFile = "Resources.resw";

    [Fact]
    public void Every_app_language_has_a_resource_folder_and_a_manifest_entry()
    {
        string manifest = File.ReadAllText(Path.Combine(RepoPath(AppFolder), "Package.appxmanifest"));

        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            string tag = UiLanguageTag.For(AppLanguages.Code(language))!;
            Assert.NotNull(tag);
            Assert.True(File.Exists(ResourcePath(tag)), $"missing {tag}\\{ResourceFile}");
            Assert.Contains($"<Resource Language=\"{tag}\"", manifest);
        }
    }

    [Fact]
    public void Every_language_carries_exactly_the_english_keys()
    {
        HashSet<string> english = Keys(EnglishFolder).ToHashSet();

        foreach (string tag in UiLanguageTag.AllTags.Where(t => t != EnglishFolder))
        {
            HashSet<string> keys = Keys(tag).ToHashSet();
            Assert.Empty(english.Except(keys));
            Assert.Empty(keys.Except(english));
        }
    }

    [Fact]
    public void No_translation_is_empty_and_placeholders_match_english()
    {
        Dictionary<string, string> english = Values(EnglishFolder);

        foreach (string tag in UiLanguageTag.AllTags.Where(t => t != EnglishFolder))
        {
            foreach ((string key, string value) in Values(tag))
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"{tag}: {key} is empty");
                Assert.Equal(Placeholders(english[key]), Placeholders(value));
            }
        }
    }

    [Fact]
    public void Every_xuid_in_the_app_xaml_has_an_entry_in_every_language()
    {
        string[] uids = Directory.GetFiles(RepoPath(AppFolder), "*.xaml")
            .SelectMany(f => XUid().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Distinct()
            .ToArray();
        Assert.NotEmpty(uids);

        foreach (string tag in UiLanguageTag.AllTags)
        {
            HashSet<string> keys = Keys(tag).ToHashSet();
            foreach (string uid in uids)
            {
                Assert.True(keys.Any(k => k.StartsWith(uid + ".", StringComparison.Ordinal)), $"{tag}: no resource for x:Uid {uid}");
            }
        }
    }

    [Fact]
    public void Every_string_the_code_asks_for_exists()
    {
        HashSet<string> english = Keys(EnglishFolder).ToHashSet();
        string[] cs = Directory.GetFiles(RepoPath(AppFolder), "*.cs");

        foreach (string key in cs.SelectMany(f => CodeKey().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value)).Distinct())
        {
            Assert.Contains(key, english);
        }
    }

    [Theory]
    [InlineData("fa", "fa", true)]
    [InlineData("AR", "en", true)]
    [InlineData("fr", "ar", false)]
    [InlineData(null, "ar", true)]
    [InlineData(null, "ar-SA", true)]
    [InlineData(null, "fa-IR", true)]
    [InlineData("xx", "fa", true)]
    [InlineData(null, "de", false)]
    [InlineData(null, "de-DE", false)]
    public void Right_to_left_follows_the_stored_language_then_the_device(string? stored, string device, bool expected)
    {
        Assert.Equal(expected, UiLanguageTag.IsRightToLeft(stored, device));
    }

    [Theory]
    [InlineData("ar-SA", "ar")]
    [InlineData("fa-IR", "fa")]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("pt_BR", "pt")]
    [InlineData("nb-NO", "nb")]
    [InlineData("nl-NL", "nl")]
    [InlineData("da-DK", "da")]
    [InlineData("de-DE", null)]
    public void Match_resolves_a_device_tag_to_a_shipped_folder(string deviceTag, string? expected)
    {
        Assert.Equal(expected, UiLanguageTag.Match(deviceTag));
    }

    [Theory]
    [InlineData("zh", "zh-Hans")]
    [InlineData("EN", "en-US")]
    [InlineData("ja", "ja")]
    [InlineData("fi", "fi")]
    [InlineData("SV", "sv")]
    [InlineData("de", null)]
    [InlineData(null, null)]
    public void Tag_maps_a_stored_code_to_its_resource_folder(string? code, string? expected)
    {
        Assert.Equal(expected, UiLanguageTag.For(code));
    }

    private static IEnumerable<string> Keys(string tag) => Values(tag).Keys;

    private static Dictionary<string, string> Values(string tag) =>
        XDocument.Load(ResourcePath(tag)).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);

    private static string Placeholders(string value) =>
        string.Join(",", Placeholder().Matches(value).Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal));

    private static string ResourcePath(string tag) => Path.Combine(RepoPath(AppFolder), "Strings", tag, ResourceFile);

    private static string RepoPath(string relative)
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, relative)))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return Path.Combine(dir ?? throw new DirectoryNotFoundException(relative), relative);
    }

    [GeneratedRegex(@"x:Uid=""([^""]+)""")]
    private static partial Regex XUid();

    [GeneratedRegex(@"UiStrings\.(?:Get|Format)\(""([A-Za-z_]+)""")]
    private static partial Regex CodeKey();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();
}
