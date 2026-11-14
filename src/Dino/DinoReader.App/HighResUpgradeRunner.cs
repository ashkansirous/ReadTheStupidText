using System.Diagnostics;
using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Slice 375 D: swaps the high-res pictures in later, in the background, for the books <see cref="HighResUpgrades"/> says are waiting. It runs at app start and when the connection changes, acts only
/// on an unmetered connection, fetches one book at a time with no loading screen, and only stages the package (<see cref="StagedBookUpdates"/>): the shelf's own apply (cold start, or the
/// shown shelf) does the swap, so an open book is never swapped. Interrupts: a kill mid-download leaves a temp file that is discarded at the next start and the entry stays pending; offline or limited,
/// nothing starts and the next connection change tries again; a book updated, deleted or no longer offered in high-res loses its entry (<see cref="HighResUpgradeTable.Reconcile"/>).
/// </summary>
public sealed class HighResUpgradeRunner(StoreClient store, StagedBookUpdates staged, HighResUpgrades upgrades, PictureSizeSource pictures)
{
	private static readonly SemaphoreSlim OneRun = new(1, 1);

	private int _started;
	private static int _again; // shared with OneRun: a call that found a pass running asks it for one more

	/// <summary>Once per process: listens for the connection to change and runs once now (half-written staged files are discarded at app start, <see cref="StagedBookUpdates.DiscardPartialFiles"/>). Never throws.</summary>
	public void Start()
	{
		if (Interlocked.Exchange(ref _started, 1) == 1)
		{
			return;
		}

		pictures.ConnectionChanged += (_, _) => _ = RunAsync(CancellationToken.None); // the trigger and the reading come from the same source (iOS: the path monitor's own snapshot)
		pictures.StartWatching();
		_ = RunAsync(CancellationToken.None);
	}

	/// <summary>One pass over the waiting books. A call that arrives while a pass runs is not lost (review #4): the running call runs one more pass when it ends, so the last connection change is always seen. Never throws.</summary>
	public async Task RunAsync(CancellationToken cancel)
	{
		if (upgrades.Snapshot().Count == 0)
		{
			return; // outcome rule: a pass runs only when the table has an entry
		}

		do
		{
			if (!await TryOwnAsync())
			{
				return;
			}

			Interlocked.Exchange(ref _again, 0); // the pass below reads the connection after this, so a flag erased here is covered by it
			await RunOnePassAsync(cancel);
		}
		while (Volatile.Read(ref _again) == 1 && !cancel.IsCancellationRequested);
	}

	// Review #3 (check 3): a caller that finds a pass running sets the flag and tries once more. The owner checks the flag after it releases, so a flag set before the release is seen by the owner; one set after
	// it is followed by this second try, which then succeeds. Review #7: no token on the wait, a cancelled one must not throw out of a "never throws" method.
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
			// no-fallback: cancelled by the caller; the next start or connection change runs again
			HighResLog.Write("run cancelled");
		}
		catch (Exception ex)
		{
			HighResLog.Write($"run failed, the next start or connection change tries again: {ex.Message}");
			DinoFallbacks.Report(Sirous.Reading.Application.Usage.FallbackArea.Store, Sirous.Reading.Application.Usage.FallbackReason.UpdateCheckFailed);
		}
		finally
		{
			OneRun.Release();
		}
	}

	private async Task PassAsync(CancellationToken cancel)
	{
		RepairLostStagedFiles();
		if (upgrades.Snapshot().Count == 0)
		{
			return;
		}

		if (pictures.Network() != PictureNetwork.Unmetered)
		{
			HighResLog.Write($"{upgrades.Snapshot().Count} waiting, no unmetered connection, nothing started");
			return;
		}

		StoreListing listing = await store.ListAsync(cancel);
		if (listing.Status != StoreLoadStatus.Ok)
		{
			HighResLog.Write("store not reachable, nothing started");
			return;
		}

		await ReconcileAsync(listing.Items);
		IReadOnlyList<StoreItem> fetchable = await store.FetchableAsync(listing.Items, cancel);
		await DownloadWhileUnmeteredAsync(listing.Items, fetchable, cancel);
	}

	// Review #4: a download in flight is cancelled the moment the connection stops being unmetered (the run then ends as cancelled; the next unmetered change tries again), not only checked between books.
	private async Task DownloadWhileUnmeteredAsync(IReadOnlyList<StoreItem> listed, IReadOnlyList<StoreItem> fetchable, CancellationToken cancel)
	{
		using var leftUnmetered = CancellationTokenSource.CreateLinkedTokenSource(cancel);
		var refused = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // outcome rule: a refused book is skipped for the rest of this run only
		void OnChanged(object? sender, EventArgs e)
		{
			if (pictures.Network() == PictureNetwork.Unmetered)
			{
				return;
			}

			try
			{
				leftUnmetered.Cancel();
			}
			catch (ObjectDisposedException)
			{
				// no-fallback: the pass already ended, nothing is left to cancel
			}
		}

		pictures.ConnectionChanged += OnChanged;
		try
		{
			while (upgrades.Snapshot().NextPending(refused) is { } next && pictures.Network() == PictureNetwork.Unmetered)
			{
				HighResOutcome? outcome = await UpgradeOneAsync(next.Key, next.Value, listed, fetchable, leftUnmetered.Token); // null: staged, the entry moved on to Staged and the swap is the shelf's
				if (outcome is { } ended)
				{
					upgrades.Record(next.Key, next.Value.Version, ended);
				}

				if (outcome == HighResOutcome.Refused)
				{
					refused.Add(next.Key);
				}
				else if (outcome == HighResOutcome.Transient)
				{
					return;
				}
			}
		}
		finally
		{
			pictures.ConnectionChanged -= OnChanged;
		}
	}

	// A staged entry whose file is gone (deleted, never renamed in) is pending again, so it is fetched again.
	private void RepairLostStagedFiles()
	{
		foreach ((string id, HighResUpgrade entry) in upgrades.Snapshot().Entries.Where(e => e.Value.Phase == HighResPhase.Staged).ToList())
		{
			if (staged.StagedHighResVersion(id) != entry.Version)
			{
				HighResLog.Write($"{id}: staged v{entry.Version} file is gone, pending again");
				upgrades.MarkLost(id, entry.Version); // review #3: no failure counted, the package was never found bad
			}
		}
	}

	private async Task ReconcileAsync(IReadOnlyList<StoreItem> items)
	{
		HashSet<string> onShelf = await store.InstalledIdsAsync();
		HighResUpgradeTable table = upgrades.Snapshot();
		upgrades.Reconcile(
			id => onShelf.Contains(id) ? StarterStories.RecordedVersion(id) : null,
			staged.StagedVersion,
			id => OffersHighRes(items, id, table.Get(id)?.Version));
	}

	// Outcome rule: a book the catalogue does not list at all is unknown (null), never "no longer offered"; only a listed version without an hd package, or another listed version, is.
	private static bool? OffersHighRes(IReadOnlyList<StoreItem> items, string id, int? version) =>
		items.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase)) is { } item ? item.HasHighRes && item.Version == version : null;

	// One attempt, one outcome (null: staged, the entry is Staged now). Transient stops the pass, Refused only skips the book.
	private async Task<HighResOutcome?> UpgradeOneAsync(string id, HighResUpgrade entry, IReadOnlyList<StoreItem> listed, IReadOnlyList<StoreItem> fetchable, CancellationToken cancel)
	{
		StoreItem? offered = listed.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
		if (offered is not null && (offered.Version != entry.Version || !offered.HasHighRes))
		{
			HighResLog.Write($"{id}: the catalogue no longer offers high-res for v{entry.Version}, upgrade dropped");
			return HighResOutcome.Gone;
		}

		if (upgrades.InstalledIsHigh(id, entry.Version))
		{
			HighResLog.Write($"{id}: the installed v{entry.Version} already is high-res");
			return HighResOutcome.Done;
		}

		StoreItem? item = fetchable.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
		if (item is null)
		{
			HighResLog.Write($"{id}: not in the catalogue or purchase not settled yet, stays pending");
			return HighResOutcome.Transient;
		}

		HighResLog.Write($"{id}: fetching the high-res pack of v{entry.Version}");
		PackageFetch fetch = await store.FetchPackageFileAsync(item.HdDownloadUrl!, cancel);
		return fetch.File is { } file ? StageFetched(id, entry.Version, file) : OutcomeOfUnfetched(id, fetch);
	}

	private static HighResOutcome OutcomeOfUnfetched(string id, PackageFetch fetch)
	{
		HighResOutcome outcome = fetch.Gone ? HighResOutcome.Gone : fetch.Refused ? HighResOutcome.Refused : HighResOutcome.Transient;
		HighResLog.Write($"{id}: high-res fetch gave no file ({outcome})");
		return outcome;
	}

	private HighResOutcome? StageFetched(string id, int version, string file)
	{
		try
		{
			upgrades.MarkStaged(id, version); // review #2: the table first, so an apply that sees the renamed file never finds a still-pending entry and drops it as stale
			staged.Stage(id, version, file, highRes: true);
			HighResLog.Write($"{id}: high-res v{version} staged, the shelf swaps it in");
			staged.NotifyStaged();
			return null;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
		{
			upgrades.MarkLost(id, version); // back to pending, no failure counted here: Record counts one only for a bad package
			HighResLog.Write($"{id}: could not stage the high-res pack, stays pending: {ex.Message}");
			store.Fallbacks.Report(Sirous.Reading.Application.Usage.FallbackArea.Store, Sirous.Reading.Application.Usage.FallbackReason.UpdateCheckFailed);
			return HighResOutcome.Transient; // Stage only copies and renames: an argument, IO or storage error never says the package is bad (only its validation at apply does)
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
			// no-fallback: housekeeping delete; nothing depends on it
			Debug.WriteLine($"[HighRes] could not remove a temp file: {ex.Message}");
		}
	}
}
