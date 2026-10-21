using System.Text.Json;
using System.Text.Json.Serialization;

namespace DinoReader.Core;

/// <summary>Raised when a story file is not valid story JSON or comes from a newer schema.</summary>
public sealed class StoryFormatException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// The versioned <c>story.json</c> format. Reading ignores fields it does not know and fills
/// missing optional ones with defaults, so a later app version can add fields without breaking
/// this one; a file with a newer <see cref="CurrentVersion"/> is refused rather than half-read.
/// </summary>
public static class StoryJson
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(Story story) => JsonSerializer.Serialize(ToFile(story), Options);

    public static Story Deserialize(string json)
    {
        try
        {
            StoryFile file = JsonSerializer.Deserialize<StoryFile>(json, Options)
                ?? throw new StoryFormatException("The story file is empty.");
            return FromFile(file);
        }
        catch (JsonException ex)
        {
            throw new StoryFormatException("The story file is not valid.", ex);
        }
    }

    private static StoryFile ToFile(Story s) => new()
    {
        Version = CurrentVersion,
        Id = s.Id,
        Title = s.Title,
        Language = s.Language,
        Created = s.Created,
        CoverStyle = s.CoverStyle,
        CoverColourIndex = s.CoverColourIndex,
        NarratorVoiceId = s.NarratorVoiceId,
        Pages = s.Pages.Select(p => new PageFile { Text = p.Text, ImageFile = p.ImageFile }).ToList(),
        Cast = s.Cast.Select(c => new CastFile
        {
            CharacterId = c.CharacterId, Name = c.Name, Gender = c.Gender, VoiceId = c.VoiceId,
        }).ToList(),
    };

    private static Story FromFile(StoryFile f)
    {
        if (f.Version > CurrentVersion || !StoryIds.IsValid(f.Id))
        {
            throw new StoryFormatException("The story file is from a newer version or has no valid id.");
        }

        RequireFields(f);
        return new Story(
            f.Id, f.Title, f.Language, f.Created,
            f.Pages.Select(p => new StoryPage(p.Text, p.ImageFile)).ToList(),
            f.Cast.Select(c => new CastMember(c.CharacterId, c.Name, c.Gender, c.VoiceId)).ToList(),
            f.CoverStyle, f.CoverColourIndex, f.NarratorVoiceId);
    }

    // JSON null overwrites the property defaults, so a hand-edited or corrupt file can still
    // carry nulls; refuse it as a format error rather than failing later with a NullReference.
    private static void RequireFields(StoryFile f)
    {
        bool complete = f.Title is not null && f.Language is not null && f.NarratorVoiceId is not null
            && f.Pages is not null && f.Pages.All(p => p is { Text: not null })
            && f.Cast is not null
            && f.Cast.All(c => c is { CharacterId: not null, Name: not null, VoiceId: not null });
        if (!complete)
        {
            throw new StoryFormatException("The story file is missing required fields.");
        }
    }

    private sealed class StoryFile
    {
        public int Version { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Language { get; set; } = "en";
        public DateTimeOffset Created { get; set; }
        public CoverStyle CoverStyle { get; set; } = CoverStyle.Colour;
        public int CoverColourIndex { get; set; }
        public string NarratorVoiceId { get; set; } = string.Empty;
        public List<PageFile> Pages { get; set; } = [];
        public List<CastFile> Cast { get; set; } = [];
    }

    private sealed class PageFile
    {
        public string Text { get; set; } = string.Empty;
        public string? ImageFile { get; set; }
    }

    private sealed class CastFile
    {
        public string CharacterId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public Gender Gender { get; set; }
        public string VoiceId { get; set; } = string.Empty;
    }
}
