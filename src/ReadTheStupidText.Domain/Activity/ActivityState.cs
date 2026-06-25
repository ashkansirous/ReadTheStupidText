namespace ReadTheStupidText.Domain.Activity;

/// <summary>
/// The lifecycle of one intercepted piece of text, as surfaced in the activity
/// log. A single entry moves through these states in place.
/// </summary>
public enum ActivityState
{
    /// <summary>Captured; waiting out the debounce before it is read.</summary>
    Pending,

    /// <summary>Synthesizing the audio; nothing is playing yet.</summary>
    GeneratingAudio,

    /// <summary>Currently being spoken (audio is playing).</summary>
    Reading,

    /// <summary>Finished being spoken to the end.</summary>
    Read,

    /// <summary>Superseded by a newer selection during the debounce — never read.</summary>
    Ignored,

    /// <summary>A read in progress was stopped by a new selection or a deselect.</summary>
    Interrupted,

    /// <summary>Synthesis or playback failed.</summary>
    Failed,
}
