using System.Text.Json;

namespace DinoReader.Core;

/// <summary>Raised when a backup file cannot be read as a backup (Slice 93).</summary>
public class StoryBackupException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Raised, before anything is added, when a backup comes from a newer app than this one (Slice 93).</summary>
public sealed class StoryBackupNewerException() : StoryBackupException("The backup is from a newer version of Dino Book Reader.");

/// <summary>
/// How a restore went: stories added, stories skipped because their id was already on the shelf, stories that could not be read, and
/// stories left unrestored because the backup outgrew the restore's size budget (the restore stops there, keeping what it added), and stories already on the shelf
/// that got recordings back (<see cref="Merged"/>, Slice 230).
/// </summary>
public sealed record StoryRestoreResult(int Added, int Skipped, int Damaged, int TooLarge = 0, int Merged = 0, IReadOnlyList<MissingBook>? Missing = null);

/// <summary>A book the backup lists that is not on the phone (Slice 346): its store id and title. The restore never carries book files; the app downloads it from the store (free or owned) and then applies the backup to it.</summary>
public sealed record MissingBook(string Id, string Title);

/// <summary>One shelf book in a backup (Slice 346): the store id it is known by and its title (shown if it cannot be fetched).</summary>
public sealed record BackupShelfEntry(string Id, string Title);

/// <summary>
/// The backup file (Slice 93, Decision 96; Slice 346 schema 2): a zip holding <c>backup.json</c> (this manifest) and, since schema 2, no book files at all:
/// the <see cref="Shelf"/> (book ids), <c>books/&lt;id&gt;.json</c> per book (reading view and recordings), the parents' recordings under <c>books/&lt;id&gt;/</c> and a few
/// <see cref="Settings"/>. Schema 1 held one <c>&lt;story id&gt;.dino</c> package per story; those still restore (their recordings and shelf list are used, their book files are ignored).
/// Its own <see cref="SchemaVersion"/> is separate from the packages'.
/// </summary>
public sealed record StoryBackupManifest(int SchemaVersion, int Count, DateTimeOffset Created, IReadOnlyList<BackupShelfEntry>? Shelf = null, IReadOnlyDictionary<string, string>? Settings = null)
{
    /// <summary>Schema 1: one <c>.dino</c> package per story (the backup of Slice 93).</summary>
    public const int PackageSchemaVersion = 1;

    /// <summary>Schema 2: state and recordings only (Slice 346).</summary>
    public const int StateSchemaVersion = 2;

    public const int CurrentSchemaVersion = StateSchemaVersion;
    public const string ManifestFileName = "backup.json";
    public const string Extension = ".zip";

    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>Reads the manifest. A newer schema is refused as <see cref="StoryBackupNewerException"/> even if its other fields changed shape.</summary>
    public static StoryBackupManifest Parse(string json)
    {
        try
        {
            if (PeekSchemaVersion(json) > CurrentSchemaVersion)
            {
                throw new StoryBackupNewerException();
            }

            StoryBackupManifest manifest = JsonSerializer.Deserialize<StoryBackupManifest>(json, Options)
                ?? throw new StoryBackupException("The backup manifest is empty.");
            return manifest.SchemaVersion >= 1 ? manifest : throw new StoryBackupException("The backup manifest is not valid.");
        }
        catch (JsonException ex)
        {
            throw new StoryBackupException("The backup manifest is not valid.", ex);
        }
    }

    private static int PeekSchemaVersion(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("schemaVersion", out JsonElement v) && v.TryGetInt32(out int n) ? n : 0;
    }
}
