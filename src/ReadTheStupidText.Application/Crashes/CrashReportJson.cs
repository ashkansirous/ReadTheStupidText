using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Application.Crashes;

/// <summary>
/// The JSON of a <see cref="CrashReport"/> (Slice 227): the request body of <c>POST /api/crash</c> and the pending file on the phone. The writer applies
/// every cap, so what leaves the phone is always within the Worker's limits; the parser never throws.
/// </summary>
public static class CrashReportJson
{
    public const int SchemaVersion = 1;

    private const int MinLogTailBytes = 256;

    // Relaxed escaping keeps non-ASCII text as UTF-8 (the default would turn each character into a 6-byte \uXXXX escape and blow the body cap).
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal sealed record Wire(
        int SchemaVersion, string App, string Platform, string Version, string Channel, string Kind,
        string ExceptionType, string Message, string Stack, string LogTail, string Minute, DeviceContext? Device);

    /// <summary>The report with every text field cut to its cap.</summary>
    public static CrashReport Capped(CrashReport report) => report with
    {
        ExceptionType = TextCap.Head(report.ExceptionType, CrashReportLimits.ExceptionTypeBytes),
        Message = TextCap.Head(report.Message, CrashReportLimits.MessageBytes),
        Stack = TextCap.Head(report.Stack, CrashReportLimits.StackBytes),
        LogTail = TextCap.Tail(report.LogTail, CrashReportLimits.LogTailBytes),
    };

    /// <summary>
    /// The request body for <paramref name="report"/>: capped, then shortened until the JSON-escaped body fits <see cref="CrashReportLimits.BodyBytes"/>. Escaping can
    /// make a text several times longer (a control character is 6 bytes, a quote 2), so the check is on the final body: the log tail is halved first (oldest lines
    /// go first), then the stack, then the message.
    /// </summary>
    public static string ToJson(CrashReport report)
    {
        CrashReport capped = Capped(report);
        string json = Serialize(capped);
        while (Encoding.UTF8.GetByteCount(json) > CrashReportLimits.BodyBytes && HasShrinkableText(capped))
        {
            capped = ShrinkLargest(capped);
            json = Serialize(capped);
        }

        return json;
    }

    private static bool HasShrinkableText(CrashReport r) => r.LogTail.Length > 0 || r.Stack.Length > 0 || r.Message.Length > 0;

    private static CrashReport ShrinkLargest(CrashReport r)
    {
        if (r.LogTail.Length > 0)
        {
            return r with { LogTail = Halve(r.LogTail, keepEnd: true) };
        }

        return r.Stack.Length > 0 ? r with { Stack = Halve(r.Stack, keepEnd: false) } : r with { Message = Halve(r.Message, keepEnd: false) };
    }

    // Half of the text by characters (a text under the minimum goes entirely), cut on a whole character.
    private static string Halve(string text, bool keepEnd)
    {
        int keep = text.Length / 2;
        if (keep < MinLogTailBytes)
        {
            return string.Empty;
        }

        return keepEnd ? TextCap.Tail(text, keep) : TextCap.Head(text, keep);
    }

    private static string Serialize(CrashReport r) => JsonSerializer.Serialize(
        new Wire(SchemaVersion, r.App, r.Platform, r.Version, r.Channel, r.Kind, r.ExceptionType, r.Message, r.Stack, r.LogTail, r.Minute, r.Device), Options);

    /// <summary>The report in <paramref name="json"/>, or null when it is missing, damaged or not this schema.</summary>
    public static CrashReport? Parse(string? json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            Wire? wire = JsonSerializer.Deserialize<Wire>(json, Options);
            if (wire is null || wire.SchemaVersion != SchemaVersion || wire.App is null || wire.Platform is null || wire.Version is null || wire.Channel is null || wire.Kind is null)
            {
                return null;
            }

            return new CrashReport(
                wire.App, wire.Platform, wire.Version, wire.Channel, wire.Kind,
                wire.ExceptionType ?? string.Empty, wire.Message ?? string.Empty, wire.Stack ?? string.Empty, wire.LogTail ?? string.Empty, wire.Minute ?? string.Empty, wire.Device);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The report time to the minute, UTC, as the report carries it.</summary>
    public static string MinuteOf(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
