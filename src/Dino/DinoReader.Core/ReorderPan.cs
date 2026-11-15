namespace DinoReader.Core;

/// <summary>
/// When a touch on a cover in the reorder mode becomes a drag (Slice 382, no MAUI): in edit mode a touch that moves further than <see cref="ThresholdDp"/> starts the drag at once, with no hold;
/// a touch that stays under it is a tap (which lifts). Also where the finger is on the grid surface, so the view can place the dragged cover and the lit spot while the grid scrolls under the finger.
/// </summary>
public static class ReorderPan
{
    /// <summary>How far the finger moves, in dp, before the touch is a drag and not a tap. About the platforms' own touch slop (8 dp on Android, 10 pt on iOS).</summary>
    public const double ThresholdDp = 8;

    /// <summary>
    /// The drag threshold in dp: never below the platform's own touch slop, so by the time a drag starts the platform's tap detection (Android's GestureDetector under MAUI's tap recognizer)
    /// has already given up on a tap and no stray tap follows the drag. A slop of 0 (iOS, unknown) leaves <see cref="ThresholdDp"/>.
    /// </summary>
    public static double ThresholdFor(double platformSlopDp) => Math.Max(ThresholdDp, platformSlopDp);

    /// <summary>True when the finger has moved far enough from where it touched down to start a drag.</summary>
    public static bool StartsDrag(double dx, double dy, double thresholdDp = ThresholdDp) => dx * dx + dy * dy >= thresholdDp * thresholdDp;

    /// <summary>
    /// The finger's position on the grid surface: where the dragged cover sat on it (<paramref name="coverX"/>, <paramref name="coverY"/>) plus where inside the cover the finger touched,
    /// plus how far the finger has moved on screen since the drag began, plus how far the grid has scrolled since then (the surface moves up under a finger that stays put).
    /// </summary>
    public static (double X, double Y) FingerOnSurface(double coverX, double coverY, double touchInCoverX, double touchInCoverY, double dx, double dy, double scrolledY) =>
        (coverX + touchInCoverX + dx, coverY + touchInCoverY + dy + scrolledY);
}
