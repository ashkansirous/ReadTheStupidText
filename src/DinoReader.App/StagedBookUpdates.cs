using System.Diagnostics;
using DinoReader.Core;
using ReadTheStupidText.Application.Usage;

namespace DinoReader;

/// <summary>
/// The packages the background check downloaded for books that are due an update (Slice 181), kept in app data as <c>book-updates/&lt;id&gt;/v&lt;n&gt;.dino</c> until the next cold start.
/// The background run never touches a stored book; <see cref="ApplyOnceAsync"/> swaps the staged packages in before the gallery first loads, when no book can be open.
/// A crash mid-apply leaves the entry, and the next launch tries it again (<see cref="StarterStories.SwapFromStoreAsync"/> is safe to run twice).
/// </summary>
public sealed class StagedBookUpdates(IStoryLibrary library, StarterStories starters)
{
	private const string LogTag = "[BookUpdate]";
	private const string FolderName = "book-updates";
	private const string FilePrefix = "v";
	private const string FileExtension = ".dino";
	private const string PartExtension = ".part";

	private readonly object _lock = new();
#if DEBUG
	private readonly ForcedVersionTracker _forcedVersions = new(); // Debug-only forced updates (Slice 209)
#endif
	private Task? _applyTask;

	private sealed record Entry(string Id, int Version, string Path);

	private static string Root => System.IO.Path.Combine(FileSystem.AppDataDirectory, FolderName);

	/// <summary>The books the library can be judged on: every catalogue book on the shelf, against the given store (or staged) versions.</summary>
	public async Task<IReadOnlyList<BookUpdateDecision>> PlanAsync(IReadOnlyList<StoreBookVersion> versions)
	{
		List<Story> shelf = (await library.ListAsync()).Where(s => s.SourceId is { } id && StarterStories.IsCatalogueId(id)).ToList();
		var shelfBooks = new List<ShelfBook>();
		foreach (Story story in shelf)
		{
			shelfBooks.Add(new ShelfBook(story.SourceId!, StarterUpdate.PageTextHash(story), await starters.ContentHashOfAsync(story)));
		}

		var records = shelf.Select(s => s.SourceId!).Distinct(StringComparer.OrdinalIgnoreCase)
			.Select(id => new InstalledBookRecord(id, StarterStories.RecordedVersion(id), StarterStories.RecordedTextHash(id), StarterStories.RecordedSeenVersion(id))).ToList();
		return BookUpdatePlan.Decide(versions, records, shelfBooks);
	}

	/// <summary>The newest version already staged for the book (null when none).</summary>
	public int? StagedVersion(string id) => ListEntries().Where(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)).Select(e => (int?)e.Version).Max();

	/// <summary>Keeps a downloaded package for the next cold start (copied under a temporary name and renamed, so a half-copied file is never taken for a whole one); the caller deletes <paramref name="file"/>.</summary>
	public void Stage(string id, int version, string file)
	{
		string folder = FolderOf(id);
		Directory.CreateDirectory(folder);
		string target = System.IO.Path.Combine(folder, $"{FilePrefix}{version}{FileExtension}");
		string part = target + PartExtension;
		File.Copy(file, part, overwrite: true);
		File.Move(part, target, overwrite: true);
		foreach (string older in Directory.GetFiles(folder).Where(f => f != target))
		{
			File.Delete(older);
		}
	}

	/// <summary>
	/// Applies every staged package once per process: the first call starts the one shared task, every later call (the gallery's refreshes, whoever asks first) gets the same task, so
	/// the shelf is never read while a swap runs. Each is judged again against the library as it is now: a book edited, deleted or already newer meanwhile just loses its entry.
	/// It never throws: an unexpected failure is logged, and a staged entry that failed for a reason other than storage is dropped, so one bad package cannot crash every launch.
	/// </summary>
	public Task ApplyOnceAsync()
	{
		lock (_lock)
		{
			return _applyTask ??= RunApplyAsync();
		}
	}

	/// <summary>Slice 313: raised (on a background thread) when a newer package was just staged; a shown shelf then applies it at once (<see cref="ApplyPendingAsync"/>).</summary>
	public event EventHandler? Staged;

	public void NotifyStaged() => Staged?.Invoke(this, EventArgs.Empty);

	/// <summary>
	/// Slice 313: raised after an apply that swapped at least one book's text in, whichever path applied it (the cold start, the shown shelf): the audio packs are planned again, so the pack
	/// made for the new text is fetched even when a run was live or nothing else would have asked.
	/// </summary>
	public event EventHandler? Swapped;

	private bool _swappedInRun; // one apply at a time, so one flag is enough

	/// <summary>Whether a package waits to be applied.</summary>
	public bool HasPending => ListEntries().Count > 0;

	/// <summary>
	/// Slice 313: applies whatever is staged now, after any apply already running (never two at once, so the shelf is never read mid-swap). Called only by the gallery, when it is shown or
	/// appears, which is when no book can be open; the one-per-process <see cref="ApplyOnceAsync"/> still covers the first load. Never throws.
	/// </summary>
	public Task ApplyPendingAsync()
	{
		lock (_lock)
		{
			return _applyTask = ApplyAfterAsync(_applyTask ?? Task.CompletedTask);
		}
	}

	private async Task ApplyAfterAsync(Task previous)
	{
		await previous;
		await RunApplyAsync();
	}

	private async Task RunApplyAsync()
	{
		var clock = Stopwatch.StartNew();
		Debug.WriteLine($"{LogTag} apply start");
		try
		{
			await ApplyAllAsync(ListEntries());
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"{LogTag} applying staged books failed, retried at the next launch: {ex}");
			DinoFallbacks.Report(FallbackArea.Store, FallbackReason.UpdateCheckFailed);
		}

		Debug.WriteLine($"{LogTag} apply end after {clock.ElapsedMilliseconds} ms");
		RaiseSwapped();
	}

	private void RaiseSwapped()
	{
		if (!_swappedInRun)
		{
			return;
		}

		_swappedInRun = false;
		try
		{
			Swapped?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex)
		{
			// no-fallback: a listener's fault never fails the apply; the packs are planned again at the next launch or return
			Debug.WriteLine($"{LogTag} swapped listener failed: {ex.Message}");
		}
	}

	private async Task ApplyAllAsync(IReadOnlyList<Entry> entries)
	{
		if (entries.Count == 0)
		{
			return;
		}

#if DEBUG
		foreach (Entry forced in entries.Where(e => StarterStories.ConsumeForcedApplyForTest(e.Id)))
		{
			Debug.WriteLine($"{LogTag} {forced.Id}: forced apply, recorded version lowered to v{BookUpdatePlan.ForcedRecordedVersion(forced.Version)}");
			_forcedVersions.Lowered(forced.Id, StarterStories.RecordedVersion(forced.Id));   // Slice 225 item 16: remembered, so a dropped entry puts it back
			StarterStories.RecordVersionForTest(forced.Id, BookUpdatePlan.ForcedRecordedVersion(forced.Version));
		}
#endif
		IReadOnlyList<BookUpdateDecision> decisions = await PlanAsync(entries.Select(e => new StoreBookVersion(e.Id, e.Version)).ToList());
		foreach (Entry entry in entries)
		{
			BookUpdateDecision? decision = decisions.FirstOrDefault(d => string.Equals(d.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
			await ApplyOneAsync(entry, decision);
		}
	}

	private async Task ApplyOneAsync(Entry entry, BookUpdateDecision? decision)
	{
		if (decision is not { Action: BookUpdateAction.Update })
		{
			Debug.WriteLine($"{LogTag} {entry.Id}: staged v{entry.Version} dropped ({decision?.Skip.ToString() ?? decision?.Action.ToString() ?? "no record"})");
			if (decision?.Skip == BookUpdateSkip.Edited)
			{
				StarterStories.RecordSeenVersion(entry.Id, entry.Version);
				ReportApply(entry, BookUpdateResult.SkippedEdited);
			}

			RestoreForcedVersion(entry.Id);
			DeleteEntry(entry);
			return;
		}

		int from = StarterStories.RecordedVersion(entry.Id) ?? StarterUpdate.LegacyInstalledVersion;
		try
		{
			bool swapped = await SwapAsync(entry);
			BookUpdateEvents.Line($"{LogTag} {entry.Id}: {(swapped ? $"swapped to v{entry.Version}" : "edited, left as it is")}");
			BookUpdateEvents.Report(entry.Id, from, entry.Version, swapped ? BookUpdateResult.Updated : BookUpdateResult.SkippedEdited);
			if (swapped)
			{
				ForgetForcedVersion(entry.Id);
				_swappedInRun = true;
			}
			else
			{
				StarterStories.RecordSeenVersion(entry.Id, entry.Version);
				RestoreForcedVersion(entry.Id);
			}

			DeleteEntry(entry);
		}
		catch (Exception ex) when (ex is StoryPackageException or InvalidDataException)
		{
			Debug.WriteLine($"{LogTag} {entry.Id}: bad staged package dropped: {ex.Message}");
			ReportApply(entry, BookUpdateResult.Failed);
			DinoFallbacks.Report(FallbackArea.Store, FallbackReason.UpdateCheckFailed);
			// Slice 313: not a give-up (that would release the pack gate and bring new sound on old text): the bad copy is deleted so the next store-listing pass downloads it again.
			RestoreForcedVersion(entry.Id);
			DeleteEntry(entry);
		}
		catch (Exception ex)
		{
			// Slice 313: any other failure (storage, an unexpected fault) is retryable: the staged entry stays and the swap is tried again at the next apply; the old pack stays in use meanwhile.
			// Never the stored book is touched, and nothing is counted toward a give-up.
			Debug.WriteLine($"{LogTag} {entry.Id}: swap failed, entry kept for the next apply: {ex}");
			ReportApply(entry, BookUpdateResult.Failed);
			DinoFallbacks.Report(FallbackArea.Store, FallbackReason.UpdateCheckFailed);
		}
	}

	// Slice 225 item 16: a forced entry that is dropped must not leave the lowered recorded version behind (a later ordinary update would then look newer than it is).
	// A swap records the store's version itself, so a swapped entry is only forgotten. Nothing was lowered (every normal run, any Release build): nothing happens.
	private void RestoreForcedVersion(string id)
	{
#if DEBUG
		if (_forcedVersions.TryTakeRestore(id, out int? original))
		{
			StarterStories.RestoreVersionForTest(id, original);
			Debug.WriteLine($"{LogTag} {id}: forced entry dropped, recorded version put back to {(original is { } v ? $"v{v}" : "none")}");
		}
#endif
	}

	// A swapped book recorded the store's version itself: forget the note so a later forced run does not write the old original over it.
	private void ForgetForcedVersion(string id)
	{
#if DEBUG
		_forcedVersions.Forget(id);
#endif
	}

	// Slice 313: the book_update count for a staged entry that was dropped or failed at apply (a swap reports itself, it knows the version it replaced).
	private static void ReportApply(Entry entry, string result) =>
		BookUpdateEvents.Report(entry.Id, StarterStories.RecordedVersion(entry.Id) ?? StarterUpdate.LegacyInstalledVersion, entry.Version, result);

	private async Task<bool> SwapAsync(Entry entry)
	{
		var package = new MemoryStream();
		await using (FileStream stream = File.OpenRead(entry.Path))
		{
			await stream.CopyToAsync(package);
		}

		return await starters.SwapFromStoreAsync(package, entry.Id, entry.Version);
	}

	private static string FolderOf(string id) =>
		id == System.IO.Path.GetFileName(id) && id.Length > 0 ? System.IO.Path.Combine(Root, id) : throw new ArgumentException("Not a book id.", nameof(id));

	private static List<Entry> ListEntries()
	{
		var entries = new List<Entry>();
		if (!Directory.Exists(Root))
		{
			return entries;
		}

		foreach (string folder in Directory.GetDirectories(Root))
		{
			string id = System.IO.Path.GetFileName(folder);
			entries.AddRange(Directory.GetFiles(folder, $"{FilePrefix}*{FileExtension}")
				.Select(f => (File: f, Version: ParseVersion(f))).Where(x => x.Version is not null)
				.Select(x => new Entry(id, x.Version!.Value, x.File)));
		}

		return entries;
	}

	private static int? ParseVersion(string file)
	{
		string name = System.IO.Path.GetFileNameWithoutExtension(file);
		return name.StartsWith(FilePrefix, StringComparison.Ordinal) && int.TryParse(name.AsSpan(FilePrefix.Length), out int version) ? version : null;
	}

	private static void DeleteEntry(Entry entry)
	{
		try
		{
			File.Delete(entry.Path);
			string folder = System.IO.Path.GetDirectoryName(entry.Path)!;
			if (!Directory.EnumerateFileSystemEntries(folder).Any())
			{
				Directory.Delete(folder);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// no-fallback: housekeeping delete; nothing depends on it
			Debug.WriteLine($"{LogTag} could not remove a staged file: {ex.Message}");
		}
	}
}
