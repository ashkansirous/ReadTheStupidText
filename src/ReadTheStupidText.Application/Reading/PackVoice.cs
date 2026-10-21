namespace ReadTheStupidText.Application.Reading;

/// <summary>One voice a pack's <c>pack.json</c> offers: stable id, display name and the model's speaker id.</summary>
public sealed record PackVoice(string Id, string DisplayName, int SpeakerId);
