namespace ReadTheStupidText.Application.Reading;

/// <summary>What Play does about the voice for the text it is about to read (Slice 82).</summary>
public enum VoiceRoute
{
    Read,
    Downloading,
    NoVoice,
}

/// <summary>
/// The pure routing rules of Slice 82 (Decision 89): which language a read is in, which voice
/// reads it, and what to tell the user when none can. An unknown phone (null) never blocks.
/// </summary>
public static class VoiceRouting
{
    /// <summary>
    /// Whether the stored "Phone voice" choice must give way to the natural voice: the phone
    /// can't read the language (known) and a pack is ready to.
    /// </summary>
    public static bool ShouldLeavePhoneVoice(bool phoneVoiceChosen, bool packReady, bool? phoneCanRead) =>
        phoneVoiceChosen && packReady && phoneCanRead == false;

    /// <summary>Whether the phone voice is the one that would speak.</summary>
    public static bool PhoneReads(bool phoneVoiceChosen, bool packReady) => phoneVoiceChosen || !packReady;

    public static VoiceRoute Route(bool phoneReads, bool? phoneCanRead, bool packDownloading)
    {
        if (!phoneReads || phoneCanRead != false)
        {
            return VoiceRoute.Read;
        }

        return packDownloading ? VoiceRoute.Downloading : VoiceRoute.NoVoice;
    }
}
