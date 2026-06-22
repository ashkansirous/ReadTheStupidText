using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Settings;

/// <summary>
/// Persists the user's preferences across sessions: the last-used reading speed
/// and whether read-aloud is enabled. Backed by OS-local storage.
/// </summary>
public interface ISettingsStore
{
    ReadingSpeed Speed { get; set; }

    bool IsEnabled { get; set; }
}
