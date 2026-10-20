using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Parses <c>packs.json</c> (an array of <see cref="PackInfo"/>-shaped objects).
/// Entries with an unknown <c>kind</c> are skipped so a newer catalog still works
/// with an older app, as are entries whose id or version fail <see cref="PackNaming"/>; a structurally broken entry throws <see cref="FormatException"/>.
/// </summary>
public static class PackCatalogParser
{
    public static IReadOnlyList<PackInfo> Parse(string json)
    {
        PackDto[] dtos = Deserialize(json);
        var packs = new List<PackInfo>();
        foreach (PackDto dto in dtos)
        {
            if (TryConvert(dto, out PackInfo? pack))
            {
                packs.Add(pack);
            }
        }

        return packs;
    }

    private static PackDto[] Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<PackDto[]>(json) ?? throw new FormatException("Empty pack catalog.");
        }
        catch (JsonException ex)
        {
            throw new FormatException("Malformed pack catalog.", ex);
        }
    }

    private static bool TryConvert(PackDto dto, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PackInfo? pack)
    {
        pack = null;
        if (!Enum.TryParse(dto.Kind, ignoreCase: true, out PackKind kind) || !Enum.IsDefined(kind))
        {
            return false;
        }

        if (dto.Id is null || dto.Language is null || dto.Version is null || dto.Sha256 is null || dto.Url is null || dto.SizeBytes is not > 0)
        {
            throw new FormatException($"Pack '{dto.Id}' is missing required fields.");
        }

        if (!PackNaming.IsValidId(dto.Id) || !PackNaming.IsValidVersion(dto.Version))
        {
            return false; // would become a file/folder name: skip rather than trust it
        }

        pack = new PackInfo(dto.Id, SplitLanguages(dto.Language), kind, dto.Version, dto.SizeBytes.Value, dto.Sha256, dto.License ?? string.Empty, dto.Url);
        return true;
    }

    internal static IReadOnlyList<string> SplitLanguages(string languages) =>
        languages.Split(PackConstants.LanguageSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private sealed record PackDto(
        [property: JsonPropertyName(PackJsonKeys.Id)] string? Id,
        [property: JsonPropertyName(PackJsonKeys.Language)] string? Language,
        [property: JsonPropertyName(PackJsonKeys.Kind)] string? Kind,
        [property: JsonPropertyName(PackJsonKeys.Version)] string? Version,
        [property: JsonPropertyName(PackJsonKeys.SizeBytes)] long? SizeBytes,
        [property: JsonPropertyName(PackJsonKeys.Sha256)] string? Sha256,
        [property: JsonPropertyName(PackJsonKeys.License)] string? License,
        [property: JsonPropertyName(PackJsonKeys.Url)] string? Url);
}
