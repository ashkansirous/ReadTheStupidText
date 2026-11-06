using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// What the app keeps of the four former starter stories (Slice 91, Decision 95; Slice 261 took their packages out of the app): a fresh install starts with an empty shelf and picks its
/// first books from the store (<see cref="FirstBooks"/>). An install made before Slice 261 still holds the four books, recorded by package id in preferences
/// (<see cref="IsStarterId"/>, <see cref="IsCatalogueId"/>); they have the same ids as the store copies, so they update and get audio like any store book.
/// This class also swaps a store book's newer package in for the copy on the shelf (<see cref="SwapFromStoreAsync"/>, Slice 181) and finishes a swap a crash interrupted.
/// </summary>
public sealed class StarterStories(IStoryLibrary library)
{
	private const string InstalledKeyPrefix = "dino.starter.installed.";

	// Set once a starter's stored copy has taken the bundled translations (Slice 95; v2 key, Slice 105: the five Nordic and Dutch tables; v3 key, Slice 154: the "how to read" tables, added to copies that already adopted): earlier versions installed the starters without them.
	private const string TranslationsAdoptedKeyPrefix = "dino.starter.translations.v3.";

	// Set for every starter and every store story kept on the shelf (Slice 133): a book with this mark, a starter record or a recorded content version came from the catalogue, not from a grown-up.
	private const string CatalogueKeyPrefix = "dino.catalogue.";
	private const string StoreInstalledKeyPrefix = "dino.store.installed.";

	// The content version a starter was installed or refreshed at; none recorded means the first release (StarterUpdate.LegacyInstalledVersion).
	private const string VersionKeyPrefix = "dino.starter.version.";

	// The hash of the page text a catalogue book arrived with or was last swapped to (Slice 181): a stored copy whose text still has it was not edited by the grown-up.
	private const string TextHashKeyPrefix = "dino.book.texthash.";

	// The newest store version found while the stored copy was edited (Slice 181): the package is not fetched again until the store has a newer one.
	private const string SeenVersionKeyPrefix = "dino.book.seenversion.";

	// The swap in progress, if any (Slice 181, SwapMarker): finished at the next launch if the process died halfway.
	private const string SwapMarkerKey = "dino.book.swapping";

	// How many launches failed to finish the marked swap, and how many failures are allowed before the marker is dropped (both copies are then kept; nothing is deleted).
	private const string SwapFailuresKey = "dino.book.swapping.failures";
	private const int MaxSwapRecoveryFailures = 3;

	private readonly SemaphoreSlim _gate = new(1, 1);
	private bool _complete;

	/// <summary>True once <see cref="InstallOnceAsync"/> finished its start-up pass (Slice 181 does not trust the shelf before).</summary>
	public bool InstallComplete => _complete;

	/// <summary>The shelf's start-up pass: finishes a store swap a crash interrupted (no book is bundled any more, Slice 261). Safe to call again and from several places (the gallery, the book updater):
	/// calls wait for the one pass. In a Debug build it also waits for the test shelf a flow asked for (<see cref="Testing.TestBooks"/>), so the first shelf read already holds it.</summary>
	public async Task InstallOnceAsync()
	{
		await _gate.WaitAsync();
		try
		{
			if (!_complete)
			{
				await FinishInterruptedSwapAsync();
				await QueueStartersForStoreAsync();
				_complete = true;
			}
		}
		finally
		{
			_gate.Release();
		}

#if DINO_TEST_BOOKS
		await Testing.TestBooks.PendingAsync();
#endif
	}

	// An install from before Slice 261 holds starters that never took the translations of the bundled packages (no longer shipped): their recorded version is lowered once, so the book updater
	// (BookUpdatePlan, which judges every catalogue book against the store) swaps in the store's copy, translations included. A copy the grown-up edited is skipped there, as any store update.
	private async Task QueueStartersForStoreAsync()
	{
		try
		{
			foreach (string id in (await library.ListAsync()).Select(s => s.SourceId).OfType<string>().Where(IsStarterId).Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (!Preferences.Default.Get(TranslationsAdoptedKeyPrefix + id, false))
				{
					RecordVersion(id, StarterUpdate.VersionToRefetchFromStore(InstalledVersion(id)));
					RecordTranslationsAdopted(id); // once: the swap records it again
				}
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader could not queue the starters for the store: {ex}");
		}
	}

	/// <summary>Whether <paramref name="packageId"/> is a starter or a story the store gave (Slice 133): only such books are counted by id in the usage counts.</summary>
	public static bool IsCatalogueId(string packageId) =>
		Preferences.Default.Get(CatalogueKeyPrefix + packageId, false) || IsRecorded(packageId) || InstalledVersion(packageId) is not null;

	/// <summary>Whether the app itself installed <paramref name="packageId"/> from its bundled starters (Slice 189): a catalogue book that is not one came from the Store.</summary>
	public static bool IsStarterId(string packageId) => IsRecorded(packageId) && !Preferences.Default.Get(StoreInstalledKeyPrefix + packageId, false);

	/// <summary>Called when the user removes a book from the shelf (Slice 189): the Store-install flag describes the copy on the shelf, so it ends with that copy.</summary>
	public static void ForgetStoreInstall(string? packageId)
	{
		if (!string.IsNullOrEmpty(packageId))
		{
			Preferences.Default.Remove(StoreInstalledKeyPrefix + packageId);
		}
	}

	private static void MarkCatalogue(string packageId) => Preferences.Default.Set(CatalogueKeyPrefix + packageId, true);

	private static bool IsRecorded(string packageId) => Preferences.Default.Get(InstalledKeyPrefix + packageId, false);

	private static void RecordTranslationsAdopted(string packageId) => Preferences.Default.Set(TranslationsAdoptedKeyPrefix + packageId, true);

	private static int? InstalledVersion(string packageId) =>
		Preferences.Default.ContainsKey(VersionKeyPrefix + packageId) ? Preferences.Default.Get(VersionKeyPrefix + packageId, 0) : null;

	private static void RecordVersion(string packageId, int version) => Preferences.Default.Set(VersionKeyPrefix + packageId, version);

	/// <summary>The content version recorded for a starter or store book (null when none was), for the background update check (Slice 181).</summary>
	public static int? RecordedVersion(string packageId) => InstalledVersion(packageId);

#if DEBUG
	/// <summary>Slice 209 (Debug only, the <c>stage-book-update force=true</c> hook): records an older version for the book, so the update plan sees the store's current version as newer and the next start really swaps it in.</summary>
	public static void RecordVersionForTest(string packageId, int version) => RecordVersion(packageId, version);

	// Slice 225 item 16: the mark holds when it was written (UTC ticks), not just "true", so a mark left by a stage that failed expires (ForcedUpdateRules.MarkLifetime) instead of
	// forcing a later ordinary update of the same book. A new key: an older install's bool value under the old key is never read as ticks.
	private const string ForcedApplyKeyPrefix = "dino.test.forceapplyat.";

	/// <summary>Slice 209 (Debug only): puts the recorded version back to what it was (none: removes it), after a forced stage planned with a lowered one.</summary>
	public static void RestoreVersionForTest(string packageId, int? version)
	{
		if (version is { } v)
		{
			RecordVersion(packageId, v);
			return;
		}

		Preferences.Default.Remove(VersionKeyPrefix + packageId);
	}

	/// <summary>Slice 209 (Debug only): marks the staged package of this book as forced, so the next cold start's apply lowers the recorded version itself (after the starter refresh ran, which would otherwise swap the bundled copy in and make the staged one "up to date").</summary>
	public static void MarkForcedApplyForTest(string packageId) => Preferences.Default.Set(ForcedApplyKeyPrefix + packageId, DateTimeOffset.UtcNow.UtcTicks);

	/// <summary>Slice 209 (Debug only): true once per mark, while the mark is still live (<see cref="ForcedUpdateRules.IsMarkLive"/>); the apply uses it to lower the recorded version to <see cref="BookUpdatePlan.ForcedRecordedVersion"/>.</summary>
	public static bool ConsumeForcedApplyForTest(string packageId)
	{
		long markedAt = Preferences.Default.Get(ForcedApplyKeyPrefix + packageId, 0L);
		Preferences.Default.Remove(ForcedApplyKeyPrefix + packageId);
		bool live = ForcedUpdateRules.IsMarkLive(markedAt, DateTimeOffset.UtcNow);
		if (markedAt > 0 && !live)
		{
			Debug.WriteLine($"[BookUpdate] {packageId}: forced-apply mark expired, ignored (an earlier stage did not finish)");
		}

		return live;
	}
#endif

	/// <summary>The hash of the page text the book arrived with (null when never recorded), Slice 181.</summary>
	public static string? RecordedTextHash(string packageId) =>
		Preferences.Default.ContainsKey(TextHashKeyPrefix + packageId) ? Preferences.Default.Get(TextHashKeyPrefix + packageId, string.Empty) : null;

	/// <summary>The newest store version seen while the copy was edited (null when none), Slice 181.</summary>
	public static int? RecordedSeenVersion(string packageId) =>
		Preferences.Default.ContainsKey(SeenVersionKeyPrefix + packageId) ? Preferences.Default.Get(SeenVersionKeyPrefix + packageId, 0) : null;

	/// <summary>Remembers that the edited copy was seen against this store version.</summary>
	public static void RecordSeenVersion(string packageId, int version) => Preferences.Default.Set(SeenVersionKeyPrefix + packageId, version);

	/// <summary>Records the page-text hash of a book as it arrived (a baseline fetched for a book installed before Slice 181).</summary>
	public static void RecordTextHash(string packageId, string hash) => Preferences.Default.Set(TextHashKeyPrefix + packageId, hash);

	// Whether the copy was replaced: a copy the grown-up edited is left as it is (equal text is still replaced, the pictures or cover may differ).
	// Strict (a store swap): a recorded hash that does not cover the pictures is never trusted, so the copy counts as edited.
	private async Task<bool> ReplaceUnlessEditedAsync(MemoryStream package, Story stored, int version, bool strict)
	{
		package.Position = 0;
		Story bundled = await StoryPackageReader.ReadStoryOnlyAsync(package);
		if (await IsEditedAsync(package, stored, bundled, strict))
		{
			return false;
		}

		await ReplaceAsync(package, stored, version);
		return true;
	}

	private async Task<bool> IsEditedAsync(MemoryStream package, Story stored, Story bundled, bool strict)
	{
		string id = stored.SourceId!;
		string? recorded = RecordedTextHash(id);
		if (!StarterUpdate.IsContentHash(recorded))
		{
			return strict || StarterUpdate.IsEdited(id, stored, bundled, recorded);
		}

		string current = await ContentHashOfAsync(stored);
		if (current == recorded)
		{
			return false;
		}

		package.Position = 0;
		return current != await StoryPackageReader.ContentHashOfPackageAsync(package);
	}

	/// <summary>The hash of a shelf copy's page text and each page picture's bytes, as recorded when the book arrived (Slice 181). A picture file that is gone hashes as missing.</summary>
	public async Task<string> ContentHashOfAsync(Story story)
	{
		var pictures = new List<string?>();
		foreach (StoryPage page in story.Pages)
		{
			pictures.Add(page.ImageFile is null ? null : await HashPictureAsync(story.Id, page.ImageFile));
		}

		return StarterUpdate.ContentHash(story, pictures);
	}

	private async Task<string> HashPictureAsync(string storyId, string name)
	{
		string? path = library.TryResolveFile(storyId, name);
		if (path is null || !File.Exists(path))
		{
			return "missing";
		}

		await using FileStream stream = File.OpenRead(path);
		return Convert.ToHexString(await SHA256.HashDataAsync(stream));
	}

	/// <summary>
	/// Swaps a store book's newer package in for the copy on the shelf (Slice 181), the same way a bundled starter is refreshed: cast, voices, translations, recordings and the
	/// cover photo stay. False when the copy was edited, text or pictures (it is left as it is). The swap is journaled (<see cref="SwapMarker"/>), so a crash halfway is finished at the next launch.
	/// </summary>
	public async Task<bool> SwapFromStoreAsync(MemoryStream package, string id, int version)
	{
		if (!string.Equals((await ManifestAsync(package)).Id, id, StringComparison.OrdinalIgnoreCase))
		{
			throw new StoryPackageException("The package is not the book it was fetched for.");
		}

		await RemoveDuplicatesAsync(package, id);
		// Read again after the duplicates were removed: the copy listed before may be the one that was deleted.
		Story stored = (await library.ListAsync()).FirstOrDefault(s => string.Equals(s.SourceId, id, StringComparison.OrdinalIgnoreCase))
			?? throw new InvalidOperationException("The book left the shelf during the swap.");
		return await ReplaceUnlessEditedAsync(package, stored, version, strict: true);
	}

	// The new copy is imported first and the old one removed last, so a failure leaves the old story in place. The journal marks the swap before the import and, once the new copy holds
	// the carried-over choices, names it; only then is the old copy deleted, and the hash and version recorded after (FinishInterruptedSwapAsync does that after a crash).
	private async Task ReplaceAsync(MemoryStream package, Story stored, int version)
	{
		var marker = new SwapMarker(stored.SourceId!, version, stored.Id);
		Preferences.Default.Set(SwapMarkerKey, marker.Format());
		package.Position = 0;
		Story fresh = await StoryPackageReader.ImportAsync(package, library, created: stored.Created);
		Story refreshed;
		try
		{
			IReadOnlyList<StarterUpdate.RecordingMove> copied = await CopyRecordingsAsync(stored, fresh, StarterUpdate.RecordingMoves(stored, fresh));
			refreshed = StarterUpdate.Refresh(stored, fresh, copied);
			await library.UpdateAsync(await WithCoverPhotoAsync(stored, fresh, refreshed));
		}
		catch
		{
			await library.DeleteAsync(fresh.Id);
			Preferences.Default.Remove(SwapMarkerKey);
			throw;
		}

		Preferences.Default.Set(SwapMarkerKey, (marker with { FreshId = fresh.Id }).Format());
		await library.DeleteAsync(stored.Id);
		RecordSwapped(marker.Id, version, await ContentHashOfAsync(refreshed));
		Preferences.Default.Remove(SwapMarkerKey);
	}

	private static void RecordSwapped(string id, int version, string contentHash)
	{
		RecordTextHash(id, contentHash);
		RecordVersion(id, version);
		RecordTranslationsAdopted(id);
	}

	// Recovery never throws out of InstallOnceAsync (the starters must still install): a failure is logged and the marker kept for the next launch, until it has failed
	// MaxSwapRecoveryFailures times; then the marker is dropped and both copies stay as they are, so the only copy is never deleted.
	private async Task FinishInterruptedSwapAsync()
	{
		try
		{
			await RecoverInterruptedSwapAsync();
			Preferences.Default.Remove(SwapFailuresKey);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Debug.WriteLine($"[BookUpdate] finishing the interrupted swap failed: {ex}");
			CountSwapRecoveryFailure();
		}
	}

	private static void CountSwapRecoveryFailure()
	{
		int failures = Preferences.Default.Get(SwapFailuresKey, 0) + 1;
		if (failures >= MaxSwapRecoveryFailures)
		{
			Debug.WriteLine("[BookUpdate] giving up on the interrupted swap; both copies are kept");
			Preferences.Default.Remove(SwapMarkerKey);
			Preferences.Default.Remove(SwapFailuresKey);
			return;
		}

		Preferences.Default.Set(SwapFailuresKey, failures);
	}

	// A swap the process died in the middle of: before the new copy was complete the old one stays (the half-made copy goes; the staged package is applied again); after, the old copy is
	// deleted if it is still there and the new one's hash and version are recorded, so the book is neither "edited forever" nor kept twice.
	private async Task RecoverInterruptedSwapAsync()
	{
		if (SwapMarker.TryParse(Preferences.Default.Get(SwapMarkerKey, (string?)null)) is not { } marker)
		{
			Preferences.Default.Remove(SwapMarkerKey);
			return;
		}

		Debug.WriteLine($"[BookUpdate] finishing the interrupted swap of {marker.Id} to v{marker.Version}");
		List<Story> copies = (await library.ListAsync()).Where(s => string.Equals(s.SourceId, marker.Id, StringComparison.OrdinalIgnoreCase)).ToList();
		Story? fresh = marker.FreshId is null ? null : copies.FirstOrDefault(c => c.Id == marker.FreshId);
		if (marker.FreshId is null)
		{
			await DeleteAllAsync(copies.Where(c => c.Id != marker.OldId));
		}
		else if (fresh is not null)
		{
			await DeleteAllAsync(copies.Where(c => c.Id == marker.OldId));
			RecordSwapped(marker.Id, marker.Version, await ContentHashOfAsync(fresh));
		}

		Preferences.Default.Remove(SwapMarkerKey);
	}

	private async Task DeleteAllAsync(IEnumerable<Story> stories)
	{
		foreach (Story story in stories.ToList())
		{
			await library.DeleteAsync(story.Id);
		}
	}

	// A gallery photo cover the grown-up chose is copied into the refreshed story's folder under a new name; a missing file falls back to the bundled cover.
	private async Task<Story> WithCoverPhotoAsync(Story stored, Story fresh, Story refreshed)
	{
		if (!StarterUpdate.KeepsStoredPhoto(stored, fresh))
		{
			return refreshed;
		}

		string? source = library.TryResolveFile(stored.Id, stored.CoverFile);
		if (source is null || !File.Exists(source))
		{
			return refreshed with { CoverStyle = fresh.CoverStyle, CoverFile = fresh.CoverFile, CoverPageIndex = fresh.CoverPageIndex };
		}

		string name = await library.StoreFileAsync(fresh.Id, Story.CoverFilePrefix, Story.CoverFileExtension, temp =>
		{
			File.Copy(source, temp, overwrite: true);
			return Task.CompletedTask;
		});
		return refreshed with { CoverFile = name };
	}

	// Two stories sharing a starter id (a replace that stopped halfway) are cleaned up: the one holding the bundled text stays, the other goes after its recordings are copied over.
	private async Task RemoveDuplicatesAsync(MemoryStream package, string packageId)
	{
		List<Story> copies = (await library.ListAsync()).Where(s => s.SourceId == packageId).ToList();
		if (copies.Count < 2)
		{
			return;
		}

		package.Position = 0;
		Story keep = StarterUpdate.CopyToKeep(copies, await StoryPackageReader.ReadStoryOnlyAsync(package));
		foreach (Story extra in copies.Where(c => c.Id != keep.Id))
		{
			keep = await MoveMissingRecordingsAsync(extra, keep);
			await library.DeleteAsync(extra.Id);
		}
	}

	private async Task<Story> MoveMissingRecordingsAsync(Story extra, Story keep)
	{
		var wanted = StarterUpdate.RecordingMoves(extra, keep).Where(m => keep.Recordings?.ContainsKey(m.To) != true).ToList();
		IReadOnlyList<StarterUpdate.RecordingMove> copied = await CopyRecordingsAsync(extra, keep, wanted);
		if (copied.Count == 0)
		{
			return keep;
		}

		Story merged = StarterUpdate.WithMovedRecordings(keep, extra, copied) with { UseRecordings = keep.UseRecordings || extra.UseRecordings };
		await library.UpdateAsync(merged);
		return merged;
	}

	// A parent's recordings of pages whose text is unchanged move to the new story's folder (named for their new page number); returns the moves whose file really copied.
	private async Task<IReadOnlyList<StarterUpdate.RecordingMove>> CopyRecordingsAsync(Story stored, Story fresh, IReadOnlyList<StarterUpdate.RecordingMove> moves)
	{
		var copied = new List<StarterUpdate.RecordingMove>();
		foreach (StarterUpdate.RecordingMove move in moves)
		{
			string? source = library.TryResolveFile(stored.Id, StoryRecordings.FileNameOf(move.From));
			if (source is null || !File.Exists(source))
			{
				continue;
			}

			await library.StoreRecordingAsync(fresh.Id, move.To, temp =>
			{
				File.Copy(source, temp, overwrite: true);
				return Task.CompletedTask;
			});
			copied.Add(move);
		}

		return copied;
	}

	/// <summary>Records the content version of a starter installed from the store (its package manifest), so a store copy at or above the bundled version is never replaced by the older bundled one.</summary>
	public static async Task RecordStoreInstallAsync(Stream package)
	{
		using var copy = new MemoryStream();
		await package.CopyToAsync(copy);
		StoryPackageManifest manifest = await ManifestAsync(copy);
		MarkCatalogue(manifest.Id);
		Preferences.Default.Set(StoreInstalledKeyPrefix + manifest.Id, true); // the copy on the shelf came from the Store, even when the bundled starter of that id was installed (and removed) earlier
		if (manifest.Version is { } version)
		{
			RecordVersion(manifest.Id, version);
		}

		copy.Position = 0;
		RecordTextHash(manifest.Id, await StoryPackageReader.ContentHashOfPackageAsync(copy));
	}

	private static async Task<StoryPackageManifest> ManifestAsync(MemoryStream package)
	{
		package.Position = 0;
		using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
		ZipArchiveEntry entry = zip.GetEntry(StoryPackageManifest.ManifestFileName)
			?? throw new StoryPackageException("A starter package has no manifest.");
		await using Stream stream = entry.Open();
		using var reader = new StreamReader(stream);
		return StoryPackageManifest.Parse(await reader.ReadToEndAsync());
	}
}
