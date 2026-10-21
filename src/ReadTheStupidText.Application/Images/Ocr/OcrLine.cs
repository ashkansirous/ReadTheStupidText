namespace ReadTheStupidText.Application.Images;

/// <summary>One recognized line of text and where it sits on the page (kept for Slice 63).</summary>
public sealed record OcrLine(string Text, OcrBox Box);
