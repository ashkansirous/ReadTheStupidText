using Windows.Storage;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Resolves and owns the on-disk audio-chunk folder under the package's
/// TemporaryFolder (Decision 49), a sibling of the diagnostic <c>logs\</c> folder
/// (<see cref="Logging.LogPaths"/>). Each read's synthesized chunks land at
/// <c>audio\&lt;activity-id&gt;\chunk-&lt;index&gt;.wav</c>, so <c>MediaPlayer</c>
/// streams them from disk instead of the whole read living in memory.
/// </summary>
public sealed class AudioChunkPaths
{
    private const string AudioSubfolder = "audio";
    private const string ChunkFileTemplate = "chunk-{0}.wav";
    private const string SentenceSubfolder = "sentences";

    public AudioChunkPaths()
    {
        Root = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, AudioSubfolder);
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    /// <summary>
    /// The persistent sentence-audio cache (Slice 56): under the package's LocalCacheFolder, which is
    /// not backed up and not swept by <see cref="PurgeOrphaned"/>. A read's play file is joined from
    /// these clips and lives under <see cref="Root"/>; a cached clip is never played or deleted by a read.
    /// </summary>
    public string SentenceFolder => Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, SentenceSubfolder);

    /// <summary>The on-disk path for one chunk of a read, creating the read's
    /// folder on first use.</summary>
    public string ChunkPath(int activityId, int index)
    {
        string folder = ReadFolder(activityId);
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, string.Format(ChunkFileTemplate, index));
    }

    /// <summary>Deletes a read's chunk folder once it reaches a terminal state or
    /// is superseded (Decision 49). Best-effort — a file the player still has open
    /// is skipped, not thrown.</summary>
    public void DeleteRead(int activityId) => TryDeleteDirectory(ReadFolder(activityId));

    /// <summary>Startup sweep (Decision 49): removes any read folder orphaned by a
    /// read that never reached a terminal state.</summary>
    public void PurgeOrphaned(TimeSpan maxAge)
    {
        DateTime now = DateTime.Now;
        foreach (string folder in Directory.EnumerateDirectories(Root))
        {
            if (AudioChunkSweep.IsEligible(Directory.GetLastWriteTime(folder), now, maxAge))
            {
                TryDeleteDirectory(folder);
            }
        }
    }

    private string ReadFolder(int activityId) => Path.Combine(Root, activityId.ToString());

    private static void TryDeleteDirectory(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
