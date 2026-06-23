namespace ReadTheStupidText.Domain.Reading;

/// <summary>
/// A narrator voice installed on the machine. Voices are an open,
/// machine-dependent set (unlike <see cref="ReadingSpeed"/>), so they are
/// modelled as a record keyed by <see cref="Id"/> rather than an enum.
/// </summary>
public sealed record VoiceInfo(string Id, string DisplayName, string Language);
