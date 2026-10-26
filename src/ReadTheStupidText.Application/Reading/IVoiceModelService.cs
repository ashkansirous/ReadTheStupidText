using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The neural model's root directory plus what the engine needs to drive it (Slice 51):
/// the <see cref="Kind"/> of model, the <see cref="Language"/> it reads, and its
/// <see cref="Voices"/>. The synthesis engine knows its own file layout under the
/// directory; the defaults describe the single English Supertonic model Windows ships.
/// <see cref="PackVersion"/> is the installed pack's manifest version (empty for a model that
/// has no pack), so cached audio is not reused after a pack update.
/// </summary>
public sealed record VoiceModelPaths(
    string RootDir,
    VoiceEngineKind Kind = VoiceEngineKind.Supertonic,
    string Language = SupertonicVoiceTable.EnglishCode,
    IReadOnlyList<VoiceInfo>? Voices = null,
    string PackVersion = "",
    string ModelDir = "")
{
    /// <summary>
    /// The folder the engine loads <c>model.onnx</c> and <c>tokens.txt</c> from: the pack root, or
    /// the chosen voice's own folder inside it (Slice 89). Shared data such as the espeak-ng
    /// directory always stays at <see cref="RootDir"/>.
    /// </summary>
    public string ModelFolder => ModelDir.Length == 0 ? RootDir : Path.Combine(RootDir, ModelDir);

    /// <summary>These paths aimed at <paramref name="voice"/>'s model folder; the engine is rebuilt only when that folder differs.</summary>
    public VoiceModelPaths ForVoice(VoiceInfo voice) =>
        ModelDir == voice.ModelDir ? this : this with { ModelDir = voice.ModelDir };

    /// <summary>The voices this model offers for <see cref="Language"/>.</summary>
    public IReadOnlyList<VoiceInfo> AvailableVoices => Voices ?? SupertonicVoiceTable.Voices;

    /// <summary>
    /// The voice <paramref name="voiceId"/> names, or this model's default voice (the Supertonic
    /// default for Supertonic, else its first) when the id belongs to another engine or
    /// language, so a stored choice never leaves the engine without a speaker.
    /// </summary>
    public VoiceInfo ResolveVoice(string? voiceId)
    {
        return Find(voiceId) ?? DefaultVoice();
    }

    /// <summary>
    /// The voice <paramref name="voiceId"/> names, or null when this model does not offer it. A Kokoro storyteller model
    /// speaks a stored Supertonic pick as the Kokoro voice of the same gender that <see cref="KokoroVoiceTable.MapFromSupertonic"/>
    /// picks (Slice 114), so different picks stay different voices while the pack is installed and the pick itself is kept.
    /// </summary>
    public VoiceInfo? Find(string? voiceId)
    {
        VoiceInfo? match = AvailableVoices.FirstOrDefault(v => v.Id == voiceId);
        if (match is not null || Kind != VoiceEngineKind.Kokoro)
        {
            return match;
        }

        string? mappedId = KokoroVoiceTable.MapFromSupertonic(voiceId, Language)?.Id;
        return mappedId is null ? null : AvailableVoices.FirstOrDefault(v => v.Id == mappedId);
    }

    // The Supertonic default when this is a Supertonic model that still lists it, the Persian
    // default (Slice 144) when this is the Persian Piper pack and lists mana, else the first listed voice; a model that lists none (a malformed pack) reads as speaker 0
    // rather than throwing.
    private VoiceInfo DefaultVoice()
    {
        IReadOnlyList<VoiceInfo> voices = AvailableVoices;
        VoiceInfo? preferred = Kind switch
        {
            VoiceEngineKind.Supertonic => voices.FirstOrDefault(v => v.SpeakerId == SupertonicVoiceTable.DefaultSpeakerId),
            VoiceEngineKind.Vits when Language == VoiceChoices.PersianLanguageCode => voices.FirstOrDefault(v => v.Id == VoiceChoices.PersianDefaultVoiceId),
            _ => null,
        };
        return preferred ?? voices.FirstOrDefault() ?? new VoiceInfo(string.Empty, string.Empty, Language, Kind);
    }
}

/// <summary>
/// Locates the local neural voice model, reports readiness, and exposes its directory.
/// The synthesis engine stays silent on the neural path until <see cref="IsReady"/> is true.
/// </summary>
public interface IVoiceModelService
{
    /// <summary>Whether the model is present and ready to synthesize.</summary>
    bool IsReady { get; }

    /// <summary>The model directory once <see cref="IsReady"/> is true; otherwise null.</summary>
    VoiceModelPaths? Paths { get; }

    /// <summary>Raised whenever readiness or the resolved model changes (a pack finishes
    /// installing, is removed, or the language moves to another pack).</summary>
    event EventHandler? ReadyChanged;

    /// <summary>
    /// Locates the model and marks the service ready. Safe to call once at startup;
    /// leaves <see cref="IsReady"/> false if the model files are missing. The
    /// <paramref name="progress"/> parameter is unused for a packaged model.
    /// </summary>
    Task InitializeAsync(IProgress<double>? progress = null);
}
