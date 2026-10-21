using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// The voices the picker offers (Slice 56): the installed pack's voices for the stored language,
/// then the labelled "Windows voice" (<see cref="SystemVoice.Id"/>) that always works, including
/// before any pack has arrived and after the user prefers it over the natural voice (Decision 61).
/// </summary>
public sealed class PackVoiceCatalog : IVoiceCatalog
{
    public const string WindowsVoiceName = "Windows voice";

    private static readonly VoiceInfo WindowsVoice = new(SystemVoice.Id, WindowsVoiceName, string.Empty);

    private readonly IVoiceModelService _model;

    public PackVoiceCatalog(IVoiceModelService model) => _model = model;

    public IReadOnlyList<VoiceInfo> InstalledVoices =>
        [.. _model.Paths?.AvailableVoices ?? [], WindowsVoice];

    // The natural voice once a pack is ready; until then the Windows voice is the only one.
    public VoiceInfo? DefaultVoice =>
        _model.Paths is { AvailableVoices.Count: > 0 } paths ? paths.ResolveVoice(null) : WindowsVoice;
}
