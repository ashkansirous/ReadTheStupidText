using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Whether picking a voice may speak a sample of it. The sample is a read of its own on the shared reader,
/// so while a real read is playing or paused it would supersede that read (and end it when the sample
/// finishes) instead of letting the reader swap to the new voice at the next chunk.
/// </summary>
public static class VoicePreviewPolicy
{
    public static bool ShouldPreview(PlaybackState readerState) => readerState == PlaybackState.Idle;
}
