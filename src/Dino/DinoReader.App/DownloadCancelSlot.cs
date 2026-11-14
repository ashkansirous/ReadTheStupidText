namespace DinoReader;

/// <summary>
/// Slice 346 review (pass 5 #7, pass 6): the cancel sources of one store row's downloads. A source is disposed only by <see cref="End"/> of the download that owns it, after that download's task has ended,
/// so a cancelled run never reads a disposed token. A new download cancels any run still going (the row normally allows none). All calls come from the UI thread.
/// </summary>
public sealed class DownloadCancelSlot
{
    private readonly List<CancellationTokenSource> _sources = [];

    /// <summary>Starts a download: a run still going is cancelled (not disposed; its own <see cref="End"/> does that); returns the new token.</summary>
    public CancellationToken Begin()
    {
        Cancel();
        var source = new CancellationTokenSource();
        _sources.Add(source);
        return source.Token;
    }

    /// <summary>The end of the download that got <paramref name="own"/> from <see cref="Begin"/>, once its task has ended: disposes its source.</summary>
    public void End(CancellationToken own)
    {
        int index = _sources.FindIndex(s => s.Token == own);
        if (index >= 0)
        {
            CancellationTokenSource source = _sources[index];
            _sources.RemoveAt(index);
            source.Dispose();
        }
    }

    /// <summary>Trips every download that is running; the sources stay until their own End.</summary>
    public void Cancel()
    {
        foreach (CancellationTokenSource running in _sources)
        {
            running.Cancel();
        }
    }
}
