using System.Text.Json;

namespace DinoReader.BookBuilder;

/// <summary>What the live store holds for one story (Slice 239): its listed version (0 when it is not listed), the content hash its <c>meta.json</c> carries (null for a book published before the hash existed)
/// and the highest <c>story-v&lt;n&gt;.dino</c> that exists in the bucket (0 when none), listed or not: a package left by a half-finished run or a delisted book must never be reused.</summary>
internal sealed record LiveEntry(int Version, string? ContentHash, int MaxPackage = 0);

/// <summary>
/// Slice 239: which version a store book is built as. <c>publish-store</c> writes the live state (<c>--live</c> file: story id to version and hash) before the build; a book whose
/// content hash equals the live one is built as the live version (nothing to upload), a changed one, or one live before the hash existed, as the higher of its
/// <c>book.json</c> version and one above every version live or already in the bucket, so an existing <c>story-v&lt;n&gt;.dino</c> is never reused or overwritten (the <c>meta.json</c> goes up last,
/// so a half-finished earlier run leaves live meta behind its content and the next run publishes at a new version). A book with no entry keeps its <c>book.json</c> version.
/// </summary>
internal static class StoreVersioning
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static int Decide(int bookVersion, string contentHash, LiveEntry? live) =>
        live is null ? bookVersion
            : live.ContentHash == contentHash && live.Version > 0 ? live.Version
            : Math.Max(bookVersion, Math.Max(live.Version, live.MaxPackage) + 1);

    public static IReadOnlyDictionary<string, LiveEntry> Read(string path) =>
        JsonSerializer.Deserialize<Dictionary<string, LiveEntry>>(File.ReadAllText(path), Options) ?? [];

    /// <summary>The book as the store builder takes it: its content hash and the version it is uploaded as.</summary>
    public static StoreStorySpec Versioned(BookFolder book, IReadOnlyDictionary<string, LiveEntry>? live)
    {
        string hash = BookHash.Of(book);
        LiveEntry? entry = live is not null && live.TryGetValue(book.Info.Id, out LiveEntry? found) ? found : null;
        return book.ToSpec() with { ContentHash = hash, Version = Decide(book.Info.Version, hash, entry) };
    }
}
