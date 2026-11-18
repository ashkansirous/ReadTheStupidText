using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>What the gallery grid binds to for one story: its cover, labels and the mode the "⋯" overlay follows.</summary>
public sealed record StoryTile(Story Story, Color CoverColour, string? CoverImagePath, GrownUpMode Mode)
{
	/// <summary>The title in the app language when the book has one (Slice 159); the story's own otherwise.</summary>
	public string? DisplayTitle { get; init; }

	public string Title => DisplayTitle ?? Story.Title;

	/// <summary>Slice 384: the subscription that opened this book has ended: greyed out on the parents' shelf with Subscribe again and Buy this book (the child's shelf does not get the tile at all).</summary>
	public bool IsLapsed { get; init; }

	/// <summary>The store's own price string for the book (Slice 398); null while the store has not given one.</summary>
	public string? BuyPrice { get; init; }

	private const double LockedCoverOpacity = 0.55;

	/// <summary>Slice 398 (mock SB6): a locked cover is dimmed to 55% (and greyed by <see cref="ShowGreyVeil"/>); the title and the two pills under it are not.</summary>
	public double TileOpacity => IsLapsed ? LockedCoverOpacity : 1.0;

	/// <summary>Slice 398: no "⋯" menu on a locked tile.</summary>
	public bool ShowMore => !IsLapsed;

	/// <summary>Slice 398: MAUI has no greyscale filter for an image on every platform, so a locked cover gets a grey veil over it that takes the colour out as far as a veil can.</summary>
	public bool ShowGreyVeil => IsLapsed;

	/// <summary>Slice 398: what a screen reader says on a locked cover; the caption "Locked: the subscription ended" is not drawn (mock SB6).</summary>
	public string? CoverAccessibleName => IsLapsed ? $"{Title}. {LockedLabel}" : null;

	public string LockedLabel => AppResources.Sub_Locked;

	public string SubscribeAgainLabel => AppResources.Sub_Again;

	/// <summary>"Buy · £0.99" with the store's price (Slice 398); with no price known the plain "Buy this book".</summary>
	public string BuyThisLabel => BuyPrice is { Length: > 0 } price ? AppResources.Format(AppResources.Sub_BuyPrice, price) : AppResources.Sub_BuyThis;

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

	public static StoryTile From(Story story, IStoryLibrary library, GrownUpMode mode, string? displayTitle = null, bool lapsed = false, string? buyPrice = null)
	{
		string? image = library.TryResolveFile(story.Id, story.CoverImageFile);
		return new StoryTile(story, Color.FromArgb(CoverPalette.ColourAt(story.CoverColourIndex)), image, mode) { DisplayTitle = displayTitle, IsLapsed = lapsed, BuyPrice = buyPrice };
	}
}

/// <summary>Slice 398 (mock SB7): whether the device is offline, behind one seam so a Debug test hook can pretend (the real answer is the platform's network state).</summary>
public static class NetworkProbe
{
#if DEBUG
	private static Func<bool>? _override;

	/// <summary>Raised when the Debug <c>fake-offline</c> hook changes the pretend answer, so the open gallery works out its banners again.</summary>
	public static event EventHandler? OverrideChanged;

	/// <summary>Only the Debug <c>fake-offline</c> hook sets this; null = ask the platform.</summary>
	public static void SetOverride(Func<bool>? offline)
	{
		_override = offline;
		OverrideChanged?.Invoke(null, EventArgs.Empty);
	}

	public static bool IsOffline() => _override?.Invoke() ?? PlatformOffline();
#else
	public static bool IsOffline() => PlatformOffline();
#endif

	// Only None and Local (no way out to the internet) count as offline; Unknown and ConstrainedInternet (a captive portal, a limited route) are online for the notice, which says "No internet" and so must not lie while connected.
	private static bool PlatformOffline() => Connectivity.Current.NetworkAccess is NetworkAccess.None or NetworkAccess.Local;
}

/// <summary>Maps the Core layout (<see cref="GalleryLayout"/>) onto the grid's items.</summary>
public static class GalleryItems
{
	public static List<StoryTile> Build(IReadOnlyList<Story> stories, IStoryLibrary library, GrownUpMode mode, Func<Story, string>? titleOf = null, Func<Story, bool>? lapsedOf = null, Func<Story, string?>? priceOf = null) =>
		GalleryLayout.Build(stories)
			.Select(e => StoryTile.From(e.Story, library, mode, titleOf?.Invoke(e.Story), lapsedOf?.Invoke(e.Story) ?? false, priceOf?.Invoke(e.Story)))
			.ToList();
}

/// <summary>Picks the story template for every item.</summary>
public sealed class GalleryTemplateSelector : DataTemplateSelector
{
	public DataTemplate? StoryTemplate { get; set; }

	protected override DataTemplate OnSelectTemplate(object item, BindableObject container) => StoryTemplate!;
}
