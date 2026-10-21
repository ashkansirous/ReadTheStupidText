using ReadTheStupidText.Infrastructure.Translation;

namespace ReadTheStupidText.Tests;

/// <summary>
/// Runs the real Opus-MT engine on a real pack (Slice 75). Opt-in: set <c>RTST_TRANSLATION_PACKS</c> to a
/// packs folder holding at least <c>translation-fr-en</c>; the test is skipped otherwise, so CI needs no model.
/// </summary>
public class MarianEngineEndToEndTests
{
    private const string PacksVariable = "RTST_TRANSLATION_PACKS";

    [Fact]
    public async Task A_french_sentence_translates_to_english_through_the_real_model()
    {
        string? root = Environment.GetEnvironmentVariable(PacksVariable);
        Assert.SkipWhen(string.IsNullOrEmpty(root), $"{PacksVariable} is not set");
        using var translator = new PackTranslator(() => root!);

        string result = await translator.TranslateAsync(
            "Le chat dort sur le canapé. Il fait beau aujourd'hui.", "fr", "en", CancellationToken.None);

        Console.WriteLine("E2E fr->en: " + result);
        Assert.Contains("cat", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task An_english_sentence_translates_to_portuguese_with_the_target_token()
    {
        string? root = Environment.GetEnvironmentVariable(PacksVariable);
        Assert.SkipWhen(string.IsNullOrEmpty(root) || !Directory.Exists(Path.Combine(root, "translation-en-pt")), "translation-en-pt is not installed");
        using var translator = new PackTranslator(() => root!);

        string result = await translator.TranslateAsync("The cat sleeps on the sofa.", "en", "pt", CancellationToken.None);

        Console.WriteLine("E2E en->pt: " + result);
        Assert.Contains("gato", result, StringComparison.OrdinalIgnoreCase);
    }
}
