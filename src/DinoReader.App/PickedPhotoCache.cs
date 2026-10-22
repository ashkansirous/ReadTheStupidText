namespace DinoReader;

/// <summary>The cover picker's copies of gallery photos live in the app cache until a Save keeps one; this removes the ones that are not needed.</summary>
public static class PickedPhotoCache
{
	/// <summary>Deletes a picked photo's cache copy. A file outside the cache is the user's own and stays; failures are only logged.</summary>
	public static void Delete(string? path)
	{
		try
		{
			if (path is not null && path.StartsWith(FileSystem.CacheDirectory, StringComparison.OrdinalIgnoreCase))
			{
				File.Delete(path);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not clear the picked photo: {ex.Message}");
		}
	}
}
