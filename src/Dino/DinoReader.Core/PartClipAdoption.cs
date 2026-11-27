using System.Globalization;
using System.Text.RegularExpressions;

namespace DinoReader.Core;

/// <summary>
/// Slice 395: part takes live in their own list of the story file (<c>PartRecordings</c>), which an older app ignores and drops when it saves the book. The take's audio file stays in the book's folder under its
/// part file name (<c>rec-&lt;lang&gt;-&lt;page&gt;-p&lt;first&gt;-&lt;last&gt;[-&lt;8 hex&gt;].m4a</c>), so on load a part-named file that no clip of the story names is put back as a part clip: an older app's re-save never orphans a take.
/// </summary>
public static class PartClipAdoption
{
    /// <summary>The search pattern of the files <see cref="Adopt"/> looks at, for a folder listing.</summary>
    public const string FilePattern = StoryRecordings.FilePrefix + "-*-p*" + StoryRecordings.FileExtension;

    /// <summary>Slice 443 review: the suffix of the empty marker file the library leaves beside a recording whose delete failed; a part file with this marker is never adopted.</summary>
    public const string DeletedSuffix = ".deleted";

    private static readonly Regex PartFileName = new(@"^rec-(?<lang>[a-z]{2,3})-(?<page>[1-9][0-9]{0,3})-p(?<first>[1-9][0-9]{0,3})-(?<last>[1-9][0-9]{0,3})(-[0-9a-f]{8})?\.m4a$", RegexOptions.CultureInvariant);

    /// <summary>Slice 443 review pass 2: whether <paramref name="fileName"/> is a part take's file name, the only kind of file a <see cref="DeletedSuffix"/> marker is written for.</summary>
    public static bool IsPartFileName(string fileName) => PartFileName.IsMatch(fileName);

    /// <summary><paramref name="story"/> with a part clip for each of <paramref name="fileNames"/> that is a part file name, names a page of the story, is named by no clip, and whose range no part clip of the story already
    /// covers or shares a part with (the clips the story has are the newer takes). The clip's length is unknown (0). Several such files that overlap keep the first by name.</summary>
    public static Story Adopt(Story story, IEnumerable<string> fileNames)
    {
        Story adopted = story;
        HashSet<string> named = StoryRecordings.Files(story).ToHashSet(StringComparer.Ordinal); // Slice 443: the files the story names, worked out once (the switch is on, so this runs on every read of a book that has part files)
        foreach (string name in fileNames.Order(StringComparer.Ordinal))
        {
            if (KeyOf(name, story.Pages.Count) is { } key && !named.Contains(name) && StoryRecordings.Clips(adopted).All(c => !c.Key.Overlaps(key)))
            {
                adopted = StoryRecordings.WithClip(adopted, key, new PageRecording(key.Language, 0, name)) with { UseRecordings = adopted.UseRecordings }; // finding a file never turns the book's switch on
                named.Add(name);
            }
        }

        return adopted;
    }

    // The key a part file name stands for, or null when it is no part file, names a page the story lacks, or the name is not the one that key makes.
    private static StoryClipKey? KeyOf(string name, int pageCount)
    {
        Match match = PartFileName.Match(name);
        if (!match.Success)
        {
            return null;
        }

        StoryClipKey key = StoryClipKey.OfParts(Number(match, "page") - 1, Number(match, "first") - 1, Number(match, "last") - 1, match.Groups["lang"].Value).In(match.Groups["lang"].Value);
        bool ownName = key.IsValid && key.Page < pageCount && (name == StoryRecordings.FileNameOf(key) || StoryRecordings.IsFreshFileNameOf(name, key));
        return ownName ? key : null;
    }

    private static int Number(Match match, string group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
}
