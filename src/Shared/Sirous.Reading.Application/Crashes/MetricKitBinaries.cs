using System.Text.Json;

namespace Sirous.Reading.Application.Crashes;

/// <summary>
/// Reads the app executable's UUID out of a MetricKit call-stack tree (<c>MXCallStackTree.JsonRepresentation</c>, Slice 243 finding 3): every frame lists its <c>binaryName</c> and
/// <c>binaryUUID</c>, so the crashed launch's own binary is named by the payload and not guessed from the installed one. Pure and defensive: anything else gives null.
/// </summary>
public static class MetricKitBinaries
{
    private const string BinaryNameKey = "binaryName";
    private const string BinaryUuidKey = "binaryUUID";
    private const int MaxDepth = 256;
    private const int UuidDigits = 32;

    /// <summary>The first <c>binaryUUID</c> of a frame whose <c>binaryName</c> is <paramref name="binaryName"/>, as 32 uppercase hex digits with no dashes; null when there is none or the JSON is not a call-stack tree.</summary>
    public static string? UuidOf(string? json, string? binaryName)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrEmpty(binaryName))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return Find(document.RootElement, binaryName, 0);
        }
        catch (JsonException)
        {
            // no-fallback: an unreadable call-stack tree means the report has no UUID, never another binary's
            return null;
        }
    }

    private static string? Find(JsonElement node, string binaryName, int depth)
    {
        if (depth > MaxDepth)
        {
            return null;
        }

        return node.ValueKind switch
        {
            JsonValueKind.Array => node.EnumerateArray().Select(item => Find(item, binaryName, depth + 1)).FirstOrDefault(found => found is not null),
            JsonValueKind.Object => FindInObject(node, binaryName, depth),
            _ => null,
        };
    }

    private static string? FindInObject(JsonElement node, string binaryName, int depth)
    {
        if (node.TryGetProperty(BinaryNameKey, out JsonElement name) && name.ValueKind == JsonValueKind.String && name.GetString() == binaryName
            && node.TryGetProperty(BinaryUuidKey, out JsonElement uuid) && Normalize(uuid) is { } own)
        {
            return own;
        }

        return node.EnumerateObject().Select(property => Find(property.Value, binaryName, depth + 1)).FirstOrDefault(found => found is not null);
    }

    private static string? Normalize(JsonElement uuid)
    {
        string digits = uuid.ValueKind == JsonValueKind.String ? (uuid.GetString() ?? string.Empty).Replace("-", string.Empty).ToUpperInvariant() : string.Empty;
        return digits.Length == UuidDigits && digits.All(Uri.IsHexDigit) ? digits : null;
    }
}
