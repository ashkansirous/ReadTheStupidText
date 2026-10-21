namespace DinoReader.Core;

/// <summary>The stories stored on this device. Nothing here leaves the phone.</summary>
public interface IStoryLibrary
{
    /// <summary>All readable stories, newest first. A story whose file cannot be read is skipped.</summary>
    Task<IReadOnlyList<Story>> ListAsync();

    Task<Story?> GetAsync(string id);

    /// <summary>Saves a new story; throws if the id is already used.</summary>
    Task AddAsync(Story story);

    /// <summary>Saves changes to an existing story; throws if it does not exist.</summary>
    Task UpdateAsync(Story story);

    /// <summary>Removes the story's folder with its pictures. Deleting an unknown id does nothing.</summary>
    Task DeleteAsync(string id);

    /// <summary>The folder that holds the story's files (page images), whether or not it exists yet.</summary>
    string FolderOf(string id);

    /// <summary>
    /// The full path of a file inside the story's folder, or null if <paramref name="fileName"/> is
    /// not a plain file name (rooted, has a directory part or "..") or the id is not valid.
    /// Every image path read from a story file goes through this.
    /// </summary>
    string? TryResolveFile(string storyId, string? fileName);
}
