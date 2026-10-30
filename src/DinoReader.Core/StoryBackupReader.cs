using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace DinoReader.Core;

/// <summary>
/// Restores a backup zip into a library (Slice 93, Decision 96). A story whose id, or whose source (a starter or store story the phone installed itself), is already on the shelf is skipped and left as it is;
/// the others are imported with their backed-up id (so restoring twice adds nothing). A damaged package costs only its own story.
/// A newer backup schema, a missing manifest or too many entries refuse the whole file before anything is added.
/// </summary>
public static class StoryBackupReader
{
    public const int MaxStories = 1000;
    public const long MaxManifestBytes = 64 * 1024;

    /// <summary>The most a backup may be, and the most it may unpack to temp files in one restore.</summary>
    public const long MaxBackupBytes = 2L * 1024 * 1024 * 1024;

    // A package may hold up to the importer's own total, plus its manifest and story file.
    private const long MaxPackageBytes = StoryPackageReader.MaxTotalBytes + StoryPackageReader.MaxStoryBytes + StoryPackageReader.MaxManifestBytes;
    private const int CopyBufferBytes = 81920;
    private const string MacMetadataFolder = "__MACOSX/";
    private const string TempPrefix = "dino-restore-";

    /// <summary>Restores <paramref name="backup"/>; <paramref name="onAdded"/> is called once for each story the restore put on the shelf (Slice 191), so the app can count them.</summary>
    public static Task<StoryRestoreResult> RestoreAsync(Stream backup, IStoryLibrary library, Action<Story>? onAdded = null) => RestoreAsync(backup, library, MaxBackupBytes, onAdded);

    internal static async Task<StoryRestoreResult> RestoreAsync(Stream backup, IStoryLibrary library, long maxBytes, Action<Story>? onAdded = null)
    {
        try
        {
            using var zip = new ZipArchive(backup, ZipArchiveMode.Read, leaveOpen: true);
            await ReadManifestAsync(zip);
            int[] counts = new int[Enum.GetValues<Outcome>().Length];
            var budget = new ByteBudget(maxBytes);
            HashSet<string> known = await KnownSourcesAsync(library);
            List<ZipArchiveEntry> packages = PackageEntries(zip);
            int tooLarge = 0;
            for (int i = 0; i < packages.Count && tooLarge == 0; i++)
            {
                Outcome outcome = await RestoreOneAsync(packages[i], library, known, budget, onAdded);
                counts[(int)outcome]++;
                tooLarge = outcome == Outcome.TooLarge ? packages.Count - i : 0;
            }

            return new StoryRestoreResult(counts[(int)Outcome.Added], counts[(int)Outcome.Skipped], counts[(int)Outcome.Damaged], tooLarge);
        }
        catch (Exception ex) when (ex is InvalidDataException or StoryPackageException)
        {
            throw new StoryBackupException("The file is not a valid backup.", ex);
        }
    }

    private enum Outcome
    {
        Added,
        Skipped,
        Damaged,
        TooLarge,
    }

    private static async Task ReadManifestAsync(ZipArchive zip)
    {
        ZipArchiveEntry entry = zip.GetEntry(StoryBackupManifest.ManifestFileName)
            ?? throw new StoryBackupException("The file is not a Dino Book Reader backup.");
        if (entry.Length > MaxManifestBytes)
        {
            throw new StoryBackupException("The backup manifest is too large.");
        }

        await using Stream stream = new CappedStream(entry.Open(), MaxManifestBytes);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        StoryBackupManifest.Parse(await reader.ReadToEndAsync());
    }

    // Only top-level packages count; anything else in the zip (folders, macOS metadata, stray files) is ignored.
    private static List<ZipArchiveEntry> PackageEntries(ZipArchive zip)
    {
        List<ZipArchiveEntry> packages = zip.Entries.Where(IsPackage).ToList();
        return packages.Count > MaxStories ? throw new StoryBackupException("The backup has too many stories.") : packages;
    }

    private static bool IsPackage(ZipArchiveEntry entry) =>
        !entry.FullName.StartsWith(MacMetadataFolder, StringComparison.Ordinal)
        && entry.FullName == entry.Name
        && entry.Name.EndsWith(StoryPackageManifest.Extension, StringComparison.OrdinalIgnoreCase);

    // The ids and source ids already on the shelf: a starter or store story installed on this phone has its own id but the same source.
    private static async Task<HashSet<string>> KnownSourcesAsync(IStoryLibrary library)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (Story story in await library.ListAsync())
        {
            known.Add(story.Id);
            if (story.SourceId is { } source)
            {
                known.Add(source);
            }
        }

        return known;
    }

    // The shared byte budget running out ends the whole restore (TooLarge); any other failure costs only its own story.
    private static async Task<Outcome> RestoreOneAsync(ZipArchiveEntry entry, IStoryLibrary library, HashSet<string> known, ByteBudget budget, Action<Story>? onAdded)
    {
        try
        {
            return entry.Length > MaxPackageBytes ? Outcome.Damaged : await ImportEntryAsync(entry, library, known, budget, onAdded);
        }
        catch (Exception ex) when (ex is StoryPackageException or StoryFormatException or InvalidDataException)
        {
            return budget.Exhausted ? Outcome.TooLarge : Outcome.Damaged;
        }
    }

    private static async Task<Outcome> ImportEntryAsync(ZipArchiveEntry entry, IStoryLibrary library, HashSet<string> known, ByteBudget budget, Action<Story>? onAdded)
    {
        await using FileStream package = TempFile();
        await CopyCappedAsync(entry, package, budget);
        Story packaged = await PeekStoryAsync(package);
        string? source = await PeekSourceAsync(package, packaged);
        if (known.Contains(packaged.Id) || (source is not null && known.Contains(source)))
        {
            return Outcome.Skipped;
        }

        package.Position = 0;
        Story? added = await ImportKeepingIdAsync(package, library, packaged);
        if (added is null)
        {
            return Outcome.Skipped;
        }

        known.Add(added.Id);
        if (added.SourceId is { } addedSource)
        {
            known.Add(addedSource);
        }

        NotifyAdded(onAdded, added);
        return Outcome.Added;
    }

    // A callback that throws must not abort the restore partway: the story is already on the shelf (Slice 191).
    private static void NotifyAdded(Action<Story>? onAdded, Story added)
    {
        try
        {
            onAdded?.Invoke(added);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Dino Reader restore: the added-story callback failed: {ex.Message}");
        }
    }

    // The library refuses an id whose file already exists: that is "already on the shelf" only if the story really reads back (null here);
    // an unreadable existing file is a story the restore could not place, so it is counted as damaged, not as present.
    private static async Task<Story?> ImportKeepingIdAsync(Stream package, IStoryLibrary library, Story packaged)
    {
        try
        {
            return await StoryPackageReader.ImportAsync(package, library, keepId: true, created: packaged.Created);
        }
        catch (InvalidOperationException)
        {
            return await library.GetAsync(packaged.Id) is null
                ? throw new StoryPackageException("The story's file on the shelf cannot be read.")
                : null;
        }
    }

    // A nested zip needs a seekable stream; a temp file keeps a large package out of memory and vanishes when closed.
    private static FileStream TempFile() =>
        new(Path.Combine(Path.GetTempPath(), TempPrefix + Guid.NewGuid().ToString("N")), FileMode.Create, FileAccess.ReadWrite,
            FileShare.None, CopyBufferBytes, FileOptions.DeleteOnClose | FileOptions.Asynchronous);

    private static async Task CopyCappedAsync(ZipArchiveEntry entry, Stream target, ByteBudget budget)
    {
        await using Stream source = new CappedStream(entry.Open(), MaxPackageBytes, budget);
        await source.CopyToAsync(target, CopyBufferBytes);
        await target.FlushAsync();
    }

    private static async Task<Story> PeekStoryAsync(Stream package)
    {
        package.Position = 0;
        using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        ZipArchiveEntry entry = zip.GetEntry(StoryPackageManifest.StoryFileName)
            ?? throw new StoryPackageException("The package has no story.json.");
        if (entry.Length > StoryPackageReader.MaxStoryBytes)
        {
            throw new StoryPackageException("The package is too large.");
        }

        await using Stream stream = new CappedStream(entry.Open(), StoryPackageReader.MaxStoryBytes);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return StoryJson.Deserialize(await reader.ReadToEndAsync());
    }

    // The writer drops SourceId from story.json and keeps it as the package id in the manifest, so the source is read from there.
    private static async Task<string?> PeekSourceAsync(Stream package, Story packaged)
    {
        package.Position = 0;
        using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        ZipArchiveEntry entry = zip.GetEntry(StoryPackageManifest.ManifestFileName)
            ?? throw new StoryPackageException("The package has no manifest.");
        if (entry.Length > StoryPackageReader.MaxManifestBytes)
        {
            throw new StoryPackageException("The package is too large.");
        }

        await using Stream stream = new CappedStream(entry.Open(), StoryPackageReader.MaxManifestBytes);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        StoryPackageManifest manifest = StoryPackageManifest.Parse(await reader.ReadToEndAsync());
        return manifest.Id != packaged.Id ? manifest.Id : packaged.SourceId;
    }
}
