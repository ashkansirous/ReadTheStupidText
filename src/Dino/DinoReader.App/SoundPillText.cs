namespace DinoReader;

/// <summary>One run of the pill's words: <see cref="IsName"/> is the language's own name, set in that script's font (Batch 50 frame 197).</summary>
public sealed record SoundPillRun(string Text, bool IsName);

/// <summary>Splits the pill's line around the native language name so the name can be set in its own font (Slice 290 part B).</summary>
public static class SoundPillText
{
	/// <summary>The runs of <paramref name="text"/>, with <paramref name="name"/> its own run (see <see cref="IndexOfName"/>); one plain run when the name is absent.</summary>
	public static IReadOnlyList<SoundPillRun> Split(string text, string? name)
	{
		int at = string.IsNullOrEmpty(name) ? -1 : IndexOfName(text, name);
		if (at < 0 || name is null)
		{
			return [new SoundPillRun(text, false)];
		}

		var runs = new List<SoundPillRun>(3);
		AddRun(runs, text[..at], false);
		AddRun(runs, name, true);
		AddRun(runs, text[(at + name.Length)..], false);
		return runs;
	}

	/// <summary>The font for one run: <paramref name="nameFont"/> only for a right-to-left language name, else <paramref name="lineFont"/>.</summary>
	public static string FontFor(SoundPillRun run, string lineFont, string nameFont) =>
		run.IsName && DinoReader.Core.TextDirection.StartsRightToLeft(run.Text) ? nameFont : lineFont;

	/// <summary>
	/// Where the name stands in the line: the first occurrence that is a whole word (nothing but a non-letter on each side), so a name that is also a piece of another word is not marked there;
	/// else the first occurrence at all (a name written against a joiner or a script with no spaces); -1 when absent.
	/// </summary>
	public static int IndexOfName(string text, string name)
	{
		int first = -1;
		for (int from = 0; from <= text.Length - name.Length; from++)
		{
			int at = text.IndexOf(name, from, StringComparison.Ordinal);
			if (at < 0)
			{
				break;
			}

			first = first < 0 ? at : first;
			if (IsWordEdge(text, at - 1) && IsWordEdge(text, at + name.Length))
			{
				return at;
			}

			from = at; // the loop's own increment steps past this occurrence
		}

		return first;
	}

	private static bool IsWordEdge(string text, int index) => index < 0 || index >= text.Length || !IsWordChar(text[index]);

	// A combining mark (an Arabic or Indic vowel sign) belongs to the letter before it, so it is part of the word.
	private static bool IsWordChar(char c) =>
		char.IsLetter(c) || char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.NonSpacingMark or System.Globalization.UnicodeCategory.SpacingCombiningMark;

	private static void AddRun(List<SoundPillRun> runs, string text, bool isName)
	{
		if (text.Length > 0)
		{
			runs.Add(new SoundPillRun(text, isName));
		}
	}
}
