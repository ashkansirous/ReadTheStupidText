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
        ActivityState.GeneratingAudio => "Generating audio",
        _ => state.ToString(),
    };
}
