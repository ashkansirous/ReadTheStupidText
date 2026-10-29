using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace ReadTheStupidText.Mobile.Resources.Strings;

/// <summary>
/// Typed access to the UI strings in AppResources*.resx (Slice 53, Decision 68). Each
/// property's name is its resource key, so a typo is a compile error. Add a key to the
/// neutral resx, to every language file, and here. Pages never hold string literals.
/// </summary>
public static class AppResources
{
    private static readonly ResourceManager Manager =
        new(typeof(AppResources));

    /// <summary>The string for <paramref name="key"/> in the current UI culture, falling back to English.</summary>
    public static string Get([CallerMemberName] string key = "") =>
        Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>Formats <paramref name="template"/> with <paramref name="args"/> in the current UI culture.</summary>
    public static string Format(string template, params object[] args) =>
        string.Format(CultureInfo.CurrentUICulture, template, args);

    public static string Voice_NbWoman => Get();
    public static string Voice_NbMan => Get();
    public static string App_Name => Get();
    public static string Shell_ReaderTitle => Get();
    public static string Common_Ok => Get();
    public static string Common_Cancel => Get();
    public static string Common_Voice => Get();
    public static string Common_Language => Get();
    public static string Settings_Title => Get();
    public static string Reader_EditLink => Get();
    public static string Reader_TextFromPictures => Get();
    public static string Reader_TextFromFile => Get();
    public static string Reader_EmptyHint => Get();
    public static string Settings_LanguageCaption => Get();
    public static string Settings_ReadingSpeed => Get();
    public static string Settings_CountsTitle => Get();
    public static string Settings_CountsCaption => Get();
    public static string Settings_CountsOn => Get();
    public static string Settings_CountsOff => Get();
    public static string Language_CountsNote => Get();
    public static string Common_Back => Get();
    public static string Reader_SkipBack => Get();
    public static string Reader_SkipForward => Get();
    public static string Reader_Play => Get();
    public static string Reader_Pause => Get();
    public static string Reader_PasteButton => Get();
    public static string Reader_EditorHint => Get();
    public static string Reader_PasteFailedTitle => Get();
    public static string Reader_PasteFailedMessage => Get();
    public static string Reader_ScanFailedTitle => Get();
    public static string Reader_ScanFailedMessage => Get();
    public static string Reader_ScannerStartFailedTitle => Get();
    public static string Reader_ScannerStartFailedMessage => Get();
    public static string Reader_ScannerPreparingTitle => Get();
    public static string Reader_ScannerPreparingMessage => Get();
    public static string Reader_ScannerUnavailableTitle => Get();
    public static string Reader_ScannerUnavailableMessage => Get();
    public static string Reader_CameraAccessTitle => Get();
    public static string Reader_CameraAccessMessage => Get();
    public static string Reader_NoCameraTitle => Get();
    public static string Reader_NoCameraMessage => Get();
    public static string Reader_ScanUnavailableTitle => Get();
    public static string Reader_ScanUnavailableMessage => Get();
    public static string Reader_ScanUnreadableTitle => Get();
    public static string Reader_ScanUnreadableMessage => Get();
    public static string Reader_NoTextFoundTitle => Get();
    public static string Reader_NoTextFoundMessage => Get();
    public static string Capture_Title => Get();
    public static string Capture_Hint => Get();
    public static string Capture_HintDone => Get();
    public static string Capture_PageLabel => Get();
    public static string Capture_Reading => Get();
    public static string Capture_AddPage => Get();
    public static string Capture_Done => Get();
    public static string Capture_ChoosePhotos => Get();
    public static string Capture_DiscardTitle => Get();
    public static string Capture_DiscardMessage => Get();
    public static string Capture_PageLimit => Get();
    public static string Capture_DiscardMessageOne => Get();
    public static string Capture_KeepTaking => Get();
    public static string Capture_Discard => Get();
    public static string Capture_RemovePage => Get();
    public static string Capture_CouldntRead => Get();
    public static string Capture_ClosePreview => Get();
    public static string Review_Title => Get();
    public static string Review_TranslateTo => Get();
    public static string Review_LooksLike => Get();
    public static string Review_TranslateAndRead => Get();
    public static string Review_Read => Get();
    public static string Wait_DownloadingModel => Get();
    public static string Wait_Translating => Get();
    public static string Wait_PreparingVoice => Get();
    public static string Wait_Stalled => Get();
    public static string Wait_ReadingFile => Get();
    public static string Review_FirstTimeOnly => Get();
    public static string Review_ShowOriginal => Get();
    public static string Review_FailedOne => Get();
    public static string Review_TextCut => Get();
    public static string Review_FailedMany => Get();
    public static string Review_HereOne => Get();
    public static string Review_HereMany => Get();
    public static string Review_TranslateFailed => Get();
    public static string Reader_PageLanguageTitle => Get();
    public static string Reader_OcrDownloading => Get();
    public static string Reader_OcrDownloadFailedTitle => Get();
    public static string Reader_OcrDownloadFailedMessage => Get();
    public static string Reader_OcrOfflineMessage => Get();
    public static string Reader_OcrFailedMessage => Get();
    public static string Reader_OcrUnreadableMessage => Get();
    public static string Reader_FilePickerTitle => Get();
    public static string Reader_FilePermissionTitle => Get();
    public static string Reader_FilePermissionMessage => Get();
    public static string Reader_FileTooLargeTitle => Get();
    public static string Reader_FileTooLargeMessage => Get();
    public static string Reader_FileUnreadableTitle => Get();
    public static string Reader_FileUnreadableMessage => Get();
    public static string Reader_ReadFailedTitle => Get();
    public static string Reader_ReadFailedMessage => Get();
    public static string Reader_VoiceUnavailableTitle => Get();
    public static string Reader_VoiceDownloadingMessage => Get();
    public static string Reader_NoVoiceMessage => Get();
    public static string PackStatus_Resolving => Get();
    public static string PackStatus_Downloading => Get();
    public static string PackStatus_Offline => Get();
    public static string PackStatus_Waiting => Get();
    public static string PackStatus_WaitingWifi => Get();
    public static string PackStatus_HashMismatch => Get();
    public static string PackStatus_InvalidPack => Get();
    public static string PackStatus_Failed => Get();
    public static string PackStatus_Unavailable => Get();
    public static string VoicePicker_Heading => Get();
    public static string VoicePicker_Subtitle => Get();
    public static string VoicePicker_SectionPhone => Get();
    public static string VoicePicker_SectionMale => Get();
    public static string VoicePicker_SectionFemale => Get();
    public static string VoicePicker_Hint => Get();
    public static string VoicePicker_PhoneVoiceName => Get();
    public static string VoicePicker_EngineSupertonic => Get();
    public static string VoicePicker_EnginePiper => Get();
    public static string VoicePicker_EngineMelo => Get();
    public static string VoicePicker_ReasonPackNotInstalled => Get();
    public static string VoicePicker_ReasonNoNeuralEngine => Get();
    public static string Language_Intro => Get();
    public static string Language_Retry => Get();
    public static string Notify_Title => Get();
    public static string Notify_Body => Get();
    public static string Notify_NotNow => Get();
    public static string Notify_Continue => Get();
    public static string Language_Continue => Get();
    public static string Language_StartReading => Get();
}
