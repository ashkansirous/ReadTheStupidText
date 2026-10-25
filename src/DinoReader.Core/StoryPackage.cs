using System.Text.Json;

namespace DinoReader.Core;

/// <summary>Raised when a <c>.dino</c> package cannot be read or must not be imported (Slice 91, Decision 94).</summary>
public sealed class StoryPackageException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>One picture or cover inside a package: its plain file name and a way to open its bytes.</summary>
public sealed record PackageFile(string Name, Func<Stream> Open);

/// <summary>
/// The <c>.dino</c> story package (Slice 91): a zip holding <c>package.json</c> (this manifest), <c>story.json</c> and the
/// story's pictures, all at the top level under plain file names. <c>story.json</c> already uses plain names and 0..1
/// layout boxes, so a story packages as it is, with no path rewriting. <see cref="Version"/> is the content version of a bundled starter
/// (null when the package carries none): the app replaces an installed starter whose version is lower (<see cref="StarterUpdate"/>).
/// </summary>
public sealed record StoryPackageManifest(int SchemaVersion, string Id, string Title, string Language, int? Version = null)
{
    public const int CurrentSchemaVersion = 1;
    public const string ManifestFileName = "package.json";
    public const string StoryFileName = "story.json";
    public const string Extension = ".dino";

    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    /// <summary>The manifest's schema version read on its own, so a newer package whose other fields changed shape can still be
    /// recognised as newer; 0 when the text has no readable version.</summary>
    public static int PeekSchemaVersion(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("schemaVersion", out JsonElement v) && v.TryGetInt32(out int n) ? n : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    public static StoryPackageManifest Parse(string json)
    {
        try
        {
            StoryPackageManifest manifest = JsonSerializer.Deserialize<StoryPackageManifest>(json, Options)
                ?? throw new StoryPackageException("The package manifest is empty.");
            return manifest.SchemaVersion >= 1 && StoryIds.IsValid(manifest.Id)
                ? manifest
                : throw new StoryPackageException("The package manifest is not valid.");
        }
        catch (JsonException ex)
        {
            throw new StoryPackageException("The package manifest is not valid.", ex);
        }
    }
}
