using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>The gallery and grown-up screens' copy that is composed from numbers or names (Slice 61), in the UI language (Slice 66).</summary>
public static class GalleryText
{
	private const string SummarySeparator = " · ";
	private const int Singular = 1;

	public static string Characters(int count) =>
		AppResources.Format(count == Singular ? AppResources.Gallery_Characters_One : AppResources.Gallery_Characters_Other, count);

	public static string PagesSummary(int count) => AppResources.Format(AppResources.Gallery_PagesSummary, count);

	public static string DeleteTitle(string title) => AppResources.Format(AppResources.Gallery_DeleteTitle, title);

	public static string EditStory(string title) => AppResources.Format(AppResources.Gallery_EditStory, title);

	public static string CastStory(string title) => AppResources.Format(AppResources.Gallery_CastStory, title);

	public static string DeleteStory(string title) => AppResources.Format(AppResources.Gallery_DeleteStoryLabel, title);

	public static string ExportStory(string title) => AppResources.Gallery_ExportStory + SummarySeparator + title;

	public static string PagesRead(int pages) =>
		AppResources.Format(pages == Singular ? AppResources.Gallery_PagesRead_One : AppResources.Gallery_PagesRead_Other, pages);

	public static string CharactersFound(int characters) =>
		AppResources.Format(characters == Singular ? AppResources.Gallery_CharactersFound_One : AppResources.Gallery_CharactersFound_Other, characters);

	public static string ImportSummary(int pages, int characters) => PagesRead(pages) + SummarySeparator + CharactersFound(characters);
}
