using DinoReader.Core;
using Sirous.Reading.Application.Usage;

namespace DinoReader;

/// <summary>
/// Slice 278 part B review: the one-time removal of the voice and storyteller packs (and half-finished downloads) an older Dino Reader left on the phone, and of the preferences that
/// belonged to them. The deleting is <see cref="OldVoicePacks"/> (known voice pack ids only; the audio packs and the books are other folders). Runs once per app run in the
/// background; a pass with a failed delete is reported and tried again at the next start, and a clean pass sets a flag so it never runs again.
/// </summary>
public sealed class OldVoicePacksCleanup(IFallbackSink sink)
{
	private const string DoneKey = "dino.oldvoicepacks.done";
	private const string PacksFolder = "packs"; // the folder MobileVoicePackService keeps its packs in
	private static readonly string[] OldPreferenceKeys = ["dino.story.nopack", "dino.story.wanted", "dino.notify.ask.pending", "dino.storyteller.ready.pending"];

	private int _started;

	/// <summary>Returns how many entries this start deleted.</summary>
	public async Task<int> RunOnceAsync()
	{
		if (Interlocked.Exchange(ref _started, 1) == 1)
		{
			return 0;
		}

		try
		{
			if (Preferences.Default.Get(DoneKey, false))
			{
				return 0;
			}

			OldVoicePacksResult result = await Task.Run(() => OldVoicePacks.Delete(PacksRoot(), sink));
			if (result.Failed == 0)
			{
				RemoveOldPreferences();
				Preferences.Default.Set(DoneKey, true);
			}

			return result.Deleted.Count;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			sink.Report(FallbackArea.Pack, FallbackReason.PackSyncFailed); // the pass ended early; the next start tries again
			System.Diagnostics.Debug.WriteLine($"[OldVoicePacks] cleanup ended early: {ex}");
			return 0;
		}
	}

	private static void RemoveOldPreferences()
	{
		foreach (string key in OldPreferenceKeys)
		{
			Preferences.Default.Remove(key);
		}
	}

	// Where MobileVoicePackService keeps the packs: the package cache on Windows (never the cloud-backed folder), the app data folder elsewhere.
	private static string PacksRoot()
	{
#if WINDOWS
		return Path.Combine(FileSystem.Current.CacheDirectory, PacksFolder);
#else
		return Path.Combine(FileSystem.Current.AppDataDirectory, PacksFolder);
#endif
	}
}
