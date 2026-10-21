namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Blocks (and the completion) of a streamed read that arrive before its <see cref="ChunkFeed"/> exists,
/// because the first block is still being planned. They are held in order and applied to the feed the moment
/// it is created, so none is dropped. Not thread-safe: the caller serialises access.
/// </summary>
internal sealed class PendingStreamBlocks
{
    private readonly List<string> _blocks = [];
    private bool _complete;

    public void Add(string text) => _blocks.Add(text);

    public void MarkComplete() => _complete = true;

    /// <summary>
    /// Appends the held blocks to <paramref name="feed"/> in order (passing <paramref name="grown"/> through,
    /// so the caller can extend its timing), then completes the feed if completion arrived. Clears the buffer.
    /// </summary>
    public bool DrainInto(ChunkFeed feed, Action<int>? grown = null)
    {
        foreach (string block in _blocks)
        {
            feed.Append(block, grown);
        }

        _blocks.Clear();
        if (_complete)
        {
            feed.Complete();
        }

        return _complete;
    }
}
