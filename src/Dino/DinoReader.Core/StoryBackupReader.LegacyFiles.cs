using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace DinoReader.Core;

/// <summary>What a restore does with the <c>.dino</c> book files of a schema 1 backup (Slice 346). Schema 2 backups hold none.</summary>
public enum LegacyBookFiles
{
    /// <summary>Import them, as before Slice 346: nothing a backup holds is lost. Used while the paid-books switch is off.</summary>
    Restore,

    /// <summary>Leave the files unused and list the books the phone lacks as <see cref="StoryRestoreResult.Missing"/>, so the app asks the store first; the file is the fallback (<see cref="StoryBackupReader.ImportLegacyBookAsync"/>).</summary>
    StoreFirst,
}

// The schema 1 restore that brings the book files back (exactly the restore of Slice 93, 191, 230), kept apart from the store-first path.
public static partial class StoryBackupReader
{
    /// <summary>
    /// Imports the one book <paramref name="bookId"/> (its store id or its own id) from a schema 1 backup that is not on the shelf yet. The app calls it when the store cannot supply a book the backup holds and
    /// the book is not sold and unowned, so nothing on the shelf is ever taken away. Returns the story put on the shelf, or null when the backup has no such file, it was already there or it is damaged.
    /// </summary>
    public static async Task<Story?> ImportLegacyBookAsync(Stream backup, IStoryLibrary library, string bookId, Action<Story>? onAdded = null)
    {
        try
        {
            using var zip = new ZipArchive(backup, ZipArchiveMode.Read, leaveOpen: true);
            StoryBackupManifest manifest = await ReadManifestAsync(zip);
            return manifest.SchemaVersion >= 2 ? null : await ImportMatchingAsync(zip, library, bookId, new ByteBudget(MaxBackupBytes), onAdded);
        }
        catch (Exception ex) when (ex is InvalidDataException or StoryPackageException or StoryFormatException or IOException or UnauthorizedAccessException)
        {
            // no-fallback: the book stays in the restore result's list of books still to get back, which the page shows
            return null;
        }
    }

    private static async Task<Story?> ImportMatchingAsync(ZipArchive zip, IStoryLibrary library, string bookId, ByteBudget budget, Action<Story>? onAdded)
    {
        foreach (ZipArchiveEntry entry in zip.Entries.Where(IsPackage).Where(e => e.Length <= MaxPackageBytes))
        {
            await using FileStream package = TempFile();
            await CopyCappedAsync(entry, package, budget);
            Story packaged = await PeekStoryAsync(package);
            string? source = await PeekSourceAsync(package, packaged);
            if (packaged.Id != bookId && source != bookId)
            {
                continue;
            }

            Story? added = null;
            if (await OnShelfAsync(library, packaged, source) is null)
            {
                package.Position = 0;
                added = await ImportKeepingIdAsync(package, library, packaged);
                NotifyAdded(onAdded, added);
            }

            return added;
        }

        return null;
    }

    private static async Task<StoryRestoreResult> RestoreLegacyFilesAsync(ZipArchive zip, IStoryLibrary library, ByteBudget budget, Action<Story>? onAdded)
    {
        List<ZipArchiveEntry> packages = zip.Entries.Where(IsPackage).ToList();
        if (packages.Count > MaxStories)
        {
            throw new StoryBackupException("The backup has too many stories.");
        }

        var counts = new int[Enum.GetValues<Outcome>().Length];
        HashSet<string> known = await KnownSourcesAsync(library);
        int tooLarge = 0;
        for (int i = 0; i < packages.Count && tooLarge == 0; i++)
        {
            Outcome outcome = await RestoreOneFileAsync(packages[i], library, known, budget, onAdded);
            counts[(int)outcome]++;
            tooLarge = outcome == Outcome.TooLarge ? packages.Count - i : 0;
        }

        return ResultOf(counts, tooLarge, []);
    }

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
    private static async Task<Outcome> RestoreOneFileAsync(ZipArchiveEntry entry, IStoryLibrary library, HashSet<string> known, ByteBudget budget, Action<Story>? onAdded)
    {
        try
        {
            return entry.Length > MaxPackageBytes ? Outcome.Damaged : await ImportEntryAsync(entry, library, known, budget, onAdded);
        }
        catch (Exception ex) when (ex is StoryPackageException or StoryFormatException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            // no-fallback: counted in the restore result (Damaged or TooLarge), which the page shows
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
            return await MergeIntoShelfAsync(package, library, packaged, source, budget);
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

    // The story is already on the shelf (by id, or as the same starter or store book): only its missing recordings are brought back (Slice 230).
    private static async Task<Outcome> MergeIntoShelfAsync(Stream package, IStoryLibrary library, Story packaged, string? source, ByteBudget budget)
    {
        Story? phone = await OnShelfAsync(library, packaged, source);
        if (phone is null)
        {
            return Outcome.Skipped;
        }

        package.Position = 0;
        Debug.WriteLine($"Dino Reader restore: merging recordings into {phone.Id}");
        return await StoryPackageReader.MergeRecordingsAsync(package, library, phone, budget) is null ? Outcome.Skipped : Outcome.Merged;
    }

    // The shelf's copy of the backed-up story: the same id, else (in order) the story whose id is the source, the one installed from the same source, the one installed from the packaged id.
    private static async Task<Story?> OnShelfAsync(IStoryLibrary library, Story packaged, string? source)
    {
        if (await library.GetAsync(packaged.Id) is { } same)
        {
            return same;
        }

        IReadOnlyList<Story> shelf = await library.ListAsync();
        return shelf.FirstOrDefault(s => s.Id == source)
            ?? (source is null ? null : shelf.FirstOrDefault(s => s.SourceId == source))
            ?? shelf.FirstOrDefault(s => s.SourceId == packaged.Id);
    }

    // A callback that throws must not abort the restore partway: the story is already on the shelf (Slice 191).
    private static void NotifyAdded(Action<Story>? onAdded, Story? added)
    {
        if (added is null)
        {
            return;
        }

        try
        {
            onAdded?.Invoke(added);
        }
        catch (Exception ex)
        {
            // no-fallback: the story is already restored; only the page's notification failed
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
            // no-fallback: the book is already on the shelf (counted as skipped) or the error becomes a StoryPackageException counted as damaged
            return await library.GetAsync(packaged.Id) is null
                ? throw new StoryPackageException("The story's file on the shelf cannot be read.")
                : null;
        }
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
