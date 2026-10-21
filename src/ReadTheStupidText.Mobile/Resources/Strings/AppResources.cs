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

    public static string App_Name => Get();
    public static string Shell_ReaderTitle => Get();
    public static string Common_Ok => Get();
    public static string Common_Cancel => Get();
    public static string Common_Voice => Get();
    public static string Common_Language => Get();
    public static string Common_BackGlyph => Get();
    public static string Reader_AddTextSection => Get();
    public static string Reader_PasteButton => Get();
    public static string Reader_ScanButton => Get();
    public static string Reader_FileButton => Get();
    public static string Reader_EditorPlaceholder => Get();
    public static string Reader_SkipBackGlyph => Get();
    public static string Reader_SkipForwardGlyph => Get();
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
    public static string Reader_PagesSkippedTitle => Get();
    public static string Reader_PagesSkippedOneMessage => Get();
    public static string Reader_PagesSkippedManyMessage => Get();
    public static string Reader_ScanProgressPage => Get();
    public static string Reader_ScanProgressSingle => Get();
    public static string Reader_FilePickerTitle => Get();
    public static string Reader_FilePermissionTitle => Get();
    public static string Reader_FilePermissionMessage => Get();
    public static string Reader_FileTooLargeTitle => Get();
    public static string Reader_FileTooLargeMessage => Get();
    public static string Reader_FileUnreadableTitle => Get();
    public static string Reader_FileUnreadableMessage => Get();
    public static string Reader_ReadFailedTitle => Get();
    public static string Reader_ReadFailedMessage => Get();
    public static string PackStatus_Resolving => Get();
    public static string PackStatus_Downloading => Get();
    public static string PackStatus_Offline => Get();
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
    public static string Language_Intro => Get();
    public static string Language_Retry => Get();
    public static string Language_Continue => Get();
    public static string Language_StartReading => Get();
}
