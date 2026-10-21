using DinoReader.Core;

namespace DinoReader;

/// <summary>What the gallery and manage grids bind to for one story: its cover and labels.</summary>
public sealed record StoryTile(Story Story, Color CoverColour, string? CoverImagePath)
{
	public string Title => Story.Title;

	public bool HasImage => CoverImagePath is not null;

	public bool ShowGeneratedCover => CoverImagePath is null;

	public string CastLabel => GalleryText.Cast(Story.Cast.Count);

	public static StoryTile From(Story story, IStoryLibrary library)
	{
		string? image = library.TryResolveFile(story.Id, story.CoverImageFile);
		return new StoryTile(story, Color.FromArgb(CoverPalette.ColourAt(story.CoverColourIndex)), image);
	}
}
