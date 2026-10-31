using System.Globalization;
using System.Text.Json;

namespace DinoReader.Core;

/// <summary>The platforms the remote config keeps versions for (Slice 132).</summary>
public enum AppPlatform
{
    Android,
    Ios,
    Windows,
}

/// <summary>Compares dotted app versions ("1.42.0"); the comparison behind the forced update (Slice 132, Decision 139).</summary>
public static class AppVersion
{
    private const int MinParts = 2;
    private const int MaxParts = 4;

    /// <summary>The numbers of a dotted version of 2 to 4 non-negative integer parts, or null when the text is anything else.</summary>
    public static int[]? Parse(string? text)
    {
        string[] parts = (text ?? string.Empty).Trim().Split('.');
        if (parts.Length < MinParts || parts.Length > MaxParts)
        {
            return null;
        }

        var numbers = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
            {
                return null;
            }
        }

        return numbers;
    }

    /// <summary>Negative when <paramref name="a"/> is older than <paramref name="b"/>; a missing part counts as 0. Null when either does not parse.</summary>
    public static int? Compare(string? a, string? b)
    {
        int[]? left = Parse(a);
        int[]? right = Parse(b);
        if (left is null || right is null)
        {
            return null;
        }

        for (int i = 0; i < MaxParts; i++)
        {
            int byPart = PartAt(left, i).CompareTo(PartAt(right, i));
            if (byPart != 0)
            {
                return byPart;
            }
        }

        return 0;
    }

    private static int PartAt(int[] parts, int index) => index < parts.Length ? parts[index] : 0;
}

/// <summary>One platform's versions: below <see cref="MinVersion"/> the app must update, below <see cref="LatestVersion"/> it only says so quietly.</summary>
public sealed record PlatformVersions(string MinVersion, string LatestVersion)
{
    public static PlatformVersions None { get; } = new(AppConfig.NoVersion, AppConfig.NoVersion);
}

/// <summary>The usage-count switch (Slices 133-134): off unless the config says on with a rate from 0 to 1.</summary>
public sealed record TelemetryConfig(bool Enabled, double SampleRate)
{
    public static TelemetryConfig Off { get; } = new(false, 0);
}

/// <summary>
/// The remote config the Worker serves at <c>/api/app-config</c> (Slice 132, Decision 138), checked again on the phone. Anything
/// missing or malformed takes the safe default: no forced update, telemetry off. <see cref="Parse"/> never throws. Since Slice 227 it also carries
/// <c>crashReports.enabled</c> (<paramref name="CrashReportsEnabled"/>): crash reports go out only while it is a literal <c>true</c>.
/// </summary>
public sealed record AppConfig(PlatformVersions Android, PlatformVersions Ios, PlatformVersions Windows, TelemetryConfig Telemetry, bool CrashReportsEnabled = false)
{
    private const string CrashReportsProperty = "crashReports";
    public const string NoVersion = "0.0.0";

    /// <summary>The newest <c>schemaVersion</c> this build understands.</summary>
    public const int SupportedSchemaVersion = 1;

    private const string SchemaVersionProperty = "schemaVersion";
    private const string AndroidProperty = "android";
    private const string IosProperty = "ios";
    private const string WindowsProperty = "windows";
    private const string TelemetryProperty = "telemetry";
    private const string MinProperty = "minVersion";
    private const string LatestProperty = "latestVersion";
    private const string EnabledProperty = "enabled";
    private const string SampleRateProperty = "sampleRate";

    public static AppConfig Default { get; } = new(PlatformVersions.None, PlatformVersions.None, PlatformVersions.None, TelemetryConfig.Off);

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

    /// <summary>The checked config in <paramref name="json"/>; the default for a body that is not an object or not JSON, and the default for each part that fails its check.</summary>
    public static AppConfig Parse(string? json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json ?? string.Empty);
            JsonElement root = document.RootElement;
            return root.ValueKind != JsonValueKind.Object
                ? Default
                : new AppConfig(PlatformOf(root, AndroidProperty), PlatformOf(root, IosProperty), PlatformOf(root, WindowsProperty), TelemetryOf(root), CrashReportsOf(root));
        }
        catch (JsonException)
        {
            return Default;
        }
    }

    /// <summary>
    /// True, with the checked config, only when <paramref name="json"/> is a JSON object that carries a numeric <c>schemaVersion</c> as the
    /// Worker always sends it. A captive-portal page or any other body fails, so it can never replace a good copy; a schema newer than
    /// <see cref="SupportedSchemaVersion"/> is accepted only while it still carries a readable <c>minVersion</c>.
    /// </summary>
    public static bool TryParseServed(string? json, out AppConfig config)
    {
        config = Default;
        bool isKnownSchema;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json ?? string.Empty);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty(SchemaVersionProperty, out JsonElement schema) || schema.ValueKind != JsonValueKind.Number)
            {
                return false;
            }

            isKnownSchema = schema.TryGetInt32(out int version) && version <= SupportedSchemaVersion;
        }
        catch (JsonException)
        {
            return false;
        }

        config = Parse(json);
        if (!isKnownSchema && !HasReadableMinVersion(config))
        {
            config = Default;
            return false;
        }

        return true;
    }

    // A newer schema is trusted only as far as it still carries a minVersion this build can read; otherwise it could rename the
    // fields, read as "no forced update" and overwrite a good copy.
    private static bool HasReadableMinVersion(AppConfig config) =>
        config.Android.MinVersion != NoVersion || config.Ios.MinVersion != NoVersion || config.Windows.MinVersion != NoVersion;

    private static PlatformVersions PlatformOf(JsonElement root, string name) =>
        Child(root, name, out JsonElement platform)
            ? new PlatformVersions(VersionOf(platform, MinProperty), VersionOf(platform, LatestProperty))
            : PlatformVersions.None;

    private static string VersionOf(JsonElement platform, string name) =>
        platform.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String && AppVersion.Parse(value.GetString()) is not null
            ? value.GetString()!.Trim()
            : NoVersion;

    private static TelemetryConfig TelemetryOf(JsonElement root)
    {
        if (!Child(root, TelemetryProperty, out JsonElement telemetry)
            || !telemetry.TryGetProperty(EnabledProperty, out JsonElement enabled) || enabled.ValueKind != JsonValueKind.True
            || !telemetry.TryGetProperty(SampleRateProperty, out JsonElement rate) || rate.ValueKind != JsonValueKind.Number
            || !rate.TryGetDouble(out double sample) || sample < 0 || sample > 1)
        {
            return TelemetryConfig.Off;
        }

        return new TelemetryConfig(true, sample);
    }

    private static bool CrashReportsOf(JsonElement root) =>
        Child(root, CrashReportsProperty, out JsonElement crash)
        && crash.TryGetProperty(EnabledProperty, out JsonElement enabled) && enabled.ValueKind == JsonValueKind.True;

    private static bool Child(JsonElement parent, string name, out JsonElement child) =>
        parent.TryGetProperty(name, out child) && child.ValueKind == JsonValueKind.Object;
}
