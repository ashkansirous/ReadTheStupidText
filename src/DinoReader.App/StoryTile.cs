using DinoReader.Core;

namespace DinoReader;

/// <summary>What the gallery grid binds to for one story: its cover, labels and the mode the overlay follows.</summary>
public sealed record StoryTile(Story Story, Color CoverColour, string? CoverImagePath, GrownUpMode Mode)
{
	public string Title => Story.Title;

	public bool HasImage => CoverImagePath is not null;

	public bool ShowGeneratedCover => CoverImagePath is null;

	private bool TitleIsRightToLeft => TextDirection.StartsRightToLeft(Title);

	/// <summary>Latin generated covers: white Andika on the saturated colour.</summary>
	public bool ShowLatinCoverTitle => ShowGeneratedCover && !TitleIsRightToLeft;

	/// <summary>Farsi and Arabic generated covers: dark Vazirmatn on a pale colour (Slice 96 mock).</summary>
	public bool ShowRtlCoverTitle => ShowGeneratedCover && TitleIsRightToLeft;

	/// <summary>The chosen colour, as a pale tint when a generated cover carries dark Farsi or Arabic text.</summary>
	public Color CoverFill => TitleIsRightToLeft && ShowGeneratedCover ? PaleTint(CoverColour) : CoverColour;

	public string RtlCoverFont => DinoFonts.Persian;

	private const float TintTowardsWhite = 0.7f;

	private static Color PaleTint(Color colour) => new(
		colour.Red + (1 - colour.Red) * TintTowardsWhite,
		colour.Green + (1 - colour.Green) * TintTowardsWhite,
		colour.Blue + (1 - colour.Blue) * TintTowardsWhite);

	public string EditHint => GalleryText.EditStory(Title);

	public string CastHint => GalleryText.CastStory(Title);

	public string DeleteHint => GalleryText.DeleteStory(Title);

	/// <summary>The developer-only export badge (Slice 91): shown only with the developer switch on.</summary>
	public bool ShowExport => DeveloperMode.Enabled;

	public string ExportHint => GalleryText.ExportStory(Title);

	public static StoryTile From(Story story, IStoryLibrary library, GrownUpMode mode)
	{
		string? image = library.TryResolveFile(story.Id, story.CoverImageFile);
		return new StoryTile(story, Color.FromArgb(CoverPalette.ColourAt(story.CoverColourIndex)), image, mode);
	}
}

/// <summary>The "Add a story" tile's place in the gallery's item list (grown-up mode only).</summary>
public sealed class AddStoryEntry
{
	public static readonly AddStoryEntry Instance = new();

	private AddStoryEntry()
	{
	}
}

/// <summary>Maps the Core layout (<see cref="GalleryLayout"/>) onto the grid's items.</summary>
public static class GalleryItems
{
	public static List<object> Build(IReadOnlyList<Story> stories, IStoryLibrary library, GrownUpMode mode) =>
		GalleryLayout.Build(stories, mode.IsGrownUp)
			.Select(e => e.Kind == GalleryEntryKind.AddStory ? (object)AddStoryEntry.Instance : StoryTile.From(e.Story!, library, mode))
			.ToList();
}

/// <summary>Picks the Add tile's template for <see cref="AddStoryEntry"/> and the story template for every story.</summary>
public sealed class GalleryTemplateSelector : DataTemplateSelector
{
	public DataTemplate? AddTemplate { get; set; }

	public DataTemplate? StoryTemplate { get; set; }

	protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
		(item is AddStoryEntry ? AddTemplate : StoryTemplate)!;
}
