using ReadTheStupidText.Domain.Reading;

namespace DinoReader.Core;

/// <summary>Whether a storyteller character tells the story (a narrator, chosen on the Narrator page) or plays a character in it.</summary>
public enum CharacterKind
{
    Speaker,
    Narrator,
}

/// <summary>
/// A storyteller character (Slice 116, Decision 122): one of a language's roster. It is a real voice of that language's engine plus a
/// flavour (the pitch and pace of Slice 113), with a kid-friendly <see cref="Name"/> and a few plain <see cref="Hint"/> words written for the
/// language itself (never translated word for word). <see cref="Id"/> names the idea ("mouse", "grandpa-bear") and is the same in every
/// language; within one roster no two characters share a voice and flavour. The engine and its voice names are never shown to a child.
/// </summary>
public sealed record StorytellerCharacter(
    string Id, string VoiceId, VoiceFlavour Flavour, Gender Gender, CharacterKind Kind, string Name, string Hint);
