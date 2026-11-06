#if DINO_TEST_BOOKS
using DinoReader.Core;

namespace DinoReader.Testing;

/// <summary>
/// The Debug test shelf (Slice 261): books for a scripted flow that cannot download from the live store. A book comes in through the store client's own download (the test store's
/// <c>testbook:</c> address), so it lands as any store book does: imported, recorded as a catalogue book, and the shelf is told. The <c>import-test-book</c> hook calls it for the
/// books a flow names, and the <c>skip-first-run</c> hook starts it for all four (<see cref="StartAll"/>), so a flow that skips the first run still has a shelf; the gallery waits for it
/// (<see cref="PendingAsync"/>, called by <see cref="StarterStories.InstallOnceAsync"/>) before its first read. Release has none of it.
/// </summary>
internal static class TestBooks
{
	private const string AllWord = "all";
	private static Task _pending = Task.CompletedTask;

	/// <summary>Completes when the books asked for by the last <see cref="StartAll"/> or <see cref="Start"/> are on the shelf (a failure is logged, not thrown).</summary>
	public static Task PendingAsync() => _pending;

	/// <summary>Imports all four test books in the background, oldest first.</summary>
	public static void StartAll(StoreClient store) => Start(store, AllWord);

	/// <summary>Imports the named test books (stems or package ids, comma separated; <c>all</c> for the four) in the background and returns the task that ends when they are done: null when all landed, else the reason.</summary>
	public static Task<string?> Start(StoreClient store, string names)
	{
		Task previous = _pending;
		Task<string?> import = ImportAfterAsync(previous, store, names);
		_pending = import;
		return import;
	}

	// Null when every book landed, else the reason (the hook logs it).
	private static async Task<string?> ImportAfterAsync(Task previous, StoreClient store, string names)
	{
		await previous;
		try
		{
			foreach (string name in Stems(names))
			{
				await ImportOneAsync(store, name);
			}

			return null;
		}
		catch (Exception ex)
		{
			// no-fallback: Debug-only test hook; the failure goes back to the hook as its result
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not import the test books '{names}': {ex}");
			return ex.Message;
		}
	}

	private static IEnumerable<string> Stems(string names) =>
		names.Trim().Equals(AllWord, StringComparison.OrdinalIgnoreCase)
			? TestStore.AllStems
			: names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	private static async Task ImportOneAsync(StoreClient store, string name)
	{
		StoreItem item = await TestStore.FindAsync(name) ?? throw new InvalidOperationException($"No test book {name}");
		DownloadOutcome outcome = await store.DownloadAsync(item, new Progress<double>(), CancellationToken.None);
		if (outcome != DownloadOutcome.Done)
		{
			throw new InvalidOperationException($"The test book {name} did not import: {outcome}");
		}
	}
}
#endif
