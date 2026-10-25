using System.Diagnostics;
using System.IO.Compression;
using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// The four bundled starter stories (Slice 91, Decision 95): <c>.dino</c> packages shipped as app assets and imported on first run.
/// Each starter is recorded as installed on its own, by package id, in preferences: a failed pass retries only the starters still
/// missing, and a starter a grown-up deleted never comes back because its record stays. A starter already in the library (restored or
/// imported by hand, found by its <see cref="Story.SourceId"/>) is recorded without being imported again. Each package carries a content
/// version (<see cref="StoryPackageManifest.Version"/>): an installed starter recorded at a lower version is replaced by the bundled one on launch,
/// keeping the grown-up's cast and switches (<see cref="StarterUpdate"/>, Decision 130); a deleted starter is not brought back.
/// </summary>
public sealed class StarterStories(IStoryLibrary library)
{
	private const string InstalledKeyPrefix = "dino.starter.installed.";

	// Set once a starter's stored copy has taken the bundled translations (Slice 95; v2 key, Slice 105: the five Nordic and Dutch tables): earlier versions installed the starters without them.
	private const string TranslationsAdoptedKeyPrefix = "dino.starter.translations.v2.";

	// The content version a starter was installed or refreshed at; none recorded means the first release (StarterUpdate.LegacyInstalledVersion).
	private const string VersionKeyPrefix = "dino.starter.version.";

	// Listed in shelf order: the first is the newest, so it comes first in the gallery.
	private static readonly string[] AssetNames = ["good-enough.dino", "three-little-pigs.dino", "gingerbread-man.dino", "tortoise-and-hare.dino"];

	/// <summary>How many starter stories ship with the app.</summary>
	public static int Count => AssetNames.Length;

	private readonly SemaphoreSlim _gate = new(1, 1);
	private bool _complete;

	/// <summary>Installs the starters not yet recorded. Safe to call again and from several places (the gallery, the first-run flow):
	/// calls wait for the one pass. A starter that fails is logged and left unrecorded, so the next call tries it again.</summary>
	public async Task InstallOnceAsync()
	{
		await _gate.WaitAsync();
		try
		{
			if (!_complete)
			{
				_complete = await InstallMissingAsync();
			}
		}
		finally
		{
			_gate.Release();
		}
	}

	private async Task<bool> InstallMissingAsync()
	{
		HashSet<string>? present = null;
		DateTimeOffset now = DateTimeOffset.UtcNow;
		bool allDone = true;
		for (int i = 0; i < AssetNames.Length; i++)
		{
			allDone &= await TryInstallOneAsync(AssetNames[i], now.AddSeconds(-i), async () => present ??= await PresentIdsAsync());
		}

		return allDone;
	}

	private async Task<HashSet<string>> PresentIdsAsync() =>
		(await library.ListAsync()).Select(s => s.SourceId).OfType<string>().ToHashSet();

	private static bool IsRecorded(string packageId) => Preferences.Default.Get(InstalledKeyPrefix + packageId, false);

	private static void Record(string packageId) => Preferences.Default.Set(InstalledKeyPrefix + packageId, true);

	private static bool TranslationsAdopted(string packageId) => Preferences.Default.Get(TranslationsAdoptedKeyPrefix + packageId, false);

	private static void RecordTranslationsAdopted(string packageId) => Preferences.Default.Set(TranslationsAdoptedKeyPrefix + packageId, true);

	private static int? InstalledVersion(string packageId) =>
		Preferences.Default.ContainsKey(VersionKeyPrefix + packageId) ? Preferences.Default.Get(VersionKeyPrefix + packageId, 0) : null;

	private static void RecordVersion(string packageId, int version) => Preferences.Default.Set(VersionKeyPrefix + packageId, version);

	private async Task<bool> TryInstallOneAsync(string assetName, DateTimeOffset created, Func<Task<HashSet<string>>> presentIds)
	{
		try
		{
			await using Stream asset = await FileSystem.OpenAppPackageFileAsync(assetName);
			await using var copy = new MemoryStream();
			await asset.CopyToAsync(copy);
			StoryPackageManifest manifest = await ManifestAsync(copy);
			string packageId = manifest.Id;
			if (!IsRecorded(packageId))
			{
				copy.Position = 0;
				if (await ImportIfAbsentAsync(copy, packageId, created, await presentIds()) && manifest.Version is { } version)
				{
					RecordVersion(packageId, version);
				}

				Record(packageId);
			}

			await RemoveDuplicatesAsync(copy, packageId);
			await UpdateIfOlderAsync(copy, manifest);

			if (!TranslationsAdopted(packageId))
			{
				await AdoptTranslationsAsync(copy, packageId);
			}

			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or StoryPackageException or InvalidDataException or InvalidOperationException or ArgumentException)
		{
			Debug.WriteLine($"Dino Reader could not install the starter {assetName}: {ex}");
			return false;
		}
	}

	// The copy in the library (found by its source id) takes the bundled translations and the switch; its cast, merges, cover and pages stay.
	// A deleted starter has no copy, so nothing comes back. Recorded once attempted (even if it failed), and only when the bundled package has translations, so a later one still adopts.
	private async Task AdoptTranslationsAsync(MemoryStream package, string packageId)
	{
		package.Position = 0;
		Story bundled = await StoryPackageReader.ReadStoryOnlyAsync(package);
		if (bundled.Translations is not { Count: > 0 })
		{
			return;
		}

		try
		{
			Story? stored = (await library.ListAsync()).FirstOrDefault(s => s.SourceId == packageId);
			if (stored is not null && BundledTranslations.Adopt(stored, bundled) is { } adopted && !ReferenceEquals(adopted, stored))
			{
				await library.UpdateAsync(adopted);
			}
		}
		finally
		{
			// Recorded even when the attempt fails, so a bad copy is not re-read and re-parsed on every launch.
			RecordTranslationsAdopted(packageId);
		}
	}

	// Whether the starter was imported (false: the library already had it).
	private async Task<bool> ImportIfAbsentAsync(MemoryStream package, string packageId, DateTimeOffset created, HashSet<string> present)
	{
		if (present.Contains(packageId))
		{
			return false;
		}

		await StoryPackageReader.ImportAsync(package, library, created: created);
		return true;
	}

	// An installed copy recorded at a lower version than the bundled one is replaced (unless the grown-up edited its text); with no copy (the grown-up deleted it)
	// only the version is recorded, so it stays deleted. A bundled package with no version never replaces anything.
	private async Task UpdateIfOlderAsync(MemoryStream package, StoryPackageManifest manifest)
	{
		if (manifest.Version is not { } bundledVersion || !StarterUpdate.NeedsUpdate(InstalledVersion(manifest.Id), bundledVersion))
		{
			return;
		}

		Story? stored = (await library.ListAsync()).FirstOrDefault(s => s.SourceId == manifest.Id);
		if (stored is not null && await ReplaceUnlessEditedAsync(package, stored))
		{
			RecordTranslationsAdopted(manifest.Id);
		}

		RecordVersion(manifest.Id, bundledVersion);
	}

	// Whether the copy was replaced: a copy whose page text the grown-up edited, or that already holds the bundled text, is left as it is.
	private async Task<bool> ReplaceUnlessEditedAsync(MemoryStream package, Story stored)
	{
		package.Position = 0;
		Story bundled = await StoryPackageReader.ReadStoryOnlyAsync(package);
		if (StarterUpdate.PageTextHash(stored) == StarterUpdate.PageTextHash(bundled) || StarterUpdate.IsEdited(stored.SourceId!, stored, bundled))
		{
			return false;
		}

		await ReplaceAsync(package, stored);
		return true;
	}

	// The bundled copy is imported first and the old one removed last, so a failure leaves the old story in place. If the process dies between the two,
	// two stories share the starter id: RemoveDuplicatesAsync clears the extra one on the next launch.
	private async Task ReplaceAsync(MemoryStream package, Story stored)
	{
		package.Position = 0;
		Story fresh = await StoryPackageReader.ImportAsync(package, library, created: stored.Created);
		try
		{
			IReadOnlyList<StarterUpdate.RecordingMove> copied = await CopyRecordingsAsync(stored, fresh, StarterUpdate.RecordingMoves(stored, fresh));
			Story refreshed = StarterUpdate.Refresh(stored, fresh, copied);
			await library.UpdateAsync(await WithCoverPhotoAsync(stored, fresh, refreshed));
		}
		catch
		{
			await library.DeleteAsync(fresh.Id);
			throw;
		}

		await library.DeleteAsync(stored.Id);
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
		if (manifest.Version is { } version)
		{
			RecordVersion(manifest.Id, version);
		}
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
