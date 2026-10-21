using DinoReader.Core;

namespace DinoReader;

/// <summary>Font aliases registered in <see cref="MauiProgram"/> (Dino Reader v2 mock: Fredoka for the
/// UI, Andika for reading text, Vazirmatn for Farsi/Arabic; CJK uses the system font).</summary>
public static class DinoFonts
{
	public const string Ui = "Fredoka";
	public const string Reading = "Andika";
	public const string ReadingBold = "AndikaBold";
	public const string Persian = "Vazirmatn";

	/// <summary>The font a story's text is set in: Vazirmatn for Farsi and Arabic, Andika otherwise.</summary>
	public static string ReadingFor(string language) => IsRightToLeft(language) ? Persian : Reading;

	/// <summary>Whether a story language runs right to left (Farsi, Arabic): its page mirrors and its swipe direction flips.</summary>
	public static bool IsRightToLeft(string language) => UiLanguage.IsRightToLeft(language);

	/// <summary>
	/// Sets the screens' labels and buttons in Vazirmatn when they run right to left (Farsi, Arabic), as the mock's
	/// Farsi frames do; Chinese and Japanese use the system font. Called once, before any page is built.
	/// </summary>
	public static void UseForRightToLeftUi(ResourceDictionary resources)
	{
		foreach (Style style in resources.Values.OfType<Style>())
		{
			foreach (Setter setter in style.Setters.Where(s => s.Property == Label.FontFamilyProperty || s.Property == Button.FontFamilyProperty))
			{
				setter.Value = Persian;
			}
		}
	}
}
