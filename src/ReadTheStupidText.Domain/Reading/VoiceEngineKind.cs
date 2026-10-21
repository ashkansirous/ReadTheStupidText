namespace ReadTheStupidText.Domain.Reading;

/// <summary>
/// Which sherpa-onnx model family synthesizes a voice (Slice 51, Decision 63).
/// A closed set: each kind has its own config shape and its own pack file layout.
/// </summary>
public enum VoiceEngineKind
{
    /// <summary>Supertonic-3: multilingual, the language is a generation option.</summary>
    Supertonic,

    /// <summary>VITS (Piper): one language per model, phonemized by espeak-ng data.</summary>
    Vits,

    /// <summary>MeloTTS (VITS with a lexicon and a jieba dictionary): Chinese + English.</summary>
    Melo,
}
