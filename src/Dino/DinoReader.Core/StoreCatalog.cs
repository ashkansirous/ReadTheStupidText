using System.Text.Json;

namespace DinoReader.Core;

/// <summary>One free story the store lists (Slice 92): the list line from <c>GET /api/stories</c>, checked.</summary>
public sealed record StoreItem(
    string Id, string Title, string Language, int SchemaVersion, int Version, int? PageCount, int? Minutes, string? CoverUrl, string DownloadUrl, string? Credits = null,
    int? AgeMin = null, int? AgeMax = null, string? Author = null, IReadOnlyDictionary<string, string>? Titles = null,
    IReadOnlyDictionary<string, string>? CreditsByLanguage = null, IReadOnlyDictionary<string, AudioPackInfo>? Audio = null,
    IReadOnlyList<StoreTag>? Tags = null, long? Bytes = null,
    BookSale Sale = BookSale.Free, string? ProductId = null, IReadOnlyList<string>? UnlockedBy = null,
    string? HdDownloadUrl = null, long? HdBytes = null)
{
    /// <summary>Slice 375: whether the store offers this version of the book as a high-res package too (older Workers and books without print-size pictures: no).</summary>
    public bool HasHighRes => !string.IsNullOrWhiteSpace(HdDownloadUrl);

    /// <summary>The size of the package a download fetches for <paramref name="size"/> (Slice 375): the high-res pack's for High when the listing knows it, else the standard one's (also for StandardThenHigh, which fetches standard first).</summary>
    public long? BytesFor(PictureSize size) => size == PictureSize.High && HdBytes is > 0 ? HdBytes : Bytes;

    /// <summary>Slice 346: every store product that unlocks this book (its own plus each bundle that lists it); empty for a free book or an older Worker.</summary>
    public IReadOnlyList<string> Products => UnlockedBy ?? [];

    /// <summary>The store tags the listing carries (Slice 260), in list order; none from an older Worker.</summary>
    public IReadOnlyList<StoreTag> TagList => Tags ?? [];

    /// <summary>The badges a card shows: at most two tags, in list order (Slice 260).</summary>
    public IReadOnlyList<StoreTag> Badges => StoreTags.Badges(TagList);

    /// <summary>The pre-made audio pack the listing offers for <paramref name="language"/> (the book-translation key, e.g. <c>fa</c>), or null (Slice 246 C2).</summary>
    public AudioPackInfo? AudioIn(string language) =>
        Audio is not null && Audio.TryGetValue(BundledTranslations.Key(language), out AudioPackInfo? pack) ? pack : null;

    /// <summary>The title in <paramref name="language"/> from the listing's <c>titles</c> (Slice 159), or null when the listing has none for it.</summary>
    public string? TitleIn(string language) =>
        Titles is not null && Titles.TryGetValue(UiLanguage.Resolve(language), out string? title) ? title : null;

    /// <summary>The credit line in <paramref name="language"/> (Slice 192): the listing's <c>creditsByLanguage</c> entry for it, else the English <c>credits</c>, else null.</summary>
    public string? CreditsIn(string language) =>
        CreditsByLanguage is not null && CreditsByLanguage.TryGetValue(UiLanguage.Resolve(language), out string? credits) ? credits : Credits;

    /// <summary>False for a story packed in a newer format than this app reads: such a story is hidden, not greyed out.</summary>
    public bool IsReadable => SchemaVersion <= StoryPackageManifest.CurrentSchemaVersion;
}

/// <summary>One audio pack the listing offers (Slice 246 B2): its version, the zip's size in bytes when the listing knows it, and the download address.</summary>
public sealed record AudioPackInfo(int Version, long? Bytes, string Url);

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
    public static IReadOnlyList<StoreItem> Parse(string json) => Parse(json, out _);

    /// <summary>Like <see cref="Parse(string)"/>, and says how many entries of the list were left out as malformed, so the caller can count a listing that lost books.</summary>
    public static IReadOnlyList<StoreItem> Parse(string json, out int skipped)
    {
        skipped = 0;
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The story list is not an object.");
        }

        if (!document.RootElement.TryGetProperty(StoriesProperty, out JsonElement stories) || stories.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<StoreItem> items = [.. stories.EnumerateArray().Select(ItemOf).OfType<StoreItem>()];
        skipped = stories.GetArrayLength() - items.Count;
        return StoreTags.TaggedFirst(items, item => item.TagList);
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
            // no-fallback: a pure parser; the left-out item is counted in Parse's skipped value, which StoreClient.LoadAsync reports (store_failed)
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
                ageMin, ageMax, AuthorOf(dto.Author), TitlesOf(dto.Titles), TextMapOf(dto.CreditsByLanguage, MaxCreditsLength), AudioOf(dto.Audio),
                StoreTags.FromJson(dto.Tags), BytesOf(dto.Bytes), dto.Access == PaidAccess ? BookSale.Paid : BookSale.Free, ProductIdOf(dto.ProductId), ProductIdsOf(dto.UnlockedBy),
                string.IsNullOrWhiteSpace(dto.HdDownloadUrl) ? null : dto.HdDownloadUrl.Trim(), BytesOf(dto.HdBytes))
            : null;
    }

    // Slice 346: the Worker's `access` is "paid" or "free"; a list without the field is an older Worker, where every book is free.
    private const string PaidAccess = "paid";
    private const int MaxProducts = 10;

    private static string? ProductIdOf(string? id) => string.IsNullOrWhiteSpace(id) || id.Trim().Length > MaxProductIdLength ? null : id.Trim();

    private const int MaxProductIdLength = 100;

    private static IReadOnlyList<string>? ProductIdsOf(JsonElement? ids) =>
        ids is { ValueKind: JsonValueKind.Array } list
            ? [.. list.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => ProductIdOf(e.GetString())).OfType<string>().Distinct().Take(MaxProducts)]
            : null;

    // Slice 246 B2/C2: language code to {version, bytes, url}. A wrong-typed pack, a version below 1, an unsupported language or a blank address drops only that pack; bytes that are not a whole
    // positive number are unknown (null), which skips the size check but not the download.
    private static IReadOnlyDictionary<string, AudioPackInfo>? AudioOf(JsonElement? audio)
    {
        if (audio is not { ValueKind: JsonValueKind.Object } map)
        {
            return null;
        }

        var kept = new Dictionary<string, AudioPackInfo>(StringComparer.Ordinal);
        foreach (JsonProperty property in map.EnumerateObject().Take(MaxTitleLanguages))
        {
            if (UiLanguage.IsSupported(property.Name) && PackOf(property.Value) is { } pack)
            {
                kept[BundledTranslations.Key(property.Name)] = pack;
            }
        }

        return kept.Count > 0 ? kept : null;
    }

    private static AudioPackInfo? PackOf(JsonElement pack)
    {
        if (pack.ValueKind != JsonValueKind.Object || !pack.TryGetProperty("url", out JsonElement url) || url.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(url.GetString()))
        {
            return null;
        }

        int? version = IntegerOf(pack.TryGetProperty("version", out JsonElement found) ? found : null);
        long? bytes = pack.TryGetProperty("bytes", out JsonElement size) && size.ValueKind == JsonValueKind.Number && size.TryGetInt64(out long length) && length > 0 ? length : null;
        return version is >= 1 ? new AudioPackInfo(version.Value, bytes, url.GetString()!.Trim()) : null;
    }

    // Slice 260: the package size; anything but a whole positive number is unknown (null), as in the Worker.
    private static long? BytesOf(JsonElement? size) =>
        size is { ValueKind: JsonValueKind.Number } number && number.TryGetInt64(out long length) && length > 0 ? length : null;

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

    private static IReadOnlyDictionary<string, string>? TitlesOf(JsonElement? titles) => TextMapOf(titles, MaxTitleLength);

    // Slice 159 (titles) and Slice 192 (credit lines): language code to text, as the Worker passes it through; a wrong-typed field, a blank or over-long text and a code that is no language are dropped.
    private static IReadOnlyDictionary<string, string>? TextMapOf(JsonElement? titles, int maxLength)
    {
        if (titles is not { ValueKind: JsonValueKind.Object } map)
        {
            return null;
        }

        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty property in map.EnumerateObject().Where(entry => UiLanguage.IsSupported(entry.Name)).Take(MaxTitleLanguages))
        {
            string? title = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString()?.Trim() : null;
            if (!string.IsNullOrEmpty(title) && title.Length <= maxLength && UiLanguage.IsSupported(property.Name))
            {
                kept[UiLanguage.Resolve(property.Name)] = title;
            }
        }

        return kept.Count > 0 ? kept : null;
    }

    private sealed class ItemDto
    {
        public JsonElement? Titles { get; set; }

        public JsonElement? CreditsByLanguage { get; set; }

        public JsonElement? Audio { get; set; }

        public JsonElement? Tags { get; set; }

        public JsonElement? Bytes { get; set; }

        public string? HdDownloadUrl { get; set; }

        public JsonElement? HdBytes { get; set; }

        public string? Access { get; set; }

        public string? ProductId { get; set; }

        public JsonElement? UnlockedBy { get; set; }

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
