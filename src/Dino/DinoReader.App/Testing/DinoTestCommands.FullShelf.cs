#if DEBUG
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using Sirous.Reading.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Slice 404: <c>full-shelf</c> puts the whole live store catalogue on the shelf, for the store pictures: a shelf or book shot never shows four lonely covers. Free books come from the live store through the
/// store client's own download. The Worker refuses a paid book without a real receipt, so a paid book is imported from its package in <c>&lt;app data&gt;/full-shelf/&lt;id&gt;.dino</c> (built by BookBuilder and
/// copied onto the device by the store-shot script) through the test store's <c>testfile:</c> address: it lands as any store book does, with its real cover, and no receipt is faked. The hook switches the test store
/// off while it runs (it wants the live list) and puts the mode back at the end. It waits for the books <c>skip-first-run</c> started, checks every paid book's package is there, and only then puts the books on the shelf one at a time (new ones first, each test copy replaced by its real book with the copy
/// put back when that fetch fails, the pinned last book at the end). It writes <see cref="FullShelfPlan.DoneMarker"/> when every book landed, and a later call ends <c>ok already</c> (a retried shot). Outcome: <c>HOOK full-shelf ok &lt;n&gt;</c> (n books fetched or already there), <c>HOOK full-shelf book &lt;id&gt; missing-package</c> then <c>failed ...</c> (nothing cleared), <c>failed ...</c>
/// (offline, empty list, or a book that did not land, with how many did) or <c>fault ...</c>. A flow calls it after <c>skip-first-run</c>.
/// </summary>
internal static partial class DinoTestCommands
{
	private const string FullShelfName = "full-shelf";

	private static async Task FullShelfAsync()
	{
		TestStoreMode previousMode = TestStore.Mode;
		try
		{
			TestStore.Mode = TestStoreMode.Off; // the live list, not the four fixture books
			await TestBooks.PendingAsync();
			if (File.Exists(DoneMarkerPath()))
			{
				Log($"{FullShelfName} ok already"); // a retried shot: the shelf is full, nothing is cleared or fetched again
				return;
			}

			IServiceProvider services = IPlatformApplication.Current!.Services;
			StoreClient store = services.GetRequiredService<StoreClient>();
			StoreListing listing = await store.ListAsync(CancellationToken.None);
			if (listing.Status != StoreLoadStatus.Ok || listing.Items.Count == 0)
			{
				Log($"{FullShelfName} failed no live list {listing.Status} {listing.Items.Count}");
				return;
			}

			IReadOnlyDictionary<string, StoreItem> testItems = await TestBookItemsAsync();
			FullShelfPlan plan = FullShelfPlan.For(listing.Items, testItems.Keys);
			if (!PackagesPresent(plan))
			{
				return; // the shelf is left as it was: nothing is cleared before every book can land
			}

			int landed = await PutOnShelfAsync(store, services.GetRequiredService<IStoryLibrary>(), plan, testItems);
			if (landed == plan.Download.Count)
			{
				File.WriteAllText(DoneMarkerPath(), FullShelfName);
			}

			Log(landed == plan.Download.Count ? $"{FullShelfName} ok {landed}" : $"{FullShelfName} failed {landed} of {plan.Download.Count} books landed");
		}
		catch (Exception ex)
		{
			// no-fallback: Debug-only test hook; the fault is written to the hook log
			Log($"{FullShelfName} fault {TestHookLog.FaultSummary(ex)}");
		}
		finally
		{
			TestStore.Mode = previousMode;
		}
	}

	private static string PackageFolderPath() => Path.Combine(FileSystem.AppDataDirectory, FullShelfPlan.PackageFolder);

	// True when every paid book's package file is in the folder; each missing one is logged and the hook ends failed.
	private static bool PackagesPresent(FullShelfPlan plan)
	{
		string folder = PackageFolderPath();
		var present = new HashSet<string>(Directory.Exists(folder) ? Directory.EnumerateFiles(folder).Select(file => Path.GetFileName(file)) : [], StringComparer.OrdinalIgnoreCase);
		IReadOnlyList<string> missing = plan.MissingPackages(present);
		foreach (string id in missing)
		{
			Log($"{FullShelfName} book {id} missing-package");
		}

		if (missing.Count > 0)
		{
			Log($"{FullShelfName} failed {missing.Count} paid book packages missing in {FullShelfPlan.PackageFolder}");
		}

		return missing.Count == 0;
	}

	private static string DoneMarkerPath() => Path.Combine(FileSystem.AppDataDirectory, FullShelfPlan.DoneMarker);

	// The Debug test books by their store id: what skip-first-run put on the shelf, and what a failed replacement puts back.
	private static async Task<IReadOnlyDictionary<string, StoreItem>> TestBookItemsAsync()
	{
		var items = new Dictionary<string, StoreItem>(StringComparer.OrdinalIgnoreCase);
		foreach (string stem in TestStore.AllStems)
		{
			if (await TestStore.FindAsync(stem) is { } item)
			{
				items[item.Id] = item;
			}
		}

		return items;
	}

	private static async Task DeleteFromShelfAsync(IStoryLibrary library, string sourceId)
	{
		foreach (Story story in await library.ListAsync())
		{
			if (string.Equals(story.SourceId, sourceId, StringComparison.OrdinalIgnoreCase))
			{
				await library.DeleteAsync(story.Id);
			}
		}
	}

	// One book at a time in plan order. A book that replaces a test copy is fetched only after the copy is deleted (the store client skips a book already on the shelf), so when the fetch fails the test copy is put back:
	// the shelf is never emptier than it was. Returns how many books landed.
	private static async Task<int> PutOnShelfAsync(StoreClient store, IStoryLibrary library, FullShelfPlan plan, IReadOnlyDictionary<string, StoreItem> testItems)
	{
		int landed = 0;
		foreach (StoreItem item in plan.Download)
		{
			bool replaces = plan.ClearFirst.Contains(item.Id);
			if (replaces)
			{
				await DeleteFromShelfAsync(library, item.Id);
			}

			DownloadOutcome outcome = await store.DownloadAsync(FetchableItem(item), new Progress<double>(), CancellationToken.None);
			Log($"{FullShelfName} book {item.Id} {outcome}");
			if (outcome == DownloadOutcome.Done)
			{
				landed++;
			}
			else if (replaces && testItems.TryGetValue(item.Id, out StoreItem? testItem))
			{
				await store.DownloadAsync(testItem, new Progress<double>(), CancellationToken.None);
			}
		}

		return landed;
	}

	// The book as the store client should fetch it: a free one from the live store, a paid one from its package file (the standard pack only: no high-res address).
	private static StoreItem FetchableItem(StoreItem item) =>
		item.Sale == BookSale.Paid ? item with { DownloadUrl = TestStore.FileScheme + Path.Combine(PackageFolderPath(), FullShelfPlan.PackageName(item.Id)), HdDownloadUrl = null } : item;
}
#endif
