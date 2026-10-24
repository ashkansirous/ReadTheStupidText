using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Resources.Strings;

namespace ReadTheStupidText.Mobile;

/// <summary>The name a voice is shown under (Slice 104): the Norwegian pack's speaker codes become a localised "Voice N (woman)" / "Voice N (man)".</summary>
public static class VoiceLabel
{
    public static string For(VoiceInfo voice)
    {
        int? position = NorwegianVoices.Position(voice.Id);
        bool? female = NorwegianVoices.IsFemale(voice.Id);
        if (position is null || female is null)
        {
            return voice.DisplayName;
        }

        return AppResources.Format(female.Value ? AppResources.Voice_NbWoman : AppResources.Voice_NbMan, position.Value);
    }

    /// <summary>The one character on a voice's avatar: the label's first letter, or the position number for a Norwegian voice.</summary>
    public static string Initial(VoiceInfo voice) =>
        NorwegianVoices.Position(voice.Id)?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? voice.DisplayName[..1];
}
