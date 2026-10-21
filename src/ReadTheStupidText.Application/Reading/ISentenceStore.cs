namespace ReadTheStupidText.Application.Reading;

/// <summary>One stored sentence clip: its cache id, where it lives, its size and length.</summary>
internal sealed record StoredSentence(string Id, string Path, long SizeBytes, TimeSpan Duration);

/// <summary>
/// Where cached sentence audio lives (Slice 47). The cache decides what to keep; the store
/// only reads, writes and deletes, so the cache's LRU logic unit-tests without a disk.
/// </summary>
internal interface ISentenceStore
{
    /// <summary>Every clip already stored, oldest-used first.</summary>
    IReadOnlyList<StoredSentence> List();

    /// <summary>The clip's bytes, or null when it is gone or unreadable.</summary>
    Task<byte[]?> ReadAsync(string id);

    /// <summary>Stores the clip and returns where it went.</summary>
    Task<StoredSentence> WriteAsync(string id, byte[] wav);

    /// <summary>Marks the clip as just used, so a later run still orders it correctly.</summary>
    void Touch(string id);

    void Delete(string id);
}
