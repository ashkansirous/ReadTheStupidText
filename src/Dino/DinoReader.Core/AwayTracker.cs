namespace DinoReader.Core;

/// <summary>
/// Measures how long the app was away while a grown-up page was deliberately kept open (the file
/// picker). A quick pick comes back within the threshold and the page stays; a long absence
/// (Home pressed, another app) means the grown-up walked away and the app returns to the gallery.
/// </summary>
public sealed class AwayTracker
{
    /// <summary>How long a picker session may last before the grown-up area is closed.</summary>
    public static readonly TimeSpan DefaultThreshold = TimeSpan.FromMinutes(2);

    /// <summary>The longest a window may stay away under the picker deferral; past it the reset happens anyway.</summary>
    public static readonly TimeSpan ExternalActivityCap = TimeSpan.FromMinutes(10);

    private readonly TimeSpan _threshold;
    private DateTimeOffset? _stoppedAt;

    public AwayTracker(TimeSpan? threshold = null) => _threshold = threshold ?? DefaultThreshold;

    /// <summary>The window stopped; the earliest stop of a session is the one kept.</summary>
    public void Stopped(DateTimeOffset now) => _stoppedAt ??= now;

    /// <summary>The window is back or the session ended. True when the absence was too long; clears the stop either way.</summary>
    public bool ReturnedTooLate(DateTimeOffset now)
    {
        bool tooLate = _stoppedAt is { } at && now - at > _threshold;
        _stoppedAt = null;
        return tooLate;
    }

    public void Clear() => _stoppedAt = null;
}
