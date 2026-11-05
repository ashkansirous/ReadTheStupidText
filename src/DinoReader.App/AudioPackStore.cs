using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.RegularExpressions;
using DinoReader.Core;

namespace DinoReader;

/// <summary>What one pack install did (Slice 246 C2). Only <see cref="Failed"/> is worth trying again at the next update check; the others are final for that listed version.</summary>
public enum PackInstall
{
	Installed,
	AlreadyInstalled,
	NotFound,
	Failed,
	Cancelled,
}

/// <summary>Downloads one pack zip to a file (the store client does it with its stall guards). Throws <see cref="AudioPackNotFoundException"/> for a 404, any other network or disk failure as it happens.</summary>
public interface IAudioPackFetcher
{
	Task FetchAsync(string url, string toPath, IProgress<long> bytesSoFar, CancellationToken cancel);
}

/// <summary>The server has no such pack version, so retrying will not help.</summary>
public sealed class AudioPackNotFoundException(string message) : Exception(message);

/// <summary>A downloaded pack whose manifest names a clip the zip did not hold (or held empty), so the pack is not kept (Slice 267: counted as a <see cref="PackFailureKind.Clip"/> failure).</summary>
public sealed class AudioPackClipMissingException(string message) : Exception(message);

/// <summary>
/// One 0..1 progress for a book and its audio packs together (Slice 246 C2): the bytes of the book (its size is known once the response starts) plus the bytes of each pack the listing
/// names. A pack whose size the listing does not know counts as nothing until it ends; the value never passes 1 and never goes down.
/// </summary>
public sealed class SummedProgress(IProgress<double> inner, long packBytesListed)
{
	private readonly object _lock = new();
	private long _bookDone;
	private long _bookTotal;
	private long _packsFinished;
	private long _packNow;
	private double _last;

	public void Book(long done, long? total)
	{
		lock (_lock)
		{
			_bookDone = done;
			_bookTotal = total ?? _bookTotal;
			Report();
		}
	}

	public void Pack(long done)
	{
		lock (_lock)
		{
			_packNow = done;
			Report();
		}
	}

	public void PackEnded()
	{
		lock (_lock)
		{
			_packsFinished += _packNow;
			_packNow = 0;
			Report();
		}
	}

	// Called with the lock held.
	private void Report()
	{
		long total = _bookTotal + Math.Max(packBytesListed, _packsFinished + _packNow);
		double value = total <= 0 ? 0 : Math.Min(1, (double)(_bookDone + _packsFinished + _packNow) / total);
		_last = Math.Max(_last, value);
		inner.Report(_last);
	}
}

/// <summary>An <see cref="IProgress{T}"/> that runs its callback on the reporting thread at once (unlike <see cref="Progress{T}"/>, which posts), so a pack's last byte count is seen before the pack ends.</summary>
public sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
{
	public void Report(T value) => report(value);
}

/// <summary>An installed pack: its folder, version and the manifest read from it.</summary>
public sealed record InstalledPack(string Folder, int Version, PremadeAudio Audio)
{
	/// <summary>The clip file for the <paramref name="index"/>-th spoken piece when the pack has one for that position, its hash confirms <paramref name="text"/> and the file is on disk, else null (the piece is read by the device voice).</summary>
	public string? PathOf(int index, string text) => Audio.ClipAt(index, text) is { } name && File.Exists(Path.Combine(Folder, name)) ? Path.Combine(Folder, name) : null;
}

/// <summary>
/// The one owner of the pre-made audio packs on disk (Slice 246 C2): <c>&lt;root&gt;/&lt;bookId&gt;/&lt;lang&gt;-v&lt;n&gt;/</c> (manifest.json and the clips), outside the book's own folder so book updates never touch it.
/// A folder with the final name exists only when complete: the zip is fetched to <c>&lt;cache&gt;/audio-packs/*.part</c>, its length checked against the listed bytes, extracted into <c>&lt;lang&gt;-v&lt;n&gt;.tmp</c>,
/// every clip the manifest names checked, then the folder is renamed and older versions of that language are deleted. A kill leaves only <c>.part</c> files and <c>.tmp</c> folders, which the next
/// operation deletes. One operation at a time; installing the version that is already installed is a no-op. Plain .NET (no MAUI), so the states are unit-tested.
/// </summary>
public sealed partial class AudioPackStore
{
	private const string PartExtension = ".part";
	private const string TmpExtension = ".tmp";
	private const string CacheFolder = "audio-packs";
	private const int MaxEntries = 4000;
	private const int MaxRetriesPerLaunch = 1; // a pack that failed is tried once more in the same launch, no further
	private const long MaxUnpackedBytes = 400L * 1024 * 1024;

	private static readonly Regex SafeName = SafeNamePattern();
	private static readonly Regex VersionFolder = VersionFolderPattern();

	private readonly string _root;
	private readonly string _partFolder;
	private readonly IAudioPackFetcher _fetcher;
	private readonly Action<string> _log;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly ConcurrentDictionary<string, InstalledPack> _opened = new();
	private readonly ConcurrentDictionary<string, bool> _failedThisLaunch = new();
	private readonly ConcurrentDictionary<string, int> _failuresThisLaunch = new(); // failed downloads per book and language in this launch: a pack is tried again at most once
	private bool _cleaned;

	/// <summary>Slice 267: raised for every pack download as (book id, language, <see cref="AudioPackUsage"/> detail): started, done or failed with its kind. Carries ids and closed words only.</summary>
	public event Action<string, string, string>? Outcome;

	/// <summary>Whether a download of this book's pack in this language failed in this launch and has not worked since (the reason a read then says <see cref="DeviceVoiceReason.PackFailed"/>).</summary>
	public bool FailedThisLaunch(string bookId, string language) => _failedThisLaunch.ContainsKey(Key(bookId, language));

	private static string Key(string bookId, string language) => $"{bookId.ToLowerInvariant()}/{language}";

	private void Report(string bookId, string language, string detail)
	{
		bool failed = detail.StartsWith(AudioPackUsage.PackFailedPrefix, StringComparison.Ordinal);
		_failedThisLaunch.AddOrUpdate(Key(bookId, language), failed, (_, was) => detail == AudioPackUsage.PackStarted ? was : failed);
		if (failed)
		{
			_failuresThisLaunch.AddOrUpdate(Key(bookId, language), 1, (_, count) => count + 1);
		}

		try
		{
			Outcome?.Invoke(bookId, language, detail);
		}
		catch (Exception ex)
		{
			_log($"pack {bookId}/{language}: the outcome count could not be queued ({ex.GetType().Name})");
		}
	}

	private static PackFailureKind KindOf(Exception ex) => ex switch
	{
		AudioPackClipMissingException => PackFailureKind.Clip,
		InvalidDataException => PackFailureKind.Unzip,
		IOException or UnauthorizedAccessException => PackFailureKind.Storage,
		_ => PackFailureKind.Network,
	};

	public AudioPackStore(string root, string cacheRoot, IAudioPackFetcher fetcher, Action<string>? log = null)
	{
		_root = root;
		_partFolder = Path.Combine(cacheRoot, CacheFolder);
		_fetcher = fetcher;
		_log = log ?? (_ => { });
	}

	/// <summary>The installed pack of a book language, or null when there is none (or its manifest cannot be read, which reads as none).</summary>
	public InstalledPack? Open(string bookId, string language)
	{
		if (!IsSafe(bookId, language) || InstalledVersion(bookId, language) is not { } version)
		{
			return null;
		}

		string folder = FolderOf(bookId, language, version);
		if (_opened.TryGetValue(folder, out InstalledPack? known))
		{
			return known;
		}

		try
		{
			var pack = new InstalledPack(folder, version, PremadeAudio.Parse(File.ReadAllText(Path.Combine(folder, PremadeAudio.ManifestFile))));
			_opened[folder] = pack;
			return pack;
		}
		catch (Exception ex) when (ex is IOException or PremadeAudioException or UnauthorizedAccessException)
		{
			_log($"pack {bookId}/{language} v{version}: manifest unreadable, no clips ({ex.GetType().Name})");
			return null;
		}
	}

	/// <summary>The installed version of a book language, or null.</summary>
	public int? InstalledVersion(string bookId, string language)
	{
		string bookFolder = BookFolder(bookId);
		if (!IsSafe(bookId, language) || !Directory.Exists(bookFolder))
		{
			return null;
		}

		int? best = null;
		foreach (string dir in Directory.EnumerateDirectories(bookFolder))
		{
			Match match = VersionFolder.Match(Path.GetFileName(dir));
			if (match.Success && match.Groups[1].Value == language && int.TryParse(match.Groups[2].ValueSpan, out int version)
				&& File.Exists(Path.Combine(dir, PremadeAudio.ManifestFile)) && (best is null || version > best))
			{
				best = version;
			}
		}

		return best;
	}

	/// <summary>Deletes what an interrupted install left: every <c>.part</c> file and <c>.tmp</c> folder this store owns. Runs by itself before the first install or sweep.</summary>
	public void CleanupInterrupted()
	{
		int removed = 0;
		removed += DeleteMatching(_partFolder, file: true, PartExtension);
		if (Directory.Exists(_root))
		{
			foreach (string book in Directory.EnumerateDirectories(_root))
			{
				removed += DeleteMatching(book, file: false, TmpExtension);
			}
		}

		_cleaned = true;
		if (removed > 0)
		{
			_log($"pack cleanup: removed {removed} interrupted leftover(s)");
		}
	}

	/// <summary>
	/// Installs the listed pack of one book language. Offline, a server error, a wrong size or a broken zip give <see cref="PackInstall.Failed"/> and leave the previous pack (if any) in place: audio is
	/// optional, so the caller's book is never failed or rolled back for it. <paramref name="bytes"/> reports the zip's bytes as they arrive.
	/// </summary>
	public async Task<PackInstall> InstallAsync(string bookId, string language, AudioPackInfo pack, IProgress<long>? bytes, CancellationToken cancel, IAudioPackFetcher? fetcher = null)
	{
		if (!IsSafe(bookId, language))
		{
			_log($"pack {bookId}/{language}: not a name this store keeps, skipped");
			return PackInstall.Failed;
		}

		try
		{
			await _gate.WaitAsync(cancel);
		}
		catch (OperationCanceledException)
		{
			return PackInstall.Cancelled;
		}

		try
		{
			return await InstallGuardedAsync(bookId, language, pack, bytes, cancel, fetcher ?? _fetcher);
		}
		finally
		{
			_gate.Release();
		}
	}

	// Audio never fails a book: whatever goes wrong here (a disk error, a bad address) is a failed install, never an exception for the caller. Only the caller's own cancel is passed on as Cancelled.
	private async Task<PackInstall> InstallGuardedAsync(string bookId, string language, AudioPackInfo pack, IProgress<long>? bytes, CancellationToken cancel, IAudioPackFetcher fetcher)
	{
		try
		{
			EnsureCleaned();
			if (InstalledVersion(bookId, language) == pack.Version)
			{
				_log($"pack {bookId}/{language} v{pack.Version}: already installed, skipped");
				return PackInstall.AlreadyInstalled;
			}

			return await InstallCoreAsync(bookId, language, pack, bytes, cancel, fetcher);
		}
		catch (OperationCanceledException) when (cancel.IsCancellationRequested)
		{
			return PackInstall.Cancelled;
		}
		catch (Exception ex)
		{
			_log($"pack {bookId}/{language} v{pack.Version}: failed, the book is kept without it ({ex.GetType().Name})");
			return PackInstall.Failed;
		}
	}

	/// <summary>Deletes every pack of one book.</summary>
	public async Task DeleteBookAsync(string bookId, CancellationToken cancel = default)
	{
		if (!IsSafe(bookId, "xx"))
		{
			return;
		}

		await _gate.WaitAsync(cancel);
		try
		{
			DeleteFolder(BookFolder(bookId));
			_log($"pack {bookId}: book removed, packs deleted");
		}
		finally
		{
			_gate.Release();
		}
	}

	/// <summary>
	/// Deletes the packs of every book that is no longer on the shelf. <paramref name="presentBooks"/> is asked once the store is free (after any install in flight has finished), so a book
	/// that was just downloaded is in it. It must be a complete reading of the library's folders; null (anything unreadable) deletes nothing.
	/// </summary>
	public async Task SweepAsync(Func<Task<ISet<string>?>> presentBooks, CancellationToken cancel = default)
	{
		await _gate.WaitAsync(cancel);
		try
		{
			EnsureCleaned();
			if (!Directory.Exists(_root))
			{
				return;
			}

			ISet<string>? listed = await presentBooks();
			if (listed is null)
			{
				_log("pack sweep: the library folders could not all be read, nothing deleted");
				return;
			}

			ISet<string> present = new HashSet<string>(listed.Select(id => id.ToLowerInvariant()), StringComparer.Ordinal);
			if (present.Count == 0)
			{
				_log("pack sweep: the shelf list is empty, nothing deleted (an empty list is not trusted)");
				return;
			}

			foreach (string book in Directory.EnumerateDirectories(_root).Where(dir => !present.Contains(Path.GetFileName(dir))))
			{
				DeleteFolder(book);
				_log($"pack {Path.GetFileName(book)}: book is not on the shelf, packs deleted");
			}
		}
		finally
		{
			_gate.Release();
		}
	}

	private async Task<PackInstall> InstallCoreAsync(string bookId, string language, AudioPackInfo pack, IProgress<long>? bytes, CancellationToken cancel, IAudioPackFetcher fetcher)
	{
		string label = $"pack {bookId}/{language} v{pack.Version}";
		string part = Path.Combine(_partFolder, $"{bookId.ToLowerInvariant()}-{language}-v{pack.Version}-{Guid.NewGuid():N}{PartExtension}");
		string tmp = FolderOf(bookId, language, pack.Version) + TmpExtension;
		if (_failuresThisLaunch.GetValueOrDefault(Key(bookId, language)) > MaxRetriesPerLaunch)
		{
			_log($"{label}: failed twice in this launch, not tried again until the next open");
			return PackInstall.Failed;
		}

		try
		{
			Directory.CreateDirectory(_partFolder);
			Report(bookId, language, AudioPackUsage.PackStarted);
			await fetcher.FetchAsync(pack.Url, part, bytes ?? new Progress<long>(), cancel);
			long length = new FileInfo(part).Length;
			if (pack.Bytes is { } listed && listed != length)
			{
				_log($"{label}: downloaded {length} bytes, the listing says {listed}, not kept");
				Report(bookId, language, AudioPackUsage.PackFailed(PackFailureKind.Size));
				return PackInstall.Failed;
			}

			Unpack(part, tmp, language);
			PublishFolder(bookId, language, pack.Version, tmp);
			_log($"{label}: installed");
			Report(bookId, language, AudioPackUsage.PackDone);
			return PackInstall.Installed;
		}
		catch (OperationCanceledException) when (cancel.IsCancellationRequested)
		{
			_log($"{label}: cancelled, nothing kept");
			return PackInstall.Cancelled;
		}
		catch (AudioPackNotFoundException ex)
		{
			_log($"{label}: not in the store");
			Report(bookId, language, AudioPackUsage.PackFailed(PackFailureKind.Missing));
			return PackInstall.NotFound;
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !cancel.IsCancellationRequested)
		{
			_log($"{label}: failed, the book is kept without it ({ex.GetType().Name})");
			Report(bookId, language, AudioPackUsage.PackFailed(KindOf(ex)));
			return PackInstall.Failed;
		}
		finally
		{
			DeleteFile(part);
			DeleteFolder(tmp);
		}
	}

	// The extracted folder becomes the final one in one rename, then the other versions of that language go.
	private void PublishFolder(string bookId, string language, int version, string tmp)
	{
		string final = FolderOf(bookId, language, version);
		DeleteFolder(final); // only a folder without a manifest can be here (InstalledVersion said the version was not installed)
		Directory.Move(tmp, final);
		_opened.TryRemove(final, out _);
		foreach (string dir in Directory.EnumerateDirectories(BookFolder(bookId)))
		{
			Match match = VersionFolder.Match(Path.GetFileName(dir));
			if (match.Success && match.Groups[1].Value == language && !string.Equals(dir, final, StringComparison.Ordinal))
			{
				DeleteFolder(dir);
				_opened.TryRemove(dir, out _);
			}
		}
	}

	private static void Unpack(string zipPath, string folder, string language)
	{
		DeleteFolder(folder);
		Directory.CreateDirectory(folder);
		using (ZipArchive zip = ZipFile.OpenRead(zipPath))
		{
			if (zip.Entries.Count > MaxEntries || zip.Entries.Sum(e => e.Length) > MaxUnpackedBytes)
			{
				throw new InvalidDataException("The pack is larger than allowed.");
			}

			foreach (ZipArchiveEntry entry in zip.Entries.Where(e => e.Name.Length > 0))
			{
				if (!PremadeAudio.IsPlainFileName(entry.FullName))
				{
					throw new InvalidDataException($"The pack holds a file outside its folder: {entry.FullName}");
				}

				entry.ExtractToFile(Path.Combine(folder, entry.Name), overwrite: true);
			}
		}

		VerifyComplete(folder, language);
	}

	// Every clip the manifest names must be there and not empty, in the manifest's own language: a pack that lost a clip is not kept.
	private static void VerifyComplete(string folder, string language)
	{
		string manifestPath = Path.Combine(folder, PremadeAudio.ManifestFile);
		if (!File.Exists(manifestPath))
		{
			throw new InvalidDataException("The pack has no manifest.");
		}

		PremadeAudio audio = PremadeAudio.Parse(File.ReadAllText(manifestPath));
		if (!string.Equals(audio.Language, language, StringComparison.Ordinal))
		{
			throw new InvalidDataException($"The pack is for {audio.Language}, not {language}.");
		}

		string? missing = audio.ClipNames.FirstOrDefault(c => !File.Exists(Path.Combine(folder, c)) || new FileInfo(Path.Combine(folder, c)).Length == 0);
		if (missing is not null)
		{
			throw new AudioPackClipMissingException($"The pack lacks the clip {missing}.");
		}
	}

	private void EnsureCleaned()
	{
		if (!_cleaned)
		{
			CleanupInterrupted();
		}
	}

	private string BookFolder(string bookId) => Path.Combine(_root, bookId.ToLowerInvariant());

	private string FolderOf(string bookId, string language, int version) => Path.Combine(BookFolder(bookId), $"{language}-v{version}");

	private static bool IsSafe(string bookId, string language) => SafeName.IsMatch(bookId) && SafeName.IsMatch(language);

	private static int DeleteMatching(string folder, bool file, string extension)
	{
		if (!Directory.Exists(folder))
		{
			return 0;
		}

		int removed = 0;
		foreach (string path in file ? Directory.EnumerateFiles(folder, "*" + extension) : Directory.EnumerateDirectories(folder, "*" + extension))
		{
			if (file)
			{
				DeleteFile(path);
			}
			else
			{
				DeleteFolder(path);
			}

			removed++;
		}

		return removed;
	}

	private static void DeleteFile(string path)
	{
		try
		{
			File.Delete(path);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not remove a pack temp file: {ex.Message}");
		}
	}

	private static void DeleteFolder(string path)
	{
		try
		{
			if (Directory.Exists(path))
			{
				Directory.Delete(path, recursive: true);
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not remove a pack folder: {ex.Message}");
		}
	}

	[GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
	private static partial Regex SafeNamePattern();

	[GeneratedRegex(@"^([a-z0-9-]+)-v(\d+)$")]
	private static partial Regex VersionFolderPattern();
}
