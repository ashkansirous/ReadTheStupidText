using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReadTheStupidText.Application.Usage;

/// <summary>The request body of <c>POST /api/events</c> (the same envelope as Dino Reader's) and how the phone keeps its queue.</summary>
public static class UsageBatch
{
    /// <summary>The Worker refuses more than this in one request.</summary>
    public const int MaxEvents = 50;

    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The JSON body for <paramref name="events"/> (at most <see cref="MaxEvents"/>; send longer lists in slices) with the optional <paramref name="device"/> object (Slice 166), left out when null.</summary>
    public static string ToJson(IReadOnlyList<UsageEvent> events, DeviceContext? device = null) => JsonSerializer.Serialize(new { schemaVersion = SchemaVersion, device, events }, Options);

    /// <summary>The events in a stored queue, or none when <paramref name="json"/> is missing or damaged.</summary>
    public static List<UsageEvent> ParseQueue(string? json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<UsageEvent>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string QueueToJson(IReadOnlyList<UsageEvent> events) => JsonSerializer.Serialize(events, Options);
}
