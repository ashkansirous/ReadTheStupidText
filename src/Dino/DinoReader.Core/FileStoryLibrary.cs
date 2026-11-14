using Sirous.Reading.Application.Usage;

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
    private int _orderUnreadableReported; // 1 once ShelfOrderUnreadable was reported by this library (Slice 275 part 3)

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
            Story? story = await TryReadAsync(Path.GetFileName(folder), includeOwn: false);
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
        if (unreadable && Interlocked.Exchange(ref _orderUnreadableReported, 1) == 0) // once per run for this order file, not on every list and delete
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
    /// <exception cref="ArgumentException">An id is not a story id (Slice 298): nothing is written, so the stored order stays as it was.</exception>
    public async Task SaveOrderAsync(IReadOnlyList<string> ids)
    {
        if (ids.Any(id => !StoryIds.IsValid(id)))
        {
            throw new ArgumentException("The shelf order holds an id that is not a story id.", nameof(ids));
        }

        Directory.CreateDirectory(root);
        string temp = $"{OrderPath}.{Guid.NewGuid():N}{TempSuffix}";
        bool unreadable = await MergeAndReplaceOrderAsync(ids, temp);
        ReportOrderUnreadable(unreadable); // after the gate is released
    }

    // Holds the gate across read, merge and replace; returns whether the stored order could not be read (it is then replaced by the shown ids).
    private async Task<bool> MergeAndReplaceOrderAsync(IReadOnlyList<string> ids, string temp)
    {
        await _gate.WaitAsync(); // one gate across read, merge and replace, so a concurrent delete's forget cannot be undone by a stale merge
        try
        {
            (IReadOnlyList<string> stored, bool unreadable) = await ReadOrderLockedAsync();
            IReadOnlyList<string> onDisk = [.. stored.Where(id => Directory.Exists(FolderOf(id)))]; // an id whose book is gone is dropped here, even if its delete could not rewrite the file
            IReadOnlyList<string> merged = ShelfOrder.MergeShown(onDisk, ids);
            await File.WriteAllTextAsync(temp, ShelfOrder.Serialize(merged));
            File.Move(temp, OrderPath, overwrite: true);
            return unreadable;
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

    // story.json goes LAST, so a delete that dies halfway leaves a folder that still has its story.json: it stays listed
    // (ListOwnFoldersAsync) and the next cleanup pass finishes the job. A folder that is already gone is not an error.
    public async Task DeleteAsync(string id)
    {
        if (!StoryIds.IsValid(id))
        {
            return;
        }

        bool orderUnreadable = false;
        await _gate.WaitAsync();
        try
        {
            DeleteFolderKeepingStoryJsonLast(FolderOf(id));

            orderUnreadable = await TryForgetInOrderAsync(id);
        }
        catch (DirectoryNotFoundException)
        {
            // no-fallback: the folder was already gone, which is what a delete wants
            orderUnreadable = await TryForgetInOrderAsync(id); // the folder was already gone; the id may still be in the shelf order
        }
        finally
        {
            _gate.Release();
        }

        ReportOrderUnreadable(orderUnreadable);
    }

    private static void DeleteFolderKeepingStoryJsonLast(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        string storyPath = Path.Combine(folder, StoryFileName);
        foreach (string entry in Directory.EnumerateFileSystemEntries(folder).ToList())
        {
            if (string.Equals(entry, storyPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }
        }

        File.Delete(storyPath);
        Directory.Delete(folder, recursive: false);
    }

    // The book folder is already gone here, so a locked shelf-order.json must not fail the delete: the next order save drops ids whose folder is gone.
    // Returns whether the order file was unreadable (not a list of ids), which the caller reports after releasing the gate.
    private async Task<bool> TryForgetInOrderAsync(string id)
    {
        try
        {
            await ForgetInOrderAsync(id);
            return false;
        }
        catch (ShelfOrderUnreadableException ex)
        {
            // no-fallback: reported by the caller once the gate is released (ReportOrderUnreadable); the file stays, the next order save replaces it
            System.Diagnostics.Debug.WriteLine($"Dino Reader: the shelf order is unreadable, {id} was not dropped from it: {ex.Message}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // no-fallback: logged only; the next order save drops the id whose folder is gone
            System.Diagnostics.Debug.WriteLine($"Dino Reader: could not drop {id} from the shelf order: {ex.Message}");
            return false;
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

    public Task<string> StoreRecordingAsync(string storyId, int page, Func<string, Task> write) =>
        StoreClipAsync(storyId, StoryClipKey.OfPage(page, StoryClipKey.NoLanguage), write);

    public async Task<string> StoreClipAsync(string storyId, StoryClipKey key, Func<string, Task> write)
    {
        if (!key.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(key));
        }

        return await StoreRecordingFileAsync(storyId, StoryRecordings.FileNameOf(key), write);
    }

    public async Task<string> StoreRecordingFileAsync(string storyId, string fileName, Func<string, Task> write)
    {
        if (!StoryRecordings.IsRecordingFileName(fileName))
        {
            throw new ArgumentException("Not a recording file name.", nameof(fileName));
        }

        string name = fileName;
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

    /// <summary>
    /// The folders of the own books (Slice 278 A3, design 2; <see cref="OwnBook.IsOwn"/> on the raw story.json) and whether every folder was seen. A folder with no story.json yet (a package mid-import) is not a book and is skipped;
    /// a story.json that cannot be read, or a root that cannot be listed, makes the answer incomplete (such a book may be an own book).
    /// </summary>
    public async Task<OwnFolders> ListOwnFoldersAsync()
    {
        var ids = new List<string>();
        try
        {
            if (!Directory.Exists(root))
            {
                return new OwnFolders(ids, true);
            }

            bool complete = true;
            foreach (string folder in Directory.EnumerateDirectories(root))
            {
                complete &= await TryCollectOwnAsync(Path.GetFileName(folder), ids);
            }

            return new OwnFolders(ids, complete);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // no-fallback: incomplete by contract, so the cleanup does not mark the pass done
            return new OwnFolders(ids, false);
        }
    }

    // False when the folder has a story.json that could not be read.
    private async Task<bool> TryCollectOwnAsync(string id, List<string> ids)
    {
        if (!StoryIds.IsValid(id) || !File.Exists(FilePathOf(id)))
        {
            return true;
        }

        try
        {
            if (OwnBook.IsOwn(await File.ReadAllTextAsync(FilePathOf(id))))
            {
                ids.Add(id);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // no-fallback: reported by the caller as an incomplete listing
            return false;
        }
    }

    // includeOwn is false only for the shelf listing, which never shows an own book (Slice 278 A3); lookups by id and the source-id sweep still see every book.
    private async Task<Story?> TryReadAsync(string id, bool includeOwn = true)
    {
        if (!StoryIds.IsValid(id) || !File.Exists(FilePathOf(id)))
        {
            return null;
        }

        try
        {
            string text = await File.ReadAllTextAsync(FilePathOf(id));
            if (!includeOwn && OwnBook.IsOwn(text))
            {
                return null;
            }

            string? mainLanguage = StoryJson.MainLanguageOf(); // read once: the returned story and the saved migration use the same language; null (not known yet) migrates and saves nothing
            Story story = StoryJson.Deserialize(text, out bool migrated, mainLanguage);
            // A story whose stored id disagrees with its folder was copied or edited by hand;
            // it is skipped as unreadable rather than risk writes landing in another folder.
            if (story.Id == id)
            {
                if (migrated)
                {
                    await SaveMigratedRecordingsAsync(story, mainLanguage!);
                }

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

    // Slice 286: a book whose recordings predate the language key was read with them filed under the main language; saving it once makes that permanent, so a later change of the
    // app language never moves them. It runs under the gate and re-reads the file, so a save made meanwhile is never overwritten by the stale copy.
    private async Task SaveMigratedRecordingsAsync(Story migrated, string mainLanguage)
    {
        await _gate.WaitAsync();
        try
        {
            string text = await File.ReadAllTextAsync(FilePathOf(migrated.Id));
            Story current = StoryJson.Deserialize(text, out bool stillOld, mainLanguage);
            if (stillOld)
            {
                await WriteAsync(current);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or StoryFormatException)
        {
            // no-fallback: the migration is read again, the same way, on the next read of the book
        }
        finally
        {
            _gate.Release();
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
