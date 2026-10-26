using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Mobile.Shared;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Moves a stored "Phone voice" choice to the natural voice (Slice 82) once the phone can't read
/// the app language and a pack is ready, so the picker, the label and the reader agree.
/// </summary>
public static class PhoneVoiceMigration
{
    public static void MoveToNatural(ISettingsStore settings, ISpeechReader reader, VoiceModelPaths paths)
    {
        string id = NeuralVoiceMemory.Resolve(paths, settings.VoiceId).Id;
        settings.VoiceId = id;
        reader.SetVoice(id);
    }
}
