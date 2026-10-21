namespace DinoReader.Core;

/// <summary>Story ids are GUIDs in "N" form; anything else is rejected before it can become a folder name.</summary>
public static class StoryIds
{
    private const string Format = "N";

    public static string New() => Guid.NewGuid().ToString(Format);

    public static bool IsValid(string? id) => Guid.TryParseExact(id, Format, out _);
}
