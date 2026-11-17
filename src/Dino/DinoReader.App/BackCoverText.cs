using System.Globalization;
using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>
/// The words of a back cover in the language it is shown in (Slice 237): labels, month names and digits come from the strings files, names from the book's own <c>namesByLanguage</c> (Slice 405; the name as written when it has none).
/// A strings file that is wrong (a missing key, a bad placeholder, not 12 months or 10 digits) falls back to the English file, never to a crash.
/// </summary>
public sealed class BackCoverText
{
	private const char MonthSeparator = '|';
	private const string AsciiDigits = "0123456789";
	private const string AndKey = "BackCover_And";
	private const string AndWordKey = "BackCover_AndWord";
	private const string ListKey = "BackCover_List";

	private static readonly BackCoverText Default = new(AppResources.For);

	private readonly Func<string, CultureInfo, string> _lookup;

	/// <summary>The string for a key in a culture (the key itself when missing); the app passes its strings files, a test passes the files read from disk.</summary>
	public BackCoverText(Func<string, CultureInfo, string> lookup)
	{
		_lookup = lookup;
	}

	public static IReadOnlyList<string> Lines(BackCover cover, string language) => Default.LinesIn(cover, language);

	public IReadOnlyList<string> LinesIn(BackCover cover, string language)
	{
		string code = UiLanguage.Resolve(language);
		CultureInfo culture = CultureInfo.GetCultureInfo(code);
		return cover.Lines().Select(line => Line(line, cover, culture, code)).ToList();
	}

	private string Line(BackCoverLine line, BackCover cover, CultureInfo culture, string code) => line.Kind switch
	{
		BackCoverLineKind.StoryBy => Format("BackCover_StoryBy", culture, JoinNames(line.Name!, culture, cover, code)),
		BackCoverLineKind.OriginalStoryBy => Format("BackCover_OriginalStoryBy", culture, JoinNames(line.Name!, culture, cover, code)),
		BackCoverLineKind.TraditionalTale => Text("BackCover_Traditional", culture),
		BackCoverLineKind.RetoldBy => Format("BackCover_RetoldBy", culture, cover.NameIn(line.Name!, code)),
		BackCoverLineKind.Illustration => Format("BackCover_Illustration", culture, cover.NameIn(line.Name!, code)),
		_ => Date(cover, culture),
	};

	/// <summary>Several names joined with the language's own "and"; each name is the book's spelling in that language (Slice 405), or as written when it has none.</summary>
	public string JoinNames(string names, CultureInfo culture, BackCover cover, string code)
	{
		IReadOnlyList<string> parts = BackCover.Names(names);
		if (parts.Count == 0)
		{
			return names;
		}

		IReadOnlyList<string> spelled = parts.Select(part => cover.NameIn(part, code)).ToList();
		return BackCover.JoinSpelled(spelled, Text(AndWordKey, culture).Trim(),
			(first, second) => Format(AndKey, culture, first, second), (first, second) => Format(ListKey, culture, first, second));
	}

	/// <summary>The month and year, e.g. "November 2026"; the month name and the digits are the language's own.</summary>
	public string Date(BackCover cover, CultureInfo culture)
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
	private string Text(string key, CultureInfo culture)
	{
		string text = _lookup(key, culture);
		return text == key ? _lookup(key, CultureInfo.InvariantCulture) : text;
	}

	private string Format(string key, CultureInfo culture, params object[] args)
	{
		try
		{
			return string.Format(culture, Text(key, culture), args);
		}
		catch (FormatException)
		{
			// no-fallback: a translation with a bad placeholder shows the English text; a build-time string defect, not a runtime degrade
			return string.Format(CultureInfo.InvariantCulture, _lookup(key, CultureInfo.InvariantCulture), args);
		}
	}
}
