namespace DinoReader.Core;

/// <summary>The colour of the Windows title bar (Slice 235 part B, mock Batch 46 frame 155): cream on the kids' pages, paper on grown-up pages, ink on the grown-ups' corner.</summary>
public enum TitleBarTone
{
	Cream,
	Paper,
	Ink,
}

/// <summary>The kinds of page that pick a title bar colour.</summary>
public enum TitleBarPage
{
	Kids,
	Reading,
	GrownUp,
	Corner,
}

/// <summary>Which title bar colour a page kind gets.</summary>
public static class TitleBarTones
{
	public static TitleBarTone For(TitleBarPage page) => page switch
	{
		TitleBarPage.GrownUp => TitleBarTone.Paper,
		TitleBarPage.Corner => TitleBarTone.Ink,
		_ => TitleBarTone.Cream,
	};

	/// <summary>The window title: the book's name on the reading screen, else the app's name.</summary>
	public static string WindowTitle(TitleBarPage page, string? bookTitle, string appName) =>
		page == TitleBarPage.Reading && !string.IsNullOrWhiteSpace(bookTitle) ? $"{bookTitle} - {appName}" : appName;
}
