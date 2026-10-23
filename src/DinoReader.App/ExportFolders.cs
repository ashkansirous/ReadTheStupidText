using System.Diagnostics;

namespace DinoReader;

/// <summary>The cache folders a share-sheet export is written into: one fresh folder per export, so a second one never truncates a file a share target is still reading; old ones go.</summary>
internal static class ExportFolders
{
	private const string ExportFolder = "exports";
	private static readonly TimeSpan StaleAge = TimeSpan.FromHours(1);

	/// <summary>Removes the exports older than an hour (best effort).</summary>
	public static void DeleteStale()
	{
		string root = Root();
		if (!Directory.Exists(root))
		{
			return;
		}

		foreach (string old in Directory.GetDirectories(root).Where(d => DateTime.UtcNow - Directory.GetCreationTimeUtc(d) > StaleAge))
		{
			TryDelete(old);
		}
	}

	/// <summary>Clears the stale exports and creates a new empty folder for this one.</summary>
	public static string CreateNew()
	{
		DeleteStale();
		string folder = Path.Combine(Root(), Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(folder);
		return folder;
	}

	/// <summary>Removes one export folder (best effort).</summary>
	public static void TryDelete(string folder)
	{
		try
		{
			Directory.Delete(folder, recursive: true);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader could not remove an export: {ex.Message}");
		}
	}

	private static string Root() => Path.Combine(FileSystem.CacheDirectory, ExportFolder);
}
