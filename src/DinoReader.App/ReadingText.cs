namespace DinoReader;

/// <summary>The reading screen's copy (Slice 62 new strings, English until Slice 66 adds the locales).</summary>
public static class ReadingText
{
	public const string ForGrownUps = "For grown-ups";
	public const string TextMode = "Text mode";
	public const string PictureMode = "Picture mode";
	public const string Back = "Back";
	public const string Play = "Play";
	public const string Pause = "Pause";
	public const string PreviousSentence = "Previous sentence";
	public const string NextSentence = "Next sentence";
	public const string Loading = "Loading…";
	public const string CouldntRead = "Couldn't read that text";
	public const string ReadFailed = "Something went wrong while reading.";
	public const string Ok = "OK";
	public const string LayoutFailed = "Couldn't lay out this story. Turn the screen or switch the mode to try again.";

	public static string ReadingPage(int page) => $"Reading page {page}";
}
