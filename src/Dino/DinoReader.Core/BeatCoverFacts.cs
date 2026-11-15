namespace DinoReader.Core;

/// <summary>
/// The fact the silent beat's cover is made of (Slice 322, reduced by Slice 383): whether the reading page is shown. The page feeds every event in and passes <see cref="Hidden"/> to <see cref="SilentBeat.SetHidden"/>
/// afterwards. The window being stopped (screen lock, screen off, Home, another app) is not part of it: a book that is playing keeps reading to the end with the screen dark. Pure, so every order is unit-tested.
/// </summary>
public sealed class BeatCoverFacts
{
    /// <summary>The page is on screen (Page.Appearing raised, Disappearing not since).</summary>
    public bool PageShown { get; private set; }

    /// <summary>The state before any event: the page is not shown yet, so the cover is on. <see cref="SilentBeat"/> is created in the same state, so a beat begun before the first Appearing does not run.</summary>
    public static bool StartsHidden => new BeatCoverFacts().Hidden;

    /// <summary>The page is covered: not shown (a gate or sheet over it, the page leaving). Only this pauses a silent beat.</summary>
    public bool Hidden => !PageShown;

    /// <summary>Page.Appearing.</summary>
    public void Appeared() => PageShown = true;

    /// <summary>Page.Disappearing.</summary>
    public void Disappeared() => PageShown = false;
}
