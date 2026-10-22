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

        return stories.OrderByDescending(s => s.Created).ToList();
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
        }
        catch (DirectoryNotFoundException)
        {
        }
        finally
        {
            _gate.Release();
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
        catch
        {
            TryDelete(temp);
            throw;
        }

        return name;
    }

    public async Task DeleteFileAsync(string storyId, string fileName)
    {
        string? path = TryResolveFile(storyId, fileName);
        if (path is null || !IsCoverFileName(fileName))
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

    // Only cover photos this library stored are ever deleted; a hand-edited story.json naming a page picture must not remove it.
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
            return story.Id == id ? story : null;
        }
        catch (Exception ex) when (ex is StoryFormatException or IOException or UnauthorizedAccessException)
        {
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
        }
    }
}
