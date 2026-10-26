namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// One run of a story read in one voice (Slice 60): the text to speak, the id of the voice
/// that speaks it (a <c>VoiceInfo.Id</c>), and the span of the whole story it came from.
/// A neutral type so any caller (Dino Reader maps its segments and cast onto it) can ask
/// for a multi-voice read without the Application layer knowing about characters.
/// <see cref="SourceStart"/>/<see cref="SourceEnd"/> may be wider than <see cref="Text"/>
/// (a quote's range includes its quote marks); <see cref="TextStart"/> is the story index
/// where <see cref="Text"/> itself begins (one past a quote's opening mark). <see cref="Transform"/> (Slice 113, 129)
/// is Dino Reader's opaque voice shaping (pitch, pace); a plain read and RTST pass none.
/// </summary>
public readonly record struct VoicedSegment(
    string Text, string VoiceId, int SourceStart, int SourceEnd, int TextStart, IVoicedAudioTransform? Transform = null)
{
    /// <summary>The segments' texts joined with a space: what a single-voice engine reads.</summary>
    public static string JoinText(IReadOnlyList<VoicedSegment> segments) =>
        string.Join(' ', segments.Select(s => s.Text));
}
