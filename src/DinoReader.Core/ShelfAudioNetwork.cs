namespace DinoReader.Core;

/// <summary>How the grown-up answered the reading-voice request that came with adding a language (Slice 262).</summary>
public enum VoiceRequestOutcome
{
    /// <summary>The voice was already there (or no question was needed), so the audio follows the ordinary Wi-Fi rule.</summary>
    NothingAsked,

    /// <summary>The voice started without a question (Wi-Fi, or a platform with no mobile-data rule).</summary>
    Started,

    /// <summary>The grown-up chose "Download now": mobile data is allowed for this add.</summary>
    DownloadNow,

    /// <summary>The grown-up chose "Wait for Wi-Fi": the voice waits, and so does the audio.</summary>
    WaitForWifi,

    /// <summary>The grown-up cancelled the request: nothing starts, nothing waits.</summary>
    Cancelled,
}

/// <summary>The network decision of a shelf audio run, made once when the run starts and never re-read from the voice's live state (Slice 262).</summary>
public static class ShelfAudioNetwork
{
    /// <summary>A cancelled voice request starts no audio run at all.</summary>
    public static bool ShouldStartRun(VoiceRequestOutcome outcome) => outcome != VoiceRequestOutcome.Cancelled;

    /// <summary>Mobile data is allowed only when the grown-up chose "Download now" in this add. A quiet launch run passes <see cref="VoiceRequestOutcome.NothingAsked"/>.</summary>
    public static bool AllowsMobileData(VoiceRequestOutcome outcome) => outcome == VoiceRequestOutcome.DownloadNow;

    /// <summary>Whether a run waits for Wi-Fi: the phone has only mobile data and the run was not allowed it.</summary>
    public static bool NeedsWifi(bool allowMobileData, bool onMobileDataOnly) => !allowMobileData && onMobileDataOnly;
}
