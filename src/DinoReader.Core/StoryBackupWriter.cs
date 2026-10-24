using System.IO.Compression;
using System.Text;

namespace DinoReader.Core;

/// <summary>Writes every story of a library into one backup zip (Slice 93): a manifest and one <c>.dino</c> package per story.</summary>
public static class StoryBackupWriter
{
    private const int CopyBufferBytes = 81920;

    /// <summary>
    /// Writes <paramref name="stories"/> (with their pictures, read from <paramref name="library"/>) to <paramref name="output"/> and returns
    /// how many could not be written (a story whose files cannot be read is left out whole, never half-written into the zip).
    /// The manifest's count is the number actually written.
    /// </summary>
    public static async Task<int> WriteAsync(IReadOnlyList<Story> stories, IStoryLibrary library, Stream output)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        int written = 0;
        foreach (Story story in stories)
        {
            written += await TryAddPackageAsync(zip, story, library) ? 1 : 0;
        }

        if (written == 0 && stories.Count > 0)
        {
            throw new StoryBackupException("No story could be written to the backup.");
        }

        await AddManifestAsync(zip, new StoryBackupManifest(StoryBackupManifest.CurrentSchemaVersion, written, DateTimeOffset.UtcNow));
        return stories.Count - written;
    }

    private static async Task AddManifestAsync(ZipArchive zip, StoryBackupManifest manifest)
    {
        ZipArchiveEntry entry = zip.CreateEntry(StoryBackupManifest.ManifestFileName, CompressionLevel.Optimal);
        await using Stream stream = entry.Open();
        await stream.WriteAsync(new UTF8Encoding(false).GetBytes(manifest.ToJson()));
    }

    // The package is built in a temp file first, so a story that fails midway leaves nothing in the zip. A failure writing the zip itself is not swallowed.
    private static async Task<bool> TryAddPackageAsync(ZipArchive zip, Story story, IStoryLibrary library)
    {
        await using var temp = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, CopyBufferBytes, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        if (!await TryBuildPackageAsync(story, library, temp))
        {
            return false;
        }

        temp.Position = 0;
        await CopyIntoZipAsync(zip, story, temp);
        return true;
    }

    private static async Task<bool> TryBuildPackageAsync(Story story, IStoryLibrary library, Stream temp)
    {
        try
        {
            await StoryPackageWriter.WriteAsync(story, library, temp, includeRecordings: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or StoryPackageException)
        {
            return false;
        }
    }

    // A package is a zip already, so it is stored, not compressed again.
    private static async Task CopyIntoZipAsync(ZipArchive zip, Story story, Stream package)
    {
        ZipArchiveEntry entry = zip.CreateEntry(story.Id + StoryPackageManifest.Extension, CompressionLevel.NoCompression);
        await using Stream stream = entry.Open();
        await package.CopyToAsync(stream, CopyBufferBytes);
    }
}
