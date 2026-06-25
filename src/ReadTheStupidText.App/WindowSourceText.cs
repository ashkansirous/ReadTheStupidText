using ReadTheStupidText.Domain.Activity;

namespace ReadTheStupidText_App;

/// <summary>
/// Composes the activity log's Source column from a <see cref="WindowSource"/>.
/// The domain carries the app and title separately; the UI joins them into the
/// "App — Title" label (e.g. "Chrome — Inbox - Gmail"), degrading gracefully when
/// either field is missing.
/// </summary>
internal static class WindowSourceText
{
    public static string ForDisplay(WindowSource? window)
    {
        if (window is null)
        {
            return string.Empty;
        }

        bool hasApp = !string.IsNullOrWhiteSpace(window.App);
        bool hasTitle = !string.IsNullOrWhiteSpace(window.Title);

        if (hasApp && hasTitle)
        {
            return $"{window.App} — {window.Title}";
        }

        return hasApp ? window.App : window.Title;
    }
}
