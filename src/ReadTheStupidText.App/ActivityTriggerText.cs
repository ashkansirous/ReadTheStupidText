using ReadTheStupidText.Domain.Activity;

namespace ReadTheStupidText_App;

/// <summary>Maps the <see cref="ActivityTrigger"/> enum to the label in the activity log's Trigger column.</summary>
internal static class ActivityTriggerText
{
    public static string ForDisplay(ActivityTrigger trigger) => trigger switch
    {
        ActivityTrigger.AutoRead => UiStrings.Get("Trigger_AutoRead"),
        ActivityTrigger.Hotkey => UiStrings.Get("Trigger_Hotkey"),
        ActivityTrigger.Manual => UiStrings.Get("Trigger_Manual"),
        ActivityTrigger.Clipboard => UiStrings.Get("Trigger_Clipboard"),
        ActivityTrigger.FileUpload => UiStrings.Get("Trigger_FileUpload"),
        _ => trigger.ToString(),
    };
}
