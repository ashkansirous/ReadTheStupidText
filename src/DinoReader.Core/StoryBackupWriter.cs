using System.IO.Compression;
using System.Text;

namespace DinoReader.Core;

/// <summary>Which backup the writer makes (Slice 346): the app picks <see cref="StateOnly"/> only while the paid-books switch is known to be on.</summary>
public enum BackupFormat
{
    /// <summary>Schema 1, exactly the backup of Slice 93: one <c>.dino</c> package (book files and recordings) per story. Used while the switch is off or unknown.</summary>
    WithBookFiles,

    /// <summary>Schema 2: the shelf (book ids), each book's reading view and recordings, the parents' recordings and a few settings; no book content.</summary>
    StateOnly,
}

/// <summary>
/// Writes a backup zip (Slice 93; Slice 346 adds the schema 2 <see cref="BackupFormat.StateOnly"/> backup, which never holds a book's text, pictures or audio packs: a restore downloads the books from the store again).
/// </summary>
public static partial class StoryBackupWriter
{
    private const int CopyBufferBytes = 81920;

    /// <summary>
    /// Writes <paramref name="stories"/> (read from <paramref name="library"/>) to <paramref name="output"/> in the given <paramref name="format"/> and returns
    /// how many could not be written (a story whose files cannot be read is left out whole, never half-written into the zip).
    /// The manifest's count is the number actually written. <paramref name="settings"/> are filtered through <see cref="BackupSettings.Sanitize"/> and only written by <see cref="BackupFormat.StateOnly"/>.
    /// </summary>
    public static Task<int> WriteAsync(IReadOnlyList<Story> stories, IStoryLibrary library, Stream output, BackupFormat format = BackupFormat.WithBookFiles, IReadOnlyDictionary<string, string>? settings = null) =>
        format == BackupFormat.StateOnly ? WriteStateAsync(stories, library, output, settings) : WriteWithBookFilesAsync(stories, library, output);

    private static async Task<int> WriteStateAsync(IReadOnlyList<Story> stories, IStoryLibrary library, Stream output, IReadOnlyDictionary<string, string>? settings)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);
        var shelf = new List<BackupShelfEntry>();
        int notWritten = 0;
        foreach (IGrouping<string, Story> copies in stories.GroupBy(StoryBackupState.IdOf, StringComparer.OrdinalIgnoreCase)) // two shelf books with one store id are one book in the backup
        {
            if (await TryAddBookAsync(zip, [.. copies], library))
            {
                shelf.Add(new BackupShelfEntry(copies.Key, copies.First().Title));
            }
            else
            {
                notWritten += copies.Count();
            }
        }

        if (shelf.Count == 0 && stories.Count > 0)
        {
            throw new StoryBackupException("No story could be written to the backup.");
        }

        var manifest = new StoryBackupManifest(StoryBackupManifest.StateSchemaVersion, shelf.Count, DateTimeOffset.UtcNow, shelf, BackupSettings.Sanitize(settings));
        await AddTextAsync(zip, StoryBackupManifest.ManifestFileName, manifest.ToJson());
        return notWritten;
    }

    private static async Task AddTextAsync(ZipArchive zip, string name, string text)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using Stream stream = entry.Open();
        await stream.WriteAsync(new UTF8Encoding(false).GetBytes(text));
    }

    // The recordings are read first, so a book whose audio cannot be read leaves nothing in the zip. A failure writing the zip itself is not swallowed.
    // Copies of one store book (the same id on the shelf twice) become one entry: the first readable copy, plus every clip of the others it does not already have.
    private static async Task<bool> TryAddBookAsync(ZipArchive zip, IReadOnlyList<Story> copies, IStoryLibrary library)
    {
        string id = StoryBackupState.IdOf(copies[0]);
        if (!StoryIds.IsValid(id))
        {
            return false;
        }

        (Story Story, List<(string Name, string Path)> Files)? merged = MergeCopies(copies, library);
        if (merged is null)
        {
            return false;
        }

        await AddTextAsync(zip, StoryBackupState.StateFile(id), StoryJson.Serialize(StoryBackupState.Stub(merged.Value.Story)));
        foreach ((string name, string path) in merged.Value.Files)
        {
            ZipArchiveEntry entry = zip.CreateEntry(StoryBackupState.RecordingEntry(id, name), CompressionLevel.NoCompression); // already compressed audio
            await using Stream target = entry.Open();
            await using FileStream source = File.OpenRead(path);
            await source.CopyToAsync(target, CopyBufferBytes);
        }

        return true;
    }

    // Null when no copy could be read whole. A copy with an unreadable recording is left out whole; a later copy's clip joins only when its page text matches, it overlaps nothing already kept
    // and its file name is not already taken by another copy's file.
    private static (Story Story, List<(string Name, string Path)> Files)? MergeCopies(IReadOnlyList<Story> copies, IStoryLibrary library)
    {
        Story? kept = null;
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Story copy in copies)
        {
            List<(string Name, string Path)>? own = RecordingFiles(copy, library);
            if (own is null)
            {
                continue;
            }

            Dictionary<string, string> ownByName = own.ToDictionary(f => f.Name, f => f.Path, StringComparer.Ordinal);
            IReadOnlyList<StoryClipKey> keys = kept is null ? [] : StoryRecordings.MergeableClips(kept, copy, name => ownByName.ContainsKey(name) && !files.ContainsKey(name));
            kept = kept is null ? copy : StoryRecordings.WithMergedClips(kept, copy, keys);
            foreach ((string name, string path) in own)
            {
                files.TryAdd(name, path);
            }
        }

        return kept is null ? null : (kept, [.. StoryRecordings.Files(kept).Distinct(StringComparer.Ordinal).Where(files.ContainsKey).Select(name => (name, files[name]))]);
    }

    // Null when a recording the story names cannot be opened for reading (the whole book is then left out).
    private static List<(string Name, string Path)>? RecordingFiles(Story story, IStoryLibrary library)
    {
        var files = new List<(string, string)>();
        foreach (string name in StoryRecordings.Files(story).Distinct(StringComparer.Ordinal))
        {
            string? path = library.TryResolveFile(story.Id, name);
            if (path is null || !File.Exists(path))
            {
                continue; // a recording the folder lacks is simply not backed up, as before
            }

            if (!CanRead(path))
            {
                return null;
            }

            files.Add((name, path));
        }

        return files;
    }

    private static bool CanRead(string path)
    {
        try
        {
            using FileStream probe = File.OpenRead(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // no-fallback: the caller counts the story as not written and returns that count to the page
            return false;
        }
    }
}
