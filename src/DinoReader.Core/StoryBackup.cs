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
public sealed record StoryRestoreResult(int Added, int Skipped, int Damaged, int TooLarge = 0, int Merged = 0);

/// <summary>
/// The backup file (Slice 93, Decision 96): a zip holding <c>backup.json</c> (this manifest) and one <c>&lt;story id&gt;.dino</c>
/// package per story, all at the top level. Its own <see cref="SchemaVersion"/> is separate from the packages'.
/// </summary>
public sealed record StoryBackupManifest(int SchemaVersion, int Count, DateTimeOffset Created)
{
    public const int CurrentSchemaVersion = 1;
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
