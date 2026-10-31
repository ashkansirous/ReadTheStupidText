using System.Text.Json;
using ReadTheStupidText.Application.AppConfig;

namespace ReadTheStupidText.Application.Usage;

/// <summary>The usage-count switch of the remote config: off unless the config says on with a rate from 0 to 1.</summary>
public sealed record TelemetrySettings(bool Enabled, double SampleRate)
{
    public static TelemetrySettings Off { get; } = new(false, 0);
}

/// <summary>
/// The part of <c>GET /api/app-config</c> the apps read (Slice 137, Decision 145): the telemetry switch and, since Slice 217, each platform's
/// <c>minVersion</c> / <c>latestVersion</c> for the forced update. Anything missing or malformed means off and no update, so a typo can never
/// switch counting on or lock anybody out. The Worker serves the same shape to Dino Reader. Since Slice 227 it also carries <c>crashReports.enabled</c>
/// (<paramref name="CrashReportsEnabled"/>): crash reports go out only while it is a literal <c>true</c>; missing, malformed or false means off.
/// </summary>
public sealed record UsageConfig(TelemetrySettings Telemetry, PlatformVersions Android, PlatformVersions Ios, PlatformVersions Windows, bool CrashReportsEnabled = false)
{
    private const string CrashReportsProperty = "crashReports";

    private const string AndroidProperty = "android";
    private const string IosProperty = "ios";
    private const string WindowsProperty = "windows";
    private const string MinProperty = "minVersion";
    private const string LatestProperty = "latestVersion";

    /// <summary>The newest <c>schemaVersion</c> this build understands.</summary>
    public const int SupportedSchemaVersion = 1;

    private const string SchemaVersionProperty = "schemaVersion";
    private const string TelemetryProperty = "telemetry";
    private const string EnabledProperty = "enabled";
    private const string SampleRateProperty = "sampleRate";

    /// <summary>A config with only the telemetry switch and no update asked for.</summary>
    public UsageConfig(TelemetrySettings telemetry)
        : this(telemetry, PlatformVersions.None, PlatformVersions.None, PlatformVersions.None)
    {
    }

    public static UsageConfig Default { get; } = new(TelemetrySettings.Off);

    public PlatformVersions For(AppPlatform platform) => platform switch
    {
        AppPlatform.Android => Android,
        AppPlatform.Ios => Ios,
        _ => Windows,
    };

    /// <summary>True when <paramref name="installed"/> is below this platform's <c>minVersion</c>; an unreadable version never forces an update.</summary>
    public bool RequiresUpdate(AppPlatform platform, string installed) => AppVersion.Compare(installed, For(platform).MinVersion) < 0;

    /// <summary>True when <paramref name="installed"/> is below this platform's <c>latestVersion</c>.</summary>
    public bool HasNewerVersion(AppPlatform platform, string installed) => AppVersion.Compare(installed, For(platform).LatestVersion) < 0;

    /// <summary>The checked config in <paramref name="json"/>; the default for a body that is not an object or not JSON.</summary>
    public static UsageConfig Parse(string? json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json ?? string.Empty);
            return document.RootElement.ValueKind == JsonValueKind.Object ? From(document.RootElement) : Default;
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

            config = From(root);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static UsageConfig From(JsonElement root) =>
        new(TelemetryOf(root), PlatformOf(root, AndroidProperty), PlatformOf(root, IosProperty), PlatformOf(root, WindowsProperty), CrashReportsOf(root));

    private static bool CrashReportsOf(JsonElement root) =>
        root.TryGetProperty(CrashReportsProperty, out JsonElement crash) && crash.ValueKind == JsonValueKind.Object
        && crash.TryGetProperty(EnabledProperty, out JsonElement enabled) && enabled.ValueKind == JsonValueKind.True;

    private static PlatformVersions PlatformOf(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement platform) && platform.ValueKind == JsonValueKind.Object
            ? new PlatformVersions(VersionOf(platform, MinProperty), VersionOf(platform, LatestProperty))
            : PlatformVersions.None;

    private static string VersionOf(JsonElement platform, string name) =>
        platform.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && AppVersion.Parse(value.GetString()) is not null
            ? value.GetString()!.Trim()
            : PlatformVersions.NoVersion;

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
