using System.IO.Compression;
using System.Security.Cryptography;

namespace DinoReader.Core;

/// <summary>
/// Imports a <c>.dino</c> package into a library (Slice 91, Decision 94). The story gets a new id (the library refuses an existing one)
/// and records the package's own id as <see cref="Story.SourceId"/>; a package from a newer schema, an entry whose name could leave
/// the story folder (zip-slip) and an oversized entry are refused before anything is stored.
/// </summary>
public static class StoryPackageReader
{
    public const int MaxEntries = 2000;
    public const long MaxManifestBytes = 64 * 1024;
    public const long MaxStoryBytes = 8 * 1024 * 1024;
    public const long MaxPictureBytes = 25 * 1024 * 1024;
    public const long MaxTotalBytes = 200L * 1024 * 1024;

    private const string MacMetadataFolder = "__MACOSX/";
    private const string StagedSuffix = ".tmp";
    private const string BackupSuffix = ".bak";

    private static readonly char[] SeparatorChars = ['/', '\\', ':'];
    private static readonly HashSet<string> DeviceNames = new(["CON", "PRN", "AUX", "NUL"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Imports the package and returns the stored story. <paramref name="keepId"/> keeps the packaged story's own id (backup restore,
    /// Slice 93) instead of a new one; <paramref name="created"/> replaces its creation time (the gallery lists newest first).</summary>
    public static async Task<Story> ImportAsync(Stream package, IStoryLibrary library, bool keepId = false, DateTimeOffset? created = null)
    {
        try
        {
            using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
            Dictionary<string, ZipArchiveEntry> entries = IndexEntries(zip);
            StoryPackageManifest manifest = await ReadManifestAsync(entries);
            Story packaged = await ReadStoryAsync(entries);
            string id = keepId ? packaged.Id : StoryIds.New();
            Story story = packaged with { Id = id, SourceId = SourceOf(manifest, packaged, keepId), Created = created ?? DateTimeOffset.UtcNow };
            story = WithoutMissingPictures(keepId ? story : WithoutRecordings(story), entries);
            await library.ImportAsync(story, PicturesOf(story, entries, new ByteBudget(MaxTotalBytes)));
            return story;
        }
        catch (InvalidDataException ex)
        {
            throw new StoryPackageException("The package is not a valid zip file.", ex);
        }
        catch (StoryFormatException ex)
        {
            throw new StoryPackageException(ex.Message, ex);
        }
    }

    /// <summary>Merges the recordings a backup's package carries into the story already on the shelf (Slice 230): every recorded page the phone lacks (the backup's wins on a page both have)
    /// keeps its audio file, written first, each through a temp name and a move; only then is the story saved once with the merged map, so a kill in between leaves at most extra audio files and
    /// never an entry that names a missing file. Returns the saved story, or null when the package brought nothing new.</summary>
    internal static async Task<Story?> MergeRecordingsAsync(Stream package, IStoryLibrary library, Story phone, ByteBudget budget)
    {
        try
        {
            using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
            Dictionary<string, ZipArchiveEntry> entries = IndexEntries(zip);
            Story backed = await ReadStoryAsync(entries);
            List<int> pages = StoryRecordings.MergeablePages(phone, backed, name => entries.TryGetValue(name, out ZipArchiveEntry? e) && e.Length > 0)
                .Where(page => IsNewHere(library, phone, backed, page, entries))
                .ToList();
            if (pages.Count == 0)
            {
                return null;
            }

            SweepLeftovers(library, phone.Id);
            Story merged = StoryRecordings.WithMerged(phone, backed, pages);
            await StageAndPlaceAsync(library, phone, pages, entries, budget, () => library.UpdateAsync(merged));
            return merged;
        }
        catch (InvalidDataException ex)
        {
            throw new StoryPackageException("The package is not a valid zip file.", ex);
        }
        catch (StoryFormatException ex)
        {
            throw new StoryPackageException(ex.Message, ex);
        }
    }

    // Every audio file is written to a temp name first. Each phone file it replaces is first moved to "<name>.bak", then the new one moves in, and only then is the story saved (commit).
    // On any failure every .bak goes back, files that had no earlier file are deleted and the temps are removed, so the phone's audio and story stay exactly as they were.
    private static async Task StageAndPlaceAsync(IStoryLibrary library, Story phone, List<int> pages, Dictionary<string, ZipArchiveEntry> entries, ByteBudget budget, Func<Task> commit)
    {
        var staged = new List<(string Temp, string Target)>();
        var placed = new List<(string Target, string? Backup)>();
        try
        {
            foreach (int page in pages)
            {
                await StageOneAsync(library, phone.Id, StoryRecordings.FileNameOf(page), entries, budget, staged);
            }

            foreach ((string temp, string target) in staged)
            {
                placed.Add((target, MoveAside(target)));
                File.Move(temp, target, overwrite: true);
            }

            await commit();
            placed.ForEach(p => TryDelete(p.Backup));
        }
        catch
        {
            RollBack(staged, placed);
            throw;
        }
    }

    // The temp is listed before it is written, so a failed copy is cleaned up too.
    private static async Task StageOneAsync(IStoryLibrary library, string id, string name, Dictionary<string, ZipArchiveEntry> entries, ByteBudget budget, List<(string Temp, string Target)> staged)
    {
        string target = library.TryResolveFile(id, name) ?? throw new StoryPackageException("The story has no place for a recording.");
        string temp = library.TryResolveFile(id, $"{name}.{Guid.NewGuid():N}{StagedSuffix}") ?? throw new StoryPackageException("The story has no place for a recording.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        staged.Add((temp, target));
        await CopyEntryAsync(entries[name], temp, budget);
    }

    // The phone's own file for the page, if any, becomes "<name>.bak"; returns the backup path (null when there was no file).
    private static string? MoveAside(string target)
    {
        if (!File.Exists(target))
        {
            return null;
        }

        string backup = target + BackupSuffix;
        File.Move(target, backup, overwrite: true);
        return backup;
    }

    private static void RollBack(List<(string Temp, string Target)> staged, List<(string Target, string? Backup)> placed)
    {
        foreach ((string target, string? backup) in Enumerable.Reverse(placed))
        {
            RestoreOne(target, backup);
        }

        staged.ForEach(s => TryDelete(s.Temp));
    }

    private static void RestoreOne(string target, string? backup, bool deleteTarget = true)
    {
        try
        {
            if (backup is null)
            {
                if (deleteTarget)
                {
                    File.Delete(target);
                }
            }
            else if (File.Exists(backup))
            {
                File.Move(backup, target, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left for the next merge into this story, which sweeps leftovers first.
        }
    }

    // Leftovers of a merge that was killed: a .tmp is never read, so it is deleted; a .bak is the phone's own audio, put back when its file is missing (the new one never got in or was
    // rolled back), otherwise the file in place is kept and the .bak deleted. Only the page-N recording names are touched, never the library's own files.
    private static void SweepLeftovers(IStoryLibrary library, string id)
    {
        string folder = library.FolderOf(id);
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (string temp in Directory.GetFiles(folder, $"*{StagedSuffix}").Where(IsStagedRecording))
        {
            TryDelete(temp);
        }

        foreach (string backup in Directory.GetFiles(folder, $"*{BackupSuffix}").Where(b => StoryRecordings.IsRecordingFileName(Path.GetFileNameWithoutExtension(b))))
        {
            string target = backup[..^BackupSuffix.Length];
            RestoreOne(target, File.Exists(target) ? null : backup, deleteTarget: false);
            TryDelete(backup);
        }
    }

    // "page-1.m4a.<guid>.tmp" -> "page-1.m4a"
    private static bool IsStagedRecording(string path) =>
        StoryRecordings.IsRecordingFileName(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path)));

    private static void TryDelete(string? path)
    {
        try
        {
            if (path is not null)
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A temp file that cannot be deleted now is harmless; it is never read.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    // A backed-up recording is new to the phone when the phone has no entry for the page, a different one, or no audio file of the same size.
    private static bool IsNewHere(IStoryLibrary library, Story phone, Story backed, int page, Dictionary<string, ZipArchiveEntry> entries)
    {
        if (StoryRecordings.Of(phone, page) != backed.Recordings![page])
        {
            return true;
        }

        string name = StoryRecordings.FileNameOf(page);
        string? path = library.TryResolveFile(phone.Id, name);
        return path is null || !File.Exists(path) || new FileInfo(path).Length != entries[name].Length;
    }

    private static async Task CopyEntryAsync(ZipArchiveEntry entry, string path, ByteBudget budget)
    {
        await using Stream source = new CappedStream(entry.Open(), MaxPictureBytes, budget);
        await using FileStream target = File.Create(path);
        await source.CopyToAsync(target);
    }

    /// <summary>The story a package carries, without storing anything (to compare a bundled starter with the copy in the library).</summary>
    public static async Task<Story> ReadStoryOnlyAsync(Stream package)
    {
        try
        {
            using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
            return await ReadStoryAsync(IndexEntries(zip));
        }
        catch (InvalidDataException ex)
        {
            throw new StoryPackageException("The package is not a valid zip file.", ex);
        }
        catch (StoryFormatException ex)
        {
            throw new StoryPackageException(ex.Message, ex);
        }
    }

    /// <summary>The <see cref="StarterUpdate.ContentHash"/> of the book a package carries (its page text and each page picture's bytes), without storing anything (Slice 181).</summary>
    public static async Task<string> ContentHashOfPackageAsync(Stream package)
    {
        try
        {
            using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
            Dictionary<string, ZipArchiveEntry> entries = IndexEntries(zip);
            Story story = WithoutMissingPictures(await ReadStoryAsync(entries), entries);
            var pictures = new List<string?>();
            foreach (StoryPage page in story.Pages)
            {
                pictures.Add(page.ImageFile is { } name ? await HashEntryAsync(entries[name]) : null);
            }

            return StarterUpdate.ContentHash(story, pictures);
        }
        catch (InvalidDataException ex)
        {
            throw new StoryPackageException("The package is not a valid zip file.", ex);
        }
        catch (StoryFormatException ex)
        {
            throw new StoryPackageException(ex.Message, ex);
        }
    }

    private static async Task<string> HashEntryAsync(ZipArchiveEntry entry)
    {
        await using Stream stream = new CappedStream(entry.Open(), MaxPictureBytes);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }

    // The writer keeps the package id only in the manifest, so a kept-id restore gets its source back from there.
    private static string? SourceOf(StoryPackageManifest manifest, Story packaged, bool keepId) =>
        !keepId || manifest.Id != packaged.Id ? manifest.Id : packaged.SourceId;

    // Every entry name is checked, whether or not the story uses it: one name that could leave the folder spoils the package.
    private static Dictionary<string, ZipArchiveEntry> IndexEntries(ZipArchive zip)
    {
        if (zip.Entries.Count > MaxEntries)
        {
            throw new StoryPackageException("The package has too many files.");
        }

        long total = 0;
        var index = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (ZipArchiveEntry entry in zip.Entries.Where(e => !IsIgnorable(e)))
        {
            if (!IsPlainName(entry.FullName))
            {
                throw new StoryPackageException("The package has a file name that is not allowed.");
            }

            total += entry.Length;
            if (total > MaxTotalBytes || entry.Length > MaxLimitOf(entry.FullName))
            {
                throw new StoryPackageException("The package is too large.");
            }

            if (!index.TryAdd(entry.FullName, entry))
            {
                throw new StoryPackageException("The package has two files with the same name.");
            }
        }

        return index;
    }

    // Repacking tools add folder entries and macOS adds __MACOSX/: neither holds story data, so neither spoils the package.
    private static bool IsIgnorable(ZipArchiveEntry entry) =>
        (entry.FullName.EndsWith('/') && entry.Length == 0) || entry.FullName.StartsWith(MacMetadataFolder, StringComparison.Ordinal);

    private static bool IsPlainName(string name) =>
        name.Length > 0 && name is not ("." or "..") && name == Path.GetFileName(name) && name.IndexOfAny(SeparatorChars) < 0
        && !IsDeviceName(name);

    // CON, NUL, COM1 and friends are device names on Windows whatever their extension.
    private static bool IsDeviceName(string name)
    {
        string stem = name.Split('.')[0].TrimEnd(' ');
        return DeviceNames.Contains(stem) || (stem.Length == 4 && stem[..3].ToUpperInvariant() is "COM" or "LPT" && char.IsDigit(stem[3]));
    }

    private static bool IsReserved(string name) =>
        name.Equals(StoryPackageManifest.ManifestFileName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(StoryPackageManifest.StoryFileName, StringComparison.OrdinalIgnoreCase);

    private static long MaxLimitOf(string name) => name switch
    {
        StoryPackageManifest.ManifestFileName => MaxManifestBytes,
        StoryPackageManifest.StoryFileName => MaxStoryBytes,
        _ => MaxPictureBytes,
    };

    private static async Task<StoryPackageManifest> ReadManifestAsync(Dictionary<string, ZipArchiveEntry> entries)
    {
        string json = await ReadTextAsync(entries, StoryPackageManifest.ManifestFileName);
        return StoryPackageManifest.PeekSchemaVersion(json) > StoryPackageManifest.CurrentSchemaVersion
            ? throw new StoryPackageException("The package is from a newer version of Dino Book Reader.")
            : StoryPackageManifest.Parse(json);
    }

    private static async Task<Story> ReadStoryAsync(Dictionary<string, ZipArchiveEntry> entries) =>
        StoryJson.Deserialize(await ReadTextAsync(entries, StoryPackageManifest.StoryFileName));

    private static async Task<string> ReadTextAsync(Dictionary<string, ZipArchiveEntry> entries, string name)
    {
        if (!entries.TryGetValue(name, out ZipArchiveEntry? entry))
        {
            throw new StoryPackageException($"The package has no {name}.");
        }

        await using Stream stream = new CappedStream(entry.Open(), MaxLimitOf(name));
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    // story.json is untrusted and the writer skips pictures that were missing at export: a picture the package does not carry, or whose
    // name is not a plain file name, is dropped from the story so no stored reference points at nothing (or outside the folder).
    private static Story WithoutMissingPictures(Story story, Dictionary<string, ZipArchiveEntry> entries)
    {
        string? Keep(string? name) => name is not null && IsPlainName(name) && entries.ContainsKey(name) ? name : null;
        return WithoutMissingRecordings(story with
        {
            Pages = story.Pages.Select(p => p with { ImageFile = Keep(p.ImageFile) }).ToList(),
            CoverFile = Keep(story.CoverFile),
        }, entries);
    }

    // Only a backup restore brings a parent's recordings back (Slice 111): a shared or store package never carries any, so one that names recordings is not trusted with them.
    private static Story WithoutRecordings(Story story) => story with { Recordings = null, UseRecordings = false };

    // A recording the package does not carry (the writer skips a missing file) is dropped, so "Read with my voice" never names a page with nothing to play (Slice 111).
    private static Story WithoutMissingRecordings(Story story, Dictionary<string, ZipArchiveEntry> entries)
    {
        Story kept = story;
        foreach (int page in StoryRecordings.RecordedPages(story).Where(p => !entries.ContainsKey(StoryRecordings.FileNameOf(p))))
        {
            kept = StoryRecordings.Without(kept, page);
        }

        return kept;
    }

    // Only the pictures and recordings the story names are imported. The caps count the bytes actually read, whatever an entry's header declared.
    private static List<PackageFile> PicturesOf(Story story, Dictionary<string, ZipArchiveEntry> entries, ByteBudget budget)
    {
        string[] names = story.PictureFiles().Concat(StoryRecordings.Files(story)).ToArray();
        if (names.Any(IsReserved))
        {
            throw new StoryPackageException("The story names a file it must not.");
        }

        return names.Where(entries.ContainsKey)
            .Select(n => new PackageFile(n, () => new CappedStream(entries[n].Open(), MaxPictureBytes, budget)))
            .ToList();
    }
}

/// <summary>The bytes still allowed for one import, shared by every picture stream.</summary>
internal sealed class ByteBudget(long total)
{
    private long _left = total;

    /// <summary>True once more than the total has been spent.</summary>
    public bool Exhausted { get; private set; }

    public void Spend(int n)
    {
        _left -= n;
        if (_left < 0)
        {
            Exhausted = true;
            throw new StoryPackageException("The package is too large.");
        }
    }
}

/// <summary>A read-only stream that throws once more than <c>limit</c> bytes have been read.</summary>
internal sealed class CappedStream(Stream inner, long limit, ByteBudget? budget = null) : Stream
{
    private long _read;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _read;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Count(await inner.ReadAsync(buffer, cancellationToken));

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private int Count(int n)
    {
        _read += n;
        budget?.Spend(n);
        return _read <= limit ? n : throw new StoryPackageException("The package is too large.");
    }
}
