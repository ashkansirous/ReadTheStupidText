namespace ReadTheStupidText.Application.Images;

/// <summary>One line of text and where it sits on its page (Slice 63).</summary>
public sealed record TextLine(string Text, NormalizedRect Box);
