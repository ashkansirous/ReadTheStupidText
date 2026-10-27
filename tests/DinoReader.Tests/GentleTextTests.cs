using DinoReader.BookBuilder;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DinoReader.Tests;

/// <summary>No Dino book shows harm or pain (Decision 130): every bundled and store story text is scanned for words that name it.</summary>
public sealed class GentleTextTests
{
    // Whole words or phrases, matched case-insensitively. Plain "hot" and "cold" stay allowed (weather, porridge), so do the sound "trip, trap", "teeth" in a harmless line ("what big teeth you have", a mouse nibbling ropes) and "chase" or "afraid" in a game or a worry that resolves.
    private static readonly string[] Banned =
    [
        "eat you", "eat him", "eat her", "gobble", "kill", "killed", "dead", "die", "died", "blood", "axe", "knife", "oven", "burn", "burned", "burnt",
        "fire", "boil", "boiled", "boiling", "hot pot", "pie", "trapped", "claws", "sharp teeth", "bared teeth", "gnash", "gnashing", "ugly", "frightened", "trembling", "sobs", "sob", "bleed", "hurt",
    ];

    private static readonly Regex BannedPattern = new(
        @"\b(" + string.Join("|", Banned.Select(Regex.Escape)) + @")\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IEnumerable<object[]> Stories()
    {
        foreach (BookFolder book in Books.Starters.Where(b => b.Text is not null))
        {
            yield return [book.Info.Title, book.Text!];
        }

        foreach (StoreStorySpec spec in Books.Store)
        {
            yield return [spec.Title, spec.Text];
        }

        yield return ["Good Enough", GoodEnoughText()];
    }

    [Theory]
    [MemberData(nameof(Stories))]
    public void EveryBundledAndStoreText_HasNoBannedHarmWord(string title, string text)
    {
        var found = BannedPattern.Matches(text).Select(m => m.Value.ToLowerInvariant()).Distinct().ToList();

        Assert.True(found.Count == 0, $"{title} contains: {string.Join(", ", found)}");
    }

    [Fact]
    public void TheBannedList_CatchesWholeWordsOnly()
    {
        Assert.Matches(BannedPattern, "He was Dead tired, they said it was ugly; sharp teeth!");
        Assert.DoesNotMatch(BannedPattern, "The soldier admired the diesel fireplace and an axel; a tie, a spied tiger, trip, trap, big teeth.");
    }

    private static string GoodEnoughText()
    {
        string path = Path.Combine(Books.Folder("good-enough").Dir, "lines.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        return string.Join("\n", TextsOf(doc.RootElement));
    }

    private static IEnumerable<string> TextsOf(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => p.Name == "text" && p.Value.ValueKind == JsonValueKind.String ? [p.Value.GetString()!] : TextsOf(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany(TextsOf),
        _ => [],
    };
}
