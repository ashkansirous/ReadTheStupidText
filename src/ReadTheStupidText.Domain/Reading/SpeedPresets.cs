namespace ReadTheStupidText.Domain.Reading;

/// <summary>
/// The quick-pick speeds offered by the native tray menu, which can't host a
/// slider. The control panel exposes the full <see cref="PlaybackRate"/> range
/// in <see cref="PlaybackRate.Step"/> increments; these are just the common
/// stops surfaced as menu items.
/// </summary>
public static class SpeedPresets
{
    public static IReadOnlyList<PlaybackRate> All { get; } =
    [
        new PlaybackRate(1.0),
        new PlaybackRate(1.25),
        new PlaybackRate(1.5),
        new PlaybackRate(1.75),
        new PlaybackRate(2.0),
    ];
}
