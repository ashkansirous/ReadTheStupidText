using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Domain.Sanitizing;

namespace ReadTheStupidText.Application.Settings;

/// <summary>
/// Persists the user's preferences across sessions: the last-used reading speed,
/// the two independent auto-read gates, and the chosen narrator voice. Backed by
/// OS-local storage.
/// </summary>
public interface ISettingsStore
{
    PlaybackRate Speed { get; set; }

    /// <summary>
    /// Whether selecting text in a UIA-aware app auto-reads it (gates the
    /// selection monitor). Defaults on. The global hotkey is unaffected.
    /// </summary>
    bool AutoReadOnSelection { get; set; }

    /// <summary>
    /// Whether copying text auto-reads it (gates the clipboard monitor — the path
    /// for the console and other apps with no UIA selection). Defaults on. The
    /// global hotkey is unaffected.
    /// </summary>
    bool AutoReadOnCopy { get; set; }

    /// <summary>
    /// The chosen narrator voice id, or null to use the system default. Stored
    /// by id so it survives across sessions; a no-longer-installed id falls back
    /// to the default when applied.
    /// </summary>
    string? VoiceId { get; set; }

    /// <summary>
    /// Which text-sanitizer categories run before a read (and before logging).
    /// A flags value; defaults to <see cref="SanitizerCategory.All"/>. Persisted
    /// so the user's choices survive across sessions.
    /// </summary>
    SanitizerCategory EnabledSanitizers { get; set; }
}
