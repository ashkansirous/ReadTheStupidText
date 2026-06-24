using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Exposes the neural (Supertonic) voices to the UI — and only those, by design.
/// The list is empty until the model has downloaded, which the UI reads as a
/// "preparing voice" state; the built-in Windows voices are never offered for
/// selection (they are only an internal fallback while the model is missing).
/// </summary>
public sealed class NeuralVoiceCatalog : IVoiceCatalog
{
    private readonly IVoiceModelService _model;

    public NeuralVoiceCatalog(IVoiceModelService model) => _model = model;

    public IReadOnlyList<VoiceInfo> InstalledVoices =>
        _model.IsReady ? SupertonicVoiceTable.Voices : Array.Empty<VoiceInfo>();

    public VoiceInfo? DefaultVoice => _model.IsReady ? SupertonicVoiceTable.Default : null;
}
