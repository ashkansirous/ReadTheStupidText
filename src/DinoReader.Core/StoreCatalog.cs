using System.Text.Json;

namespace DinoReader.Core;

/// <summary>One free story the store lists (Slice 92): the list line from <c>GET /api/stories</c>, checked.</summary>
public sealed record StoreItem(
    string Id, string Title, string Language, int SchemaVersion, int Version, int? PageCount, int? Minutes, string? CoverUrl, string DownloadUrl, string? Credits = null,
    int? AgeMin = null, int? AgeMax = null, string? Author = null, IReadOnlyDictionary<string, string>? Titles = null)
{
    /// <summary>The title in <paramref name="language"/> from the listing's <c>titles</c> (Slice 159), or null when the listing has none for it.</summary>
    public string? TitleIn(string language) =>
        Titles is not null && Titles.TryGetValue(UiLanguage.Resolve(language), out string? title) ? title : null;

    /// <summary>False for a story packed in a newer format than this app reads: such a story is hidden, not greyed out.</summary>
    public bool IsReadable => SchemaVersion <= StoryPackageManifest.CurrentSchemaVersion;
}

/// <summary>Reads the store's list response (Slice 92).</summary>
public static class StoreCatalog
{
    private const string StoriesProperty = "stories";
    private const int MaxCreditsLength = 200;
    private const int MaxAuthorLength = 100;
    private const int MaxAge = 18;
    private const int MaxTitleLength = 200;
    private const int MaxTitleLanguages = 20;

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The well-formed entries of a list response; a malformed entry is skipped, a body that is no list throws <see cref="JsonException"/>.</summary>
    public static IReadOnlyList<StoreItem> Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The story list is not an object.");
        }

        if (!document.RootElement.TryGetProperty(StoriesProperty, out JsonElement stories) || stories.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return stories.EnumerateArray().Select(ItemOf).OfType<StoreItem>().ToList();
    }

    /// <summary>The entries this app can read, in list order.</summary>
    public static IReadOnlyList<StoreItem> Readable(IEnumerable<StoreItem> items) => items.Where(i => i.IsReadable).ToList();

    // One entry with a wrong-typed field is skipped; it does not spoil the list.
    private static StoreItem? ItemOf(JsonElement element)
    {
        try
        {
            ItemDto? dto = element.Deserialize<ItemDto>(Options);
            return dto is null ? null : ItemOf(dto);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static StoreItem? ItemOf(ItemDto dto)
    {
        bool valid = StoryIds.IsValid(dto.Id) && !string.IsNullOrWhiteSpace(dto.Title) && !string.IsNullOrWhiteSpace(dto.Language)
            && !string.IsNullOrWhiteSpace(dto.DownloadUrl) && dto.SchemaVersion >= 1 && dto.Version >= 1;
        (int? ageMin, int? ageMax) = AgeRangeOf(dto);
        return valid
            ? new StoreItem(dto.Id!, dto.Title!, dto.Language!, dto.SchemaVersion, dto.Version, dto.PageCount, dto.Minutes, dto.CoverUrl, dto.DownloadUrl!, CreditsOf(dto.Credits),
                ageMin, ageMax, AuthorOf(dto.Author), TitlesOf(dto.Titles))
            : null;
    }

    // The optional credit line: blank, or longer than the Worker allows, means none.
    private static string? CreditsOf(string? credits) =>
        string.IsNullOrWhiteSpace(credits) || credits.Trim().Length > MaxCreditsLength ? null : credits.Trim();

    // Slice 112: an age range counts only when both ends are 0-18 and min <= max (the Worker's rule); otherwise the story has none.
    // A wrong-typed end (3.5, "x") drops only the range, as the Worker does.
    private static (int? Min, int? Max) AgeRangeOf(ItemDto dto)
    {
        int? min = IntegerOf(dto.AgeMin);
        int? max = IntegerOf(dto.AgeMax);
        bool valid = min is >= 0 and <= MaxAge && max is >= 0 and <= MaxAge && min <= max;
        return valid ? (min, max) : (null, null);
    }

    private static int? IntegerOf(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Number } number && number.TryGetDouble(out double result) && Math.Floor(result) == result && Math.Abs(result) <= int.MaxValue
            ? (int)result
            : null;

    // A wrong-typed author (7, true) counts as none, as in the Worker.
    private static string? AuthorOf(JsonElement? author) =>
        author is { ValueKind: JsonValueKind.String } text ? AuthorOf(text.GetString()) : null;

    private static string? AuthorOf(string? author) =>
        string.IsNullOrWhiteSpace(author) || author.Trim().Length > MaxAuthorLength ? null : author.Trim();

    // Slice 159: language code to title, as the Worker passes it through; a wrong-typed field, a blank or over-long title and a code that is no language are dropped.
    private static IReadOnlyDictionary<string, string>? TitlesOf(JsonElement? titles)
    {
        if (titles is not { ValueKind: JsonValueKind.Object } map)
        {
            return null;
        }

        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty property in map.EnumerateObject().Take(MaxTitleLanguages))
        {
            string? title = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()?.Trim() : null;
            if (!string.IsNullOrEmpty(title) && title.Length <= MaxTitleLength && UiLanguage.IsSupported(property.Name))
            {
                kept[UiLanguage.Resolve(property.Name)] = title;
            }
        }

        return kept.Count > 0 ? kept : null;
    }

    private sealed class ItemDto
    {
        public JsonElement? Titles { get; set; }

        public string? Id { get; set; }

        public string? Title { get; set; }

        public string? Language { get; set; }

        public int SchemaVersion { get; set; }

        public int Version { get; set; }

        public int? PageCount { get; set; }

        public int? Minutes { get; set; }

        public string? CoverUrl { get; set; }

        public string? DownloadUrl { get; set; }

        public string? Credits { get; set; }

        public JsonElement? AgeMin { get; set; }

        public JsonElement? AgeMax { get; set; }

        public JsonElement? Author { get; set; }
    }
}
