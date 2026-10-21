using ReadTheStupidText.Domain.Activity;

namespace ReadTheStupidText_App;

/// <summary>
/// Maps the <see cref="ActivityReason"/> enum to the short label shown in the
/// activity log's Reason column. The domain returns the enum; the UI composes the
/// user-facing copy here (no display strings leak into the lower layers).
/// </summary>
internal static class ActivityReasonText
{
    public static string ForDisplay(ActivityReason reason) => reason switch
    {
        ActivityReason.NewSelection => UiStrings.Get("Reason_NewSelection"),
        ActivityReason.Deselected => UiStrings.Get("Reason_Deselected"),
        ActivityReason.Error => UiStrings.Get("Reason_Error"),
        _ => string.Empty,
    };
}
