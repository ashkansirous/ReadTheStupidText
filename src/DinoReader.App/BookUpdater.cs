using System.Diagnostics;
using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Downloads the store's newer versions of the books on the shelf in the background (Slice 181): on a cold start, at most once every 7 days, over any network, silently.
/// It never touches a stored book: each due package goes into <see cref="StagedBookUpdates"/>, and the next cold start applies it before the gallery first loads, when nothing
/// can be open. The time of the last finished check is saved when every due book was dealt with (downloaded, up to date, edited, missing from the store or not ours);
/// a failed starter install, an offline run or a failed download stops without saving, so the next open tries again.
/// </summary>
public sealed class BookUpdater(StoreClient store, StarterStories starters, StagedBookUpdates staged)
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
			Debug.WriteLine($"{LogTag} cancelled");
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or StoryPackageException or InvalidDataException or InvalidOperationException or ArgumentException)
		{
			Debug.WriteLine($"{LogTag} check failed: {ex}");
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

		IReadOnlyList<BookUpdateDecision>? decisions = await RecordBaselinesAsync(await PlanAsync(listing.Items), listing.Items, cancel);
		return decisions is not null && await StageAllAsync(decisions, listing.Items, cancel);
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
		if (staged.StagedVersion(item.Id) is { } have && have >= item.Version)
		{
			Debug.WriteLine($"{LogTag} {item.Id}: v{have} already staged");
			return true;
		}

		PackageFetch fetch = await store.FetchPackageFileAsync(item.DownloadUrl, cancel);
		if (fetch.File is not { } file)
		{
			Debug.WriteLine($"{LogTag} {item.Id}: {(fetch.NotFound ? "package not in the store, skipped" : "download failed, run stopped")}");
			return fetch.NotFound;
		}

		try
		{
			staged.Stage(item.Id, item.Version, file);
			Debug.WriteLine($"{LogTag} {item.Id}: v{item.Version} staged for the next start");
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
		{
			Debug.WriteLine($"{LogTag} {item.Id}: could not stage, run stopped: {ex.Message}");
			return false;
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
			Debug.WriteLine($"{LogTag} could not remove a temp file: {ex.Message}");
		}
	}
}
