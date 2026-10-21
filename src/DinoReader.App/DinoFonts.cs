namespace DinoReader;

/// <summary>Font aliases registered in <see cref="MauiProgram"/> (Dino Reader v2 mock: Fredoka for the
/// UI, Andika for reading text, Vazirmatn for Farsi/Arabic; CJK uses the system font).</summary>
public static class DinoFonts
{
	public const string Ui = "Fredoka";
	public const string Reading = "Andika";
	public const string ReadingBold = "AndikaBold";
	public const string Persian = "Vazirmatn";

	private static readonly string[] PersianLanguages = ["fa", "ar"];

	/// <summary>The font a story's text is set in: Vazirmatn for Farsi and Arabic, Andika otherwise.</summary>
	public static string ReadingFor(string language) => IsRightToLeft(language) ? Persian : Reading;

	/// <summary>Whether a story language runs right to left (Farsi, Arabic): its page mirrors and its swipe direction flips.</summary>
	public static bool IsRightToLeft(string language) =>
		PersianLanguages.Contains(PrimaryLanguage(language), StringComparer.OrdinalIgnoreCase);

	// "fa-IR" and "fa_IR" are Farsi: compare on the primary language subtag.
	private static string PrimaryLanguage(string language) =>
		language.Split('-', '_')[0].Trim();
}
