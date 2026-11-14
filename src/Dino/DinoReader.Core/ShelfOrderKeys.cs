namespace DinoReader.Core;

/// <summary>The keyboard's step in the reorder mode's grid (Slice 264): which spot an arrow key goes to.</summary>
public static class ShelfOrderKeys
{
	/// <summary>The spot after an arrow key from <paramref name="index"/>: Left and Right step one, Up and Down one row; a step off the grid stays where it is. No other action moves.</summary>
	public static int Target(int index, int count, int columns, DinoKeyAction arrow)
	{
		int step = arrow switch
		{
			DinoKeyAction.ReorderLeft => -1,
			DinoKeyAction.ReorderRight => 1,
			DinoKeyAction.ReorderUp => -Math.Max(1, columns),
			DinoKeyAction.ReorderDown => Math.Max(1, columns),
			_ => 0,
		};
		int target = index + step;
		return target < 0 || target >= count ? index : target;
	}
}
