using ReadTheStupidText.Domain.Activity;

namespace ReadTheStupidText_App;

/// <summary>
/// Maps the <see cref="ActivityState"/> enum to the label shown in the activity
/// log's State column. The domain owns the state; the UI composes the copy (so
/// the multi-word states read naturally, e.g. "Generating audio").
/// </summary>
internal static class ActivityStateText
{
    public static string ForDisplay(ActivityState state) => state switch
    {
        ActivityState.Pending => UiStrings.Get("State_Pending"),
        ActivityState.GeneratingAudio => UiStrings.Get("State_GeneratingAudio"),
        ActivityState.Reading => UiStrings.Get("State_Reading"),
        ActivityState.Read => UiStrings.Get("State_Read"),
        ActivityState.Ignored => UiStrings.Get("State_Ignored"),
        ActivityState.Interrupted => UiStrings.Get("State_Interrupted"),
        ActivityState.Failed => UiStrings.Get("State_Failed"),
        _ => state.ToString(),
    };
}
