using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>The one definition of the phone reader's "empty player" (Slice 164 follow-up): no text and nothing playing.</summary>
public static class EmptyPlayer
{
    public static bool Is(string? text, PlaybackState state) =>
        string.IsNullOrWhiteSpace(text) && state != PlaybackState.Playing;
}
