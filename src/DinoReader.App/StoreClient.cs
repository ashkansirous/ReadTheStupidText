using System.Diagnostics;
using System.Text.Json;
using DinoReader.Core;

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
public sealed class StoreClient(IStoryLibrary library)
{
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
	private static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(20);
	private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);

	// One client for the process; a redirect is never followed (a 3xx fails the call), so the app only ever talks to BaseAddress. Infinite timeout because every call sets its own (a download can legitimately take minutes).
	private readonly Dictionary<string, Task<DownloadOutcome>> _inFlight = [];

	private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };

	// Same query as StarterStories.PresentIdsAsync; StarterStories.cs is outside Slice 92's Touches, so the copy stays for now.
	// Ids compare case-insensitively, as the import check below does.
	public event EventHandler? LibraryChanged;

	/// <summary>The source ids of the stories on the shelf: a store entry with one of these shows "Downloaded".</summary>
	public async Task<HashSet<string>> InstalledIdsAsync() =>
		(await library.ListAsync()).Select(s => s.SourceId).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);

	/// <summary>The readable stories the server lists, or <see cref="StoreLoadStatus.Offline"/> when it cannot be reached.</summary>
	public async Task<StoreListing> ListAsync(CancellationToken cancel)
	{
		try
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
			timeout.CancelAfter(ListTimeout);
			using HttpResponseMessage response = await Http.GetAsync(Resolve(ListPath), HttpCompletionOption.ResponseHeadersRead, timeout.Token);
			response.EnsureSuccessStatusCode();
			string json = await ReadCappedTextAsync(response, timeout.Token);
			return new StoreListing(StoreLoadStatus.Ok, StoreCatalog.Readable(StoreCatalog.Parse(json)));
		}
		catch (Exception ex) when (IsNetworkFailure(ex) && !cancel.IsCancellationRequested)
		{
			Debug.WriteLine($"Dino Reader could not load the free stories: {ex.Message}");
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

			await FetchPackageAsync(item.DownloadUrl, part, progress, cancel);
			return await ImportPackageAsync(part, item.Id) ? DownloadOutcome.Done : DownloadOutcome.Failed;
		}
		catch (OperationCanceledException) when (cancel.IsCancellationRequested)
		{
			return DownloadOutcome.Cancelled;
		}
		catch (Exception ex) when (IsNetworkFailure(ex) || ex is StoreStorageException or StoryPackageException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader could not get the story {item.Id}: {ex}");
			return DownloadOutcome.Failed;
		}
		finally
		{
			TryDelete(part);
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
		using HttpResponseMessage response = await Http.GetAsync(Resolve(url), HttpCompletionOption.ResponseHeadersRead, timeout.Token);
		response.EnsureSuccessStatusCode();
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

	private static async Task FetchPackageAsync(string url, string part, IProgress<double> progress, CancellationToken cancel)
	{
		using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancel);
		stall.CancelAfter(StallTimeout);
		using HttpResponseMessage response = await Http.GetAsync(Resolve(url), HttpCompletionOption.ResponseHeadersRead, stall.Token);
		response.EnsureSuccessStatusCode();
		Directory.CreateDirectory(Path.GetDirectoryName(part)!);
		await using FileStream file = CreateLocalFile(part);
		await CopyCappedAsync(response, file, StoryPackageReader.MaxTotalBytes, progress, stall, stall.Token);
	}

	// Copies the body, giving up when it passes the cap or stalls: the stall timer is restarted after every chunk.
	private static async Task CopyCappedAsync(
		HttpResponseMessage response, Stream target, long cap, IProgress<double>? progress, CancellationTokenSource stall, CancellationToken cancel)
	{
		long? total = response.Content.Headers.ContentLength;
		if (total > cap)
		{
			throw new IOException("The download is larger than allowed.");
		}

		await using Stream body = await response.Content.ReadAsStreamAsync(cancel);
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
		}
	}

	private static async Task<string> ReadCappedTextAsync(HttpResponseMessage response, CancellationToken cancel)
	{
		if (response.Content.Headers.ContentLength > MaxListBytes)
		{
			throw new IOException("The story list is larger than allowed.");
		}

		await using Stream body = await response.Content.ReadAsStreamAsync(cancel);
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
			Debug.WriteLine($"Dino Reader could not prune old store covers: {ex.Message}");
		}
	}

	private static bool IsNetworkFailure(Exception ex) =>
		ex is HttpRequestException or IOException or OperationCanceledException or JsonException;

	private static void TryDelete(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader could not remove a store temp file: {ex.Message}");
		}
	}
}

/// <summary>A failure writing the store's own temp files, not reaching the server.</summary>
public sealed class StoreStorageException(Exception inner) : Exception(inner.Message, inner);
