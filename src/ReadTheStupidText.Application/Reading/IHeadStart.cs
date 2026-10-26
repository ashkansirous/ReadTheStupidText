namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// An engine that can synthesize the opening sentences of a text ahead of time so Play starts
/// almost at once (Slice 47, Decision 66). Separate from <see cref="ISpeechReader"/> because
/// only the neural engine has audio worth preparing: the system-voice readers need nothing.
/// </summary>
public interface IHeadStart
{
    /// <summary>
    /// Synthesizes the first sentences of <paramref name="text"/> into the cache, in the voice
    /// that would read them now, without playing anything. Best effort, and truthful: the task
    /// completes normally only when the clips are stored; with no neural engine ready, a read in progress, or a
    /// superseded or cancelled preparation it ends cancelled (see <see cref="HeadStartRun"/>).
    /// </summary>
    Task PrepareAsync(string text);

    /// <summary>True from when a read starts until it ends, including the wait for its first
    /// audio, when the playback state is still idle. A prepare is skipped while it is set.</summary>
    bool ReadInProgress { get; }

    /// <summary>Stops any preparation in flight. Audio already cached stays.</summary>
    void CancelPrepare();
}
