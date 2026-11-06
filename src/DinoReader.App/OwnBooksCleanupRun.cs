using DinoReader.Core;
using ReadTheStupidText.Application.Usage;

namespace DinoReader;

/// <summary>What one pass of the own-books cleanup did: the books it deleted, how many it could not, and whether the listing saw every folder.</summary>
public sealed record OwnBooksCleanupResult(IReadOnlyList<string> Deleted, int Failed, bool Complete);

/// <summary>Where the cleanup keeps its two small facts (the done flag and the count of starts that had a failed delete). The app backs it with Preferences; tests with a dictionary.</summary>
public interface IOwnBooksCleanupKeys
{
	bool Done { get; set; }

	int FailedStarts { get; set; }
}

/// <summary>One cleanup per app run (the gallery appears many times): later starts do nothing. Never throws (cancellation ends it quietly); any other failure is reported and counts as a failed start, retried at the next app start.</summary>
public sealed class OwnBooksCleanupFlight(IStoryLibrary library, IOwnBooksCleanupKeys keys, IFallbackSink sink)
{
	private int _started;

	/// <summary>Returns how many books this start deleted.</summary>
	public async Task<int> RunOnceAsync()
	{
		if (Interlocked.Exchange(ref _started, 1) == 1)
		{
			return 0;
		}

		try
		{
			return await OwnBooksCleanupRun.RunOnceAsync(library, keys, sink);
		}
		catch (OperationCanceledException)
		{
			// no-fallback: cancellation is not a failure; the pass simply runs again at the next start
			return 0;
		}
		catch (Exception ex)
		{
			sink.Report(FallbackArea.Library, FallbackReason.StoryUnreadable); // the shelf could not be listed; counts as a failed start
			System.Diagnostics.Debug.WriteLine($"[OwnBooks] cleanup ended early: {ex}");
			OwnBooksCleanupRun.Record(keys, 1);
			return 0;
		}
	}
}

/// <summary>
/// The pure part of Slice 278 A3 (design 2): deletes every own book (<see cref="OwnBook.IsOwn"/>, listed by <see cref="IStoryLibrary.ListOwnFoldersAsync"/>) with its pictures and recordings (they live in the book's folder).
/// Idempotent: a pass a crash cut short can simply run again. Each book is isolated: whatever one book throws (except cancellation) is reported and the next book still goes.
/// </summary>
public static class OwnBooksCleanupRun
{
	/// <summary>A pass with failed deletes is retried at the next start, but only this many starts; after that it is marked done and the stuck book stays hidden on disk.</summary>
	public const int MaxFailedStarts = 3;

	/// <summary>Runs the cleanup unless an earlier start finished it, and records the outcome in <paramref name="keys"/>. Returns how many books were deleted.
	/// The start counts as failed when a delete failed or the listing did not see every folder, so the pass is never marked done on a partial view.</summary>
	public static async Task<int> RunOnceAsync(IStoryLibrary library, IOwnBooksCleanupKeys keys, IFallbackSink sink)
	{
		if (keys.Done)
		{
			return 0;
		}

		OwnBooksCleanupResult result = await RunAsync(library, sink);
		Record(keys, result.Complete ? result.Failed : result.Failed + 1);
		return result.Deleted.Count;
	}

	/// <summary>Deletes the own books. A failure on one book is reported to <paramref name="sink"/> (area library) and the others still go. The caller decides what the result means for the retry count.</summary>
	public static async Task<OwnBooksCleanupResult> RunAsync(IStoryLibrary library, IFallbackSink sink)
	{
		OwnFolders own = await library.ListOwnFoldersAsync();
		var deleted = new List<string>();
		int failed = 0;
		foreach (string id in own.Ids)
		{
			if (await TryDeleteAsync(library, id, sink))
			{
				deleted.Add(id);
			}
			else
			{
				failed++;
			}
		}

		return new OwnBooksCleanupResult(deleted, failed, own.Complete);
	}

	/// <summary>Counts a start that ended in a failure (also a pass that threw before finishing); done once a start is clean or <see cref="MaxFailedStarts"/> starts have failed.</summary>
	public static void Record(IOwnBooksCleanupKeys keys, int failed)
	{
		if (failed == 0)
		{
			keys.Done = true;
			return;
		}

		keys.FailedStarts++;
		keys.Done = keys.FailedStarts >= MaxFailedStarts;
	}

	private static async Task<bool> TryDeleteAsync(IStoryLibrary library, string id, IFallbackSink sink)
	{
		try
		{
			await library.DeleteAsync(id);
			return true;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			sink.Report(FallbackArea.Library, FallbackReason.StoryUnreadable); // Slice 278 A3: the book stays and the next start tries again (the reason list is shared with RTST and the Worker allow-list, so no delete-specific reason)
			System.Diagnostics.Debug.WriteLine($"[OwnBooks] could not delete {id}: {ex.Message}");
			return false;
		}
	}
}
