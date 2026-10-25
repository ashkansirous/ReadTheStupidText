using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>What the storyteller card shows for a language: its state and the download size it names (Slice 114).</summary>
internal sealed record StorytellerView(StorytellerState State, int Megabytes);

/// <summary>
/// The storyteller voices pack of the cast screen (Slice 114, Decision 118): works out the card's state, downloads the pack, plays its
/// sample before the download, and removes the pack again ("Use Dino's first voices"). The decisions are the pure <see cref="StorytellerOffer"/>;
/// this only talks to the pack service, the network for the sample and the file player. One per cast screen.
/// </summary>
internal sealed class StorytellerController(IVoicePackService packs, IRecordingPlayer player, IPackUpdateNetwork network)
{
	private static readonly HttpClient Http = new();
	private static readonly TimeSpan CatalogMaxAge = TimeSpan.FromMinutes(10);
	private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(30);
	private const long MaxSampleBytes = 4_000_000;
	private const string PartialSuffix = ".partial";

	private PackInfo? _listed;

	/// <summary>The card's state for <paramref name="language"/>. An installed pack needs no network; otherwise the catalog is asked (cached for a few minutes).</summary>
	public async Task<StorytellerView> LoadAsync(string language, CancellationToken cancellationToken)
	{
		string code = UiLanguage.Resolve(language);
		if (!packs.SupportsStorytellerPacks || !KokoroVoiceTable.Serves(code))
		{
			// No card, and no catalog request: a head without storyteller support, or a language no storyteller voice speaks.
			return new StorytellerView(StorytellerState.Hidden, 0);
		}

		IReadOnlyList<InstalledVoicePack> installed = InstalledPacks();
		if (StorytellerOffer.StateFor(code, installed, null) == StorytellerState.Installed)
		{
			return new StorytellerView(StorytellerState.Installed, 0);
		}

		PackCatalogResult catalog = await packs.GetCatalogCachedAsync(CatalogMaxAge, cancellationToken);
		_listed = catalog.Status == PackDownloadResult.Ok ? StorytellerOffer.ListedFor(catalog.Packs, code) : null;
		StorytellerState state = StorytellerOffer.StateFor(code, installed, _listed);
		return new StorytellerView(state, _listed is null ? 0 : StorytellerOffer.Megabytes(_listed.SizeBytes));
	}

	/// <summary>
	/// Installs a newer version of the installed pack when the catalog lists one, quietly and only on an unmetered connection (the same rule as the
	/// voice packs' automatic update). Nothing happens when the pack is not installed. The pack service raises <c>InstalledPacksChanged</c> afterwards.
	/// </summary>
	public async Task UpdateInstalledAsync(CancellationToken cancellationToken)
	{
		InstalledVoicePack? installed = InstalledPacks().FirstOrDefault(p => p.Manifest.Id == PackConstants.StorytellerPackId);
		if (installed is null || !network.AllowsAutomaticUpdate)
		{
			return;
		}

		PackCatalogResult catalog = await packs.GetCatalogCachedAsync(CatalogMaxAge, cancellationToken);
		if (catalog.Status == PackDownloadResult.Ok && StorytellerOffer.UpdateAvailable(installed, catalog.Packs))
		{
			await packs.DownloadAsync(PackConstants.StorytellerPackId, null, cancellationToken);
		}
	}

	/// <summary>Downloads and installs the pack; the pack service raises <c>InstalledPacksChanged</c> when it is in place.</summary>
	public Task<PackDownloadResult> DownloadAsync(IProgress<double> progress, CancellationToken cancellationToken) =>
		packs.DownloadAsync(PackConstants.StorytellerPackId, progress, cancellationToken);

	/// <summary>Removes the pack, going back to Dino's first voices. False when nothing was removed. Only called while nothing is reading.</summary>
	public bool UseFirstVoices() => packs.Uninstall(PackConstants.StorytellerPackId);

	/// <summary>The path of the listed pack's sample, fetching it first (once per pack version) and dropping the samples of older versions.</summary>
	public async Task<string> PrepareSampleAsync(CancellationToken cancellationToken)
	{
		PackInfo pack = _listed ?? throw new InvalidOperationException("The storyteller pack is not listed.");
		string name = StorytellerOffer.SampleFileName(pack);
		string path = Path.Combine(FileSystem.CacheDirectory, name);
		if (!File.Exists(path))
		{
			await FetchSampleAsync(StorytellerOffer.SampleUri(pack), path, cancellationToken);
			DeleteOldSamples(StorytellerOffer.SamplePattern(pack), name);
		}

		return path;
	}

	/// <summary>Plays a sample made by <see cref="PrepareSampleAsync"/>. <paramref name="ended"/> and <paramref name="failed"/> are the player's and may run before this returns.</summary>
	public void PlaySample(string path, Action ended, Action failed) => player.Play(path, ended, failed);

	private static void DeleteOldSamples(string pattern, string keepName)
	{
		try
		{
			foreach (string old in Directory.EnumerateFiles(FileSystem.CacheDirectory, pattern).Where(f => Path.GetFileName(f) != keepName))
			{
				File.Delete(old);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			System.Diagnostics.Debug.WriteLine($"Could not drop an old storyteller sample: {ex.Message}");
		}
	}

	public void StopSample() => player.Stop();

	// The sample is a small wav next to the pack on the packs release; it goes to a partial file first so a cut download is never played.
	private static async Task FetchSampleAsync(Uri uri, string path, CancellationToken cancellationToken)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(SampleTimeout);
		using HttpResponseMessage response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
		response.EnsureSuccessStatusCode();
		if (response.Content.Headers.ContentLength is not > 0 or > MaxSampleBytes)
		{
			throw new InvalidDataException("The voice sample has an unexpected size.");
		}

		string partial = path + PartialSuffix;
		await using (Stream body = await response.Content.ReadAsStreamAsync(timeout.Token))
		try
		{
			await using (var file = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
			{
				await body.CopyToAsync(file, timeout.Token);
			}

			File.Move(partial, path, overwrite: true);
		}
		catch
		{
			// A cut or failed fetch leaves no partial file behind.
			File.Delete(partial);
			throw;
		}
	}

	private IReadOnlyList<InstalledVoicePack> InstalledPacks()
	{
		try
		{
			return packs.GetInstalledStorytellerPacks();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return [];
		}
	}
}
