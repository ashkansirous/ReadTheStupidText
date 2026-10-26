namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// What the user is waiting for (Slice 143, Decision 149). Shared by both MAUI apps: the shared wait overlay shows the
/// app's own words for the stage, and usage counts (<c>wait_shown</c>) name it. A closed set, so it is an enum.
/// </summary>
public enum WaitStage
{
    /// <summary>The voice or the first audio of a read is not ready yet.</summary>
    PreparingVoice,

    /// <summary>A read that had started stopped for want of the next chunk.</summary>
    Stalled,

    /// <summary>A language model is downloading.</summary>
    DownloadingModel,

    /// <summary>Text is being translated.</summary>
    Translating,

    /// <summary>Pictures or PDF pages are being read for their text.</summary>
    ReadingPictures,

    /// <summary>Something is being written to the device (a recording, a story).</summary>
    Saving,

    /// <summary>A file (a backup, a book) is being read or written.</summary>
    GettingFile,
}

/// <summary>
/// The pure timing rule of the wait overlay: nothing shows for the first <see cref="ShowAfter"/> of a wait (so a short
/// wait never flashes), the overlay then shows until the wait ends (first audio, finish, cancel). The clock is passed
/// in, so the rule is testable; the shared overlay owns the real timer. Not thread-safe: use it from the UI thread.
/// </summary>
public sealed class WaitGate
{
    /// <summary>How long a wait must last before it is shown (Decision 146).</summary>
    public static readonly TimeSpan ShowAfter = TimeSpan.FromMilliseconds(500);

    private DateTimeOffset _began;

    /// <summary>True from <see cref="Begin"/> until <see cref="End"/>.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The stage of the wait in progress; meaningful only while <see cref="IsActive"/>.</summary>
    public WaitStage Stage { get; private set; }

    /// <summary>Starts a wait. A second Begin while one runs only changes its stage: the clock keeps running.</summary>
    public void Begin(WaitStage stage, DateTimeOffset now)
    {
        if (!IsActive)
        {
            IsActive = true;
            _began = now;
        }

        Stage = stage;
    }

    /// <summary>True once the wait has lasted <see cref="ShowAfter"/>; false when no wait runs.</summary>
    public bool IsVisible(DateTimeOffset now)
    {
        if (!IsActive || now - _began < ShowAfter)
        {
            return false;
        }

        return true;
    }

    /// <summary>How long until the overlay should show; zero when it is due (or no wait runs).</summary>
    public TimeSpan TimeUntilVisible(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return TimeSpan.Zero;
        }

        TimeSpan left = ShowAfter - (now - _began);
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    /// <summary>Ends the wait. Returns how long the overlay was showing, or null when it never showed (a short wait).</summary>
    public TimeSpan? End(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return null;
        }

        IsActive = false;
        TimeSpan shown = now - _began - ShowAfter;
        return shown >= TimeSpan.Zero ? shown : null;
    }
}

/// <summary>
/// The pure rules of the Windows wait indicator (Slice 141): which stage a read that has not yet made a sound is in, and the
/// percentage a pack download shows. The windows only compose the words.
/// </summary>
public static class ReadWaitStages
{
    /// <summary>A pack download comes first, then the translation; once the text to speak is ready only the voice is left.</summary>
    public static WaitStage Pick(bool downloadingPack, bool translating)
    {
        if (downloadingPack)
        {
            return WaitStage.DownloadingModel;
        }

        return translating ? WaitStage.Translating : WaitStage.PreparingVoice;
    }

    /// <summary>A download's 0..1 fraction as a whole percent, clamped to 0..100; NaN counts as 0.</summary>
    public static int Percent(double fraction)
    {
        if (double.IsNaN(fraction))
        {
            return 0;
        }

        return (int)Math.Round(Math.Clamp(fraction, 0, 1) * 100);
    }
}
