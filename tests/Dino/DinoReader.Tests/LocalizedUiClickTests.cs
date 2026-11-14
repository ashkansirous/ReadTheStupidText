namespace DinoReader.Tests;

// Slice 235 part B: the Windows window says "click" where a phone says "tap", from separate strings (AppResources.UseClickWording picks them), in every language.
public partial class LocalizedUiTests
{
    private static readonly string[] ClickWordedKeys = ["Gallery_CornerLine"]; // Slice 362: the old intro's first card (Intro_PlacesKidsLine) went with the wizard

    [Fact]
    public void Every_language_words_each_click_twin_differently_from_its_tap_string()
    {
        foreach (string file in new[] { NeutralFile }.Concat(LanguageFiles()))
        {
            Dictionary<string, string> strings = Load(file);

            foreach (string key in ClickWordedKeys)
            {
                Assert.True(strings.ContainsKey(key + "Click"), $"{file}: {key}Click missing");
                Assert.NotEqual(strings[key], strings[key + "Click"]);
            }
        }
    }

    [Fact]
    public void The_english_click_twins_say_click_and_never_tap()
    {
        Dictionary<string, string> neutral = Load(NeutralFile);

        foreach (string key in ClickWordedKeys)
        {
            Assert.Contains("click", neutral[key + "Click"], StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("tap", neutral[key + "Click"], StringComparison.OrdinalIgnoreCase);
        }
    }
}
