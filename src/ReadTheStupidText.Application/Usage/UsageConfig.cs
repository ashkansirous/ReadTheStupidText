using System.Text.Json;

namespace ReadTheStupidText.Application.Usage;

/// <summary>The usage-count switch of the remote config: off unless the config says on with a rate from 0 to 1.</summary>
public sealed record TelemetrySettings(bool Enabled, double SampleRate)
{
    public static TelemetrySettings Off { get; } = new(false, 0);
}

/// <summary>
/// The part of <c>GET /api/app-config</c> the phone reads (Slice 137, Decision 145): the telemetry switch. Anything missing or
/// malformed means off, so a typo can never switch counting on. The Worker serves the same shape to Dino Reader.
/// </summary>
public sealed record UsageConfig(TelemetrySettings Telemetry)
{
    /// <summary>The newest <c>schemaVersion</c> this build understands.</summary>
    public const int SupportedSchemaVersion = 1;

    private const string SchemaVersionProperty = "schemaVersion";
    private const string TelemetryProperty = "telemetry";
    private const string EnabledProperty = "enabled";
    private const string SampleRateProperty = "sampleRate";

    public static UsageConfig Default { get; } = new(TelemetrySettings.Off);

    /// <summary>The checked config in <paramref name="json"/>; the default for a body that is not an object or not JSON.</summary>
    public static UsageConfig Parse(string? json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json ?? string.Empty);
            return document.RootElement.ValueKind == JsonValueKind.Object ? new UsageConfig(TelemetryOf(document.RootElement)) : Default;
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    /// <summary>
    /// True, with the checked config, only when <paramref name="json"/> is a JSON object with a numeric <c>schemaVersion</c> this build knows,
    /// as the Worker always sends it. A captive-portal page or a newer, unknown schema fails, so it can never replace a good copy.
    /// </summary>
    public static bool TryParseServed(string? json, out UsageConfig config)
    {
        config = Default;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json ?? string.Empty);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(SchemaVersionProperty, out JsonElement schema)
                || !schema.TryGetInt32(out int version)
                || version > SupportedSchemaVersion)
            {
                return false;
            }

            config = new UsageConfig(TelemetryOf(root));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static TelemetrySettings TelemetryOf(JsonElement root)
    {
        if (!root.TryGetProperty(TelemetryProperty, out JsonElement telemetry) || telemetry.ValueKind != JsonValueKind.Object
            || !telemetry.TryGetProperty(EnabledProperty, out JsonElement enabled) || enabled.ValueKind != JsonValueKind.True
            || !telemetry.TryGetProperty(SampleRateProperty, out JsonElement rate) || rate.ValueKind != JsonValueKind.Number
            || !rate.TryGetDouble(out double sample) || sample < 0 || sample > 1)
        {
            return TelemetrySettings.Off;
        }

        return new TelemetrySettings(true, sample);
    }
}
