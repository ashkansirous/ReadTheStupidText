using System.Globalization;
using System.Text.Json;

namespace DinoReader.Core;

/// <summary>The few things Dino counts (Slice 133, Decision 140). The Worker accepts exactly these names.</summary>
public static class UsageEventName
{
    public const string BookOpened = "book_opened";
    public const string BookFinished = "book_finished";
}

/// <summary>Where a counted book came from: Dino's own shelf (a starter or a store story) or a grown-up's own book.</summary>
public static class UsageSource
{
    public const string Shelf = "shelf";
    public const string Own = "own";
}

/// <summary>
/// One anonymous count (Slice 133, Decision 140): a name plus context, and nothing that identifies a person or a device. The book is the
/// shelf book's package id (32 lower-case hex digits) or <see cref="UsageSource.Own"/> for a grown-up's own book, never a title; the day is
/// the UTC date, no finer. <see cref="SampleRate"/> is the share of launches counted when this was queued, so the Worker can weight each count by 1 / rate.
/// </summary>
public sealed record UsageEvent(string Name, string Book, string Language, string Source, bool HasRecording, string Version, string Platform, string Day, double SampleRate)
{
    private const int PackageIdLength = 32;
    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>
    /// The count for <paramref name="name"/> about <paramref name="story"/>. Only a book that came from the starters or the store
    /// (<paramref name="fromCatalogue"/>) with a usable package id is a shelf book; any other book, a re-imported own one included, counts as the grown-up's own.
    /// </summary>
    public static UsageEvent For(string name, Story story, bool fromCatalogue, double sampleRate, string version, AppPlatform platform, DateTimeOffset now)
    {
        string? shelfId = fromCatalogue ? ShelfId(story.SourceId) : null;
        return new UsageEvent(
            name,
            shelfId ?? UsageSource.Own,
            story.Language,
            shelfId is null ? UsageSource.Own : UsageSource.Shelf,
            StoryRecordings.Count(story) > 0,
            version,
            platform.ToString().ToLowerInvariant(),
            now.UtcDateTime.ToString(DayFormat, CultureInfo.InvariantCulture),
            sampleRate);
    }

    /// <summary>The package id in its stored form (hex digits, no dashes, lower case), or null when <paramref name="sourceId"/> is missing or not an id.</summary>
    public static string? ShelfId(string? sourceId)
    {
        string compact = (sourceId ?? string.Empty).Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return compact.Length == PackageIdLength && compact.All(Uri.IsHexDigit) ? compact : null;
    }
}

/// <summary>The request body of <c>POST /api/events</c> and the rule for who is counted at all.</summary>
public static class UsageBatch
{
    /// <summary>The Worker refuses more than this in one request.</summary>
    public const int MaxEvents = 50;

    public const int SchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>The JSON body for <paramref name="events"/> (at most <see cref="MaxEvents"/>; send longer lists in slices).</summary>
    public static string ToJson(IReadOnlyList<UsageEvent> events) => JsonSerializer.Serialize(new { schemaVersion = SchemaVersion, events }, Options);

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

    /// <summary>
    /// True when a session with this <paramref name="roll"/> (a random number from 0 up to but not including 1) is counted at the config's
    /// <paramref name="telemetry"/>: the switch is on and the roll is under the sample rate. No id is involved, so the roll is made once per app launch.
    /// </summary>
    public static bool Samples(TelemetryConfig telemetry, double roll) => telemetry.Enabled && roll < telemetry.SampleRate;
}
