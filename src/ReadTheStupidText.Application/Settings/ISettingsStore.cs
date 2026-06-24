using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Settings;

/// <summary>
/// Persists the user's preferences across sessions: the last-used reading speed,
/// whether read-aloud is enabled, and the chosen narrator voice. Backed by
/// OS-local storage.
/// </summary>
public interface ISettingsStore
{
    PlaybackRate Speed { get; set; }

    bool IsEnabled { get; set; }

    /// <summary>
    /// The chosen narrator voice id, or null to use the system default. Stored
    /// by id so it survives across sessions; a no-longer-installed id falls back
    /// to the default when applied.
    /// </summary>
    string? VoiceId { get; set; }
}
