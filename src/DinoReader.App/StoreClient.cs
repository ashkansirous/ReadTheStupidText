using System.Diagnostics;
using System.Text.Json;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Usage;

namespace DinoReader;

/// <summary>Why the list is what it is: <see cref="StoreLoadStatus.Offline"/> covers no network and a server that cannot be reached.</summary>
public enum StoreLoadStatus
{
	Ok,
	Offline,
}

public sealed record StoreListing(StoreLoadStatus Status, IReadOnlyList<StoreItem> Items);

public enum DownloadOutcome
{
	Done,
	Failed,
	Cancelled,
}

/// <summary>
/// The free story store's client (Slice 92, Decisions 94-99): the list, the cover pictures and the <c>.dino</c> download, all from
/// <see cref="BaseAddress"/>. It sends no account, identifier or body, only plain GETs, and keeps no cookies. A download is written to a
/// temp file first and imported into the library only when whole, so a dropped one leaves nothing on the shelf (the importer writes
/// <c>story.json</c> last).
/// </summary>
public sealed class StoreClient(IStoryLibrary library, AudioPackStore packs, BookSales sales, IFallbackSink? fallbacks = null)
{
	// Slice 346: the books of the last listing by id, so a request for a book's files (a download, an update, an audio pack) can carry that book's purchase proof.
	private readonly System.Collections.Concurrent.ConcurrentDictionary<string, StoreItem> _listed = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>What the app sells and what the store account owns (Slice 346).</summary>
	public BookSales Sales => sales;

	public const string BaseAddress = "https://dinoreader.sirous.uk";

	private const string ListPath = "/api/stories";
	private const string StoryPathPrefix = "/api/stories/";
	private const string CoverFolder = "store-covers";
	private const string DownloadFolder = "store-downloads";
	private const string PartExtension = ".part";
	private const string CoverExtension = ".jpg";
	private const int BufferBytes = 81920;
	private const long MaxListBytes = 1024 * 1024;
	private const long MaxCoverBytes = 5 * 1024 * 1024;
	private const long MaxPackBytes = 400L * 1024 * 1024;
	private static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(20);
	private static readonly TimeSpan PurchaseReadBudget = TimeSpan.FromMilliseconds(400);
	private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);

	// One client for the process; a redirect is never followed (a 3xx fails the call), so the app only ever talks to BaseAddress. Infinite timeout because every call sets its own (a download can legitimately take minutes).
	private readonly Dictionary<string, Task<DownloadOutcome>> _inFlight = [];

	private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };

	/// <summary>Where the store and the book update check report that they fell back (Slice 273 part C); counts nothing when the app gave no sink.</summary>
	public IFallbackSink Fallbacks { get; } = fallbacks ?? NoFallbackSink.Instance;

	// Ids compare case-insensitively, as the import check below does.
	public event EventHandler? LibraryChanged;

	/// <summary>The source ids of the stories on the shelf: a store entry with one of these shows "Downloaded".</summary>
	public async Task<HashSet<string>> InstalledIdsAsync() =>
		(await library.ListAsync()).Select(s => s.SourceId).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);

	/// <summary>The source ids found by reading every story folder on disk, or null when any folder could not be read (or the library is not folder-backed): the audio pack sweep deletes nothing then.</summary>
	public async Task<ISet<string>?> CompleteInstalledIdsAsync() =>
		library is FileStoryLibrary files ? await files.TryListSourceIdsAsync() : null;

	// Slice 346: the platform store is the one owner of what is bought, so a listing asks it (in memory only), but it never holds the list up: a read the snapshot is still fresh for is skipped, and a slow one
	// (Play setup up to 15 s, StoreKit products up to 20 s, a Windows Store ID key) is waited for only PurchaseReadBudget; it finishes in the background and raises the purchases' Changed event, which the
	// store page listens to. The store page itself does its own full read behind its loading view.
	private async Task ReadPurchasesAsync(IReadOnlyList<StoreItem> items, CancellationToken cancel)
	{
		if (!sales.Enabled || sales.Purchases.IsFresh)
		{
			return;
		}

		Task read = sales.Purchases.RefreshAsync(items, cancel);
		await Task.WhenAny(read, Task.Delay(PurchaseReadBudget, cancel));
	}

	/// <summary>The readable stories the server lists, or <see cref="StoreLoadStatus.Offline"/> when it cannot be reached.</summary>
	public async Task<StoreListing> ListAsync(CancellationToken cancel)
	{
		sales.RefreshConfigInBackground(); // never awaited: the listing uses the last known switch (default off) and this only fills it in for the next read
#if DINO_TEST_BOOKS
		if (Testing.TestStore.IsListing)
		{
			StoreListing fixture = await Testing.TestStore.ListAsync(); // Slice 261: the Debug test store (a scripted flow's fixture list), switched on by the test-store hook
			foreach (StoreItem item in fixture.Items)
			{
				_listed[item.Id] = item;
			}

			if (fixture.Status == StoreLoadStatus.Ok)
			{
				await ReadPurchasesAsync(fixture.Items, cancel); // Slice 346: the fake store of the fake-purchases hook answers here
			}

			return fixture;
		}
#endif
		try
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
			timeout.CancelAfter(ListTimeout);
			using GuardedResponse response = await GetGuardedAsync(ListPath, timeout.Token);
			response.Message.EnsureSuccessStatusCode();
			string json = await ReadCappedTextAsync(response, timeout.Token);
			IReadOnlyList<StoreItem> parsed = StoreCatalog.Parse(json, out int skipped);
			if (skipped > 0)
			{
				Fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed); // Slice 275: the listing lost books the server sent (malformed entries); the rest is shown
			}

			IReadOnlyList<StoreItem> readable = StoreCatalog.Readable(parsed);
			foreach (StoreItem item in readable)
			{
				_listed[item.Id] = item;
			}

			await ReadPurchasesAsync(readable, cancel);
			return new StoreListing(StoreLoadStatus.Ok, readable);
		}
		catch (Exception ex) when (IsNetworkFailure(ex) && !cancel.IsCancellationRequested)
		{
			Debug.WriteLine($"Dino Reader could not load the free stories: {ex.Message}");
			ReportFallback(ex);
			return new StoreListing(StoreLoadStatus.Offline, []);
		}
	}

	/// <summary>The local file of the story's cover, downloaded once per version; null when it has none or cannot be fetched.</summary>
	public async Task<string?> CoverPathAsync(StoreItem item, CancellationToken cancel)
	{
		if (item.CoverUrl is null)
		{
			return null;
		}

		string path = Path.Combine(FileSystem.CacheDirectory, CoverFolder, $"{item.Id}-v{item.Version}{CoverExtension}");
		if (File.Exists(path))
		{
			return path;
		}

		try
		{
			string fetched = await FetchCoverAsync(item.CoverUrl, path, cancel);
			PruneOldCovers(item.Id, path);
			return fetched;
		}
		catch (Exception ex) when ((IsNetworkFailure(ex) || ex is StoreStorageException) && !cancel.IsCancellationRequested)
		{
			Debug.WriteLine($"Dino Reader could not fetch a store cover: {ex.Message}");
			ReportFallback(ex);
			return null;
		}
	}

	/// <summary>
	/// Downloads the story's package, reporting 0..1 progress, and imports it. A story already on the shelf is not fetched again, and a
	/// second call for a story still downloading joins the first. On any failure nothing is left on the shelf.
	/// </summary>
	public async Task<DownloadOutcome> DownloadAsync(StoreItem item, IProgress<double> progress, CancellationToken cancel)
	{
		Task<DownloadOutcome> task;
		lock (_inFlight)
		{
			if (!_inFlight.TryGetValue(item.Id, out task!))
			{
				task = RunDownloadAsync(item, progress, cancel);
				_inFlight[item.Id] = task;
			}
		}

		try
		{
			DownloadOutcome outcome = await task;
			if (outcome == DownloadOutcome.Done)
			{
				LibraryChanged?.Invoke(this, EventArgs.Empty);
			}

			return outcome;
		}
		finally
		{
			lock (_inFlight)
			{
				if (_inFlight.TryGetValue(item.Id, out Task<DownloadOutcome>? current) && current == task)
				{
					_inFlight.Remove(item.Id);
				}
			}
		}
	}

	private async Task<DownloadOutcome> RunDownloadAsync(StoreItem item, IProgress<double> progress, CancellationToken cancel)
	{
		string part = Path.Combine(FileSystem.CacheDirectory, DownloadFolder, $"{item.Id}-{Guid.NewGuid():N}{PartExtension}");
		try
		{
			if ((await InstalledIdsAsync()).Contains(item.Id))
			{
				return DownloadOutcome.Done;
			}

			IReadOnlyList<(string Language, AudioPackInfo Pack)> wanted = WantedPacks(item, StoryLanguages.Current().All);
			var summed = new SummedProgress(progress, wanted.Sum(w => w.Pack.Bytes ?? 0));
			// The summed progress is the only reporter: the book fetch's own 0..1 would jump the bar between the book-only and the combined fraction.
			await FetchPackageAsync(item.DownloadUrl, part, new Progress<double>(), cancel, (done, total) => summed.Book(done, total));
			if (!await ImportPackageAsync(part, item.Id))
			{
				return DownloadOutcome.Failed;
			}

			await InstallPacksAsync(item, wanted, summed, cancel); // audio is optional: it never fails or rolls back the book
			return DownloadOutcome.Done;
		}
		catch (OperationCanceledException) when (cancel.IsCancellationRequested)
		{
			// no-fallback: the grown-up cancelled the download
			return DownloadOutcome.Cancelled;
		}
		catch (Exception ex) when (IsNetworkFailure(ex) || ex is StoreStorageException or StoryPackageException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader could not get the story {item.Id}: {ex}");
			ReportFallback(ex);
			return DownloadOutcome.Failed;
		}
		finally
		{
			TryDelete(part);
		}
	}

	/// <summary>
	/// Downloads one package (an address from the list, Slice 181) into the cache for the background book update and returns the file; the caller deletes it.
	/// File is null when the server cannot be reached or the package is not whole; NotFound is true for a 404 (the version is not in the store, so retrying will not help).
	/// Imports nothing and tells nobody.
	/// </summary>
	public async Task<PackageFetch> FetchPackageFileAsync(string downloadUrl, CancellationToken cancel)
	{
		string part = Path.Combine(FileSystem.CacheDirectory, DownloadFolder, $"update-{Guid.NewGuid():N}{PartExtension}");
		try
		{
			await FetchPackageAsync(downloadUrl, part, new Progress<double>(), cancel);
			return new PackageFetch(part, false);
		}
		catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound && !cancel.IsCancellationRequested)
		{
			// no-fallback: the store answered that this version is gone; the caller treats it as not found, not as a failure
			Debug.WriteLine($"Dino Reader found no such book version in the store: {downloadUrl}");
			TryDelete(part);
			return new PackageFetch(null, true);
		}
		catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.PaymentRequired or System.Net.HttpStatusCode.Forbidden && !cancel.IsCancellationRequested)
		{
			// Slice 346: the store refused a sold book's files (no purchase proof, or the store would not confirm it): the update is skipped quietly and the copy on the shelf stays as it is
			Debug.WriteLine($"Dino Reader: the store did not hand out an update ({(int?)ex.StatusCode}): {downloadUrl}");
			Fallbacks.Report(FallbackArea.Store, FallbackReason.StoreFailed);
			TryDelete(part);
			return new PackageFetch(null, false);
		}
		catch (Exception ex) when ((IsNetworkFailure(ex) || ex is StoreStorageException) && !cancel.IsCancellationRequested)
		{
			Debug.WriteLine($"Dino Reader could not fetch a book update: {ex.Message}");
			ReportFallback(ex);
			TryDelete(part);
			return new PackageFetch(null, false);
		}
		catch
		{
			TryDelete(part);
			throw;
		}
	}

	// A package whose own id is not the store item's would show Get again after a reload and pile up copies, so it is taken off the shelf.
	private async Task<bool> ImportPackageAsync(string path, string itemId)
	{
		await using FileStream package = File.OpenRead(path);
		Story story = await StoryPackageReader.ImportAsync(package, library);
		if (string.Equals(story.SourceId, itemId, StringComparison.OrdinalIgnoreCase))
		{
			package.Position = 0;
			await StarterStories.RecordStoreInstallAsync(package);
			return true;
		}

		Debug.WriteLine($"Dino Reader: the package for {itemId} carries id {story.SourceId}; not keeping it.");
		await library.DeleteAsync(story.Id);
		return false;
	}

	private static async Task<string> FetchCoverAsync(string url, string path, CancellationToken cancel)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
		timeout.CancelAfter(StallTimeout);
		using GuardedResponse response = await GetGuardedAsync(url, timeout.Token);
		response.Message.EnsureSuccessStatusCode();
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		string temp = $"{path}.{Guid.NewGuid():N}{PartExtension}";
		try
		{
			await using (FileStream file = CreateLocalFile(temp))
			{
				await CopyCappedAsync(response, file, MaxCoverBytes, null, timeout, timeout.Token);
			}

			File.Move(temp, path, overwrite: true);
			return path;
		}
		finally
		{
			TryDelete(temp);
		}
	}

	/// <summary>The packs the listing offers for the family's languages only, never the book's own (Slice 267, <see cref="AudioPackDue"/>).</summary>
	public static IReadOnlyList<(string Language, AudioPackInfo Pack)> WantedPacks(StoreItem item, IEnumerable<string> languages) => AudioPackDue.For(item, languages);

	/// <summary>
	/// Installs <paramref name="wanted"/> packs of a book that is on the shelf, one after the other, and returns true when none of them failed in a way a retry could fix (offline or a bad
	/// download is false; a missing pack, a pack already installed and a cancel are not). A failed pack leaves the book and the other packs alone.
	/// </summary>
	public async Task<bool> InstallPacksAsync(StoreItem item, IReadOnlyList<(string Language, AudioPackInfo Pack)> wanted, SummedProgress? summed, CancellationToken cancel)
	{
		bool allOk = true;
		foreach ((string language, AudioPackInfo pack) in wanted)
		{
			PackInstall result = await InstallOnePackAsync(item.Id, language, pack, summed, cancel);
			summed?.PackEnded();
			allOk &= result != PackInstall.Failed;
			if (result == PackInstall.Cancelled)
			{
				break; // the same cancel ends every pack; the book stays
			}
		}

		return allOk;
	}

	/// <summary>Installs one pack of a shelf book and says exactly what happened (Slice 262: the shelf audio run maps NotFound and Failed differently).</summary>
	public Task<PackInstall> InstallPackAsync(StoreItem item, string language, AudioPackInfo pack, CancellationToken cancel) =>
		InstallOnePackAsync(item.Id, language, pack, null, cancel);

	// Audio never fails the book or the update check: whatever the install throws is a failed pack. The pack bytes reach the summed progress on the downloading thread, in order.
	private async Task<PackInstall> InstallOnePackAsync(string bookId, string language, AudioPackInfo pack, SummedProgress? summed, CancellationToken cancel)
	{
		try
		{
			return await packs.InstallAsync(bookId, language, pack, summed is null ? null : new SyncProgress<long>(summed.Pack), cancel);
		}
		catch (OperationCanceledException) when (cancel.IsCancellationRequested)
		{
			// no-fallback: the grown-up cancelled; the pack store counts its own cancels
			return PackInstall.Cancelled;
		}
		catch (Exception ex)
		{
			// no-fallback: AudioPackStore.InstallAsync reports its own failures and never throws; this is only a last guard
			Debug.WriteLine($"Dino Reader could not install the audio pack {bookId}/{language}: {ex.Message}");
			return PackInstall.Failed;
		}
	}

	/// <summary>The pack store, for the reading screen's lookup and the update check.</summary>
	public AudioPackStore Packs => packs;

	/// <summary>One pack zip to a file, with the same stall guard and redirect rule as a book download (Slice 246 C2).</summary>
	internal static async Task FetchPackAsync(string url, string toPath, IProgress<long> bytesSoFar, CancellationToken cancel)
	{
		try
		{
			await FetchPackageAsync(url, toPath, new Progress<double>(), cancel, (done, _) => bytesSoFar.Report(done), MaxPackBytes);
		}
		catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			throw new AudioPackNotFoundException(url);
		}
		catch (StoreStorageException ex)
		{
			throw new IOException(ex.Message, ex); // the pack store treats a disk failure like any other failed install
		}
	}

	private static async Task FetchPackageAsync(
		string url, string part, IProgress<double> progress, CancellationToken cancel, Action<long, long?>? bytes = null, long cap = 0)
	{
#if DINO_TEST_BOOKS
		if (Testing.TestStore.Serves(url))
		{
			await Testing.TestStore.CopyAsync(url, part, bytes, cancel); // Slice 261: a test book from the Debug assets, never a network call
			return;
		}
#endif
		using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancel);
		stall.CancelAfter(StallTimeout);
		using GuardedResponse response = await GetGuardedAsync(url, stall.Token);
		response.Message.EnsureSuccessStatusCode();
		Directory.CreateDirectory(Path.GetDirectoryName(part)!);
		await using FileStream file = CreateLocalFile(part);
		await CopyCappedAsync(response, file, cap > 0 ? cap : StoryPackageReader.MaxTotalBytes, progress, stall, stall.Token, bytes);
	}

	private static Task<GuardedResponse> GetGuardedAsync(string path, CancellationToken cancel) =>
		StallGuardedRead.SendAsync(Http, () => RequestFor(path), StallGuardedRead.HeaderTimeout(StallTimeout), cancel);

	// Slice 346: a request for a sold book's files (package, audio pack, listening track) carries the store's purchase proof; every other request is the plain GET it always was.
	private static HttpRequestMessage RequestFor(string path)
	{
		Uri uri = Resolve(path);
		var request = new HttpRequestMessage(HttpMethod.Get, uri);
		if (ProofFor(uri) is { } proof)
		{
			request.Headers.TryAddWithoutValidation(PurchaseProofs.HeaderName, proof);
		}

		return request;
	}

	private static volatile Func<Uri, string?> _proofFor = _ => null;

	private static string? ProofFor(Uri uri) => _proofFor(uri);

	/// <summary>Makes <paramref name="client"/> the source of purchase proofs for every store request (called once at start-up, so the static pack fetcher can send them too).</summary>
	public static void InstallProofSource(StoreClient client) => _proofFor = client.ProofHeaderFor;

	// Only the files of a book that the last listing says is sold: /api/stories/<id>/download|audio|listen.
	private string? ProofHeaderFor(Uri uri)
	{
		string[] parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
		bool isFile = parts.Length >= 4 && parts[3] is "download" or "audio" or "listen";
		return isFile && _listed.TryGetValue(parts[2], out StoreItem? item) ? sales.ProofHeader(item) : null;
	}

	/// <summary>
	/// The books a background job (update check, shelf audio) may ask the store for (Slice 346): not sold, or owned. A sold book that is not owned, such as a copy kept from before the sale, is left out, so no
	/// request that would be refused is made and nothing on the shelf is touched.
	/// </summary>
	public IReadOnlyList<StoreItem> Fetchable(IEnumerable<StoreItem> items) => [.. items.Where(sales.MayUpdate)];

	// Copies the body, giving up when it passes the cap or stalls: the stall timer is restarted after every chunk.
	private static async Task CopyCappedAsync(
		GuardedResponse guarded, Stream target, long cap, IProgress<double>? progress, CancellationTokenSource stall, CancellationToken cancel, Action<long, long?>? bytes = null)
	{
		long? total = guarded.Message.Content.Headers.ContentLength;
		if (total > cap)
		{
			throw new IOException("The download is larger than allowed.");
		}

		await using Stream body = await guarded.OpenBodyAsync(StallTimeout, cancel);
		byte[] buffer = new byte[BufferBytes];
		long done = 0;
		int read;
		while ((read = await body.ReadAsync(buffer, cancel)) > 0)
		{
			done += read;
			if (done > cap)
			{
				throw new IOException("The download is larger than allowed.");
			}

			await WriteLocalAsync(target, buffer.AsMemory(0, read), cancel);
			stall.CancelAfter(StallTimeout);
			progress?.Report(total is > 0 ? (double)done / total.Value : 0);
			bytes?.Invoke(done, total);
		}
	}

	private static async Task<string> ReadCappedTextAsync(GuardedResponse guarded, CancellationToken cancel)
	{
		if (guarded.Message.Content.Headers.ContentLength > MaxListBytes)
		{
			throw new IOException("The story list is larger than allowed.");
		}

		await using Stream body = await guarded.OpenBodyAsync(StallTimeout, cancel);
		using var copy = new MemoryStream();
		byte[] buffer = new byte[BufferBytes];
		int read;
		while ((read = await body.ReadAsync(buffer, cancel)) > 0)
		{
			if (copy.Length + read > MaxListBytes)
			{
				throw new IOException("The story list is larger than allowed.");
			}

			copy.Write(buffer, 0, read);
		}

		return System.Text.Encoding.UTF8.GetString(copy.GetBuffer(), 0, (int)copy.Length);
	}

	/// <summary>The absolute address of a server path. Only paths under <c>/api/stories</c> on the store's own host are accepted, so a
	/// list that named another host could not make the app fetch from it.</summary>
	public static Uri Resolve(string path)
	{
		var uri = new Uri(new Uri(BaseAddress), path);
		bool ownHost = uri.Host == new Uri(BaseAddress).Host && uri.Scheme == Uri.UriSchemeHttps;
		bool ownPath = uri.AbsolutePath == ListPath || uri.AbsolutePath.StartsWith(StoryPathPrefix, StringComparison.Ordinal);
		return ownHost && ownPath ? uri : throw new HttpRequestException("The address is not one of the store's.");
	}

	// Local file trouble (a full disk, say) is told apart from a network one, so it is never shown as "offline".
	private static FileStream CreateLocalFile(string path)
	{
		try
		{
			return File.Create(path);
		}
		catch (IOException ex)
		{
			throw new StoreStorageException(ex);
		}
	}

	private static async Task WriteLocalAsync(Stream target, ReadOnlyMemory<byte> bytes, CancellationToken cancel)
	{
		try
		{
			await target.WriteAsync(bytes, cancel);
		}
		catch (IOException ex)
		{
			throw new StoreStorageException(ex);
		}
	}

	// Each story keeps only its newest cover: the files of older versions are removed once a newer one is on disk.
	private static void PruneOldCovers(string id, string keep)
	{
		try
		{
			foreach (string old in Directory.EnumerateFiles(Path.GetDirectoryName(keep)!, $"{id}-v*{CoverExtension}"))
			{
				if (!string.Equals(old, keep, StringComparison.Ordinal))
				{
					TryDelete(old);
				}
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// no-fallback: housekeeping delete of old covers; nothing depends on it
			Debug.WriteLine($"Dino Reader could not prune old store covers: {ex.Message}");
		}
	}

	// Slice 273 part C: the store could not be reached, so the shelf shows what it had (one row per session; the sink never throws). Only a network failure is store_unreachable:
	// a full disk, a bad package or a malformed listing means the store answered, so it is store_failed (Slice 275).
	private void ReportFallback(Exception ex) =>
		Fallbacks.Report(FallbackArea.Store, IsUnreachable(ex) ? FallbackReason.StoreUnreachable : FallbackReason.StoreFailed);

	private static bool IsUnreachable(Exception ex) =>
		ex is HttpRequestException or TimeoutException or OperationCanceledException or IOException { InnerException: System.Net.Sockets.SocketException };

	private static bool IsNetworkFailure(Exception ex) =>
		ex is HttpRequestException or IOException or OperationCanceledException or TimeoutException or JsonException;

	private static void TryDelete(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// no-fallback: housekeeping delete of a temp file; nothing depends on it
			Debug.WriteLine($"Dino Reader could not remove a store temp file: {ex.Message}");
		}
	}
}

/// <summary>The audio pack store's download, through the store client's guarded fetch (Slice 246 C2). Separate from <see cref="StoreClient"/> so the store client can own the pack store.</summary>
public sealed class StoreAudioPackFetcher : IAudioPackFetcher
{
	public Task FetchAsync(string url, string toPath, IProgress<long> bytesSoFar, CancellationToken cancel) => StoreClient.FetchPackAsync(url, toPath, bytesSoFar, cancel);
}

/// <summary>A failure writing the store's own temp files, not reaching the server.</summary>
public sealed class StoreStorageException(Exception inner) : Exception(inner.Message, inner);

/// <summary>Result of a background package download (Slice 181): the cached file, or null with <see cref="NotFound"/> telling a 404 from a network failure.</summary>
public sealed record PackageFetch(string? File, bool NotFound);
