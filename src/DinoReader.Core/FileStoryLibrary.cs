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
