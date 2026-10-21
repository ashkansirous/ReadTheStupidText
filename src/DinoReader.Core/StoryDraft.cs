

namespace DinoReader.Core;

/// <summary>The editable copy of a story while the edit screens are open; nothing is saved until Save.</summary>
public sealed class StoryDraft(Story original)
{
	public Story Original { get; } = original;

	public string Title { get; set; } = original.Title;

	public CoverStyle CoverStyle { get; set; } = original.CoverStyle;

	public List<StoryPage> Pages { get; } = [.. original.Pages];

	public bool HasPageImages => Pages.Any(p => p.ImageFile is not null);

	public bool TextChanged => Story.JoinPages(Pages) != Original.Text;

	/// <summary>Moves a page up (-1) or down (+1); does nothing at either end.</summary>
	public void Move(int index, int direction)
	{
		int target = index + direction;
		if (index < 0 || index >= Pages.Count || target < 0 || target >= Pages.Count)
		{
			return;
		}

		(Pages[index], Pages[target]) = (Pages[target], Pages[index]);
	}
}
