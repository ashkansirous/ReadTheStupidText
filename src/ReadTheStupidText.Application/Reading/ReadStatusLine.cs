using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>Which status line the control panel shows (Slice 164); the UI maps it to its localized text.</summary>
public enum ReadStatusKind
{
    Ready,
    Paused,
    ReadingClipboard,
    ReadingSelection,
    ReadingSelectionFrom,
}

/// <summary>The status line to show and the app name it names, if any.</summary>
public readonly record struct ReadStatusLine(ReadStatusKind Kind, string? App)
{
    /// <summary>Clipboard reads name no window; selection reads name the foreground app when known.</summary>
    public static ReadStatusLine For(PlaybackState state, bool clipboardRead, string? app)
    {
        if (state == PlaybackState.Paused)
        {
            return new ReadStatusLine(ReadStatusKind.Paused, null);
        }

        if (state != PlaybackState.Playing)
        {
            return new ReadStatusLine(ReadStatusKind.Ready, null);
        }

        if (clipboardRead)
        {
            return new ReadStatusLine(ReadStatusKind.ReadingClipboard, null);
        }

        return string.IsNullOrWhiteSpace(app)
            ? new ReadStatusLine(ReadStatusKind.ReadingSelection, null)
            : new ReadStatusLine(ReadStatusKind.ReadingSelectionFrom, app);
    }
}
