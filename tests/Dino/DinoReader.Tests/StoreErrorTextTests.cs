namespace DinoReader.Tests;

/// <summary>The store's server-error lines (Slice 357): one per status kind, translated in every language, never left in English.</summary>
public partial class LocalizedUiTests
{
    private static readonly string[] StoreErrorKeys =
    [
        "Error_PaymentRequired",
        "Error_Server",
        "Error_TooManyTries",
        "Error_BookGone",
        "Store_ServerErrorTitle",
        "Store_Free",
        "Store_BoughtBeforeTitle",
        "Store_BoughtBefore_Google",
        "Store_BoughtBefore_Google_Tablet",
        "Store_BoughtBefore_Apple",
        "Store_BoughtBefore_Apple_Tablet",
        "Store_BoughtBefore_Microsoft",
        "Store_Restored_One",
        "Store_Restored_Other",
        "Store_NothingToRestore",
    ];

    [Fact]
    public void The_store_error_strings_are_translated_in_every_language()
    {
        Dictionary<string, string> neutral = Load(NeutralFile);

        foreach (string key in StoreErrorKeys)
        {
            Assert.False(string.IsNullOrWhiteSpace(neutral[key]), $"{key} is empty in English");
        }

        foreach (string file in LanguageFiles())
        {
            Dictionary<string, string> translated = Load(file);
            foreach (string key in StoreErrorKeys)
            {
                Assert.True(translated.ContainsKey(key), $"{file}: {key} missing");
                Assert.NotEqual(neutral[key], translated[key]);
            }
        }
    }

    [Fact]
    public void The_error_lines_are_the_copy_notes_english_text()
    {
        Dictionary<string, string> neutral = Load(NeutralFile);
        Assert.Equal("You need to buy this book first.", neutral["Error_PaymentRequired"]);
        Assert.Equal("Something went wrong on the server. Please try again later.", neutral["Error_Server"]);
        Assert.Equal("Too many tries. Wait a minute, then try again.", neutral["Error_TooManyTries"]);
        Assert.Equal("This book isn’t in the Store any more.", neutral["Error_BookGone"]);
        Assert.Equal("{0} books restored. They’re on the shelf.", neutral["Store_Restored_Other"]);
    }

    [Fact]
    public void The_old_per_status_keys_and_the_restored_alert_are_gone_from_every_file()
    {
        string[] gone = ["Store_Error_Unauthorized", "Store_Error_Forbidden", "Store_Error_NotFound", "Store_Error_PaymentRequired", "Store_Error_TooManyRequests", "Store_Error_Server", "Store_Restored"];
        foreach (string file in new[] { NeutralFile }.Concat(LanguageFiles()))
        {
            Dictionary<string, string> strings = Load(file);
            foreach (string key in gone)
            {
                Assert.False(strings.ContainsKey(key), $"{file}: {key} should be gone");
            }
        }
    }

    [Fact]
    public void The_restored_other_line_keeps_its_count_placeholder_in_every_language()
    {
        foreach (string file in LanguageFiles())
        {
            Assert.Contains("{0}", Load(file)["Store_Restored_Other"]);
        }
    }
}
