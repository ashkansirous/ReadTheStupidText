using System.Diagnostics;
using DinoReader.Core;
using Sirous.Reading.Application.Usage;

namespace DinoReader;

/// <summary>
/// The packages the background check downloaded for books that are due an update (Slice 181), kept in app data as <c>book-updates/&lt;id&gt;/v&lt;n&gt;.dino</c> until the next cold start.
/// The background run never touches a stored book; <see cref="ApplyOnceAsync"/> swaps the staged packages in before the gallery first loads, when no book can be open.
/// A crash mid-apply leaves the entry, and the next launch tries it again (<see cref="StarterStories.SwapFromStoreAsync"/> is safe to run twice).
/// </summary>
public sealed class StagedBookUpdates(IStoryLibrary library, StarterStories starters, HighResUpgrades? upgrades = null)
{
	private const string LogTag = "[BookUpdate]";
	private const string FolderName = "book-updates";
	private const string FilePrefix = "v";
	private const string FileExtension = ".dino";
	private const string HighResMarker = ".hd"; // Slice 375 D: v<n>.hd.dino is the high-res package of the version already installed as standard
	private const string PartExtension = ".part";

	private readonly object _lock = new();
#if DEBUG
	private readonly ForcedVersionTracker _forcedVersions = new(); // Debug-only forced updates (Slice 209)
#endif
	private Task? _applyTask;

	private sealed record Entry(string Id, int Version, string Path, bool HighRes = false);

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
	public int? StagedVersion(string id) => ListEntries().Where(e => !e.HighRes && string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)).Select(e => (int?)e.Version).Max();

	/// <summary>Slice 375 D: the version whose high-res package is staged for the book (null when none).</summary>
	public int? StagedHighResVersion(string id) => ListEntries().Where(e => e.HighRes && string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)).Select(e => (int?)e.Version).Max();

	/// <summary>Keeps a downloaded package for the next cold start (copied under a temporary name and renamed, so a half-copied file is never taken for a whole one); the caller deletes <paramref name="file"/>.
	/// A <paramref name="highRes"/> package (Slice 375 D) is the same version as the installed standard copy; staging one never removes a staged update, and the other way round.</summary>
	public void Stage(string id, int version, string file, bool highRes = false)
	{
		string folder = FolderOf(id);
		Directory.CreateDirectory(folder);
		string target = System.IO.Path.Combine(folder, StagedFileName(version, highRes));
		string part = target + PartExtension;
		File.Copy(file, part, overwrite: true);
		File.Move(part, target, overwrite: true);
		foreach (string older in Directory.GetFiles(folder).Where(f => f != target && ParseName(f) is { } other && other.HighRes == highRes)) // only named packages of the same kind: a .part may be another staging in flight (DiscardPartialFiles clears leftovers at start)
		{
			File.Delete(older);
		}
	}

	private static string StagedFileName(int version, bool highRes) => $"{FilePrefix}{version}{(highRes ? HighResMarker : string.Empty)}{FileExtension}";

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

	/// <summary>Slice 375 D: deletes the half-copied <c>.part</c> files of an interrupted staging (the app was killed mid-copy); called once at app start, before the update check can stage anything (<c>App.CheckBookUpdatesAsync</c>); never later, it would delete a staging in flight. Never throws.</summary>
	public void DiscardPartialFiles()
	{
		try
		{
			foreach (string part in Directory.Exists(Root) ? Directory.GetFiles(Root, $"*{PartExtension}", SearchOption.AllDirectories) : [])
			{
				File.Delete(part);
				HighResLog.Write($"discarded a partial staged file in {System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(part))}");
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// no-fallback: housekeeping delete; a leftover part file is replaced by the next staging of that book
			Debug.WriteLine($"{LogTag} could not remove a partial staged file: {ex.Message}");
		}
	}

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
			Swapped?.Invoke(this, EventArgs.Empty); // ShelfAudioPacks subscribes at app start (DinoApp constructor), before any apply can run
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
		List<Entry> updates = entries.Where(e => !e.HighRes).ToList();
		IReadOnlyList<BookUpdateDecision> decisions = updates.Count == 0 ? [] : await PlanAsync(updates.Select(e => new StoreBookVersion(e.Id, e.Version)).ToList());
		foreach (Entry entry in updates)
		{
			BookUpdateDecision? decision = decisions.FirstOrDefault(d => string.Equals(d.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
			await ApplyOneAsync(entry, decision);
		}

		foreach (Entry entry in entries.Where(e => e.HighRes)) // after the updates: a newer version staged for the same book has replaced the entry's version by now
		{
			await ApplyHighResAsync(entry);
		}
	}

	// Slice 375 D: swaps the high-res package in for the standard copy of the SAME version (the plan above would call it up to date). It runs only when HighResUpgrades says this book's
	// upgrade is staged for this version and the shelf copy still is that version; the swap is the normal journaled one, which leaves an edited copy alone. Applied (or given up) removes the entry.
	private async Task ApplyHighResAsync(Entry entry)
	{
		if (upgrades is null)
		{
			DeleteEntry(entry);
			return;
		}

		bool stagedForVersion = upgrades.IsStaged(entry.Id, entry.Version);
		bool onShelf = (await library.ListAsync()).Any(s => string.Equals(s.SourceId, entry.Id, StringComparison.OrdinalIgnoreCase));
		HighResOutcome outcome = OutcomeBeforeSwap(entry, stagedForVersion, onShelf, upgrades) ?? await SwapHighResAsync(entry);
		HighResLog.Write($"{entry.Id}: staged high-res v{entry.Version} ended as {outcome}");
		if (stagedForVersion || !onShelf)
		{
			upgrades.Record(entry.Id, entry.Version, outcome); // the outcome rule: the only thing that changes the entry. A book gone from the shelf loses its entry (Gone) even when it was only pending (review #6); otherwise a file with no staged entry of its own never touches the table
		}

		if (outcome != HighResOutcome.Transient)
		{
			DeleteEntry(entry); // a transient end keeps the file and the entry for the next apply
		}
	}

	// Slice 375 D outcome rule, before the swap: null when the package may be swapped in (HighResUpgradeTable.OutcomeBeforeSwap decides; a standard update still staged for this book keeps the entry waiting).
	private HighResOutcome? OutcomeBeforeSwap(Entry entry, bool stagedForVersion, bool onShelf, HighResUpgrades table)
	{
		return HighResUpgradeTable.OutcomeBeforeSwap(
			stagedForVersion, StarterStories.RecordedVersion(entry.Id), entry.Version, StagedVersion(entry.Id), onShelf, table.InstalledIsHigh(entry.Id, entry.Version));
	}

	private async Task<HighResOutcome> SwapHighResAsync(Entry entry)
	{
		try
		{
			bool swapped = await SwapAsync(entry);
			HighResLog.Write(swapped ? $"{entry.Id}: swapped to the high-res pictures of v{entry.Version}" : $"{entry.Id}: edited, keeps its standard pictures");
			return swapped ? HighResOutcome.Done : HighResOutcome.Gone;
		}
		catch (Exception ex) when (ex is StoryPackageException or InvalidDataException)
		{
			HighResLog.Write($"{entry.Id}: bad staged high-res package: {ex.Message}");
			DinoFallbacks.Report(FallbackArea.Store, FallbackReason.UpdateCheckFailed);
			return HighResOutcome.BadPackage;
		}
		catch (Exception ex)
		{
			// Storage, cancellation or an unexpected fault is retryable, like a standard update's: the entry and the file stay and the swap is tried again at the next apply, nothing is counted.
			HighResLog.Write($"{entry.Id}: high-res swap failed, tried again at the next apply: {ex.Message}");
			DinoFallbacks.Report(FallbackArea.Store, FallbackReason.UpdateCheckFailed);
			return HighResOutcome.Transient;
		}
	}

	/// <summary>Slice 375 D: a deleted book forgets its high-res upgrade entry and removes its staged high-res file.</summary>
	public void DiscardHighRes(string id)
	{
		upgrades?.Remove(id);
		foreach (Entry entry in ListEntries().Where(e => e.HighRes && string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase)))
		{
			DeleteEntry(entry);
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
				upgrades?.SettleInstalledAtSwap(entry.Id, entry.Version); // review #8: the installed-size record changes here, at the swap
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
				.Select(f => (File: f, Name: ParseName(f))).Where(x => x.Name is not null)
				.Select(x => new Entry(id, x.Name!.Value.Version, x.File, x.Name.Value.HighRes)));
		}

		return entries;
	}

	// v<n>.dino is a staged update, v<n>.hd.dino (Slice 375 D) a staged high-res package; any other name (a .part, a stray file) is not an entry.
	private static (int Version, bool HighRes)? ParseName(string file)
	{
		string name = System.IO.Path.GetFileNameWithoutExtension(file);
		bool highRes = name.EndsWith(HighResMarker, StringComparison.Ordinal);
		string digits = highRes ? name[..^HighResMarker.Length] : name;
		return digits.StartsWith(FilePrefix, StringComparison.Ordinal) && int.TryParse(digits.AsSpan(FilePrefix.Length), out int version) && string.Equals(System.IO.Path.GetExtension(file), FileExtension, StringComparison.Ordinal) ? (version, highRes) : null;
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
