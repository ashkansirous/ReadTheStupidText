using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile;

namespace ReadTheStupidText.Tests;

public partial class LocalizedUiTests
{
    private const string NeutralFile = "AppResources.resx";
    private const string ResourcesFolder = @"src\ReadTheStupidText.Mobile\Resources\Strings";

    // Keys whose value is deliberately inherited from English (the product name).
    private static readonly HashSet<string> MayInherit =
    [
        "App_Name",
    ];

    [Theory]
    [InlineData("fa", "en-US", "fa")]
    [InlineData("AR", "en-US", "ar")]
    [InlineData(null, "fr-CA", "fr")]
    [InlineData(null, "zh-Hans-CN", "zh")]
    [InlineData("xx", "ja-JP", "ja")]
    [InlineData(null, "de-DE", "en")]
    [InlineData("", "de-DE", "en")]
    public void Resolve_prefers_stored_then_device_then_english(string? stored, string device, string expected)
    {
        CultureInfo culture = UiCulture.Resolve(stored, CultureInfo.GetCultureInfo(device));

        Assert.Equal(expected, culture.TwoLetterISOLanguageName);
    }

    [Fact]
    public async Task Apply_sets_the_ui_culture_for_other_threads_and_after_awaits()
    {
        CultureInfo? savedUi = CultureInfo.DefaultThreadCurrentUICulture;
        CultureInfo? savedCulture = CultureInfo.DefaultThreadCurrentCulture;
        try
        {
            UiCulture.Apply(CultureInfo.GetCultureInfo("fa"));
            string onOtherThread = await Task.Run(() => CultureInfo.CurrentUICulture.Name);
            UiCulture.Apply(CultureInfo.GetCultureInfo("fr"));
            string afterSecondApply = await Task.Run(() => CultureInfo.CurrentUICulture.Name);

            Assert.Equal("fa", onOtherThread);
            Assert.Equal("fr", afterSecondApply);
            Assert.Equal(savedCulture, CultureInfo.DefaultThreadCurrentCulture);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentUICulture = savedUi;
            CultureInfo.DefaultThreadCurrentCulture = savedCulture;
        }
    }

    [Fact]
    public void Resolve_covers_all_nine_languages()
    {
        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            string code = AppLanguages.Code(language);

            Assert.Equal(code, UiCulture.Resolve(code, CultureInfo.InvariantCulture).TwoLetterISOLanguageName);
        }
    }

    [Theory]
    [InlineData("fa", true)]
    [InlineData("ar", true)]
    [InlineData("en", false)]
    [InlineData("fr", false)]
    [InlineData("zh", false)]
    [InlineData("ja", false)]
    [InlineData("ru", false)]
    public void Only_farsi_and_arabic_are_right_to_left(string code, bool expected)
    {
        Assert.Equal(expected, UiCulture.IsRightToLeft(CultureInfo.GetCultureInfo(code)));
    }

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
    public void There_is_a_file_for_each_non_english_language()
    {
        string[] expected = Enum.GetValues<AppLanguage>()
            .Select(AppLanguages.Code).Where(c => c != "en").Order().ToArray();
        string[] actual = LanguageFiles()
            .Select(f => f.Split('.')[1]).Order().ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Every_typed_property_has_a_neutral_resource()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), ResourcesFolder, "AppResources.cs"));
        string[] properties = PropertyRegex().Matches(source).Select(m => m.Groups[1].Value).ToArray();

        Assert.Equal(properties.Order(), Load(NeutralFile).Keys.Order());
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
}

public class LocalizedPackStatusTests
{
    [Theory]
    [MemberData(nameof(AllStates))]
    public void Every_state_is_null_or_non_null_exactly_like_the_shared_status_text(PackInstallState state)
    {
        string? shared = ReadTheStupidText.Mobile.PackStatusText.Compose(state, 0.5);
        string? localized = LocalizedPackStatus.Compose(state, 0.5);

        Assert.Equal(shared is null, localized is null);
    }

    public static TheoryData<PackInstallState> AllStates() => new(Enum.GetValues<PackInstallState>());
}
