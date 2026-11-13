using ReadTheStupidText.Application.Net;
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

    public static string For(PackSetupState state, double progress, HttpErrorKind? failure = null) => state switch
    {
        PackSetupState.Failed when failure is { } kind => FailureText(kind),
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
    public static string ForPanel(PackSetupState state, double progress, HttpErrorKind? failure = null) =>
        state == PackSetupState.Idle ? Windows : For(state, progress, failure);

    /// <summary>
    /// Slice 373 (Missing States mock RT2): what a language's row shows for the setup state. A row whose pack is not installed says "not downloaded" on its second line
    /// (<see cref="VoicePackSetup.IsInstalled"/> tells installed from not), a failed download adds the error line under the row and Retry at its end; a state that is
    /// still going shows its own line on the chosen row.
    /// </summary>
    public static RowText ForRow(bool isRunRow, RunView run, bool? installed) =>
        PackRowText.Compose(isRunRow, run, installed, UiStrings.Get("Pack_NotDownloaded"),
            r => For(r.State, r.Progress, r.Failure), r => For(r.State, r.Progress, r.Failure));

    /// <summary>Slice 357 / 364 (Missing States mock RT2): the line for a failed pack download by what the server answered; a 404 says the voice is gone, every other answer shows the general server text.</summary>
    public static string FailureText(HttpErrorKind kind) =>
        kind == HttpErrorKind.NotFound ? UiStrings.Get("Error_VoiceGone") : UiStrings.Get("Error_Server");
}
