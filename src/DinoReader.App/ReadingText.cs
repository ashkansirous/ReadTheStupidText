using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>The reading screen's copy (Slice 62), in the UI language (Slice 66).</summary>
public static class ReadingText
{
	public static string ForGrownUps => AppResources.Gallery_ForGrownUps;
	public static string TextMode => AppResources.Reading_TextMode;
	public static string PictureMode => AppResources.Reading_PictureMode;
	public static string Back => AppResources.Common_Back;
	public static string Play => AppResources.Reading_Play;
	public static string Pause => AppResources.Reading_Pause;
	public static string PreviousSentence => AppResources.Reading_Previous;
	public static string NextSentence => AppResources.Reading_Next;
	public static string Loading => AppResources.Reading_Loading;
	public static string CouldntRead => AppResources.Reading_CouldntRead;
	public static string ReadFailed => AppResources.Reading_ReadFailed;
	public static string Ok => AppResources.Common_Ok;
	public static string LayoutFailed => AppResources.Reading_LayoutFailed;
	// Slice 110: the "This book" sheet.
	public static string BookTitle => AppResources.Book_Title;
	public static string ShowOriginal => AppResources.Book_ShowOriginal;
	public static string BackToStory => AppResources.Book_Back;
	public static string CastButton => AppResources.Gallery_CastButton;
	// Slice 111: parents read in their own voice.
	public static string RecordButton => AppResources.Rec_Title;
	public static string UseMyVoice => AppResources.Rec_UseVoice;

	public static string UseMyVoiceHint(int recorded, int pages) => AppResources.Format(AppResources.Rec_UseVoiceHint, recorded, pages);

	public static string ShowOriginalHint(string original, string spoken) => AppResources.Format(AppResources.Book_ShowOriginalHint, original, spoken);

	public static string ReadingPage(int page) => AppResources.Format(AppResources.Reading_PageN, page);
}
