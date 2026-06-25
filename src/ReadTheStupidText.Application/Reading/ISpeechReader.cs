using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Speaks text aloud and exposes live playback control. The implementation
/// (Infrastructure) owns the synthesis and audio engine; the Application and
/// UI layers only ever see this contract.
/// </summary>
public interface ISpeechReader
{
    /// <summary>Raised whenever <see cref="State"/> changes.</summary>
    event EventHandler<PlaybackState>? StateChanged;

    /// <summary>Raised once when an utterance finishes playing to its natural end
    /// (not when it is stopped/superseded), so the caller can mark it complete.</summary>
    event EventHandler? Completed;

    PlaybackState State { get; }

    /// <summary>
    /// Synthesizes <paramref name="text"/> and begins playback. Starting a new
    /// utterance cancels any in-flight synthesis/playback of a previous one, so a
    /// superseded (e.g. long) read can never play after the one that replaced it.
    /// </summary>
    Task SpeakAsync(string text);

    void Pause();

    void Resume();

    /// <summary>Stops playback immediately, cancels any in-flight synthesis, and
    /// returns to idle. Unlike <see cref="Pause"/>, the utterance cannot resume.</summary>
    void Stop();

    /// <summary>Applies a new speed live, without restarting playback.</summary>
    void SetSpeed(PlaybackRate speed);

    /// <summary>
    /// Selects the narrator voice (by <see cref="VoiceInfo.Id"/>) for the next
    /// read. A voice cannot be swapped mid-utterance; an unknown id is ignored
    /// (the current voice is kept).
    /// </summary>
    void SetVoice(string voiceId);
}
