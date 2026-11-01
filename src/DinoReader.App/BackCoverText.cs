using System.Globalization;
using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>
/// The words of a back cover in the language it is shown in (Slice 237): labels, month names and digits come from the strings files, names are never translated.
/// A strings file that is wrong (a missing key, a bad placeholder, not 12 months or 10 digits) falls back to the English file, never to a crash.
/// </summary>
public static class BackCoverText
{
	private const char MonthSeparator = '|';
	private const string AsciiDigits = "0123456789";
	private const string AndKey = "BackCover_And";

	public static IReadOnlyList<string> Lines(BackCover cover, string language)
	{
		CultureInfo culture = CultureInfo.GetCultureInfo(UiLanguage.Resolve(language));
		return cover.Lines().Select(line => Line(line, cover, culture)).ToList();
	}

	private static string Line(BackCoverLine line, BackCover cover, CultureInfo culture) => line.Kind switch
	{
		BackCoverLineKind.StoryBy => Format("BackCover_StoryBy", culture, JoinNames(line.Name!, culture)),
		BackCoverLineKind.OriginalStoryBy => Format("BackCover_OriginalStoryBy", culture, JoinNames(line.Name!, culture)),
		BackCoverLineKind.TraditionalTale => Text("BackCover_Traditional", culture),
		BackCoverLineKind.RetoldBy => Format("BackCover_RetoldBy", culture, line.Name!),
		BackCoverLineKind.Illustration => Format("BackCover_Illustration", culture, line.Name!),
		_ => Date(cover, culture),
	};

	/// <summary>Several names joined with the language's own "and"; the names themselves stay as written.</summary>
	public static string JoinNames(string names, CultureInfo culture)
	{
		IReadOnlyList<string> parts = BackCover.Names(names);
		string joined = parts.Count == 0 ? names : parts[0];
		for (int i = 1; i < parts.Count; i++)
		{
			joined = Format(AndKey, culture, joined, parts[i]);
		}

		return joined;
	}

	/// <summary>The month and year, e.g. "November 2026"; the month name and the digits are the language's own.</summary>
	public static string Date(BackCover cover, CultureInfo culture)
	{
		string[] months = Text("BackCover_Months", culture).Split(MonthSeparator);
		if (months.Length != BackCover.LastMonth)
		{
			months = Text("BackCover_Months", CultureInfo.InvariantCulture).Split(MonthSeparator);
		}

		string digits = Text("BackCover_Digits", culture);
		if (digits.Length != AsciiDigits.Length)
		{
			digits = AsciiDigits;
		}

		string year = new(cover.Year.ToString(CultureInfo.InvariantCulture).Select(c => digits[AsciiDigits.IndexOf(c)]).ToArray());
		return Format("BackCover_Date", culture, months[cover.Month - BackCover.FirstMonth], year);
	}

	// The string in the culture, or the English one when the file lacks the key (the resource lookup then returns the key itself).
	private static string Text(string key, CultureInfo culture)
	{
		string text = AppResources.For(key, culture);
		return text == key ? AppResources.For(key, CultureInfo.InvariantCulture) : text;
	}

	private static string Format(string key, CultureInfo culture, params object[] args)
	{
		try
		{
			return string.Format(culture, Text(key, culture), args);
		}
		catch (FormatException)
		{
			return string.Format(CultureInfo.InvariantCulture, AppResources.For(key, CultureInfo.InvariantCulture), args);
		}
	}
}
