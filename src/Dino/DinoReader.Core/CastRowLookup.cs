namespace DinoReader.Core;

/// <summary>Finds a cast row as it is now, for a tap that was drawn before an edit changed the row.</summary>
public static class CastRowLookup
{
	/// <summary>The row of <paramref name="rows"/> for the same character as <paramref name="tapped"/>; the tapped row itself when that character is gone.</summary>
	public static CastRow Current(IReadOnlyList<CastRow> rows, CastRow tapped) =>
		rows.FirstOrDefault(r => r.Member.CharacterId == tapped.Member.CharacterId) ?? tapped;
}
