using System.Diagnostics;
using System.IO.Compression;
using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// The four bundled starter stories (Slice 91, Decision 95): <c>.dino</c> packages shipped as app assets and imported on first run.
/// Each starter is recorded as installed on its own, by package id, in preferences: a failed pass retries only the starters still
/// missing, and a starter a grown-up deleted never comes back because its record stays. A starter already in the library (restored or
/// imported by hand, found by its <see cref="Story.SourceId"/>) is recorded without being imported again.
/// </summary>
public sealed class StarterStories(IStoryLibrary library)
{
	private const string InstalledKeyPrefix = "dino.starter.installed.";

	// Set once a starter's stored copy has taken the bundled translations (Slice 95; v2 key, Slice 105: the five Nordic and Dutch tables): earlier versions installed the starters without them.
	private const string TranslationsAdoptedKeyPrefix = "dino.starter.translations.v2.";

	// Listed in shelf order: the first is the newest, so it comes first in the gallery.
	private static readonly string[] AssetNames = ["good-enough.dino", "three-little-pigs.dino", "gingerbread-man.dino", "tortoise-and-hare.dino"];

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

	private async Task<bool> TryInstallOneAsync(string assetName, DateTimeOffset created, Func<Task<HashSet<string>>> presentIds)
	{
		try
		{
			await using Stream asset = await FileSystem.OpenAppPackageFileAsync(assetName);
			await using var copy = new MemoryStream();
			await asset.CopyToAsync(copy);
			string packageId = await PackageIdAsync(copy);
			if (!IsRecorded(packageId))
			{
				copy.Position = 0;
				await ImportIfAbsentAsync(copy, packageId, created, await presentIds());
				Record(packageId);
			}

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

	private async Task ImportIfAbsentAsync(MemoryStream package, string packageId, DateTimeOffset created, HashSet<string> present)
	{
		if (!present.Contains(packageId))
		{
			await StoryPackageReader.ImportAsync(package, library, created: created);
		}
	}

	private static async Task<string> PackageIdAsync(MemoryStream package)
	{
		package.Position = 0;
		using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
		ZipArchiveEntry entry = zip.GetEntry(StoryPackageManifest.ManifestFileName)
			?? throw new StoryPackageException("A starter package has no manifest.");
		await using Stream stream = entry.Open();
		using var reader = new StreamReader(stream);
		return StoryPackageManifest.Parse(await reader.ReadToEndAsync()).Id;
	}
}
