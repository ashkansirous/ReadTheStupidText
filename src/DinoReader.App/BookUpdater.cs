using System.Diagnostics;
using DinoReader.Core;
using ReadTheStupidText.Application.Usage;

namespace DinoReader;

/// <summary>
/// Downloads the store's newer versions of the books on the shelf in the background (Slice 181): on a cold start, at most once every 7 days, over any network, silently.
/// It never touches a stored book: each due package goes into <see cref="StagedBookUpdates"/>, and the next cold start applies it before the gallery first loads, when nothing
/// can be open. The time of the last finished check is saved when every due book was dealt with (downloaded, up to date, edited, missing from the store or not ours);
/// a failed starter install, an offline run or a failed download stops without saving, so the next open tries again.
/// </summary>
public sealed class BookUpdater(StoreClient store, StarterStories starters, StagedBookUpdates staged) : IBookTextSync
{
	public const string LastCheckKey = "dino.books.lastupdatecheck";

	private const string LogTag = "[BookUpdate]";
	private const string StoryPathFormat = "/api/stories/{0}/download?v={1}";

	// One run at a time in the whole process, whichever instance asks.
	private static readonly SemaphoreSlim Run = new(1, 1);

	private enum Baseline
	{
		Recorded,
		Unavailable,
		NetworkFailure,
	}

	/// <summary>Runs one check when 7 days have passed since the last finished one.</summary>
	public async Task RunIfDueAsync(CancellationToken cancel)
	{
		await SweepPacksAsync(cancel);
		if (!BookUpdateSchedule.IsDue(LastFinished(), DateTimeOffset.UtcNow))
		{
			Debug.WriteLine($"{LogTag} not due, no store call");
			return;
		}

		if (!await Run.WaitAsync(0, cancel))
		{
			return;
		}

		try
		{
			await RunGuardedAsync(cancel);
		}
		finally
		{
			Run.Release();
		}
	}

#if DEBUG
	/// <summary>
	/// Slice 209 (Debug only, for the <c>stage-book-update</c> test hook): stages the store's newer version of one book now, whatever the 7-day schedule says, and saves no check time,
	/// so the next cold start applies it and the <c>[BookUpdate]</c> apply lines time it. Returns <c>ok &lt;id&gt; v&lt;n&gt;</c>, or <c>failed &lt;reason&gt;</c> (offline, not in the store, edited,
	/// up to date, not on the shelf, another run busy, download failed): never throws for those. With <paramref name="force"/>, a copy that is already up to date is staged anyway
	/// (its recorded version is set one lower first) and the outcome ends with <c>forced</c>.
	/// </summary>
	public async Task<string> StageNowAsync(string id, CancellationToken cancel, bool force = false)
	{
		if (!await Run.WaitAsync(0, cancel))
		{
			return "failed busy";
		}

		try
		{
			string outcome = await StageNowGuardedAsync(id, force, cancel);
			Debug.WriteLine($"{LogTag} stage-now {id}: {outcome}");
			return outcome;
		}
		finally
		{
			Run.Release();
		}
	}

	private async Task<string> StageNowGuardedAsync(string id, bool force, CancellationToken cancel)
	{
		await starters.InstallOnceAsync();
		if (!starters.InstallComplete)
		{
			return "failed starters not installed";
		}

		StoreListing listing = await store.ListAsync(cancel);
		if (listing.Status != StoreLoadStatus.Ok)
		{
			return "failed offline";
		}

		StoreItem? item = listing.Items.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
		if (item is null)
		{
			return "failed not in the store";
		}

		if (!store.Sales.MayUpdate(item))
		{
			return "failed not owned"; // Slice 346: a sold book the store account does not own is never asked for
		}

		IReadOnlyList<BookUpdateDecision>? decisions = await PlanOneBookAsync(item, listing.Items, cancel);
		BookUpdateDecision? decision = decisions?.FirstOrDefault(d => string.Equals(d.Id, item.Id, StringComparison.OrdinalIgnoreCase));
		bool forced = force && decision is { Skip: BookUpdateSkip.UpToDate };
		if (forced)
		{
			decision = await ForceUpdateAsync(item, listing.Items, cancel);
		}

		if (decision is not { Action: BookUpdateAction.Update })
		{
			return $"failed {(decisions is null ? "network" : decision is null ? "not on the shelf" : decision.Skip?.ToString() ?? decision.Action.ToString())}";
		}

		if (!await StageOneAsync(item, cancel))
		{
			return "failed download";
		}

		// StageOneAsync also returns true for a package the store does not have (404): only a staged version counts as done.
		return staged.StagedVersion(item.Id) is { } have && have >= item.Version ? $"ok {item.Id} v{item.Version}{(forced ? " forced" : string.Empty)}" : "failed package not in the store";
	}

	// force=true on a copy that already matches the store: its text hash is recorded when missing (from the version it has), then its recorded version is set one lower, so the plan sees the
	// store's version as an update and the next start swaps it in as a real update would. Returns the new decision for the book (null when it is not on the shelf).
	private async Task<BookUpdateDecision?> ForceUpdateAsync(StoreItem item, IReadOnlyList<StoreItem> items, CancellationToken cancel)
	{
		Debug.WriteLine($"{LogTag} {item.Id}: up to date, forced to stage v{item.Version} anyway");
		if (StarterStories.RecordedTextHash(item.Id) is null && await RecordBaselineAsync(item.Id, cancel) != Baseline.Recorded)
		{
			return new BookUpdateDecision(item.Id, BookUpdateAction.Skip, BookUpdateSkip.NotInStore);
		}

		// The lowered version is only for this plan: left in place, the next launch's starter refresh (InstallOnceAsync, before the apply) would swap the bundled copy in, record
		// the store's version and the apply would drop the staged package as up to date. The apply lowers it again itself (MarkForcedApplyForTest).
		int? before = StarterStories.RecordedVersion(item.Id);
		StarterStories.RecordVersionForTest(item.Id, BookUpdatePlan.ForcedRecordedVersion(item.Version));
		IReadOnlyList<BookUpdateDecision> again;
		try
		{
			again = await PlanAsync(items);
		}
		finally
		{
			StarterStories.RestoreVersionForTest(item.Id, before);
		}

		BookUpdateDecision? decision = again.FirstOrDefault(d => string.Equals(d.Id, item.Id, StringComparison.OrdinalIgnoreCase));
		if (decision is { Action: BookUpdateAction.Update })
		{
			StarterStories.MarkForcedApplyForTest(item.Id);
		}

		return decision;
	}

	// Plans like the real check, but records a missing baseline for the one named book only, so the hook leaves the other shelf books' state as production would find it.
	private async Task<IReadOnlyList<BookUpdateDecision>?> PlanOneBookAsync(StoreItem item, IReadOnlyList<StoreItem> items, CancellationToken cancel)
	{
		IReadOnlyList<BookUpdateDecision> decisions = await PlanAsync(items);
		bool needsBaseline = decisions.Any(d => d.Action == BookUpdateAction.NeedsBaseline && string.Equals(d.Id, item.Id, StringComparison.OrdinalIgnoreCase));
		if (!needsBaseline)
		{
			return decisions;
		}

		if (await RecordBaselineAsync(item.Id, cancel) == Baseline.NetworkFailure)
		{
			return null;
		}

		return await PlanAsync(items);
	}
#endif

	private async Task RunGuardedAsync(CancellationToken cancel)
	{
		try
		{
			bool finished = await CheckAsync(cancel);
			Debug.WriteLine($"{LogTag} check {(finished ? "finished, time saved" : "not finished, will try at the next open")}");
			if (finished)
			{
				SaveCheckTime();
			}
		}
		catch (OperationCanceledException) when (cancel.IsCancellationRequested)
		{
			// no-fallback: cancelled by the app; the check runs again at the next open
			Debug.WriteLine($"{LogTag} cancelled");
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or StoryPackageException or InvalidDataException or InvalidOperationException or ArgumentException)
		{
			Debug.WriteLine($"{LogTag} check failed: {ex}");
			store.Fallbacks.Report(FallbackArea.Store, FallbackReason.UpdateCheckFailed);
		}
	}

	private static void SaveCheckTime() => Preferences.Default.Set(LastCheckKey, DateTimeOffset.UtcNow.ToString("O"));

	private static DateTimeOffset? LastFinished() =>
		DateTimeOffset.TryParse(Preferences.Default.Get(LastCheckKey, string.Empty), null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTimeOffset last) ? last : null;

	// True when the run is complete and the time may be saved. False when anything should be tried again at the next open: a starter that failed to install (a missing
	// starter would be planned as deleted), no network, or a failed baseline or download.
	private async Task<bool> CheckAsync(CancellationToken cancel)
	{
		await starters.InstallOnceAsync();
		if (!starters.InstallComplete)
		{
			Debug.WriteLine($"{LogTag} a starter is not installed yet, nothing done");
			return false;
		}

		StoreListing listing = await store.ListAsync(cancel);
		if (listing.Status != StoreLoadStatus.Ok)
		{
			Debug.WriteLine($"{LogTag} offline, nothing done");
			return false;
		}

		// Slice 346: a sold book the store account does not own (a copy kept from before the sale) is not asked for: no update, no refused request, and nothing on the shelf is touched.
		IReadOnlyList<StoreItem> fetchable = await store.FetchableAsync(listing.Items, cancel);
		IReadOnlyList<BookUpdateDecision>? decisions = await RecordBaselinesAsync(await PlanAsync(fetchable), fetchable, cancel);
		bool booksDone = decisions is not null && await StageAllAsync(decisions, fetchable, cancel);
		ISet<string> textBehind = await TextBehindAsync(fetchable, decisions);
		if (staged.HasPending)
		{
			staged.NotifyStaged(); // Slice 313: a shown shelf swaps the staged text in at once (only when a package really waits)
		}

		// A late pack, a new pack version or a language added since the download (Slice 246 C2). A pack that fails never keeps the check from finishing: it is tried again at the next scheduled check.
		// Slice 313: a book whose text is still behind keeps its old pack; the pack for the newer text comes once that text is in.
		await SyncPacksSafelyAsync(fetchable, textBehind, cancel);
		return booksDone;
	}

	// Slice 313: the shelf books whose text is still older than the store's after this plan (all the ones the cheap check finds when there is no plan, a failed baseline).
	private async Task<ISet<string>> TextBehindAsync(IReadOnlyList<StoreItem> items, IReadOnlyList<BookUpdateDecision>? decisions)
	{
		IReadOnlyList<string> behind = (await BehindAsync(items)).Select(b => b.Id).ToList();
		return decisions is null ? behind.ToHashSet(StringComparer.OrdinalIgnoreCase) : BookTextCurrency.StillBehind(decisions, behind);
	}

	// Slice 313: the catalogue books on the shelf whose recorded version is below the store's, from the listing alone (no hashing, no network). A book whose edit was already settled for that
	// version, and one given up after repeated failures, are not behind: neither forces a plan or holds its pack back.
	private async Task<IReadOnlyList<StoreBookVersion>> BehindAsync(IReadOnlyList<StoreItem> items)
	{
		HashSet<string> onShelf = (await store.InstalledIdsAsync()).Where(StarterStories.IsCatalogueId).ToHashSet(StringComparer.OrdinalIgnoreCase);
		return BookTextCurrency.Behind(items.Select(i => new StoreBookVersion(i.Id, i.Version)), onShelf, StarterStories.RecordedVersion, StarterStories.RecordedSeenVersion)
			.Where(b => !BookTextFailures.IsGivenUp(b.Id, b.Version)).ToList();
	}

	/// <summary>
	/// Slice 313: called with every store listing the app fetches for audio (launch, return, a language change), not weekly. A shelf book whose text is behind the store's is staged now
	/// (the shown shelf swaps it in at once, else the next time the shelf appears) and counted (<c>book_update</c>). Returns the ids whose text is still older, so their packs wait for the text.
	/// Nothing behind: no plan, no network, an empty set.
	/// </summary>
	public async Task<ISet<string>> SyncTextAsync(IReadOnlyList<StoreItem> items, Func<CancellationToken, Task> beforeDownload, CancellationToken cancel)
	{
		var behind = (await BehindAsync(items)).Select(b => b.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
		if (behind.Count == 0)
		{
			return behind;
		}

		if (await NeedsDownloadAsync(items, behind))
		{
			await beforeDownload(cancel); // the pack downloads' network rule (Wi-Fi unless mobile data was allowed), only when a download is due, outside the one-run lock
		}

		await Run.WaitAsync(cancel);
		try
		{
			return await SyncBehindGuardedAsync(items, behind, cancel);
		}
		finally
		{
			Run.Release();
		}
	}

	// Slice 313: whether the plan for the behind books needs a download (an update to fetch or a baseline). Edited and not-in-the-store books need none, so they never wait for Wi-Fi. A plan that
	// cannot be made says yes: the guarded sync below reports its failure.
	private async Task<bool> NeedsDownloadAsync(IReadOnlyList<StoreItem> items, ISet<string> behind)
	{
		try
		{
			await starters.InstallOnceAsync();
			if (!starters.InstallComplete)
			{
				return false;
			}

			return (await PlanAsync(items)).Any(d => behind.Contains(d.Id) && d.Action != BookUpdateAction.Skip);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or StoryPackageException or InvalidDataException or InvalidOperationException or ArgumentException)
		{
			// no-fallback: only decides whether to wait for Wi-Fi; the guarded sync that follows makes the same plan and reports its failure
			Debug.WriteLine($"{LogTag} text sync pre-plan failed: {ex.Message}");
			return true;
		}
	}

	private async Task<ISet<string>> SyncBehindGuardedAsync(IReadOnlyList<StoreItem> items, ISet<string> behind, CancellationToken cancel)
	{
		try
		{
			return await SyncBehindAsync(items, behind, cancel);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or StoryPackageException or InvalidDataException or InvalidOperationException or ArgumentException)
		{
			BookUpdateEvents.Line($"{LogTag} text sync failed, the packs wait: {ex.Message}");
			store.Fallbacks.Report(FallbackArea.Store, FallbackReason.UpdateCheckFailed);
			return behind;
		}
	}

	private async Task<ISet<string>> SyncBehindAsync(IReadOnlyList<StoreItem> items, ISet<string> behind, CancellationToken cancel)
	{
		await starters.InstallOnceAsync();
		if (!starters.InstallComplete)
		{
			BookUpdateEvents.Line($"{LogTag} text sync: a starter is not installed yet, the packs wait");
			return behind;
		}

		IReadOnlyList<BookUpdateDecision>? decisions = await RecordBaselinesAsync(await PlanAsync(items), items, cancel);
		if (decisions is null)
		{
			BookUpdateEvents.Line($"{LogTag} text sync: no network for the baseline, the packs wait");
			ReportEach(behind, items, BookUpdateResult.Failed); // retryable: nothing is given up, the next listing pass tries again; counted once for this pass
			store.Fallbacks.Report(FallbackArea.Store, FallbackReason.UpdateCheckFailed);
			return WithoutGivenUp(behind, items);
		}

		if (await StageBehindAsync(decisions, behind, items, cancel))
		{
			staged.NotifyStaged();
		}

		return WithoutGivenUp(BookTextCurrency.StillBehind(decisions, behind), items);
	}

	// A book given up for this store version (the store has no usable package for it) stops holding its pack back: sound is never kept waiting forever for a text that cannot come.
	private static ISet<string> WithoutGivenUp(ISet<string> ids, IReadOnlyList<StoreItem> items) =>
		ids.Where(id => !items.Any(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase) && BookTextFailures.IsGivenUp(i.Id, i.Version))).ToHashSet(StringComparer.OrdinalIgnoreCase);

	// True when at least one book's newer text is now staged. Each behind book is counted with its own result.
	private async Task<bool> StageBehindAsync(IReadOnlyList<BookUpdateDecision> decisions, ISet<string> behind, IReadOnlyList<StoreItem> items, CancellationToken cancel)
	{
		bool anyStaged = false;
		foreach (BookUpdateDecision decision in decisions.Where(d => behind.Contains(d.Id)))
		{
			cancel.ThrowIfCancellationRequested();
			StoreItem item = items.First(i => string.Equals(i.Id, decision.Id, StringComparison.OrdinalIgnoreCase));
			bool alreadyStaged = staged.StagedVersion(item.Id) is { } have && have >= item.Version;
			string? result = await StageResultAsync(decision, item, cancel);
			if (result is not null)
			{
				BookUpdateEvents.Report(item.Id, StarterStories.RecordedVersion(item.Id) ?? StarterUpdate.LegacyInstalledVersion, item.Version, result);
				anyStaged |= result == BookUpdateResult.Staged && !alreadyStaged; // only a package that just arrived wakes the shelf: one that already waited failed to apply
			}
		}

		return anyStaged;
	}

	private async Task<string?> StageResultAsync(BookUpdateDecision decision, StoreItem item, CancellationToken cancel)
	{
		if (decision.Skip == BookUpdateSkip.Edited)
		{
			StarterStories.RecordSeenVersion(item.Id, item.Version); // settled for this store version: the book is no longer behind, so it is not planned again at every listing
			return BookUpdateResult.SkippedEdited;
		}

		if (decision.Action != BookUpdateAction.Update)
		{
			return null;
		}

		StageOutcome outcome = await FetchAndStageAsync(item, cancel);
		if (outcome == StageOutcome.Staged && staged.StagedVersion(item.Id) is { } have && have >= item.Version)
		{
			return BookUpdateResult.Staged;
		}

		if (outcome == StageOutcome.NotInStore)
		{
			BookTextFailures.GiveUp(item.Id, item.Version); // the only permanent case: the store has no package for this version
		}

		store.Fallbacks.Report(FallbackArea.Store, FallbackReason.UpdateCheckFailed);
		return BookUpdateResult.Failed; // every other failure is retried at the next listing pass
	}

	private void ReportEach(IEnumerable<string> ids, IReadOnlyList<StoreItem> items, string result)
	{
		foreach (StoreItem item in items.Where(i => ids.Contains(i.Id, StringComparer.OrdinalIgnoreCase)))
		{
			BookUpdateEvents.Report(item.Id, StarterStories.RecordedVersion(item.Id) ?? StarterUpdate.LegacyInstalledVersion, item.Version, result);
		}
	}

	// Audio is optional: nothing the pack sync throws (except the caller's cancel) may fail the book check or the start.
	private async Task<bool> SyncPacksSafelyAsync(IReadOnlyList<StoreItem> items, ISet<string> textBehind, CancellationToken cancel)
	{
		try
		{
			return await SyncPacksAsync(items, textBehind, cancel);
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !cancel.IsCancellationRequested)
		{
			Debug.WriteLine($"{LogTag} the audio pack sync failed, will try at the next open: {ex.Message}");
			store.Fallbacks.Report(FallbackArea.Pack, FallbackReason.PackSyncFailed);
			return false;
		}
	}

	// Slice 246 C2: the audio packs of the books on the shelf, for the user's book languages and each story's own. True when none failed in a way a retry could fix (offline, a bad download);
	// the caller only logs it, so a failed pack is retried at the next scheduled check, not at every open.
	private async Task<bool> SyncPacksAsync(IReadOnlyList<StoreItem> items, ISet<string> textBehind, CancellationToken cancel)
	{
		HashSet<string> onShelf = await store.InstalledIdsAsync();
		// A language the grown-up cancelled (Slice 262) stays cancelled: the weekly check does not fetch its packs again (Slice 269).
		IReadOnlyList<string> languages = ShelfAudioPacks.WithoutCancelled(StoryLanguages.Current().All);
		Debug.WriteLine($"{LogTag} pack sync languages: {string.Join(",", languages)}");
		bool allOk = true;
		foreach (StoreItem item in items.Where(i => onShelf.Contains(i.Id)))
		{
			cancel.ThrowIfCancellationRequested();
			if (textBehind.Contains(item.Id))
			{
				BookUpdateEvents.Line($"{LogTag} {item.Id}: text is behind the store, its pack waits for the text");
				continue;
			}

			IReadOnlyList<(string Language, AudioPackInfo Pack)> wanted = StoreClient.WantedPacks(item, languages);
			allOk &= wanted.Count == 0 || await store.InstallPacksAsync(item, wanted, null, cancel);
		}

		return allOk;
	}

	// Packs of a book that is no longer on the shelf are deleted here, at the next cold start after the delete (the delete screen is outside this slice).
	private async Task SweepPacksAsync(CancellationToken cancel)
	{
		try
		{
			await store.Packs.SweepAsync(store.CompleteInstalledIdsAsync, cancel);
		}
		catch (OperationCanceledException) when (cancel.IsCancellationRequested)
		{
			// no-fallback: cancelled by the app; the sweep runs again at the next check
			Debug.WriteLine($"{LogTag} audio pack sweep cancelled");
		}
		catch (Exception ex)
		{
			// The sweep is housekeeping: whatever it hits, the weekly book check still runs.
			Debug.WriteLine($"{LogTag} could not sweep the audio packs: {ex.Message}");
			store.Fallbacks.Report(FallbackArea.Pack, FallbackReason.PackSyncFailed);
		}
	}

	private async Task<IReadOnlyList<BookUpdateDecision>> PlanAsync(IReadOnlyList<StoreItem> items)
	{
		IReadOnlyList<BookUpdateDecision> decisions = await staged.PlanAsync(items.Select(i => new StoreBookVersion(i.Id, i.Version)).ToList());
		foreach (BookUpdateDecision d in decisions)
		{
			Debug.WriteLine($"{LogTag} {d.Id}: {d.Action}{(d.Skip is { } why ? $" ({why})" : string.Empty)}");
		}

		return decisions;
	}

	// A book installed before the text hash was recorded: the installed version is fetched once (versions are never overwritten in R2) and its hash recorded, then the plan is made again.
	// A network failure returns null: the run stops and is retried at the next open. A baseline that is not in the store (404) or unreadable cannot prove the copy unedited, so that
	// book is skipped as not in the store and the run still finishes.
	private async Task<IReadOnlyList<BookUpdateDecision>?> RecordBaselinesAsync(IReadOnlyList<BookUpdateDecision> decisions, IReadOnlyList<StoreItem> items, CancellationToken cancel)
	{
		var pending = decisions.Where(d => d.Action == BookUpdateAction.NeedsBaseline).ToList();
		if (pending.Count == 0)
		{
			return decisions;
		}

		foreach (BookUpdateDecision decision in pending)
		{
			cancel.ThrowIfCancellationRequested();
			if (await RecordBaselineAsync(decision.Id, cancel) == Baseline.NetworkFailure)
			{
				return null;
			}
		}

		IReadOnlyList<BookUpdateDecision> again = await PlanAsync(items);
		return again.Select(d => d.Action == BookUpdateAction.NeedsBaseline ? new BookUpdateDecision(d.Id, BookUpdateAction.Skip, BookUpdateSkip.NotInStore) : d).ToList();
	}

	private async Task<Baseline> RecordBaselineAsync(string id, CancellationToken cancel)
	{
		int installed = StarterStories.RecordedVersion(id) ?? StarterUpdate.LegacyInstalledVersion;
		PackageFetch fetch = await store.FetchPackageFileAsync(string.Format(StoryPathFormat, Uri.EscapeDataString(id), installed), cancel);
		if (fetch.File is not { } file)
		{
			Debug.WriteLine($"{LogTag} {id}: baseline v{installed} {(fetch.NotFound ? "not in the store, book skipped" : "not fetched, run stopped")}");
			return fetch.NotFound ? Baseline.Unavailable : Baseline.NetworkFailure;
		}

		try
		{
			await using FileStream stream = File.OpenRead(file);
			StarterStories.RecordTextHash(id, await StoryPackageReader.ContentHashOfPackageAsync(stream));
			Debug.WriteLine($"{LogTag} {id}: baseline v{installed} recorded");
			return Baseline.Recorded;
		}
		catch (Exception ex) when (ex is StoryPackageException or InvalidDataException)
		{
			Debug.WriteLine($"{LogTag} {id}: baseline unreadable, book skipped: {ex.Message}");
			store.Fallbacks.Report(FallbackArea.Store, FallbackReason.UpdateCheckFailed);
			return Baseline.Unavailable;
		}
		finally
		{
			TryDelete(file);
		}
	}

	private async Task<bool> StageAllAsync(IReadOnlyList<BookUpdateDecision> decisions, IReadOnlyList<StoreItem> items, CancellationToken cancel)
	{
		foreach (BookUpdateDecision decision in decisions.Where(d => d.Action == BookUpdateAction.Update))
		{
			cancel.ThrowIfCancellationRequested();
			StoreItem item = items.First(i => string.Equals(i.Id, decision.Id, StringComparison.OrdinalIgnoreCase));
			if (!await StageOneAsync(item, cancel))
			{
				return false;
			}
		}

		return true;
	}

	// True when the run goes on: the package is staged (or already was), or the version is not in the store (404). False stops the run without saving the time:
	// a failed download or a storage error.
	private async Task<bool> StageOneAsync(StoreItem item, CancellationToken cancel)
	{
		StageOutcome outcome = await FetchAndStageAsync(item, cancel);
		if (outcome == StageOutcome.NotInStore)
		{
			BookTextFailures.GiveUp(item.Id, item.Version); // Slice 313: the weekly check gives up on a 404 exactly like the listing pass
			BookUpdateEvents.Report(item.Id, StarterStories.RecordedVersion(item.Id) ?? StarterUpdate.LegacyInstalledVersion, item.Version, BookUpdateResult.Failed);
			store.Fallbacks.Report(FallbackArea.Store, FallbackReason.UpdateCheckFailed);
		}

		return outcome != StageOutcome.Failed;
	}

	private enum StageOutcome
	{
		Staged,
		NotInStore,
		Failed,
	}

	private async Task<StageOutcome> FetchAndStageAsync(StoreItem item, CancellationToken cancel)
	{
		if (staged.StagedVersion(item.Id) is { } have && have >= item.Version)
		{
			Debug.WriteLine($"{LogTag} {item.Id}: v{have} already staged");
			return StageOutcome.Staged;
		}

		PackageFetch fetch = await store.FetchPackageFileAsync(item.DownloadUrl, cancel);
		if (fetch.File is not { } file)
		{
			Debug.WriteLine($"{LogTag} {item.Id}: {(fetch.NotFound ? "package not in the store, skipped" : "download failed, run stopped")}");
			return fetch.NotFound ? StageOutcome.NotInStore : StageOutcome.Failed;
		}

		try
		{
			staged.Stage(item.Id, item.Version, file);
			Debug.WriteLine($"{LogTag} {item.Id}: v{item.Version} staged for the next start");
			return StageOutcome.Staged;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
		{
			Debug.WriteLine($"{LogTag} {item.Id}: could not stage, run stopped: {ex.Message}");
			store.Fallbacks.Report(FallbackArea.Store, FallbackReason.UpdateCheckFailed);
			return StageOutcome.Failed;
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
			Debug.WriteLine($"{LogTag} could not remove a temp file: {ex.Message}");
		}
	}
}

/// <summary>
/// Slice 313: the <c>book_update</c> count (a shelf book was behind the store: result, from-version, to-version, no text and no title) and its Debug-only log line, which a scripted flow reads
/// through the test log. One count per book, versions and result in each process, so a listing fetched again does not repeat it (a failed result is the exception: every capped retry is counted). Counting goes through <see cref="UsageCounter"/>, so the one
/// production-only gate applies; it never throws.
/// </summary>
internal static class BookUpdateEvents
{
	private static readonly HashSet<string> Sent = [];

	public static void Line(string text)
	{
		Debug.WriteLine(text);
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write(text);
#endif
	}

	public static void Report(string bookId, int fromVersion, int toVersion, string result)
	{
		string detail = BookUpdateUsage.Detail(result, fromVersion, toVersion);
		Line($"[BookUpdate] book_update {detail}");
		lock (Sent)
		{
			// A failure is counted every time it is reported: once per listing pass (throttled to launch / return / language change), so a retried failure shows as retries, not as one event per process.
			if (!Sent.Add($"{bookId}|{detail}") && result != BookUpdateResult.Failed)
			{
				return;
			}
		}

		try
		{
			IPlatformApplication.Current?.Services.GetService<UsageCounter>()?.RecordShelfBook(DinoReader.Core.UsageEventName.BookUpdate, bookId, LanguageChoice.Current(), detail);
		}
		catch (Exception)
		{
			// no-fallback: counting is best effort and never reports itself
		}
	}
}
