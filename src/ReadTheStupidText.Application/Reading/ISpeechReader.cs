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

    PlaybackState State { get; }

    /// <summary>Synthesizes <paramref name="text"/> and begins playback.</summary>
    Task SpeakAsync(string text);

    void Pause();

    void Resume();

    /// <summary>Applies a new speed live, without restarting playback.</summary>
    void SetSpeed(ReadingSpeed speed);
}
