using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DinoReader.BookBuilder;

/// <summary>What the book remembers about one published audio pack: its version, the hash of its content and its size. Lives in the book's <c>audio-cast.json</c> under <c>audioPacks</c>.</summary>
/// <remarks>Slice 281: <paramref name="ListenSeconds"/> is the length of the book's listening track (<c>listen-&lt;lang&gt;-v&lt;n&gt;.m4a</c>, <see cref="ListenTrack"/>) of this pack version; null until it was built.</remarks>
internal sealed record AudioPackRecord(int Version, string Hash, long Bytes, int? ListenSeconds = null);

/// <summary>
/// Slice 246 B2: the per-language audio pack of a store book or a bundled starter (Slice 258). One zip per book language, <c>audio-&lt;lang&gt;-v&lt;n&gt;.zip</c>, holding the generator's <c>manifest.json</c> (version 3) and the
/// clips it names at the zip's root, written beside the book in <c>artifacts/store/&lt;id&gt;/</c>. The clips live outside git, so the pack's version and content hash are kept in the book's
/// <c>audio-cast.json</c> (<c>audioPacks</c>): the version goes up only when the content hash changes, and <see cref="StoreBuilder"/> copies the records into <c>meta.json</c> (<c>audio</c>), so a
/// later <c>build</c> (and publish-store's meta refresh) keeps the field. The zip is deterministic (sorted entries, fixed time, stored), so the same clips always give the same bytes.
/// </summary>
internal static class AudioPacks
{
    public const string CastFile = "audio-cast.json";
    public const string ManifestFile = "manifest.json";
    public const string RecordsKey = "audioPacks";
    public const int ManifestVersion = 3;
    public const string EntriesKey = "entries";
    public const string IndexKey = "index";
    public const string ClipKey = "clip";

    private static readonly DateTimeOffset ZipTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    public static string PackFileName(string language, int version) => $"audio-{language}-v{version}.zip";

    /// <summary>Slice 265: where a book keeps its clips, <c>content/dino/&lt;book&gt;/audio/&lt;language&gt;</c> (Git LFS; a normal checkout holds only pointer files).</summary>
    public const string ClipsFolderName = "audio";

    /// <summary>The first bytes of a Git LFS pointer file, which a checkout holds instead of a clip until <c>git lfs pull</c> fetched it.</summary>
    private const string LfsPointerPrefix = "version https://git-lfs";

    public static string ClipsFolderOf(BookFolder book, string language) => Path.Combine(book.Dir, ClipsFolderName, language);

    /// <summary>True for a Git LFS pointer file (a clip or manifest that was never downloaded).</summary>
    public static bool IsLfsPointer(string path)
    {
        using FileStream stream = File.OpenRead(path);
        Span<byte> head = stackalloc byte[LfsPointerPrefix.Length];
        return stream.Read(head) == head.Length && Encoding.ASCII.GetString(head) == LfsPointerPrefix;
    }

    private static void RejectLfsPointer(string path)
    {
        if (IsLfsPointer(path))
        {
            throw new InvalidOperationException($"{path} is a Git LFS pointer, not the file: run git lfs pull --include \"content/dino/<book>/audio/**\" --exclude \"\" first.");
        }
    }

    /// <summary>The version a pack is built as: 1 for the first, the recorded one when the content hash is unchanged, else one above it.</summary>
    public static int Decide(AudioPackRecord? recorded, string contentHash) =>
        recorded is null ? 1 : recorded.Hash == contentHash ? recorded.Version : recorded.Version + 1;

    /// <summary>The records of a book folder's <c>audio-cast.json</c>, language to record (empty when the file or the key is missing).</summary>
    public static IReadOnlyDictionary<string, AudioPackRecord> RecordsIn(string bookDir)
    {
        string path = Path.Combine(bookDir, CastFile);
        if (!File.Exists(path))
        {
            return new Dictionary<string, AudioPackRecord>();
        }

        JsonNode? node = JsonNode.Parse(File.ReadAllText(path))?[RecordsKey];
        return node?.Deserialize<Dictionary<string, AudioPackRecord>>(ReadOptions) ?? [];
    }

    /// <summary>The <c>audio</c> object of a book's <c>meta.json</c>: language to {version, bytes}, sorted; null when the book has no pack.</summary>
    public static SortedDictionary<string, object>? MetaOf(BookFolder? book)
    {
        IReadOnlyDictionary<string, AudioPackRecord> records = book is null ? new Dictionary<string, AudioPackRecord>() : RecordsIn(book.Dir);
        return records.Count == 0
            ? null
            : new SortedDictionary<string, object>(records.ToDictionary(r => r.Key, r => MetaEntryOf(r.Value)), StringComparer.Ordinal);
    }

    // Slice 281: listenSeconds is listed only once the track was built (key order version, bytes, listenSeconds, so an unchanged book's meta.json text does not change).
    private static object MetaEntryOf(AudioPackRecord record) => record.ListenSeconds is { } seconds
        ? new { version = record.Version, bytes = record.Bytes, listenSeconds = seconds }
        : new { version = record.Version, bytes = record.Bytes };

    /// <summary>The files a pack holds, in zip order: <c>manifest.json</c> first, then each clip the manifest names, once, by ordinal name. Fails on a manifest of another version or language, or a missing clip.</summary>
    public static IReadOnlyList<string> PackFiles(string clipsFolder, string language)
    {
        using JsonDocument manifest = OpenManifest(clipsFolder, language);
        List<string> clips = manifest.RootElement.GetProperty(EntriesKey).EnumerateArray().Select(e => ClipName(e, clipsFolder)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        RejectClashingNames(clips, clipsFolder);
        return [ManifestFile, .. clips];
    }

    /// <summary>Slice 281: the clip file name of each manifest entry by its <c>index</c>, under the same checks as <see cref="PackFiles"/> (manifest present, not an LFS pointer, version and language, plain existing clip names that are no LFS pointers).</summary>
    public static IReadOnlyDictionary<int, string> ClipsByIndex(string clipsFolder, string language)
    {
        using JsonDocument manifest = OpenManifest(clipsFolder, language);
        var byIndex = new Dictionary<int, string>();
        foreach (JsonElement entry in manifest.RootElement.GetProperty(EntriesKey).EnumerateArray())
        {
            string clip = ClipName(entry, clipsFolder);
            if (entry.TryGetProperty(IndexKey, out JsonElement value) && value.TryGetInt32(out int index))
            {
                byIndex[index] = clip;
            }
        }

        return byIndex;
    }

    private static JsonDocument OpenManifest(string clipsFolder, string language)
    {
        string manifestPath = Path.Combine(clipsFolder, ManifestFile);
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException($"{clipsFolder} has no {ManifestFile}; point --clips at a generator folder (tools/dino/premade-audio).");
        }

        RejectLfsPointer(manifestPath);
        JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        CheckManifest(manifest.RootElement, language, manifestPath);
        return manifest;
    }

    private static void RejectClashingNames(IReadOnlyList<string> clips, string clipsFolder)
    {
        var seen = new HashSet<string>([ManifestFile], StringComparer.OrdinalIgnoreCase);
        foreach (string clip in clips)
        {
            if (!seen.Add(clip))
            {
                throw new InvalidOperationException($"The clip {clip} in {clipsFolder} clashes with {ManifestFile} or another clip whose name differs only in case.");
            }
        }
    }

    private static void CheckManifest(JsonElement root, string language, string path)
    {
        bool versionOk = root.TryGetProperty("version", out JsonElement version) && version.ValueKind == JsonValueKind.Number && version.GetInt32() == ManifestVersion;
        bool languageOk = root.TryGetProperty("language", out JsonElement lang) && lang.ValueKind == JsonValueKind.String && lang.GetString() == language;
        if (!versionOk || !languageOk || !root.TryGetProperty(EntriesKey, out JsonElement entries) || entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"{path} is not a version {ManifestVersion} manifest of language '{language}' with at least one entry.");
        }
    }

    private static string ClipName(JsonElement entry, string clipsFolder)
    {
        string? clip = entry.TryGetProperty(ClipKey, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (string.IsNullOrEmpty(clip) || clip != Path.GetFileName(clip))
        {
            throw new InvalidOperationException($"A manifest entry in {clipsFolder} has no plain clip file name.");
        }

        string path = Path.Combine(clipsFolder, clip);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"The manifest names {clip} but {clipsFolder} has no such file.");
        }

        RejectLfsPointer(path);
        return clip;
    }

    /// <summary>The SHA-256 (lower-case hex) over each file's name, length and bytes, in the given order.</summary>
    public static string ContentHash(string clipsFolder, IReadOnlyList<string> files)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string name in files)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(clipsFolder, name));
            hash.AppendData(Encoding.UTF8.GetBytes($"{name}\0{bytes.Length}\0"));
            hash.AppendData(bytes);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    /// <summary>Writes the zip: stored entries (clips are already compressed) with a fixed time, so equal content is equal bytes.</summary>
    public static void WriteZip(string zipPath, string clipsFolder, IReadOnlyList<string> files)
    {
        using FileStream output = File.Create(zipPath);
        using var zip = new ZipArchive(output, ZipArchiveMode.Create);
        foreach (string name in files)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.NoCompression);
            entry.LastWriteTime = ZipTime;
            using Stream target = entry.Open();
            using FileStream source = File.OpenRead(Path.Combine(clipsFolder, name));
            source.CopyTo(target);
        }
    }

    /// <summary>Builds one pack into <paramref name="storeFolder"/><c>/&lt;id&gt;/</c> and records it in the book's <c>audio-cast.json</c>. Returns the record, whose version is the pack's.</summary>
    public static AudioPackRecord Build(BookFolder book, string language, string clipsFolder, string storeFolder)
    {
        if (!DinoReader.Core.UiLanguage.Supported.Contains(language, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"'{language}' is not one of the app's book languages ({string.Join(", ", DinoReader.Core.UiLanguage.Supported)}); use the lower-case code, e.g. fa.");
        }

        if (!File.Exists(Path.Combine(book.Dir, CastFile)))
        {
            throw new InvalidOperationException($"{book.Stem} has no {CastFile}; a book gets audio packs only after a cast was written for it.");
        }

        IReadOnlyList<string> files = PackFiles(clipsFolder, language);
        string hash = ContentHash(clipsFolder, files);
        IReadOnlyDictionary<string, AudioPackRecord> records = RecordsIn(book.Dir);
        int version = Decide(records.GetValueOrDefault(language), hash);
        string folder = Path.Combine(storeFolder, book.Info.Id);
        Directory.CreateDirectory(folder);
        string zipPath = Path.Combine(folder, PackFileName(language, version));
        WriteZip(zipPath, clipsFolder, files);
        AudioPackRecord? recorded = records.GetValueOrDefault(language);
        var record = new AudioPackRecord(version, hash, new FileInfo(zipPath).Length, recorded?.Version == version ? recorded.ListenSeconds : null);
        SaveRecord(book.Dir, language, record);
        return record;
    }

    internal static void SaveRecord(string bookDir, string language, AudioPackRecord record)
    {
        string path = Path.Combine(bookDir, CastFile);
        JsonObject cast = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new InvalidOperationException($"{path} is not a JSON object.");
        JsonObject packs = cast[RecordsKey]?.AsObject() ?? [];
        packs[language] = record.ListenSeconds is { } seconds
            ? JsonSerializer.SerializeToNode(new { version = record.Version, hash = record.Hash, bytes = record.Bytes, listenSeconds = seconds })
            : JsonSerializer.SerializeToNode(new { version = record.Version, hash = record.Hash, bytes = record.Bytes });
        cast[RecordsKey] = new JsonObject(packs.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
        File.WriteAllText(path, cast.ToJsonString(WriteOptions).ReplaceLineEndings("\n") + "\n");
    }
}
