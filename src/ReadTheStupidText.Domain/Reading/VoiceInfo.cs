namespace ReadTheStupidText.Domain.Reading;

/// <summary>
/// A narrator voice installed on the machine. Voices are an open,
/// machine-dependent set (unlike <see cref="ReadingSpeed"/>), so they are
/// modelled as a record keyed by <see cref="Id"/> rather than an enum.
/// <see cref="Engine"/> and <see cref="SpeakerId"/> say how a language pack's
/// voice is synthesized (Slice 51); the defaults are the Supertonic case.
/// </summary>
public sealed record VoiceInfo(
    string Id,
    string DisplayName,
    string Language,
    VoiceEngineKind Engine = VoiceEngineKind.Supertonic,
    int SpeakerId = 0);
