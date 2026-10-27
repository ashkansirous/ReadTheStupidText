using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>When a started read's "first audio pending" state ends (Slice 164).</summary>
public static class PendingReadSettle
{
    /// <summary>True on Playing, or on an Idle that arrives after the speak call returned for the
    /// pending read (a short text finished before the Playing event was handled).</summary>
    public static bool IsDone(PlaybackState state, int pendingReadId, int returnedReadId)
    {
        bool idleAfterReturn = state == PlaybackState.Idle
            && pendingReadId != 0 && pendingReadId == returnedReadId;
        return state == PlaybackState.Playing || idleAfterReturn;
    }
}
