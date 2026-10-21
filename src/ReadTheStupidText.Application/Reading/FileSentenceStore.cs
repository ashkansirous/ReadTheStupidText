namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// <see cref="ISentenceStore"/> over a folder: one <c>&lt;id&gt;.wav</c> per sentence, written
/// to a temp name first so a killed app never leaves a half-written clip under a real name.
/// </summary>
internal sealed class FileSentenceStore : ISentenceStore
{
    private const string ClipExtension = ".wav";
    private const string PartialExtension = ".tmp";

    private readonly string _folder;

    public FileSentenceStore(string folder)
    {
        _folder = folder;
    }

    // Also the startup purge: partial files from a killed write are deleted, and a clip with
    // no valid header is not offered to the cache.
    public IReadOnlyList<StoredSentence> List()
    {
        if (!Directory.Exists(_folder))
        {
            return [];
        }

        DeletePartials();
        return new DirectoryInfo(_folder).EnumerateFiles("*" + ClipExtension)
            .OrderBy(f => f.LastWriteTimeUtc)
            .Select(ToStored)
            .OfType<StoredSentence>()
            .ToList();
    }

    public async Task<byte[]?> ReadAsync(string id)
    {
        try
        {
            return await File.ReadAllBytesAsync(PathFor(id));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task<StoredSentence> WriteAsync(string id, byte[] wav)
    {
        Directory.CreateDirectory(_folder); // the OS may have cleared the cache folder meanwhile
        string path = PathFor(id);
        string partial = $"{path}.{Guid.NewGuid():N}{PartialExtension}"; // unique: two writers may race on one id
        await File.WriteAllBytesAsync(partial, wav);
        File.Move(partial, path, overwrite: true);
        TimeSpan duration = WavFile.TryGetDuration(wav) ?? TimeSpan.Zero;
        return new StoredSentence(id, path, wav.LongLength, duration);
    }

    public void Touch(string id) => TryRun(() => File.SetLastWriteTimeUtc(PathFor(id), DateTime.UtcNow));

    public void Delete(string id) => TryRun(() => File.Delete(PathFor(id)));

    private string PathFor(string id) => Path.Combine(_folder, id + ClipExtension);

    private void DeletePartials()
    {
        foreach (string partial in Directory.EnumerateFiles(_folder, "*" + PartialExtension))
        {
            TryRun(() => File.Delete(partial));
        }
    }

    // A clip with a bad header or fewer bytes than it declares (a truncated write) is deleted
    // rather than offered to the cache, where playing it would fail.
    private static StoredSentence? ToStored(FileInfo file)
    {
        StoredSentence? stored = TryReadStored(file);
        if (stored is null)
        {
            TryRun(file.Delete);
        }

        return stored;
    }

    private static StoredSentence? TryReadStored(FileInfo file)
    {
        byte[] header = new byte[WavFile.HeaderBytes];
        try
        {
            using FileStream stream = file.OpenRead();
            int read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
            ReadOnlySpan<byte> bytes = header.AsSpan(0, read);
            return WavFile.IsComplete(bytes, file.Length) && WavFile.TryGetDuration(bytes) is { } duration
                ? new StoredSentence(Path.GetFileNameWithoutExtension(file.Name), file.FullName, file.Length, duration)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Cache housekeeping is best-effort: a locked or already-gone file is not an error.
    private static void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing to do: the cache index no longer lists the clip either way.
        }
    }
}
