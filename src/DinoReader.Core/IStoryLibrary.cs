namespace DinoReader.Core;

/// <summary>The stories stored on this device. Nothing here leaves the phone.</summary>
public interface IStoryLibrary
{
    /// <summary>All readable stories, newest first. A story whose file cannot be read is skipped.</summary>
    Task<IReadOnlyList<Story>> ListAsync();

    Task<Story?> GetAsync(string id);

    /// <summary>Saves a new story; throws if the id is already used.</summary>
    Task AddAsync(Story story);

    /// <summary>Saves a new story together with its picture files (a package import, Slice 91): the files are copied into the story's
    /// folder under their plain names and <c>story.json</c> is written last, so a failure leaves no listed half-story. Throws if the id is
    /// already used or a file name is not a plain one. A library that does not store stories on disk (the wizard's draft view) leaves it unsupported.</summary>
    Task ImportAsync(Story story, IReadOnlyList<PackageFile> files) => throw new NotSupportedException();

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

    /// <summary>Creates a new file in the story's folder named <c>&lt;prefix&gt;-&lt;short id&gt;&lt;extension&gt;</c>: <paramref name="write"/> fills a temp file
    /// (given its path) which is then renamed into place, so a failed write leaves nothing behind. Returns the new file name.
    /// Only cover files (<see cref="Story.CoverFilePrefix"/>, <see cref="Story.CoverFileExtension"/>) are accepted, the same ones <see cref="DeleteFileAsync"/> removes. A fresh name each time means a replaced picture is never served from an image cache (Slice 77).</summary>
    Task<string> StoreFileAsync(string storyId, string prefix, string extension, Func<string, Task> write);

    /// <summary>Deletes one cover file this library stored; a missing file, an unusable name or any other file does nothing.</summary>
    Task DeleteFileAsync(string storyId, string fileName);
}
