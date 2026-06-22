namespace ReadTheStupidText.Domain.Reading;

/// <summary>
/// The reader's high-level state, surfaced to the UI so the tray flyout can
/// reflect whether speech is idle, currently playing, or paused.
/// </summary>
public enum PlaybackState
{
    Idle,
    Playing,
    Paused,
}
