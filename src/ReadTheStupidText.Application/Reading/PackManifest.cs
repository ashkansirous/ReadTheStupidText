using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReadTheStupidText.Application.Reading;

/// <summary>The <c>pack.json</c> inside an extracted pack (id, kind, language, version, engine, file list).</summary>
public sealed record PackManifest(string Id, PackKind Kind, string Language, string Version, string Engine, IReadOnlyList<string> Files)
{
    /// <summary>The files the manifest lists that <paramref name="exists"/> does not find.</summary>
    public IReadOnlyList<string> MissingFiles(Func<string, bool> exists) =>
        Files.Where(f => !exists(f)).ToList();

    public static PackManifest Parse(string json)
    {
        ManifestDto dto = Deserialize(json);
        if (dto.Id is null || dto.Language is null || dto.Version is null || dto.Engine is null || dto.Files is null
            || !Enum.TryParse(dto.Kind, ignoreCase: true, out PackKind kind) || !Enum.IsDefined(kind))
        {
            throw new FormatException("pack.json is missing required fields.");
        }

        return new PackManifest(dto.Id, kind, dto.Language, dto.Version, dto.Engine, dto.Files);
    }

    private static ManifestDto Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ManifestDto>(json) ?? throw new FormatException("Empty pack.json.");
        }
        catch (JsonException ex)
        {
            throw new FormatException("Malformed pack.json.", ex);
        }
    }

    private sealed record ManifestDto(
        [property: JsonPropertyName(PackJsonKeys.Id)] string? Id,
        [property: JsonPropertyName(PackJsonKeys.Kind)] string? Kind,
        [property: JsonPropertyName(PackJsonKeys.Language)] string? Language,
        [property: JsonPropertyName(PackJsonKeys.Version)] string? Version,
        [property: JsonPropertyName(PackJsonKeys.Engine)] string? Engine,
        [property: JsonPropertyName(PackJsonKeys.Files)] string[]? Files);
}
