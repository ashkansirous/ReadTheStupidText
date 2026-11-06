using ReadTheStupidText.Application.Usage;

namespace DinoReader.Core;

/// <summary>
/// Stories as one folder each, <c>&lt;root&gt;/&lt;id&gt;/story.json</c> (plus page images later).
/// Writes go to a temp file that is then renamed over <c>story.json</c>, so a crash mid-save
/// leaves the old story intact. Lives in Core (plain System.IO, no framework) so it is
/// unit-tested; the app only supplies the root folder.
/// </summary>
public sealed class FileStoryLibrary(string root) : IStoryLibrary
{
    private const string StoryFileName = "story.json";
    private const string TempSuffix = ".tmp";
    private const int ShortIdLength = 8;
    private static readonly char[] SeparatorChars = ['/', '\\', ':'];

    // One lock for the whole library: stories are few and writes are small, so per-id locks
    // would add bookkeeping for no gain. It makes "exists, then write" atomic and stops two
    // saves of one story from interleaving.
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Slice 273 part D: where a read of the shelf that fell back (a stored book or the shelf order that cannot be read) is reported; the app sets it at start-up, a no-op until then.</summary>
    public IFallbackSink Fallbacks { get; set; } = NoFallbackSink.Instance;

    public string FolderOf(string id) => Path.Combine(root, id);

    public string? TryResolveFile(string storyId, string? fileName)
    {
        bool plainName = !string.IsNullOrEmpty(fileName)
            && fileName == Path.GetFileName(fileName)
            && fileName is not ("." or "..")
            && fileName.IndexOfAny(SeparatorChars) < 0;
        return plainName && StoryIds.IsValid(storyId) ? Path.Combine(FolderOf(storyId), fileName!) : null;
    }

    public async Task<IReadOnlyList<Story>> ListAsync()
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        var stories = new List<Story>();
        foreach (string folder in Directory.EnumerateDirectories(root))
        {
            Story? story = await TryReadAsync(Path.GetFileName(folder));
            if (story is not null)
            {
                stories.Add(story);
            }
        }

        List<Story> newestFirst = stories.OrderByDescending(s => s.Created).ThenBy(s => s.Id, StringComparer.Ordinal).ToList(); // Id breaks ties: directory order is not a contract
        return ShelfOrder.Apply(newestFirst, s => s.Id, await ReadOrderAsync());
    }

    private string OrderPath => Path.Combine(root, ShelfOrder.FileName);

    /// <summary>The stored shelf order (Slice 264); empty when there is none or it cannot be read, which leaves today's order.</summary>
    private async Task<IReadOnlyList<string>> ReadOrderAsync()
    {
        (IReadOnlyList<string> ids, bool unreadable) result;
        await _gate.WaitAsync(); // the replace in SaveOrderAsync holds the gate, so a read never meets a half-swapped file
        try
        {
            result = await ReadOrderLockedAsync();
        }
        finally
        {
            _gate.Release();
        }

        ReportOrderUnreadable(result.unreadable);
        return result.ids;
    }

    // Reported outside the gate, so counting never runs while a save waits.
    private void ReportOrderUnreadable(bool unreadable)
    {
        if (unreadable)
        {
            Fallbacks.Report(FallbackArea.Library, FallbackReason.ShelfOrderUnreadable); // Slice 273 part D: the default order is used
        }
    }

    // The caller holds the gate, so it does not report: it returns whether the order file could not be read and reports after releasing.
    private async Task<(IReadOnlyList<string> Ids, bool Unreadable)> ReadOrderLockedAsync()
    {
        try
        {
            return (File.Exists(OrderPath) ? ShelfOrder.Parse(await File.ReadAllTextAsync(OrderPath)) : [], false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // no-fallback: reported by the caller once the gate is released (ReportOrderUnreadable)
            return ([], true);
        }
    }

    /// <summary>Stores the shelf order (Slice 264): a temp file, then a replace, so a crash keeps the old order. Only the reorder mode's Done calls it. Books stored earlier but not in <paramref name="ids"/> (not on the screen) keep their old relative place.</summary>
    public async Task SaveOrderAsync(IReadOnlyList<string> ids)
    {
        Directory.CreateDirectory(root);
        string temp = $"{OrderPath}.{Guid.NewGuid():N}{TempSuffix}";
        bool unreadable;
        await _gate.WaitAsync(); // one gate across read, merge and replace, so a concurrent delete's forget cannot be undone by a stale merge
        try
        {
            (IReadOnlyList<string> stored, unreadable) = await ReadOrderLockedAsync();
            IReadOnlyList<string> onDisk = [.. stored.Where(id => Directory.Exists(FolderOf(id)))]; // an id whose book is gone is dropped here, even if its delete could not rewrite the file
            IReadOnlyList<string> merged = ShelfOrder.MergeShown(onDisk, ids);
            await File.WriteAllTextAsync(temp, ShelfOrder.Serialize(merged));
            File.Move(temp, OrderPath, overwrite: true);
        }
        catch
        {
            // no-fallback: removes the temp file and rethrows
            TryDelete(temp);
            throw;
        }
        finally
        {
            _gate.Release();
        }

        ReportOrderUnreadable(unreadable);
    }

    /// <summary>
    /// The source ids of every story folder on disk, or null when that cannot be said for certain: the root is missing or unreadable, or any story folder's story.json could not be read.
    /// Unlike <see cref="ListAsync"/>, which skips what it cannot read, a null here means "do not conclude a book is gone" (the audio pack sweep deletes only on a complete answer).
    /// </summary>
    public async Task<ISet<string>?> TryListSourceIdsAsync()
    {
        try
        {
            if (!Directory.Exists(root))
            {
                return null;
            }

            var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in Directory.EnumerateDirectories(root))
            {
                Story? story = await TryReadAsync(Path.GetFileName(folder));
                if (story is null)
                {
                    return null;
                }

                if (story.SourceId is { } source)
                {
                    sources.Add(source);
                }
            }

            return sources;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // no-fallback: null means "cannot say for certain" by contract, so the audio pack sweep deletes nothing
            return null;
        }
    }

    public Task<Story?> GetAsync(string id) => TryReadAsync(id);

    public async Task AddAsync(Story story)
    {
        string path = FilePathOf(story.Id);
        await _gate.WaitAsync();
        try
        {
            if (File.Exists(path))
            {
                throw new InvalidOperationException($"Story {story.Id} already exists.");
            }

            await WriteAsync(story);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ImportAsync(Story story, IReadOnlyList<PackageFile> files)
    {
        string path = FilePathOf(story.Id);
        await _gate.WaitAsync();
        try
        {
            if (File.Exists(path))
            {
                throw new InvalidOperationException($"Story {story.Id} already exists.");
            }

            await CopyFilesThenWriteAsync(story, files);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(Story story)
    {
        string path = FilePathOf(story.Id);
        await _gate.WaitAsync();
        try
        {
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Story {story.Id} does not exist.");
            }

            await WriteAsync(story);
        }
        finally
        {
            _gate.Release();
        }
    }

    // story.json goes first, so a delete that dies halfway leaves a folder the list already
    // ignores; deleting again finishes the job. A folder that is already gone is not an error.
    public async Task DeleteAsync(string id)
    {
        if (!StoryIds.IsValid(id))
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            File.Delete(FilePathOf(id));
            if (Directory.Exists(FolderOf(id)))
            {
                Directory.Delete(FolderOf(id), recursive: true);
            }

            await TryForgetInOrderAsync(id);
        }
        catch (DirectoryNotFoundException)
        {
            // no-fallback: the folder was already gone, which is what a delete wants
            await TryForgetInOrderAsync(id); // the folder was already gone; the id may still be in the shelf order
        }
        finally
        {
            _gate.Release();
        }
    }

    // The book folder is already gone here, so a locked shelf-order.json must not fail the delete: the next order save drops ids whose folder is gone.
    private async Task TryForgetInOrderAsync(string id)
    {
        try
        {
            await ForgetInOrderAsync(id);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // no-fallback: logged only; the next order save drops the id whose folder is gone
            System.Diagnostics.Debug.WriteLine($"Dino Reader: could not drop {id} from the shelf order: {ex.Message}");
        }
    }

    // A removed book leaves the stored shelf order (the gate is held by the caller), so the same id coming back, restored or downloaded again, goes to the front and not to its old slot.
    private async Task ForgetInOrderAsync(string id)
    {
        if (!File.Exists(OrderPath))
        {
            return;
        }

        IReadOnlyList<string> stored = ShelfOrder.Parse(await File.ReadAllTextAsync(OrderPath));
        List<string> kept = ShelfOrder.Without(stored, id);
        if (kept.Count == stored.Count)
        {
            return;
        }

        string temp = $"{OrderPath}.{Guid.NewGuid():N}{TempSuffix}";
        try
        {
            await File.WriteAllTextAsync(temp, ShelfOrder.Serialize(kept));
            File.Move(temp, OrderPath, overwrite: true);
        }
        catch
        {
            // no-fallback: removes the temp file and rethrows
            TryDelete(temp);
            throw;
        }
    }

    public async Task<string> StoreFileAsync(string storyId, string prefix, string extension, Func<string, Task> write)
    {
        if (prefix != Story.CoverFilePrefix)
        {
            throw new ArgumentException("Only cover files can be stored.", nameof(prefix));
        }

        if (extension != Story.CoverFileExtension)
        {
            throw new ArgumentException("Only cover files can be stored.", nameof(extension));
        }

        string name = $"{prefix}-{Guid.NewGuid().ToString("N")[..ShortIdLength]}{extension}";
        string target = TryResolveFile(storyId, name) ?? throw new ArgumentException("Not a plain file name.", nameof(storyId));
        Directory.CreateDirectory(FolderOf(storyId));
        string temp = $"{target}.{Guid.NewGuid():N}{TempSuffix}";
        try
        {
            // The slow write (decode, rescale, encode) runs outside the library-wide gate; only the move is serialised.
            await write(temp);
            await MoveIntoPlaceAsync(temp, target);
        }
        catch
        {
            // no-fallback: removes the temp file and rethrows
            TryDelete(temp);
            throw;
        }

        return name;
    }

    public async Task<string> StoreRecordingAsync(string storyId, int page, Func<string, Task> write)
    {
        if (page < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        string name = StoryRecordings.FileNameOf(page);
        string target = TryResolveFile(storyId, name) ?? throw new ArgumentException("Not a plain file name.", nameof(storyId));
        Directory.CreateDirectory(FolderOf(storyId));
        string temp = $"{target}.{Guid.NewGuid():N}{TempSuffix}";
        try
        {
            await write(temp);
            await MoveIntoPlaceAsync(temp, target);
        }
        catch
        {
            // no-fallback: removes the temp file and rethrows
            TryDelete(temp);
            throw;
        }

        return name;
    }

    private async Task MoveIntoPlaceAsync(string temp, string target)
    {
        await _gate.WaitAsync();
        try
        {
            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteFileAsync(string storyId, string fileName)
    {
        string? path = TryResolveFile(storyId, fileName);
        if (path is null || !(IsCoverFileName(fileName) || StoryRecordings.IsRecordingFileName(fileName)))
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            TryDelete(path);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Only cover photos and page recordings this library stored are ever deleted; a hand-edited story.json naming a page picture must not remove it.
    private static bool IsCoverFileName(string fileName) =>
        fileName.StartsWith(Story.CoverFilePrefix + "-", StringComparison.Ordinal)
        && fileName.EndsWith(Story.CoverFileExtension, StringComparison.Ordinal);

    private string FilePathOf(string id) =>
        StoryIds.IsValid(id)
            ? Path.Combine(FolderOf(id), StoryFileName)
            : throw new ArgumentException("Not a story id.", nameof(id));

    private async Task<Story?> TryReadAsync(string id)
    {
        if (!StoryIds.IsValid(id) || !File.Exists(FilePathOf(id)))
        {
            return null;
        }

        try
        {
            Story story = StoryJson.Deserialize(await File.ReadAllTextAsync(FilePathOf(id)));
            // A story whose stored id disagrees with its folder was copied or edited by hand;
            // it is skipped as unreadable rather than risk writes landing in another folder.
            if (story.Id == id)
            {
                return story;
            }

            Fallbacks.Report(FallbackArea.Library, FallbackReason.StoryUnreadable); // Slice 273 part D: the book is left off the shelf
            return null;
        }
        catch (StoryFormatException)
        {
            Fallbacks.Report(FallbackArea.Library, FallbackReason.StoryUnreadable); // Slice 273 part D: the book is left off the shelf
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // no-fallback: a busy or locked file (a save in progress) is transient; the book is back on the next refresh, so it is not counted as unreadable
            return null;
        }
    }

    // story.json is written last; any failure removes what this import created (the files it wrote, and the folder if it made it),
    // so nothing half-imported is left behind and a pre-existing folder is never wiped. Files are created, never overwritten.
    private async Task CopyFilesThenWriteAsync(Story story, IReadOnlyList<PackageFile> files)
    {
        string folder = FolderOf(story.Id);
        bool createdFolder = !Directory.Exists(folder);
        var written = new List<string>();
        try
        {
            foreach (PackageFile file in files)
            {
                string target = TryResolveFile(story.Id, file.Name) ?? throw new ArgumentException("Not a plain file name.", nameof(files));
                Directory.CreateDirectory(folder);
                await using FileStream output = new(target, FileMode.CreateNew, FileAccess.Write);
                written.Add(target);
                await using Stream input = file.Open();
                await input.CopyToAsync(output);
            }

            await WriteAsync(story);
        }
        catch
        {
            // no-fallback: removes what this import created and rethrows
            if (createdFolder)
            {
                TryDeleteFolder(folder);
            }
            else
            {
                written.ForEach(TryDelete);
            }

            throw;
        }
    }

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // no-fallback: cleanup of a failed import's folder, nothing the person gets changes
        }
    }

    private async Task WriteAsync(Story story)
    {
        string path = FilePathOf(story.Id);
        Directory.CreateDirectory(FolderOf(story.Id));
        string temp = $"{path}.{Guid.NewGuid():N}{TempSuffix}";
        try
        {
            await File.WriteAllTextAsync(temp, StoryJson.Serialize(story));
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // no-fallback: removes the temp file and rethrows
            TryDelete(temp);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // no-fallback: temp file cleanup, nothing the person gets changes
        }
    }
}
