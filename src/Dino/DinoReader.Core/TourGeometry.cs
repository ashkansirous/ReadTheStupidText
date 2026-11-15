namespace DinoReader.Core;

/// <summary>A rectangle in dp, in whichever space the caller says (the screen, or a layer's own).</summary>
public readonly record struct TourRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

/// <summary>
/// Where the first-run tour draws its copies (Slice 381). The real tabs and pills sit in a page that draws under the status bar and pads itself by the inset, so summing layout offsets up the parents
/// misses the inset and the copy lands a status-bar height below its target. Both the target and the tour's own layer are read in screen space; a copy's place in the layer is the difference.
/// </summary>
public static class TourGeometry
{
    /// <summary>The copy is drawn this many dp larger than its target on every side, so it reads as lifted.</summary>
    public const double LiftGrow = 6;

    /// <summary>How far (dp) a copy may sit from its target before the device check calls it misplaced.</summary>
    public const double Tolerance = 4;

    /// <summary>The target's rectangle in the layer's own coordinates, from both origins on the screen.</summary>
    public static TourRect InLayer(TourRect targetOnScreen, double layerScreenX, double layerScreenY) =>
        new(targetOnScreen.X - layerScreenX, targetOnScreen.Y - layerScreenY, targetOnScreen.Width, targetOnScreen.Height);

    /// <summary>The copy's rectangle for a target: <see cref="LiftGrow"/> larger on every side.</summary>
    public static TourRect LiftedCopy(TourRect target) =>
        new(target.X - LiftGrow, target.Y - LiftGrow, target.Width + (2 * LiftGrow), target.Height + (2 * LiftGrow));

    /// <summary>How far the lifted copy's centre is from the target's centre (dp): zero when it stands exactly over it.</summary>
    public static (double Dx, double Dy) CentreOffset(TourRect copyOnScreen, TourRect targetOnScreen) =>
        ((copyOnScreen.X + (copyOnScreen.Width / 2)) - (targetOnScreen.X + (targetOnScreen.Width / 2)),
         (copyOnScreen.Y + (copyOnScreen.Height / 2)) - (targetOnScreen.Y + (targetOnScreen.Height / 2)));

    /// <summary>True when the copy's centre is within <see cref="Tolerance"/> of the target's on both axes.</summary>
    public static bool IsAligned(TourRect copyOnScreen, TourRect targetOnScreen)
    {
        (double dx, double dy) = CentreOffset(copyOnScreen, targetOnScreen);
        return Math.Abs(dx) <= Tolerance && Math.Abs(dy) <= Tolerance;
    }
}
