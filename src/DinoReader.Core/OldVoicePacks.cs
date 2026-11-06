using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Usage;

namespace DinoReader.Core;

/// <summary>What one pass of the old-voice-pack cleanup did: the entries it deleted and how many it could not delete.</summary>
public sealed record OldVoicePacksResult(IReadOnlyList<string> Deleted, int Failed);

/// <summary>
/// Slice 278 part B review: Dino Reader no longer uses a phone voice, so the voice and storyteller packs (and their half-finished downloads) an older version left in the packs folder are
/// deleted once. Only entries named for a known voice pack id are touched: the pack folder, its scratch (<c>.extracting</c>, <c>.removing</c>) folders, and the <c>&lt;id&gt;-&lt;version&gt;.zip</c> and
/// <c>.zip.partial</c> files. Anything else in the packs folder is left alone, and the audio packs and the books live in other folders altogether, which this never looks at.
/// </summary>
public static class OldVoicePacks
{
    /// <summary>Every voice or storyteller pack id the catalogue has ever published for the apps; nothing else is deleted.</summary>
    public static readonly IReadOnlyList<string> KnownPackIds =
    [
        PackConstants.SupertonicPackId,
        PackConstants.PersianPackId,
        PackConstants.ChinesePackId,
        PackConstants.NorwegianPackId,
        PackConstants.StorytellerPackId,
    ];

    private const string ExtractingSuffix = ".extracting";
    private const string RemovingSuffix = ".removing";

    /// <summary>True when <paramref name="entryName"/> (a name in the packs folder) belongs to a known voice pack: its folder, a scratch folder, or a zip or partial of one of its versions.</summary>
    public static bool IsOldVoicePackEntry(string entryName)
    {
        foreach (string id in KnownPackIds)
        {
            if (entryName == id || entryName == id + ExtractingSuffix || entryName == id + RemovingSuffix || PackNaming.IsOrphanFileName(entryName, id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Deletes the old voice pack entries directly under <paramref name="packsRoot"/> (a missing folder is nothing to do). A delete that fails is reported to <paramref name="sink"/> (area pack) and counted,
    /// and the other entries still go; the caller retries a pass with failures at the next start.
    /// </summary>
    public static OldVoicePacksResult Delete(string packsRoot, IFallbackSink sink)
    {
        var deleted = new List<string>();
        int failed = 0;
        if (!Directory.Exists(packsRoot))
        {
            return new OldVoicePacksResult(deleted, failed);
        }

        foreach (string path in Directory.EnumerateFileSystemEntries(packsRoot).Where(p => IsOldVoicePackEntry(Path.GetFileName(p))))
        {
            if (TryDelete(path, sink))
            {
                deleted.Add(Path.GetFileName(path));
            }
            else
            {
                failed++;
            }
        }

        return new OldVoicePacksResult(deleted, failed);
    }

    private static bool TryDelete(string path, IFallbackSink sink)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else
            {
                File.Delete(path);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            sink.Report(FallbackArea.Pack, FallbackReason.PackSyncFailed); // the entry stays and the next start tries again
            System.Diagnostics.Debug.WriteLine($"[OldVoicePacks] could not delete {Path.GetFileName(path)}: {ex.Message}");
            return false;
        }
    }
}
