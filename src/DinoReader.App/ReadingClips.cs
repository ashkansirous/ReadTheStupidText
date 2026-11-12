using System.Diagnostics;
using DinoReader.Core;
using FallbackArea = ReadTheStupidText.Application.Usage.FallbackArea;
using FallbackReason = ReadTheStupidText.Application.Usage.FallbackReason;

namespace DinoReader;

/// <summary>
/// The animals' bundled recordings as files (Slice 255). The player takes a file path and a packaged asset is not one on every platform, so each clip is copied once
/// into the cache folder (one folder per app version, so an update with a new clip never plays an old one). <see cref="EnsureAsync"/> runs before a read starts;
/// <see cref="PathOf"/> is then a plain lookup, so a clip starts the moment its slot is reached. A clip that is missing or cannot be copied has no path and the slot stays silent
/// (never the spoken word).
/// </summary>
internal static class AnimalClipFiles
{
	private static readonly SemaphoreSlim Gate = new(1, 1);

	private static string Folder => Path.Combine(FileSystem.CacheDirectory, "animal-sounds-" + AppInfo.VersionString);

	public static string? PathOf(AnimalKind kind)
	{
		string path = Path.Combine(Folder, AnimalSoundClips.FileNameOf(kind));
		return AnimalSoundClips.Has(kind) && File.Exists(path) ? path : null;
	}

	// A clip that failed to play (a corrupt or truncated copy) is removed, so the next read copies it again from the package.
	public static void Discard(AnimalKind kind)
	{
		try
		{
			string path = Path.Combine(Folder, AnimalSoundClips.FileNameOf(kind));
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch (Exception ex)
		{
			// no-fallback: removing a clip is housekeeping; the leftover file is replaced on the next preparation
			Debug.WriteLine($"Dino Reader animal clip {kind}: could not be removed: {ex.Message}");
		}
	}

	public static async Task EnsureAsync(IEnumerable<AnimalKind> kinds, string? language = null)
	{
		await Gate.WaitAsync();
		try
		{
			foreach (AnimalKind kind in kinds.Where(k => AnimalSoundClips.Has(k) && PathOf(k) is null))
			{
				await CopyAsync(kind, language);
			}
		}
		finally
		{
			Gate.Release();
		}
	}

	private static async Task CopyAsync(AnimalKind kind, string? language)
	{
		string path = Path.Combine(Folder, AnimalSoundClips.FileNameOf(kind));
		string partial = path + ".part";
		try
		{
			Directory.CreateDirectory(Folder);
			await using Stream asset = await FileSystem.OpenAppPackageFileAsync(AnimalSoundClips.AssetNameOf(kind));
			await using (FileStream file = File.Create(partial))
			{
				await asset.CopyToAsync(file);
			}

			File.Move(partial, path, overwrite: true);
		}
		catch (Exception ex)
		{
			DinoFallbacks.Report(FallbackArea.Speech, FallbackReason.AnimalClipCopyFailed, language); // Slice 275 part 3, split in Slice 351 (the player's failure is animal_clip_play_failed): that animal's slot stays silent
			Debug.WriteLine($"Dino Reader animal clip {kind}: could not be prepared, that slot stays silent: {ex.Message}");
		}
	}
}
