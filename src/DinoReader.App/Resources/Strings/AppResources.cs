using System.Globalization;
using System.Resources;
using System.Runtime.CompilerServices;

namespace DinoReader.Resources.Strings;

/// <summary>
/// Typed access to the UI strings in AppResources*.resx (Slice 66, Decision 74), the same pattern as Read The
/// Stupid Text (Slice 53). Each property's name is its resource key, so a typo is a compile error. Add a key to
/// the neutral resx, to every language file, and here. Pages never hold string literals. The strings are
/// machine-drafted and wait for a native-speaker read before release.
/// </summary>
public static class AppResources
{
	private static readonly ResourceManager Manager = new(typeof(AppResources));

	private static volatile CultureInfo _culture = CultureInfo.CurrentUICulture;

	/// <summary>
	/// The one culture the whole app speaks (Slice 159, Decision 164): set from the stored language pick, never read from a
	/// thread's <c>CurrentUICulture</c>, which an async flow loses.
	/// </summary>
	public static CultureInfo Culture => _culture;

	/// <summary>Makes <paramref name="culture"/> the language of every string, on every thread.</summary>
	public static void UseCulture(CultureInfo culture) => _culture = culture;

	/// <summary>The string for <paramref name="key"/> in the app culture, falling back to English.</summary>
	public static string Get([CallerMemberName] string key = "") => For(UseClickWording ? DesktopKey(key) : key, _culture);

	// On the Windows head a worded-for-desktop string is read from its twin: "...Click" for the tap hints, "...Windows" for a hint that names the keyboard (Slice 235 part C).
	private static string DesktopKey(string key) =>
		ClickWorded.Contains(key) ? key + ClickSuffix : KeyboardWorded.Contains(key) ? key + KeyboardSuffix : key;

	private const string KeyboardSuffix = "Windows";

	// The strings that name the keyboard at a desktop window and have a "...Windows" twin (Speed_Sub: "left and right arrows to change").
	private static readonly HashSet<string> KeyboardWorded = [nameof(Speed_Sub)];

	/// <summary>
	/// True on the Windows head (set once at start-up, Slice 235 part B): a few hints say "Tap" on a phone and "Click" at a desktop window. Each has a second string named
	/// <c>&lt;key&gt;Click</c>, written out in every language, which the plain property returns when this is on. False everywhere else, so phones and tablets read what they always did.
	/// </summary>
	public static bool UseClickWording { get; set; }

	private const string ClickSuffix = "Click";

	// The strings that say "Tap" and have a "...Click" twin; <see cref="Get"/> swaps to the twin when <see cref="UseClickWording"/> is on.
	private static readonly HashSet<string> ClickWorded =
	[
		nameof(Gallery_CornerLine),
	];

	/// <summary>The string for <paramref name="key"/> in <paramref name="culture"/> (a story's language, say), falling back to English.</summary>
	public static string For(string key, CultureInfo culture) => Manager.GetString(key, culture) ?? key;

	/// <summary>Formats <paramref name="template"/> with <paramref name="args"/> in the app culture.</summary>
	public static string Format(string template, params object[] args) =>
		string.Format(_culture, template, args);

	public static string App_Name => Get();
	public static string Add_FileSub => Get();
	public static string Update_Title => Get();
	public static string Update_Body => Get();
	public static string Update_Button => Get();
	public static string Update_OpenTestFlight => Get();
	public static string Update_OpenAppStore => Get();
	public static string Version_NewReady => Get();
	public static string Version_Title => Get();
	public static string Usage_Title => Get();
	public static string Usage_Helper => Get();
	// Slice 249: Report a problem
	public static string ReportProblem_Title => Get();
	public static string ReportProblem_Explain => Get();
	public static string ReportProblem_Send => Get();
	public static string ReportProblem_Sending => Get();
	public static string ReportProblem_Sent => Get();
	public static string ReportProblem_QuoteCode => Get();
	public static string ReportProblem_Copy => Get();
	public static string ReportProblem_Copied => Get();
	public static string ReportProblem_Failed => Get();
	public static string ReportProblem_FailedNotKept => Get();
	public static string ReportProblem_TryAgain => Get();
	public static string Speed_Title => Get();
	public static string Speed_Sub => Get();
	public static string Speed_EndSlow => Get();
	public static string Speed_Value => Get();
	public static string FirstRun_Usage => Get();
	public static string Storyteller_Size => Get();
	public static string Book_Title => Get();
	public static string Book_Back => Get();
	public static string Rec_Title => Get();
	public static string Rec_Stop => Get();
	public static string Rec_Start => Get();
	public static string Rec_Redo => Get();
	public static string Rec_UseVoice => Get();
	public static string Rec_UseVoiceHint => Get();
	public static string Rec_NoMicTitle => Get();
	public static string Rec_NoMic => Get();
	public static string Rec_Failed => Get();
	public static string Record_Title => Get();
	public static string Record_YourVoice => Get();
	public static string Record_NoteRead => Get();
	public static string Record_NoteSkip => Get();
	public static string Record_NoteStays => Get();
	public static string Record_NoteStaysDevice => Get();
	public static string Record_ModeCharacter => Get();
	public static string Record_ModePage => Get();
	public static string Record_Who => Get();
	public static string Record_Storyteller => Get();
	public static string Record_Lines => Get();
	public static string Record_OfCount => Get();
	public static string Record_Language => Get();
	public static string Record_LanguagePages => Get();
	public static string Record_LanguageNone => Get();
	public static string Record_LanguageRetry => Get();
	public static string Record_LanguageUnavailable => Get();
	public static string Record_Start => Get();
	public static string Record_LongPages => Get();
	public static string Record_Says => Get();
	public static string Record_LineOf => Get();
	public static string Record_PageOf => Get();
	public static string Record_PagePart => Get();
	public static string Record_Skip => Get();
	public static string Record_Next => Get();
	public static string Record_Finish => Get();
	public static string Record_Play => Get();
	public static string Record_Playing => Get();
	public static string Record_Recorded => Get();
	public static string Record_NewTake => Get();
	public static string Record_OldTake => Get();
	public static string Record_ReplacesRest => Get();
	public static string Record_KeepOld => Get();
	public static string Record_UseDino => Get();
	public static string Record_Undo => Get();
	public static string Record_Change => Get();
	public static string Record_Listen => Get();
	public static string Record_Rest => Get();
	public static string Record_Again => Get();
	public static string Record_YourRecordings => Get();
	public static string Record_LinesRecorded => Get();
	public static string Record_PagesRecorded => Get();
	public static string Record_RestByDino => Get();
	public static string Record_RestByDinoDevice => Get();
	public static string Record_You => Get();
	public static string Record_RemoveWarn => Get();
	public static string Record_SkipKey => Get();
	public static string Record_RecordKey => Get();
	public static string Record_NextKey => Get();
	public static string Common_Ok => Get();
	public static string Common_Cancel => Get();
	public static string ShelfAudio_Getting => Get();
	public static string ShelfAudio_NoneOne => Get();
	public static string ShelfAudio_NoneMany => Get();
	public static string ShelfAudio_WaitingInternet => Get();
	public static string ShelfAudio_WaitingWifi => Get();
	public static string ShelfAudio_Preparing => Get();
	public static string ShelfAudio_Failed => Get();
	public static string Reading_AudioUnavailable => Get();
	public static string Reading_NoSound => Get();
	public static string Reading_GettingSound => Get();
	public static string StoryLanguages_Retry => Get();
	public static string Common_Back => Get();
	public static string Common_Save => Get();
	public static string Common_Done => Get();
	public static string Common_Continue => Get();
	public static string Close_Question => Get();
	public static string Common_Stay => Get();
	public static string Common_Close => Get();
	public static string Common_Delete => Get();
	public static string Common_Keep => Get();
	public static string Common_Edit => Get();
	public static string Gallery_Title => Get();
	public static string Gallery_ForGrownUps => Get();
	public static string Gallery_AddBook => Get();
	public static string Gallery_OpensGateHint => Get();
	public static string Gallery_Characters_One => Get();
	public static string Gallery_Characters_Other => Get();
	public static string Gallery_PagesSummary => Get();
	public static string Gallery_DeleteTitle => Get();
	public static string Gallery_PagesRead_One => Get();
	public static string Gallery_PagesRead_Other => Get();
	public static string Gallery_CharactersFound_One => Get();
	public static string Gallery_CharactersFound_Other => Get();
	public static string Delete_Body => Get();
	public static string Delete_FailedTitle => Get();
	public static string Delete_FailedBody => Get();
	public static string Gallery_EditStory => Get();
	public static string Gallery_CastStory => Get();
	public static string Gallery_DeleteStoryLabel => Get();
	public static string Gallery_ExportStory => Get();
	public static string Gallery_CastButton => Get();
	public static string Store_Title => Get();
	public static string Store_Subtitle => Get();
	public static string Store_Get => Get();
	public static string Store_Downloaded => Get();
	public static string Store_Tag_Hot => Get();
	public static string Store_Tag_Popular => Get();
	public static string Store_Tag_Recommended => Get();
	public static string Store_Tag_New => Get();
	public static string Store_Length => Get();
	public static string Store_Footer => Get();
	public static string Store_OfflineTitle => Get();
	public static string Store_OfflineBody => Get();
	public static string Store_TryAgain => Get();
	public static string Store_EmptyTitle => Get();
	public static string Store_EmptyBody => Get();
	public static string Store_BackToStories => Get();
	public static string Store_GetStory => Get();
	public static string Store_AllAges => Get();
	public static string Store_AgeUpTo5 => Get();
	public static string Store_Age6To8 => Get();
	public static string Store_Age9To12 => Get();
	public static string Store_Author => Get();
	public static string Store_Anyone => Get();
	public static string Store_Ages => Get();
	public static string Store_AgeSingle => Get();
	public static string Store_ChipSelected => Get();
	public static string Store_NoMatch => Get();
	public static string Backup_Title => Get();
	public static string Backup_SaveTitle => Get();
	public static string Backup_SaveBody_One => Get();
	public static string Backup_SaveBody_Other => Get();
	public static string Backup_SaveButton => Get();
	public static string Backup_RestoreTitle => Get();
	public static string Backup_RestoreBody => Get();
	public static string Backup_SaveIncludes => Get();
	public static string Backup_Merged => Get();
	public static string Recordings_StayHere_Phone => Get();
	public static string Recordings_StayHere_Tablet => Get();
	public static string Recordings_StayHere_Device => Get();
	public static string Backup_RestoreButton => Get();
	public static string Backup_Footer => Get();
	public static string Backup_Restored_One => Get();
	public static string Backup_Restored_Other => Get();
	public static string Backup_Added => Get();
	public static string Backup_Skipped => Get();
	public static string Backup_Damaged => Get();
	public static string Backup_SeeStories => Get();
	public static string Backup_Newer => Get();
	public static string Backup_NotABackup => Get();
	public static string Backup_Failed => Get();
	public static string Backup_TooLarge => Get();
	public static string Backup_SaveSkipped => Get();
	public static string FirstRun_Hello => Get();
	public static string FirstRun_HelloBody => Get();
	public static string FirstRun_Continue => Get();
	public static string FirstRun_Ready => Get();
	public static string FirstRun_ReadyBody => Get();
	public static string FirstBooks_Title => Get();
	public static string FirstBooks_Body => Get();
	public static string FirstBooks_PickTwo => Get();
	public static string FirstBooks_PickOneMore => Get();
	public static string FirstBooks_Picked => Get();
	public static string FirstBooks_PickedNoSize => Get();
	public static string FirstBooks_Getting => Get();
	public static string FirstBooks_BookOf => Get();
	public static string FirstBooks_OfflineTitle => Get();
	public static string FirstBooks_OfflineBody => Get();
	public static string Intro_SeeWhat => Get();
	public static string Intro_SkipStart => Get();
	public static string Intro_SkipAll => Get();
	public static string Intro_TryIt => Get();
	public static string Intro_Skip => Get();
	public static string Intro_Counter => Get();
	public static string Intro_PlacesTitle => Get();
	public static string Intro_PlacesKidsTitle => Get();
	public static string Intro_PlacesKidsLine => Get();
	public static string Intro_PlacesGrownLine => Get();
	public static string Intro_ReadTitle => Get();
	public static string Intro_ReadLine => Get();
	public static string Intro_ReadCaption => Get();
	public static string Intro_RecordTitle => Get();
	public static string Intro_RecordLine => Get();
	public static string Intro_ShowAgain => Get();
	public static string Gallery_ReadyForChild => Get();
	public static string Gallery_GrownUpMode => Get();
	public static string Language_Title => Get();
	public static string Language_Subtitle => Get();
	public static string Settings_Title => Get();
	public static string Settings_GroupReading => Get();
	public static string Settings_GroupPhone => Get();
	public static string Settings_NewVersion => Get();
	public static string Settings_GearLabel => Get();
	public static string Settings_About => Get();
	public static string Settings_On => Get();
	public static string Settings_Off => Get();
	public static string Language_PageSub => Get();
	public static string Gallery_GrownUpsTitle => Get();
	public static string Gallery_GrownUpsSub => Get();
	public static string Gallery_GrownUpsButton => Get();
	public static string Gallery_GrownUpsPill => Get();
	public static string Gallery_GrownUpsButtonLocked => Get();
	public static string Gallery_CornerTitle => Get();
	public static string Gallery_CornerLine => Get();
	public static string Gallery_CornerLineClick => Get();
	public static string Gallery_BackToShelf => Get();
	public static string Gallery_Reorder => Get();
	public static string Gallery_ReorderHint => Get();
	public static string Reorder_SaveFailedTitle => Get();
	public static string Reorder_SaveFailedBody => Get();
	public static string Book_ReadNow => Get();
	public static string Gallery_BookOptions => Get();
	public static string Book_FromStore => Get();
	public static string Book_Starter => Get();
	public static string Book_Own => Get();
	public static string Book_EditPages => Get();
	public static string Book_Remove => Get();
	public static string Book_RemoveTitle => Get();
	public static string Book_RemoveBodyStore => Get();
	public static string Book_RemoveConfirm => Get();
	public static string Gallery_GetMoreBooks => Get();
	public static string Language_Footer => Get();
	public static string Gate_HoldBody => Get();
	public static string Gate_HoldDescription => Get();
	public static string Gate_KeepHolding => Get();
	public static string Gate_Continue => Get();
	public static string Gate_BackToStory => Get();
	public static string Gate_WhatIs => Get();
	public static string Gate_Backspace => Get();
	public static string Gate_HoldCaptionWindows => Get();
	public static string Gate_TypeEnterWindows => Get();
	public static string Key_Space => Get();
	public static string Key_Left => Get();
	public static string Key_Right => Get();
	public static string Key_Esc => Get();
	public static string Speed_SubWindows => Get();
	public static string Reading_ReadAgain => Get();
	public static string Gate_SumQuestion => Get();
	public static string Story_Language => Get();
	public static string Story_LanguageDetected => Get();
	public static string Story_Translate => Get();
	public static string Story_TranslateReadsIn => Get();
	public static string Story_TranslateAndroidOnly => Get();
	public static string Story_TranslateUnsupportedPair => Get();
	public static string Wizard_Next => Get();
	public static string Wait_Translating => Get();
	public static string Wait_WaitingInternet => Get();
	public static string Wait_Saving => Get();
	public static string Wait_GettingBook => Get();
	public static string Wait_BackingUp => Get();
	public static string Wait_Restoring => Get();
	public static string Reading_Mode_PictureOnly => Get();
	public static string Reading_Mode_PictureAndText => Get();
	public static string Reading_Mode_TextOnly => Get();
	public static string Reading_Mode_Selected => Get();
	public static string Reading_Play => Get();
	public static string Reading_Pause => Get();
	public static string Reading_Previous => Get();
	public static string Reading_Next => Get();
	public static string Reading_Loading => Get();
	public static string Reading_CouldntRead => Get();
	public static string Reading_ReadFailed => Get();
	public static string Reading_LayoutFailed => Get();
	public static string Reading_PageN => Get();
	public static string Cast_Pair => Get();
	public static string Settings_AnimalSounds_Title => Get();
	public static string Settings_AnimalSounds_Sub => Get();
	public static string Wait_DownloadingPack => Get();
	public static string StoryLanguages_IntroTitle => Get();
	public static string StoryLanguages_IntroLine => Get();
	public static string StoryLanguages_AppLanguage => Get();
	public static string StoryLanguages_SkipOnly => Get();
	public static string StoryLanguages_Full => Get();
	public static string StoryLanguages_SettingsTitle => Get();
	public static string StoryLanguages_SettingsLine => Get();
	public static string StoryLanguages_DropTitle => Get();
	public static string StoryLanguages_DropBody => Get();
	public static string StoryLanguages_Remove => Get();
	public static string StoryLanguages_WasApp => Get();
	public static string StoryLanguages_ButtonLabel => Get();
	public static string StoryLanguages_Failed => Get();
	public static string StoryLanguages_WifiTitle => Get();
	public static string StoryLanguages_WifiBody => Get();
	public static string StoryLanguages_WifiNow => Get();
	public static string StoryLanguages_WifiWait => Get();
	public static string StoryLanguages_FlagMap => Get();
	public static string StoryLanguages_NameMap => Get();
	public static string Gallery_UpdatingBooks => Get();
	public static string BackCover_StoryBy => Get();
	public static string BackCover_OriginalStoryBy => Get();
	public static string BackCover_Traditional => Get();
	public static string BackCover_RetoldBy => Get();
	public static string BackCover_Illustration => Get();
	public static string BackCover_Date => Get();
	public static string BackCover_Months => Get();
	public static string BackCover_Digits => Get();
	public static string BackCover_And => Get();
}
