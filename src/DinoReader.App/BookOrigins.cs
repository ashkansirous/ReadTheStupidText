using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>Tells where a shelf book came from (Slice 189): the app's bundled starters, the Store, or a grown-up's own book.</summary>
public static class BookOrigins
{
	public static BookOrigin Of(Story story)
	{
		string? id = UsageEvent.ShelfId(story.SourceId);
		bool catalogue = id is not null && StarterStories.IsCatalogueId(id);
		return BookSheetPlan.OriginOf(catalogue, catalogue && StarterStories.IsStarterId(id!));
	}

	/// <summary>The line under a book's name in its options sheet: where it came from.</summary>
	public static string Label(BookOrigin origin) => origin switch
	{
		BookOrigin.Store => AppResources.Book_FromStore,
		BookOrigin.Starter => AppResources.Book_Starter,
		_ => AppResources.Book_Own,
	};
}
