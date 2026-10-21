namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The text a read speaks, and whether it is a translation of what was intercepted (Slice 57).
/// <paramref name="Extends"/> says this is the same read with more text after the text already shown
/// (a streamed translation, Slice 75), not a new read: the reading text box keeps its place.
/// </summary>
public sealed record ReadText(string Text, bool Translated, bool Extends = false);
