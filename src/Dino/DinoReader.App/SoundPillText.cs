namespace DinoReader;

/// <summary>One run of the pill's words: <see cref="IsName"/> is the language's own name, set in that script's font (Batch 50 frame 197).</summary>
public sealed record SoundPillRun(string Text, bool IsName);

/// <summary>Splits the pill's line around the native language name so the name can be set in its own font (Slice 290 part B).</summary>
public static class SoundPillText
{
	/// <summary>The runs of <paramref name="text"/>, with the first <paramref name="name"/> its own run; one plain run when the name is absent.</summary>
	public static IReadOnlyList<SoundPillRun> Split(string text, string? name)
	{
		int at = string.IsNullOrEmpty(name) ? -1 : text.IndexOf(name, StringComparison.Ordinal);
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

	private static void AddRun(List<SoundPillRun> runs, string text, bool isName)
	{
		if (text.Length > 0)
		{
			runs.Add(new SoundPillRun(text, isName));
		}
	}
}
