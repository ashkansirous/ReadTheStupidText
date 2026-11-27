using System.Diagnostics;
using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Slice 375 D: swaps the high-res pictures in later, in the background. There is no list of waiting books: each pass DERIVES the books to upgrade from the shelf, the store catalogue, which size is
/// installed and the failure ledger (<see cref="HighResUpgradePlan.Derive"/>). A pass starts at app start, after a book is installed or an update swapped in, and when the connection changes; it acts only
/// on an unmetered connection and only on a device that takes high-res (a tablet or a PC), fetches one book at a time with no loading screen, and only stages the package (<see cref="StagedBookUpdates"/>):
/// the shelf's own apply (cold start, or the shown shelf) does the swap, so an open book is never swapped. A book that cannot be fetched right now (an unsettled purchase, a store read that failed, no network)
/// is simply not in the pass; only a downloaded package that fails to parse counts, at the swap (<see cref="HighResLedger.MaxFailures"/>).
/// </summary>
public sealed class HighResUpgradeRunner(StoreClient store, StagedBookUpdates staged, HighResUpgrades upgrades, PictureSizeSource pictures)
{
	private static readonly SemaphoreSlim OneRun = new(1, 1);

	private int _started;
	private bool _reconciled;
	private static int _again; // shared with OneRun: a call that found a pass running asks it for one more

	/// <summary>Once per process (called from the shown shelf, on the main thread): reads the device if start-up could not, listens for the triggers and runs once now. Never throws.</summary>
	public void Start()
	{
		if (Interlocked.Exchange(ref _started, 1) == 1)
		{
			return;
		}

		pictures.InitializeDevice(); // a no-op when start-up already read it; a display that was not ready then is read now, still on the main thread
		pictures.ConnectionChanged += (_, _) => _ = RunAsync(CancellationToken.None); // the trigger and the reading come from the same source (iOS: the path monitor's own snapshot)
		store.LibraryChanged += (_, _) => _ = RunAsync(CancellationToken.None); // a book was installed: it may be one that wants its high-res pack
		staged.Swapped += (_, _) => _ = RunAsync(CancellationToken.None); // an update was swapped in: its new version may want its high-res pack
		pictures.StartWatching();
		_ = RunAsync(CancellationToken.None);
	}

	/// <summary>One pass. A call that arrives while a pass runs is not lost: the running call runs one more pass when it ends, so the last trigger is always seen. Never throws.</summary>
	public async Task RunAsync(CancellationToken cancel)
	{
		do
		{
			if (!await TryOwnAsync())
			{
				return;
			}

			Interlocked.Exchange(ref _again, 0); // the pass below reads the state after this, so a flag erased here is covered by it
			await RunOnePassAsync(cancel);
		}
		while (Volatile.Read(ref _again) == 1 && !cancel.IsCancellationRequested);
	}

	// A caller that finds a pass running sets the flag and tries once more. The owner checks the flag after it releases, so a flag set before the release is seen by the owner; one set after
	// it is followed by this second try, which then succeeds. No token on the wait: a cancelled one must not throw out of a "never throws" method.
	private static async Task<bool> TryOwnAsync()
	{
		if (await OneRun.WaitAsync(0))
		{
			return true;
		}

		Interlocked.Exchange(ref _again, 1);
		return await OneRun.WaitAsync(0);
	}

	private async Task RunOnePassAsync(CancellationToken cancel)
	{
		try
		{
			await PassAsync(cancel);
		}
		catch (OperationCanceledException)
		{
			// no-fallback: cancelled by the caller or by the connection leaving unmetered; the next trigger runs again
			HighResLog.Write("run cancelled");
		}
		catch (Exception ex)
		{
			HighResLog.Write($"run failed, the next trigger tries again: {ex.Message}");
			DinoFallbacks.Report(Sirous.Reading.Application.Usage.FallbackArea.Store, Sirous.Reading.Application.Usage.FallbackReason.UpdateCheckFailed);
		}
		finally
		{
			OneRun.Release();
		}
	}

	private async Task PassAsync(CancellationToken cancel)
	{
		if (!pictures.DeviceKnown)
		{
			await MainThread.InvokeOnMainThreadAsync(pictures.InitializeDevice); // review round 6 #5: a display not ready at start-up is read again at each pass
		}

		StoreListing? listing = null; // read once per pass: the quick part's listing is the high-res part's too
		if (pictures.Network() != PictureNetwork.None)
		{
			listing = await QuickPassAsync(cancel); // Slice 463: a quick first book becomes a standard one on any connection, a metered link included; before the high-res part, which only follows a standard copy
		}

		if (!pictures.DeviceWantsHighRes)
		{
			return; // a phone, or a display that could not be read: the standard size, no network
		}

		if (pictures.Network() != PictureNetwork.Unmetered)
		{
			HighResLog.Write("no unmetered connection, nothing started");
			return;
		}

		listing ??= await store.ListAsync(cancel);
		if (listing.Status != StoreLoadStatus.Ok)
		{
			HighResLog.Write("store not reachable, nothing started");
			return;
		}

		IReadOnlyList<StoreItem> due = await DeriveAsync(listing.Items, cancel);
		HighResLog.Write($"{due.Count} book(s) due for their high-res pack");
		await DownloadWhileUnmeteredAsync(due, cancel);
	}

	// The books to upgrade now, from what exists: the shelf, the catalogue, the installed-size record, staged files and the ledger. A book not fetchable right now is left out, never counted.
	private async Task<IReadOnlyList<StoreItem>> DeriveAsync(IReadOnlyList<StoreItem> listed, CancellationToken cancel)
	{
		HashSet<string> onShelf = await store.InstalledIdsAsync();
		IReadOnlyDictionary<string, int> quick = upgrades.InstalledQuick();
		var shelfVersions = onShelf.Where(id => !quick.ContainsKey(id)).ToDictionary(id => id, StarterStories.RecordedVersion, StringComparer.OrdinalIgnoreCase); // Slice 463: a quick copy becomes standard first; high-res follows the standard copy
		var stagedHigh = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (string id in onShelf)
		{
			if (staged.StagedHighResVersion(id) is { } version)
			{
				stagedHigh[id] = version;
			}
		}

		IReadOnlyList<StoreItem> fetchable = await store.FetchableAsync(listed, cancel);
		var fetchableIds = fetchable.Select(i => i.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
		return HighResUpgradePlan.Derive(pictures.DeviceWantsHighRes, shelfVersions, listed, fetchableIds, upgrades.InstalledHigh(), stagedHigh, upgrades.Ledger);
	}

	// Slice 463: the quick-to-standard upgrade. Nothing recorded as quick, nothing to do (no store call). A failed or killed download stages nothing (the package is fetched whole first), so the book simply stays quick and the next
	// trigger derives it again.
	// Review #8: a fault in this part is reported (QuickUpgradeFailed) and never fails the pass, so the high-res upgrades of other books still run. Returns the listing it read (null: none read), for the high-res part.
	private async Task<StoreListing?> QuickPassAsync(CancellationToken cancel)
	{
		StoreListing? listing = null;
		try
		{
			await DropOrphanedQuickRecordsAsync();
			if (upgrades.InstalledQuick().Count == 0 && upgrades.QuickPacks().Count == 0)
			{
				return null;
			}

			listing = await store.ListAsync(cancel);
			if (listing.Status == StoreLoadStatus.Ok)
			{
				await QuickUpgradesAsync(listing, cancel);
			}
			else
			{
				HighResLog.Write("store not reachable, no quick upgrade started");
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			HighResLog.Write($"the quick upgrade part failed, the high-res part still runs: {ex.Message}");
			store.Fallbacks.Report(Sirous.Reading.Application.Usage.FallbackArea.Store, Sirous.Reading.Application.Usage.FallbackReason.QuickUpgradeFailed);
		}

		return listing;
	}

	// Review round 2 (#6): a quick record with no book on the shelf (a kill between the record and the import) is dropped at the start of the pass. Skipped when the shelf could not be read completely.
	private async Task DropOrphanedQuickRecordsAsync()
	{
		if (_reconciled || await store.CompleteInstalledIdsAsync() is not { } onShelf)
		{
			return; // once per process, at the first pass: a later pass could meet a quick import still in flight (its record is written first)
		}

		_reconciled = true;
		foreach (string id in QuickUpgradePlan.OrphanedQuickRecords(upgrades.InstalledQuick(), onShelf.ToHashSet(StringComparer.OrdinalIgnoreCase), upgrades.QuickWrittenThisLaunch()))
		{
			upgrades.ForgetInstalledQuick(id);
			HighResLog.Write($"{id}: quick record dropped, the book is not on the shelf");
		}
	}

	private async Task QuickUpgradesAsync(StoreListing listing, CancellationToken cancel)
	{
		IReadOnlyList<StoreItem> fetchable = await store.FetchableAsync(listing.Items, cancel);
		var fetchableIds = fetchable.Select(i => i.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
		await UpgradeQuickPacksAsync(listing.Items, fetchableIds, cancel);
		IReadOnlyList<StoreItem> due = await DeriveQuickAsync(listing.Items, fetchableIds);
		HighResLog.Write($"{due.Count} book(s) due for their standard package over the quick one");
		foreach (StoreItem item in due)
		{
			if (!await UpgradeQuickOneAsync(item, cancel))
			{
				return;
			}
		}
	}

	private async Task<IReadOnlyList<StoreItem>> DeriveQuickAsync(IReadOnlyList<StoreItem> listed, IReadOnlySet<string> fetchableIds)
	{
		HashSet<string> onShelf = await store.InstalledIdsAsync();
		var shelfVersions = onShelf.ToDictionary(id => id, StarterStories.RecordedVersion, StringComparer.OrdinalIgnoreCase);
		var stagedStandard = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		foreach (string id in onShelf)
		{
			if (staged.StagedStandardVersion(id) is { } version)
			{
				stagedStandard[id] = version;
			}
		}

		return QuickUpgradePlan.Derive(shelfVersions, listed, fetchableIds, upgrades.InstalledQuick(), stagedStandard, upgrades.Ledger);
	}

	// One attempt. True: go on to the next book. False: stop the pass (no or lost network, a storage error); the next trigger derives the book again.
	private async Task<bool> UpgradeQuickOneAsync(StoreItem item, CancellationToken cancel)
	{
		HighResLog.Write($"{item.Id}: fetching the standard package of v{item.Version} to replace the quick one");
		PackageFetch fetch = await store.FetchPackageFileAsync(item.DownloadUrl, cancel);
		if (fetch.File is { } file)
		{
			return StageStandard(item.Id, item.Version, file);
		}

		if (fetch.Gone)
		{
			upgrades.RecordQuickUpgrade(item.Id, item.Version, HighResUpgradePlan.OutcomeForMissingPackage()); // a 404 counts as one failure of the ledger, like the high-res package's
			return true;
		}

		bool skipThisBook = fetch.Refused || fetch.NotFound;
		HighResLog.Write($"{item.Id}: the standard package gave no file ({(skipThisBook ? "skipped" : "stopped")}), the book stays quick");
		if (!skipThisBook)
		{
			store.Fallbacks.Report(Sirous.Reading.Application.Usage.FallbackArea.Store, Sirous.Reading.Application.Usage.FallbackReason.QuickUpgradeFailed);
		}

		return skipThisBook;
	}

	private bool StageStandard(string id, int version, string file)
	{
		try
		{
			staged.Stage(id, version, file, standard: true);
			HighResLog.Write($"{id}: standard v{version} staged, swapped in at the next cold start or when the shelf next appears");
			return true;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			HighResLog.Write($"{id}: could not stage the standard package, derived again next pass: {ex.Message}");
			store.Fallbacks.Report(Sirous.Reading.Application.Usage.FallbackArea.Store, Sirous.Reading.Application.Usage.FallbackReason.QuickUpgradeFailed);
			return HighResUpgradePlan.PassContinuesAfterStageFault(ex);
		}
		finally
		{
			TryDelete(file);
		}
	}

	// The packs a quick first book fetched as their quick copies are replaced once the book itself is standard (so the pack follows the book's own swap, never before it). A pack whose listing moved on, or whose book is gone, is
	// forgotten: the ordinary pack update installs the newer one. A failed replacement keeps the record, so the next pass tries again.
	private async Task UpgradeQuickPacksAsync(IReadOnlyList<StoreItem> listed, IReadOnlySet<string> fetchableIds, CancellationToken cancel)
	{
		if (await store.CompleteInstalledIdsAsync() is not { } shelf)
		{
			return; // check pass 2 #1 (rule 2): a shelf that could not be read completely says nothing about which books are gone; no record is dropped and nothing is replaced this pass
		}

		var onShelf = shelf.ToHashSet(StringComparer.OrdinalIgnoreCase);
		IReadOnlyDictionary<string, int> quickBooks = upgrades.InstalledQuick();
		foreach ((string id, string language, int version) in upgrades.QuickPacks())
		{
			if (quickBooks.ContainsKey(id))
			{
				continue; // the book is still quick: its packs follow its own swap
			}

			AudioPackInfo? pack = listed.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase))?.AudioIn(language);
			if (!onShelf.Contains(id) || pack is null || pack.Version != version)
			{
				bool listingMatches = pack is not null && pack.Version == version;
				if (QuickUpgradePlan.ShouldForgetQuickPack(onShelf.Contains(id), listingMatches, upgrades.QuickPackWrittenThisLaunch(id, language)))
				{
					upgrades.ForgetQuickPack(id, language);
				}

				continue;
			}

			if (!fetchableIds.Contains(id) || !QuickUpgradePlan.PackMayRetry(upgrades.Ledger, id, language, version))
			{
				continue; // not fetchable now, or given up for this version after MaxFailures (the pack stays the quick copy)
			}

			QuickPackUpgrade result = await store.UpgradeQuickPackAsync(id, language, pack, cancel);
			if (result == QuickPackUpgrade.Replaced)
			{
				upgrades.ForgetQuickPack(id, language);
				HighResLog.Write($"{id}/{language}: pack v{version} is the standard copy now");
			}
			else if (result == QuickPackUpgrade.BadFile)
			{
				upgrades.RecordQuickPackFailure(id, language, version); // check pass 1, rule 4: only a bad file (size, hash, zip, a 404) is counted; a network or disk fault leaves the record as it is
			}
		}
	}

	// A download in flight is cancelled the moment the connection stops being unmetered (the pass then ends as cancelled; the next trigger tries again), not only checked between books.
	private async Task DownloadWhileUnmeteredAsync(IReadOnlyList<StoreItem> due, CancellationToken cancel)
	{
		using var leftUnmetered = pictures.WatchConnection(network => network != PictureNetwork.Unmetered, cancel);
		foreach (StoreItem item in due)
		{
			if (pictures.Network() != PictureNetwork.Unmetered || !await UpgradeOneAsync(item, leftUnmetered.Token))
			{
				return;
			}
		}
	}

	// One attempt. True: go on to the next book. False: stop the pass (no or lost network, a storage error); the next trigger derives the book again.
	private async Task<bool> UpgradeOneAsync(StoreItem item, CancellationToken cancel)
	{
		HighResLog.Write($"{item.Id}: fetching the high-res pack of v{item.Version}");
		PackageFetch fetch = await store.FetchPackageFileAsync(item.HdDownloadUrl!, cancel);
		if (fetch.File is { } file)
		{
			return StageFetched(item.Id, item.Version, file);
		}

		if (fetch.Gone)
		{
			upgrades.Record(item.Id, item.Version, HighResUpgradePlan.OutcomeForMissingPackage()); // review round 7 #6: a 404 counts as one failure, given up after MaxFailures for this version
			return true;
		}

		bool skipThisBook = fetch.Refused || fetch.NotFound; // a refused or invalid purchase is not this pass's business: nothing is counted, the next pass derives the book again
		HighResLog.Write($"{item.Id}: high-res fetch gave no file ({(skipThisBook ? "skipped" : "stopped")})");
		return skipThisBook;
	}

	private bool StageFetched(string id, int version, string file)
	{
		try
		{
			staged.Stage(id, version, file, highRes: true);
			HighResLog.Write($"{id}: high-res v{version} staged, swapped in at the next cold start or when the shelf next appears"); // review round 6 #6: no shelf signal and no overlay for a background upgrade
			return true;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			HighResLog.Write($"{id}: could not stage the high-res pack, derived again next pass: {ex.Message}");
			store.Fallbacks.Report(Sirous.Reading.Application.Usage.FallbackArea.Store, Sirous.Reading.Application.Usage.FallbackReason.UpdateCheckFailed);
			return HighResUpgradePlan.PassContinuesAfterStageFault(ex); // review round 7 #5: a storage error stops the pass; a fault of this one book (a bad id) skips it only. Nothing says the package is bad (only its validation at apply does)
		}
		finally
		{
			TryDelete(file);
		}
	}

	private static void TryDelete(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// no-fallback: housekeeping delete; nothing depends on it, and it never replaces the outcome of the attempt it follows
			Debug.WriteLine($"[HighRes] could not remove a temp file: {ex.Message}");
		}
	}
}
