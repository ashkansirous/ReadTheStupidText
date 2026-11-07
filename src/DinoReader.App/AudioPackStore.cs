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

/// <summary>
/// The grown-up's Cancel on a side language's audio (Slice 262) is kept as a list of language codes in one preference. The weekly book check must not fetch packs for those languages (Slice 269).
/// The preference key and its locked read belong to <c>ShelfAudioPacks.WithoutCancelled</c>; this is the pure filter it uses.
/// </summary>
public static class CancelledAudioLanguages
{
	/// <summary>The languages with the cancelled ones left out; <paramref name="savedMarks"/> is the preference's text.</summary>
	public static IReadOnlyList<string> Without(IEnumerable<string> languages, string? savedMarks)
	{
		HashSet<string> cancelled = [.. StoryLanguageSet.Split(savedMarks).Select(UiLanguage.Resolve)];
		return languages.Where(language => !cancelled.Contains(UiLanguage.Resolve(language))).ToList();
	}
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

/// <summary>The outcome of looking a piece's clip up (Slice 273): its file when found, else why not.</summary>
public readonly record struct ClipLookup(string? Path, ClipMiss Miss);

#if DEBUG
/// <summary>Slice 310 (Debug only): how the next pack downloads are made to fail, so a flow can check the failure line and the cleanup without corrupting a file by hand.</summary>
public enum PackFailureInjection
{
	None,

	/// <summary>The connection drops part-way: some bytes are written, then the fetch throws (counted as a network failure).</summary>
	Network,

	/// <summary>The download finishes but its last bytes are damaged, so the zip cannot be read (counted as an unzip failure).</summary>
	Corrupt,

	/// <summary>The phone runs out of room part-way: some bytes are written, then the fetch throws an <see cref="IOException"/> (counted as a storage failure).</summary>
	DiskFull,
}

/// <summary>Slice 310 (Debug only): the hook's words for each <see cref="PackFailureInjection"/>.</summary>
public static class ForcedPackFailures
{
	public const string Off = "off";
	public const string NetworkWord = "network";
	public const string CorruptWord = "corrupt";
	public const string DiskFullWord = "disk-full";

	public static bool TryParse(string? word, out PackFailureInjection kind)
	{
		(bool known, kind) = word switch
		{
			Off => (true, PackFailureInjection.None),
			NetworkWord => (true, PackFailureInjection.Network),
			CorruptWord => (true, PackFailureInjection.Corrupt),
			DiskFullWord => (true, PackFailureInjection.DiskFull),
			_ => (false, PackFailureInjection.None),
		};
		return known;
	}
}

/// <summary>Slice 310 (Debug only): wraps the real fetcher and makes it fail in the named way. The caller's <see cref="PackFailureInjection.None"/> never gets here (the store only wraps for a kind that is set).</summary>
public sealed class FailingAudioPackFetcher(IAudioPackFetcher inner, PackFailureInjection kind) : IAudioPackFetcher
{
	public const int PartialBytes = 1024;
	private const int DamagedTailBytes = 64;

	public async Task FetchAsync(string url, string toPath, IProgress<long> bytesSoFar, CancellationToken cancel)
	{
		if (kind == PackFailureInjection.Corrupt)
		{
			await inner.FetchAsync(url, toPath, bytesSoFar, cancel);
			DamageTail(toPath);
			return;
		}

		if (kind == PackFailureInjection.None)
		{
			await inner.FetchAsync(url, toPath, bytesSoFar, cancel);
			return;
		}

		await File.WriteAllBytesAsync(toPath, new byte[PartialBytes], cancel);
		bytesSoFar.Report(PartialBytes);
		throw kind == PackFailureInjection.DiskFull
			? new IOException("Injected: no space left on the device.")
			: new HttpRequestException("Injected: the connection dropped.");
	}

	// The zip's end-of-directory record sits in the last bytes, so overwriting them keeps the length (the size check passes) and breaks the unzip.
	private static void DamageTail(string path)
	{
		using FileStream file = new(path, FileMode.Open, FileAccess.Write);
		int tail = (int)Math.Min(DamagedTailBytes, file.Length);
		file.Seek(-tail, SeekOrigin.End);
		file.Write(new byte[tail]);
	}
}
#endif

/// <summary>An installed pack: its folder, version and the manifest read from it.</summary>
public sealed record InstalledPack(string Folder, int Version, PremadeAudio Audio)
{
	/// <summary>The clip file for the <paramref name="index"/>-th spoken piece when the pack has one for that position, its hash confirms <paramref name="text"/> and the file is on disk, else null (the piece is read by the device voice).</summary>
	public string? PathOf(int index, string text) => Find(index, text).Path;

	/// <summary>Like <see cref="PathOf"/>, and when there is no clip says why: a hash that does not confirm the text (or no entry for the position) is <see cref="ClipMiss.TextMismatch"/>; an entry whose file is not on disk is <see cref="ClipMiss.FileMissing"/>.</summary>
	public ClipLookup Find(int index, string text)
	{
		if (Audio.ClipAt(index, text) is not { } name)
		{
			return new ClipLookup(null, ClipMiss.TextMismatch);
		}

		string path = Path.Combine(Folder, name);
		return File.Exists(path) ? new ClipLookup(path, ClipMiss.None) : new ClipLookup(null, ClipMiss.FileMissing);
	}
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

#if DEBUG
	/// <summary>Slice 310 (Debug only): while not <see cref="PackFailureInjection.None"/>, every download fails in that way (set by the <c>force-pack-failure</c> test hook).</summary>
	public static PackFailureInjection ForcedFailure
	{
		get => (PackFailureInjection)Volatile.Read(ref _forcedFailure);
		set => Volatile.Write(ref _forcedFailure, (int)value);
	}

	private static int _forcedFailure;
#endif

	/// <summary>Slice 267: raised for every pack download as (book id, language, <see cref="AudioPackUsage"/> detail): started, done or failed with its kind. Carries ids and closed words only.</summary>
	public event Action<string, string, string>? Outcome;

	/// <summary>Raised after the installed packs changed on disk (a pack installed, a book's or a language's packs removed, the sweep deleted a book). The shelf's installed-audio snapshot rescans on it.</summary>
	public event Action? PacksChanged;

	private void NotifyPacksChanged()
	{
		try
		{
			PacksChanged?.Invoke();
		}
		catch (Exception ex)
		{
			// no-fallback: a listener failed; the snapshot is also rescanned on resume and when a page appears
			_log($"pack change notice failed ({ex.GetType().Name})");
		}
	}

	/// <summary>Whether a download of this book's pack in this language failed in this launch and has not worked since (the reason a read then says <see cref="DeviceVoiceReason.PackFailed"/>).</summary>
	public bool FailedThisLaunch(string bookId, string language) => _failedThisLaunch.TryGetValue(Key(bookId, language), out bool failed) && failed;

	private static string Key(string bookId, string language) => $"{bookId.ToLowerInvariant()}/{language}";

	private void Report(string bookId, string language, string detail)
	{
		bool failed = detail.StartsWith(AudioPackUsage.PackFailedPrefix, StringComparison.Ordinal);
		_failedThisLaunch.AddOrUpdate(Key(bookId, language), failed, (_, was) => detail is AudioPackUsage.PackStarted or AudioPackUsage.PackCancelled ? was : failed);
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
			// no-fallback: the count itself could not be queued; counting is best effort and never reports itself
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
			// no-fallback: marked failed for this launch, so the read that finds no clips reports premade_audio pack_failed (not no_pack) at the place that decides (StoryVoicing)
			_failedThisLaunch[Key(bookId, language)] = true;
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
			// no-fallback: cancelled while waiting for another install; this download never started, so nothing is reported
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
			// no-fallback: cancelled before the download began (PackStarted is reported only inside InstallCoreAsync, which reports its own cancel), so no PackCancelled without a PackStarted
			return PackInstall.Cancelled;
		}
		catch (Exception ex)
		{
			_log($"pack {bookId}/{language} v{pack.Version}: failed, the book is kept without it ({ex.GetType().Name})");
			Report(bookId, language, AudioPackUsage.PackFailed(KindOf(ex))); // failed before InstallCoreAsync, which reports its own failures
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

		NotifyPacksChanged();
	}

	/// <summary>Deletes every installed version of one book language's pack (the Debug fixture hook uses it so a fixture install is never skipped as already installed).</summary>
	public async Task RemoveAsync(string bookId, string language, CancellationToken cancel = default)
	{
		if (!IsSafe(bookId, language) || !Directory.Exists(BookFolder(bookId)))
		{
			return;
		}

		await _gate.WaitAsync(cancel);
		try
		{
			foreach (string dir in Directory.EnumerateDirectories(BookFolder(bookId)))
			{
				Match match = VersionFolder.Match(Path.GetFileName(dir));
				if (match.Success && match.Groups[1].Value == language)
				{
					DeleteFolder(dir);
					_opened.TryRemove(dir, out _);
				}
			}

			_log($"pack {bookId}/{language}: removed");
		}
		finally
		{
			_gate.Release();
		}

		NotifyPacksChanged();
	}

	/// <summary>
	/// Deletes the packs of every book that is no longer on the shelf. <paramref name="presentBooks"/> is asked once the store is free (after any install in flight has finished), so a book
	/// that was just downloaded is in it. It must be a complete reading of the library's folders; null (anything unreadable) deletes nothing.
	/// </summary>
	public async Task SweepAsync(Func<Task<ISet<string>?>> presentBooks, CancellationToken cancel = default)
	{
		bool swept = false;
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
				swept = true;
			}
		}
		finally
		{
			_gate.Release();
		}

		if (swept)
		{
			NotifyPacksChanged();
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

#if DEBUG
		bool forced = false;
#endif
		try
		{
			Directory.CreateDirectory(_partFolder);
			Report(bookId, language, AudioPackUsage.PackStarted);
#if DEBUG
			PackFailureInjection forcedNow = ForcedFailure; // read once: the hook can switch it from another thread
			forced = forcedNow != PackFailureInjection.None;
			if (forced)
			{
				_log($"{label}: Debug failure forced ({forcedNow})");
				fetcher = new FailingAudioPackFetcher(fetcher, forcedNow);
			}
#endif
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
			NotifyPacksChanged();
			return PackInstall.Installed;
		}
		catch (OperationCanceledException) when (cancel.IsCancellationRequested)
		{
			_log($"{label}: cancelled, nothing kept");
			Report(bookId, language, AudioPackUsage.PackCancelled);
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
#if DEBUG
			if (forced) // only an attempt with a forced failure logs this, so a normal install writes no probe line
			{
				_log($"{label}: temp files left {(File.Exists(part) || Directory.Exists(tmp) ? "yes" : "none")}");
			}
#endif
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
			// no-fallback: housekeeping delete; nothing depends on it
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
			// no-fallback: housekeeping delete; nothing depends on it
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not remove a pack folder: {ex.Message}");
		}
	}

	[GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
	private static partial Regex SafeNamePattern();

	[GeneratedRegex(@"^([a-z0-9-]+)-v(\d+)$")]
	private static partial Regex VersionFolderPattern();
}
