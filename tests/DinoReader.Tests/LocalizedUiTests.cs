using System.Text.RegularExpressions;
using System.Xml.Linq;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>The Dino Reader UI strings (Slice 66, the pattern of Read The Stupid Text's Slice 53): all fourteen languages carry the same keys and placeholders.</summary>
public partial class LocalizedUiTests
{
    private const string NeutralFile = "AppResources.resx";
    private static readonly string AppFolder = Path.Combine("src", "DinoReader.App");
    private static readonly string ResourcesFolder = Path.Combine(AppFolder, "Resources", "Strings");

    // Keys whose value is deliberately inherited from English: the product name, file extensions, the unknown-gender mark and the flag map (country codes, Slice 173).
    private static readonly HashSet<string> MayInherit = ["App_Name", "Add_FileSub", "Cast_UnknownGender", "StoryLanguages_FlagMap"];

    [Fact]
    public void Every_language_file_has_exactly_the_neutral_keys_with_the_same_placeholders()
    {
        Dictionary<string, string> neutral = Load(NeutralFile);

        foreach (string file in LanguageFiles())
        {
            Dictionary<string, string> translated = Load(file);

            Assert.Empty(translated.Keys.Except(neutral.Keys));
            Assert.Empty(neutral.Keys.Except(translated.Keys).Except(MayInherit).Select(k => $"{file}: {k}"));
            foreach ((string key, string value) in translated)
            {
                Assert.False(string.IsNullOrWhiteSpace(value), $"{file}: {key} is empty");
                Assert.Equal(Placeholders(neutral[key]), Placeholders(value));
            }
        }
    }

    [Fact]
    public void The_animal_sounds_strings_are_translated_in_every_language()
    {
        Dictionary<string, string> neutral = Load(NeutralFile);

        foreach (string file in LanguageFiles())
        {
            Dictionary<string, string> translated = Load(file);
            foreach (string key in new[] { "Settings_AnimalSounds_Title", "Settings_AnimalSounds_Sub" })
            {
                Assert.True(translated.ContainsKey(key), $"{file}: {key} missing");
                Assert.NotEqual(neutral[key], translated[key]);
            }
        }
    }

    // Slice 172: the Settings strings are translated (not inherited) in every language, and the store no longer calls itself free.
    [Fact]
    public void The_settings_strings_are_translated_in_every_language()
    {
        Dictionary<string, string> neutral = Load(NeutralFile);
        string[] keys = ["Settings_Title", "Settings_Books", "Settings_GroupReading", "Settings_GroupPhone", "Settings_NewVersion", "Settings_GearLabel", "Settings_About", "Settings_NarratorVoice", "Narrator_TitleFor", "Gallery_GrownUpsTitle"];

        foreach (string file in LanguageFiles())
        {
            Dictionary<string, string> translated = Load(file);
            foreach (string key in keys)
            {
                Assert.True(translated.ContainsKey(key), $"{file}: {key} missing");
                if (!SameAsEnglishAllowed.Contains($"{file}:{key}"))
                {
                    Assert.NotEqual(neutral[key], translated[key]);
                }
            }
        }
    }

    // A legitimate cognate that reads the same as English ("file:key"); add one here rather than loosening the test.
    private static readonly HashSet<string> SameAsEnglishAllowed = [];

    [Fact]
    public void The_store_strings_do_not_say_free_in_english()
    {
        Dictionary<string, string> neutral = Load(NeutralFile);

        Assert.Equal("Store", neutral["Store_Title"]);
        Assert.DoesNotContain("free", neutral["Store_OfflineBody"], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("free", neutral["Intro_StoreLine"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void There_is_a_file_for_each_non_english_language()
    {
        string[] expected = UiLanguage.Supported.Where(c => c != UiLanguage.English).Order().ToArray();
        string[] actual = LanguageFiles().Select(f => f.Split('.')[1]).Order().ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Every_typed_property_has_a_neutral_resource_and_the_other_way_round()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), ResourcesFolder, "AppResources.cs"));
        string[] properties = PropertyRegex().Matches(source).Select(m => m.Groups[1].Value).ToArray();

        Assert.Equal(properties.Order(), Load(NeutralFile).Keys.Order());
    }

    [Fact]
    public void Plural_pairs_come_together()
    {
        string[] keys = Load(NeutralFile).Keys.ToArray();

        foreach (string one in keys.Where(k => k.EndsWith("_One", StringComparison.Ordinal)))
        {
            Assert.Contains(one[..^"_One".Length] + "_Other", keys);
        }
    }

    [Fact]
    public void The_pages_hold_no_hard_coded_text()
    {
        foreach (string xaml in Directory.GetFiles(Path.Combine(RepoRoot(), AppFolder), "*.xaml").Where(f => !f.EndsWith("App.xaml", StringComparison.Ordinal)))
        {
            string text = File.ReadAllText(xaml);

            Assert.Empty(LiteralRegex().Matches(text).Select(m => $"{Path.GetFileName(xaml)}: {m.Value}"));
        }
    }

    [Fact]
    public void Every_screen_language_names_itself()
    {
        Assert.Equal(UiLanguage.Supported.Count, LanguageNames.All.Count);
        Assert.All(UiLanguage.Supported, code => Assert.False(string.IsNullOrEmpty(LanguageNames.NativeName(code))));
        Assert.Equal("فارسی", LanguageNames.NativeName("fa"));
        Assert.Equal("fa", LanguageNames.CodeOf("فارسی"));
        Assert.Null(LanguageNames.CodeOf("Klingon"));
    }

    private static string[] LanguageFiles() =>
        Directory.GetFiles(Path.Combine(RepoRoot(), ResourcesFolder), "AppResources.*.resx")
            .Select(Path.GetFileName).Select(f => f!).ToArray();

    private static Dictionary<string, string> Load(string file) =>
        XDocument.Load(Path.Combine(RepoRoot(), ResourcesFolder, file)).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);

    private static string[] Placeholders(string text) =>
        PlaceholderRegex().Matches(text).Select(m => m.Value).Order().ToArray();

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new DirectoryNotFoundException("Repo root (ReadTheStupidText.slnx) not found.");
    }

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"public static string (\w+) => Get\(\);")]
    private static partial Regex PropertyRegex();

    // Text="Letters", Placeholder="Letters" and the like: anything that is not a binding or an x:Static.
    [GeneratedRegex(@"(?:\bText|\bPlaceholder|\bTitle|SemanticProperties\.(?:Description|Hint))=""[^{""]*\p{L}[^""]*""")]
    private static partial Regex LiteralRegex();
}
