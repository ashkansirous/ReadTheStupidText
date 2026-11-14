namespace DinoReader.Core;

/// <summary>The kinds of gallery UI work that can be waiting (Slice 325, finding group "stale marks"). Shelf is a check; ShelfForce makes it a rebuild.</summary>
[Flags]
public enum UiWork
{
    None = 0,
    Shelf = 1,
    ShelfForce = 2,
    Chrome = 4,
    TabBuild = 8,
    Tab = 16,
    ShelfRetried = 32,
}

/// <summary>
/// The one record of UI work that still has to be done. Every request ORs its bits in. A pass takes the bits it can run now (<see cref="TryTake"/>) and gives back, with
/// <see cref="Restore"/>, every bit whose work did not finish (it could not run, or it threw): a bit is gone only when its work succeeded. A request that arrives while a pass runs is a
/// separate bit set in the record, so finishing the pass never wipes it.
/// </summary>
public sealed class PendingUiWork
{
    private readonly object _lock = new();
    private UiWork _bits;

    /// <summary>Something is waiting (work taken by a running pass is not).</summary>
    public bool Any
    {
        get
        {
            lock (_lock)
            {
                return _bits != UiWork.None;
            }
        }
    }

    /// <summary>The bits waiting right now.</summary>
    public UiWork Snapshot()
    {
        lock (_lock)
        {
            return _bits;
        }
    }

    public bool Has(UiWork work)
    {
        lock (_lock)
        {
            return (_bits & work) != UiWork.None;
        }
    }

    public void Request(UiWork work)
    {
        lock (_lock)
        {
            _bits |= work;
            if ((work & UiWork.Shelf) != UiWork.None)
            {
                _bits &= ~UiWork.ShelfRetried; // a real trigger starts a new retry allowance
            }
        }
    }

    /// <summary>Takes the waiting bits that are inside <paramref name="runnable"/>; the others stay waiting. False when nothing was taken.</summary>
    public bool TryTake(UiWork runnable, out UiWork taken)
    {
        lock (_lock)
        {
            taken = _bits & runnable;
            _bits &= ~taken;
            return taken != UiWork.None;
        }
    }

    /// <summary>Puts back the bits of work that did not finish.</summary>
    public void Restore(UiWork unfinished)
    {
        lock (_lock)
        {
            _bits |= unfinished;
        }
    }
}

/// <summary>The pure decisions about waiting UI work, so that "nothing is lost, nothing loops" is tested without a page.</summary>
public static class UiWorkRules
{
    /// <summary>The shelf bits a pass takes.</summary>
    public const UiWork ShelfMask = UiWork.Shelf | UiWork.ShelfForce | UiWork.ShelfRetried;

    /// <summary>What a pass can run right now. The page handler gates everything; the tab rebuild also needs the tabs' own handler; the chrome needs a real window size.</summary>
    public static UiWork Runnable(bool pageHandler, bool tabsHandler, bool windowSized)
    {
        if (!pageHandler)
        {
            return UiWork.None;
        }

        UiWork runnable = ShelfMask | UiWork.Tab;
        if (tabsHandler)
        {
            runnable |= UiWork.TabBuild;
        }

        return windowSized ? runnable | UiWork.Chrome : runnable;
    }

    /// <summary>A replay posts a pass only when a waiting bit can run now, so a bit that waits for an event never makes a pass loop.</summary>
    public static bool ShouldReplay(UiWork pending, UiWork runnable) => (pending & runnable) != UiWork.None;

    /// <summary>The shelf bits to put back after a shelf pass: none when it applied; otherwise the bits it took (a check stays a check) plus the retry mark, and nothing when the pass already was the one retry.</summary>
    public static UiWork ShelfUnfinished(UiWork taken, bool applied)
    {
        bool wasRetry = (taken & UiWork.ShelfRetried) != UiWork.None;
        if (applied || wasRetry)
        {
            return UiWork.None;
        }

        return (taken & (UiWork.Shelf | UiWork.ShelfForce)) | UiWork.ShelfRetried;
    }
}
