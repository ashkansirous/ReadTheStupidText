using System.IO.Compression;

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
            story = WithoutMissingPictures(story, entries);
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
        return story with
        {
            Pages = story.Pages.Select(p => p with { ImageFile = Keep(p.ImageFile) }).ToList(),
            CoverFile = Keep(story.CoverFile),
        };
    }

    // Only the pictures the story names are imported. The caps count the bytes actually read, whatever an entry's header declared.
    private static List<PackageFile> PicturesOf(Story story, Dictionary<string, ZipArchiveEntry> entries, ByteBudget budget)
    {
        string[] names = story.PictureFiles().ToArray();
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
