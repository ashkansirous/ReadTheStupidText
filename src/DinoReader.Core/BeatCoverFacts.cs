namespace DinoReader.Core;

/// <summary>
/// The two facts the silent beat's cover is made of (Slice 322, second design): whether the reading page is shown, and whether its window is in front. The page feeds every event in and passes
/// <see cref="Hidden"/> to <see cref="SilentBeat.SetHidden"/> afterwards, so no event order can leave a stale half behind. Pure, so every order is unit-tested.
/// </summary>
public sealed class BeatCoverFacts
{
    /// <summary>The page is on screen (Page.Appearing raised, Disappearing not since).</summary>
    public bool PageShown { get; private set; }

    /// <summary>The window is in front (not stopped by a screen lock, Home or another app). True until a window says otherwise: platforms with no such state never stop.</summary>
    public bool WindowInForeground { get; private set; } = true;

    /// <summary>The state before any event: the page is not shown yet, so the cover is on. <see cref="SilentBeat"/> is created in the same state, so a beat begun before the first Appearing does not run.</summary>
    public static bool StartsHidden => new BeatCoverFacts().Hidden;

    /// <summary>The page cannot be seen: not shown, or its window is not in front.</summary>
    public bool Hidden => !PageShown || !WindowInForeground;

    /// <summary>Why the page is hidden: the page itself is not shown, or (it is shown but) its window is not in front. None while it can be seen.</summary>
    public BeatCoverCause Cause => !PageShown ? BeatCoverCause.Page : !WindowInForeground ? BeatCoverCause.Window : BeatCoverCause.None;

    /// <summary>Page.Appearing.</summary>
    public void Appeared() => PageShown = true;

    /// <summary>Page.Disappearing.</summary>
    public void Disappeared() => PageShown = false;

    /// <summary>Window.Stopped (Android onStop, iOS DidEnterBackground).</summary>
    public void WindowStopped() => WindowInForeground = false;

    /// <summary>Window.Resumed (Android onRestart, iOS WillEnterForeground).</summary>
    public void WindowResumed() => WindowInForeground = true;

    /// <summary>Window.Activated (Android onResume, iOS OnActivated, Windows focus). Also clears the stop: an Android activity recreated while stopped gets onStart/onResume but no onRestart, so Resumed never fires.</summary>
    public void WindowActivated() => WindowInForeground = true;

    /// <summary>The window's real current state, read from the platform ONLY when the page subscribes to a window (first watch or a swap), where no event will arrive; never inside Stopped/Resumed handlers, whose events win. Null means the platform could not tell: the fact stays as it was.</summary>
    public void WindowWatched(bool? inForeground) => WindowInForeground = inForeground ?? WindowInForeground;
}

/// <summary>What makes the silent beat's cover hidden (named in the log so a lock is told from a page leaving).</summary>
public enum BeatCoverCause
{
    None,
    Page,
    Window,
}
