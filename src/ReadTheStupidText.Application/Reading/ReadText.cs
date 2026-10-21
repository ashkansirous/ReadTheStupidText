namespace ReadTheStupidText.Application.Reading;

/// <summary>The text a read speaks, and whether it is a translation of what was intercepted (Slice 57).</summary>
public sealed record ReadText(string Text, bool Translated);
