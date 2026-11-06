namespace DinoReader.Core;

/// <summary>
/// The one clock of a silent piece's beat (Slice 278 part B, design in reviews/s278b-design.md). A beat exists for the current step (<see cref="Active"/>); the page can be covered
/// (<see cref="Hidden"/>: screen lock, another app, the grown-up gate) and the person can have paused it (<see cref="HeldByPerson"/>). The timer runs only while <see cref="Running"/>; every
/// change that makes it run again starts a whole beat. Each method returns what the owner must do with the timer, so no other flag has to be kept in step. Pure, so every order is unit-tested.
/// </summary>
public sealed class SilentBeat
{
    /// <summary>A beat exists for the step now shown.</summary>
    public bool Active { get; private set; }

    /// <summary>The page is covered. Stays as it is across beats and reads: it describes the page, not the beat.</summary>
    public bool Hidden { get; private set; }

    /// <summary>The person paused the beat. Only <see cref="Resume"/> (or the beat ending) clears it; covering or showing the page never does.</summary>
    public bool HeldByPerson { get; private set; }

    /// <summary>The timer should be running.</summary>
    public bool Running => Active && !Hidden && !HeldByPerson;

    /// <summary>A beat begins for a new piece. True when the timer should start now.</summary>
    public bool Begin()
    {
        Active = true;
        HeldByPerson = false;
        return Running;
    }

    /// <summary>The person pauses. True when a running timer must stop; false (and nothing held) when no beat exists.</summary>
    public bool Pause()
    {
        if (!Active)
        {
            return false;
        }

        bool wasRunning = Running;
        HeldByPerson = true;
        return wasRunning;
    }

    /// <summary>The person resumes. True when the timer should start now (a whole beat); false when the page is covered (it starts at <see cref="Show"/>) or nothing was held.</summary>
    public bool Resume()
    {
        if (!Active || !HeldByPerson)
        {
            return false;
        }

        HeldByPerson = false;
        return Running;
    }

    /// <summary>The page is covered. True when a running timer must stop.</summary>
    public bool Hide()
    {
        bool wasRunning = Running;
        Hidden = true;
        return wasRunning;
    }

    /// <summary>The page is shown again. True when the timer should start now (a whole beat again).</summary>
    public bool Show()
    {
        bool wasRunning = Running;
        Hidden = false;
        return !wasRunning && Running;
    }

    /// <summary>The beat ended, or the read was stopped or replaced: no beat exists. The page stays covered or shown as it was.</summary>
    public void End()
    {
        Active = false;
        HeldByPerson = false;
    }
}
