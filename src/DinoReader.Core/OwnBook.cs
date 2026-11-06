using System.Text.Json;

namespace DinoReader.Core;

/// <summary>The folders of the books a family added themselves, and whether the listing saw every folder (Slice 278 A3, design 2).</summary>
public sealed record OwnFolders(IReadOnlyList<string> Ids, bool Complete);

/// <summary>
/// Slice 278 A3, design 2: the one place that decides what an "own book" is (the app holds store books only now). Own means story.json has no
/// source id at all (the field is absent or JSON null); wizard books were saved that way, every store, starter and package book carries its id.
/// Decided on the raw text, never on the parsed <see cref="Story.SourceId"/>, which turns a damaged id into null too.
/// </summary>
public static class OwnBook
{
    private const string SourceIdProperty = "sourceId";

    /// <summary>True only when the field is absent or null. Any present value (empty, a number, a malformed id) means a package book whose id was damaged: it is kept. Text that is not a JSON object is also kept.</summary>
    public static bool IsOwn(string rawStoryJson)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(rawStoryJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (JsonProperty property in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, SourceIdProperty, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind != JsonValueKind.Null)
                {
                    return false;
                }
            }

            return true;
        }
        catch (JsonException)
        {
            // no-fallback: a file that cannot be read as JSON is not positively an own book, so it is kept
            return false;
        }
    }
}
