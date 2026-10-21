using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText_App;

/// <summary>
/// Composes the user-facing copy for the voice pack download (Slice 56, localized in Slice 57). The
/// Application layer only reports <see cref="PackSetupState"/>; the words live in the resources.
/// </summary>
internal static class PackSetupText
{
    private const int PercentScale = 100;
    private static string Windows => UiStrings.Get("Pack_WindowsVoice");

    /// <summary>The name to show for a voice: the built-in Windows voice is localized (its id stays the constant).</summary>
    public static string VoiceName(VoiceInfo voice) => voice.Id == SystemVoice.Id ? Windows : voice.DisplayName;

    public static string Intro => UiStrings.Format("Pack_Intro", Windows);

    public static string For(PackSetupState state, double progress) => state switch
    {
        PackSetupState.Resolving => UiStrings.Get("Pack_Resolving"),
        PackSetupState.Downloading => UiStrings.Format("Pack_Downloading", (int)(progress * PercentScale)),
        PackSetupState.Ready => UiStrings.Get("Pack_Ready"),
        PackSetupState.Unavailable => UiStrings.Format("Pack_Unavailable", Windows),
        PackSetupState.Offline => UiStrings.Format("Pack_Offline", Windows),
        PackSetupState.HashMismatch => UiStrings.Get("Pack_HashMismatch"),
        PackSetupState.InvalidPack => UiStrings.Format("Pack_InvalidPack", Windows),
        PackSetupState.Failed => UiStrings.Get("Pack_Failed"),
        _ => string.Empty,
    };

    /// <summary>The control panel's line while the natural voice is not ready.</summary>
    public static string ForPanel(PackSetupState state, double progress) =>
        state == PackSetupState.Idle ? Windows : For(state, progress);
}
