namespace DinoReader.Tests;

/// <summary>The store's server-error lines (Slice 357): one per status kind, translated in every language, never left in English.</summary>
public partial class LocalizedUiTests
{
    private static readonly string[] StoreErrorKeys =
    [
        "Store_Error_Unauthorized",
        "Store_Error_Forbidden",
        "Store_Error_NotFound",
        "Store_Error_PaymentRequired",
        "Store_Error_TooManyRequests",
        "Store_Error_Server",
        "Store_ServerErrorTitle",
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
    public void The_payment_required_line_is_the_decided_english_text()
    {
        Assert.Equal("You need to buy this book first", Load(NeutralFile)["Store_Error_PaymentRequired"]);
        Assert.Equal("Problem connecting to the server, please retry later", Load(NeutralFile)["Store_Error_Server"]);
    }
}
