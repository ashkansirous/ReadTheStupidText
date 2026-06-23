using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using Windows.Media.SpeechSynthesis;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Enumerates the installed Windows speech voices via
/// <see cref="SpeechSynthesizer.AllVoices"/> and maps each to a
/// <see cref="VoiceInfo"/>.
/// </summary>
public sealed class WinRtVoiceCatalog : IVoiceCatalog
{
    public IReadOnlyList<VoiceInfo> InstalledVoices =>
        SpeechSynthesizer.AllVoices.Select(ToVoiceInfo).ToList();

    public VoiceInfo? DefaultVoice =>
        SpeechSynthesizer.DefaultVoice is { } voice ? ToVoiceInfo(voice) : null;

    private static VoiceInfo ToVoiceInfo(VoiceInformation voice) =>
        new(voice.Id, voice.DisplayName, voice.Language);
}
