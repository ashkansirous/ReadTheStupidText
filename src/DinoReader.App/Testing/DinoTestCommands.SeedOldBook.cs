#if DEBUG
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Slice 313: <c>seed-old-book id=&lt;stem|id&gt;</c> makes a test book on the shelf look one version behind the test store: the book's text hash is recorded when it has none (so the update plan can tell
/// it was not edited) and its recorded version is lowered by one. It then switches the test store on and starts the shelf audio run, which is the one place the app learns the store's version at launch
/// or return (<see cref="BookUpdater.SyncTextAsync"/>), so a flow sees the newer text staged and swapped in at once and the pack kept or fetched after it. With a pack installed for the book first
/// (<c>install-audio-pack</c>) this is the "old text, pack made for the book" shape of the 2026-11-07 Freddy report. Outcome: <c>HOOK seed-old-book ok &lt;id&gt; v&lt;recorded&gt;</c> or <c>failed ...</c>;
/// the run's own lines are <c>[BookUpdate] ...</c> (<c>book_update updated:&lt;from&gt;:&lt;to&gt;</c>).
/// </summary>
internal static partial class DinoTestCommands
{
	private const string SeedOldBookName = "seed-old-book";

	private static async Task SeedOldBookAsync(string id)
	{
		try
		{
			IServiceProvider services = IPlatformApplication.Current!.Services;
			StoreItem? item = id.Trim().Length == 0 ? null : await TestStore.FindAsync(id.Trim());
			Story? story = item is null ? null : await FindWhenImportedAsync(services.GetRequiredService<IStoryLibrary>(), item.Id);
			if (item is null || story is null || !StarterStories.IsCatalogueId(item.Id))
			{
				Log($"{SeedOldBookName} failed no such test book on the shelf");
				return;
			}

			await RecordOldCopyAsync(services, item, story);
			TestStore.Mode = TestStoreMode.Ok;
			Log($"{SeedOldBookName} ok {item.Id} v{StarterStories.RecordedVersion(item.Id)}");
			services.GetRequiredService<ShelfAudioPacks>().Resume(force: true);
		}
		catch (Exception ex)
		{
			// no-fallback: Debug-only test hook; the fault is written to the hook log
			Log($"{SeedOldBookName} failed {TestHookLog.FaultSummary(ex)}");
		}
	}

	private static async Task RecordOldCopyAsync(IServiceProvider services, StoreItem item, Story story)
	{
		if (StarterStories.RecordedTextHash(item.Id) is null)
		{
			StarterStories.RecordTextHash(item.Id, await services.GetRequiredService<StarterStories>().ContentHashOfAsync(story));
		}

		StarterStories.RecordVersionForTest(item.Id, BookUpdatePlan.ForcedRecordedVersion(item.Version));
	}
}
#endif
