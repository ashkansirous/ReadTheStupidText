using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>What the gallery grid binds to for one story: its cover, labels and the mode the "⋯" overlay follows.</summary>
public sealed record StoryTile(Story Story, Color CoverColour, string? CoverImagePath, GrownUpMode Mode)
{
	/// <summary>The title in the app language when the book has one (Slice 159); the story's own otherwise.</summary>
	public string? DisplayTitle { get; init; }

	public string Title => DisplayTitle ?? Story.Title;

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

	/// <summary>The font of the title under the cover (Slice 323): Vazirmatn for a Farsi or Arabic title, as on the reading page, else the UI font of the screens. It follows the title's own direction, not the app language.
	/// The label's direction follows its own text through <c>TextFlowHandlers</c>.</summary>
	public string TitleFont => TitleIsRightToLeft ? DinoFonts.Persian : DinoFonts.Ui;

	private const float TintTowardsWhite = 0.7f;

	private static Color PaleTint(Color colour) => new(
		colour.Red + (1 - colour.Red) * TintTowardsWhite,
		colour.Green + (1 - colour.Green) * TintTowardsWhite,
		colour.Blue + (1 - colour.Blue) * TintTowardsWhite);

	/// <summary>What a screen reader says on the cover's "⋯" dot (Slice 189).</summary>
	public string MoreHint => GalleryText.BookOptions(Title);

	/// <summary>Whether the cover wears the "You" chip (Slice 279, mock frame 188): the book holds at least one of the grown-up's recordings.</summary>
	public bool HasYourVoice => StoryRecordings.Count(Story) > 0;

	/// <summary>The chip's text, also what a screen reader says.</summary>
	public string YourVoiceLabel => AppResources.Record_You;

	public static StoryTile From(Story story, IStoryLibrary library, GrownUpMode mode, string? displayTitle = null)
	{
		string? image = library.TryResolveFile(story.Id, story.CoverImageFile);
		return new StoryTile(story, Color.FromArgb(CoverPalette.ColourAt(story.CoverColourIndex)), image, mode) { DisplayTitle = displayTitle };
	}
}

/// <summary>Maps the Core layout (<see cref="GalleryLayout"/>) onto the grid's items.</summary>
public static class GalleryItems
{
	public static List<StoryTile> Build(IReadOnlyList<Story> stories, IStoryLibrary library, GrownUpMode mode, Func<Story, string>? titleOf = null) =>
		GalleryLayout.Build(stories)
			.Select(e => StoryTile.From(e.Story, library, mode, titleOf?.Invoke(e.Story)))
			.ToList();
}

/// <summary>Picks the story template for every item.</summary>
public sealed class GalleryTemplateSelector : DataTemplateSelector
{
	public DataTemplate? StoryTemplate { get; set; }

	protected override DataTemplate OnSelectTemplate(object item, BindableObject container) => StoryTemplate!;
}
