namespace DinoReader.Core;

/// <summary>Why the shelf locked (Slice 418): the timer ran out, or a grown-up tapped the lock. Decides which short message shows.</summary>
public enum LockReason
{
    Timer,
    ByHand,
}

/// <summary>
/// Which taps count as a grown-up tap for the lock's timer (Slice 418 decision 13): a press inside the grown-up places (the unlocked shelf's Books tab, Store, Settings, their sub-pages, Record, Backup, ...). Reading, turning pages,
/// the reading-language button and a tap on a cover (which reads) do not. Pure on the screen and control names the usage counts already carry (<see cref="UiScreens"/>, <see cref="UiControls"/>).
/// </summary>
public static class LockTapRules
{
    private static readonly HashSet<string> GrownUpScreens =
    [
        UiScreens.CornerBooks, UiScreens.CornerStore, UiScreens.CornerSettings, UiScreens.StoryLanguages, UiScreens.Language, UiScreens.ReadingSpeed, UiScreens.AnimalSounds,
        UiScreens.RecordIntro, UiScreens.Record, UiScreens.Backup, UiScreens.UsageCounts, UiScreens.About, UiScreens.ReportProblem, UiScreens.DropLanguage,
    ];

    /// <summary>True for a screen that is a grown-up place: any press, toggle, drag or key there (not on a cover) counts as a grown-up tap.</summary>
    public static bool IsGrownUpScreen(string screen) => GrownUpScreens.Contains(screen);

    /// <summary>
    /// True when a key press on <paramref name="screen"/> is a grown-up tap (decision 13): only inside Store, Settings, their sub-pages, a ••• sheet or the reorder mode (<paramref name="sheetOrReorderOpen"/>).
    /// A key on the Books list or in the reader (arrows between covers, Esc, Enter on a cover) is the child's and never resets the timer.
    /// </summary>
    public static bool IsGrownUpKey(string screen, bool sheetOrReorderOpen) =>
        IsGrownUpScreen(screen) && (screen != UiScreens.CornerBooks || sheetOrReorderOpen);

    /// <summary>True when a press of <paramref name="control"/> on <paramref name="screen"/> is a grown-up tap.</summary>
    public static bool IsGrownUpTap(string screen, string control)
    {
        if (!IsGrownUpScreen(screen))
        {
            return false;
        }

        bool readsOrSwitchesLanguage = screen == UiScreens.CornerBooks && control is UiControls.Book or UiControls.Language;
        return !readsOrSwitchesLanguage;
    }
}

/// <summary>
/// The shelf's lock (Slice 418, decisions 8, 12, 13, 16) as a pure clock: locked by default, <see cref="Unlock"/> starts a 30-second
/// countdown, every <see cref="GrownUpTap"/> puts it back to the full time, <see cref="Hold"/> (counted) stops it while the store's buying
/// sheet or a recording is open, and <see cref="Tick"/> locks at zero with <see cref="LockReason.Timer"/>. Every member takes one lock, because a hold
/// can start on a background thread (a purchase) while the UI thread ticks.
/// </summary>
public sealed class LockClock
{
    /// <summary>The seconds a grown-up has after the last grown-up tap; the Debug hook <c>lock-seconds</c> can shorten it for a flow.</summary>
    public const int DefaultSeconds = 30;

    private readonly object _gate = new();
    private TimeSpan _total = TimeSpan.FromSeconds(DefaultSeconds);
    private TimeSpan _left;
    private int _holds;
    private int _generation;
    private bool _locked = true;
    private LockReason? _lastReason;

    public bool Locked
    {
        get
        {
            lock (_gate)
            {
                return _locked;
            }
        }
    }

    /// <summary>The reason of the last lock, or null while unlocked or before the first unlock.</summary>
    public LockReason? LastReason
    {
        get
        {
            lock (_gate)
            {
                return _lastReason;
            }
        }
    }

    /// <summary>True while at least one <see cref="Hold"/> is open.</summary>
    public bool IsHeld
    {
        get
        {
            lock (_gate)
            {
                return _holds > 0;
            }
        }
    }

    /// <summary>The whole seconds left, rounded up (24 for 23.2 s), 0 while locked.</summary>
    public int SecondsLeft
    {
        get
        {
            lock (_gate)
            {
                return _locked ? 0 : (int)Math.Ceiling(_left.TotalSeconds);
            }
        }
    }

    public void SetTotalSeconds(int seconds)
    {
        lock (_gate)
        {
            _total = TimeSpan.FromSeconds(Math.Max(1, seconds));
            if (!_locked)
            {
                _left = _total;
            }
        }
    }

    public void Unlock()
    {
        lock (_gate)
        {
            EndHolds();
            _locked = false;
            _lastReason = null;
            _left = _total;
        }
    }

    public void Lock(LockReason reason)
    {
        lock (_gate)
        {
            LockCore(reason);
        }
    }

    /// <summary>A grown-up tap (Store, Settings, a ••• menu, anything inside them): the full time again. Ignored while locked.</summary>
    public void GrownUpTap()
    {
        lock (_gate)
        {
            if (!_locked)
            {
                _left = _total;
            }
        }
    }

    /// <summary>Opens a hold and returns its token; pass the token to <see cref="Release"/>. A lock ends every open hold, so a token from before the last lock is ignored when it comes back.</summary>
    public int Hold()
    {
        lock (_gate)
        {
            _holds++;
            return _generation;
        }
    }

    /// <summary>Closes the hold that <see cref="Hold"/> gave <paramref name="token"/> for; ignored when a lock has ended that hold already.</summary>
    public void Release(int token)
    {
        lock (_gate)
        {
            if (token != _generation)
            {
                return;
            }

            if (_holds > 0)
            {
                _holds--;
            }

            if (_holds == 0 && !_locked)
            {
                _left = _total; // a purchase or a recording is a grown-up action: the time starts again when it ends
            }
        }
    }

    /// <summary>Counts down <paramref name="elapsed"/> (not while held) and locks at zero with <see cref="LockReason.Timer"/>. Returns true when that tick locked.</summary>
    public bool Tick(TimeSpan elapsed)
    {
        lock (_gate)
        {
            if (_locked || _holds > 0)
            {
                return false;
            }

            _left -= elapsed;
            if (_left > TimeSpan.Zero)
            {
                return false;
            }

            LockCore(LockReason.Timer);
            return true;
        }
    }

    private void LockCore(LockReason reason)
    {
        EndHolds(); // also when already locked: a hold opened while locked (an alert or picker on the locked shelf) must not outlive the next unlock
        if (_locked)
        {
            return;
        }

        _locked = true;
        _lastReason = reason;
        _left = TimeSpan.Zero;
    }

    // Holds only count while unlocked: every lock and every unlock ends the open ones, so a token from before can no longer release.
    private void EndHolds()
    {
        _holds = 0;
        _generation++;
    }
}
