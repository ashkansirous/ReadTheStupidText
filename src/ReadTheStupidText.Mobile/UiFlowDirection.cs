using System.Globalization;

namespace ReadTheStupidText.Mobile;

/// <summary>Maps the current UI culture to a MAUI <see cref="FlowDirection"/> (Slice 53).</summary>
public static class UiFlowDirection
{
    public static FlowDirection ForCurrentCulture() =>
        IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    private static bool IsRightToLeft => UiCulture.IsRightToLeft(CultureInfo.CurrentUICulture);

    /// <summary>
    /// Swaps left and right for right-to-left languages. A <see cref="Thickness"/> is physical,
    /// not start/end, so a design that pads or indents one side must be mirrored by hand.
    /// </summary>
    public static Thickness Mirror(Thickness thickness) =>
        IsRightToLeft ? new Thickness(thickness.Right, thickness.Top, thickness.Left, thickness.Bottom) : thickness;
}
