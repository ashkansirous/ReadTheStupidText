using System.Text.Json;

namespace DinoReader.Core;

/// <summary>One free story the store lists (Slice 92): the list line from <c>GET /api/stories</c>, checked.</summary>
public sealed record StoreItem(
    string Id, string Title, string Language, int SchemaVersion, int Version, int? PageCount, int? Minutes, string? CoverUrl, string DownloadUrl, string? Credits = null)
{
    /// <summary>False for a story packed in a newer format than this app reads: such a story is hidden, not greyed out.</summary>
    public bool IsReadable => SchemaVersion <= StoryPackageManifest.CurrentSchemaVersion;
}

/// <summary>Reads the store's list response (Slice 92).</summary>
public static class StoreCatalog
{
    private const string StoriesProperty = "stories";
    private const int MaxCreditsLength = 200;

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
        return valid
            ? new StoreItem(dto.Id!, dto.Title!, dto.Language!, dto.SchemaVersion, dto.Version, dto.PageCount, dto.Minutes, dto.CoverUrl, dto.DownloadUrl!, CreditsOf(dto.Credits))
            : null;
    }

    // The optional credit line: blank, or longer than the Worker allows, means none.
    private static string? CreditsOf(string? credits) =>
        string.IsNullOrWhiteSpace(credits) || credits.Trim().Length > MaxCreditsLength ? null : credits.Trim();

    private sealed class ItemDto
    {
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
    }
}
