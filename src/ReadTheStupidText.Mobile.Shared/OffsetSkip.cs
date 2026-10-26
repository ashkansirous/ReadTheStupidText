namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// Character-offset skip maths shared by the platform speech engines that cannot seek
/// (Android <c>TextToSpeech</c>, iOS <c>AVSpeechSynthesizer</c>): a skip re-speaks the
/// rest of the text from an offset, snapped to a word start. Best-effort, not
/// sample-accurate.
/// </summary>
public static class OffsetSkip
{
    // Rough average speaking rate at normal speed; sizes a ~10s skip only.
    private const float CharsPerSecondAtNormalRate = 15f;
    private const int SkipSeconds = 10;

    /// <summary>How many characters a ~10 second skip covers at the given playback rate.</summary>
    public static int SkipChars(double rate) => (int)(CharsPerSecondAtNormalRate * rate * SkipSeconds);

    /// <summary>Keeps the offset inside the text (at most its last character).</summary>
    public static int ClampOffset(string text, int offset) =>
        Math.Clamp(offset, 0, Math.Max(text.Length - 1, 0));

    /// <summary>Moves a mid-word offset forward to the start of the next word.</summary>
    public static int SnapToWordStart(string text, int offset)
    {
        if (offset <= 0 || offset >= text.Length)
        {
            return offset;
        }

        int nextSpace = text.IndexOf(' ', offset);
        return nextSpace < 0 ? text.Length : nextSpace + 1;
    }
}
