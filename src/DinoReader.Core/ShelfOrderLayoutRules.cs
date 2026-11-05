namespace DinoReader.Core;

/// <summary>
/// Small pure layout and press rules of the shelf reorder mode (Slice 264), kept out of the views so they are unit-tested.
/// </summary>
public static class ShelfOrderLayoutRules
{
    /// <summary>The most a platform's layout rounding moves a width between its measure and its arrange pass (WinUI measures 189.6 as 189 and arranges it as 190).</summary>
    public const double LayoutRoundingDp = 1.0;

    /// <summary>The most a finger or mouse may drift from where a press began, in dp, and still count as a hold.</summary>
    public const double HoldSlopDp = 12;

    /// <summary>A tile width as a whole number of dp, so measure and arrange agree after layout rounding.</summary>
    public static double WholeDp(double width) => Math.Max(1, Math.Floor(width));

    /// <summary>The remeasure threshold of Android and iOS, in dp: below any real width change, as before Slice 264.</summary>
    public const double PhoneRemeasureDp = 0.5;

    /// <summary>Whether an arranged width differs from the measured one by more than the platform's threshold, so it must be measured again. Only the Windows head uses the layout-rounding threshold: a difference of rounding alone loops forever there (a layout cycle); Android and iOS keep the finer one.</summary>
    public static bool NeedsRemeasure(double arrangedWidth, double measuredWidth, bool isWindows) =>
        Math.Abs(arrangedWidth - measuredWidth) > (isWindows ? LayoutRoundingDp : PhoneRemeasureDp);

    /// <summary>Whether a press has moved far enough from its start to be a scroll or a swipe instead of a hold.</summary>
    public static bool MovedPastHoldSlop(double dx, double dy) =>
        Math.Max(Math.Abs(dx), Math.Abs(dy)) > HoldSlopDp;
}
