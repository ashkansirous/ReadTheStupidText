namespace DinoReader.Core;

/// <summary>Whether a shelf audio run starts (Slice 262).</summary>
public enum ShelfAudioStartDecision
{
    Start,
    Skip,
}

/// <summary>
/// The one rule for starting a shelf audio run, so the launch check and the grown-up's add cannot disagree (Slice 262). Slice 360 (Decision 3): a book's audio is fetched on any network, so there is no
/// Wi-Fi rule and no "download on mobile data?" question any more.
/// </summary>
public static class ShelfAudioStart
{
    /// <summary>
    /// A quiet run (launch, app returns) starts for every family language the grown-up has not cancelled (Slice 267: the audio follows the languages, not the reading voice). A visible run (the grown-up
    /// added the language) always starts.
    /// </summary>
    public static ShelfAudioStartDecision Decide(bool cancelled, bool visible) =>
        visible || !cancelled ? ShelfAudioStartDecision.Start : ShelfAudioStartDecision.Skip;
}
