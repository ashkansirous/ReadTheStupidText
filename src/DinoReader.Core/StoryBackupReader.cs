using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace DinoReader.Core;

/// <summary>
/// Restores a backup zip into a library (Slice 93, Decision 96; Slice 346 Decision 5). A restore never brings book files: a book already on the shelf (by id, or as the same store book) gets back the
/// recordings it lacks (Slice 230) and its reading view; a book the phone does not have is listed in <see cref="StoryRestoreResult.Missing"/> so the app can download it from the store (free at once, paid
/// once the store confirms it) and then call <see cref="ApplyToBookAsync"/>. Schema 1 backups (a <c>.dino</c> per story) still work: with <see cref="LegacyBookFiles.Restore"/> (the paid-books switch off) their
/// book files are imported as before; with <see cref="LegacyBookFiles.StoreFirst"/> the store is asked first and the file is used by <see cref="ImportLegacyBookAsync"/> when it cannot supply the book.
/// A damaged entry costs only its own book. A newer backup schema, a missing manifest or too many entries refuse the whole file before anything changes.
/// </summary>
public static partial class StoryBackupReader
{
    public const int MaxStories = 1000;
    public const long MaxManifestBytes = 64 * 1024;

    /// <summary>The most a backup may be, and the most it may unpack to temp files in one restore.</summary>
    public const long MaxBackupBytes = 2L * 1024 * 1024 * 1024;

    // A legacy package may hold up to the importer's own total, plus its manifest and story file.
    private const long MaxPackageBytes = StoryPackageReader.MaxTotalBytes + StoryPackageReader.MaxStoryBytes + StoryPackageReader.MaxManifestBytes;
    private const int CopyBufferBytes = 81920;
    private const string MacMetadataFolder = "__MACOSX/";
    private const string TempPrefix = "dino-restore-";

    /// <summary>
    /// Restores <paramref name="backup"/>. <paramref name="legacy"/> says what a schema 1 backup's book files do: <see cref="LegacyBookFiles.Restore"/> imports them (<paramref name="onAdded"/> is called for each story put
    /// on the shelf, Slice 191), <see cref="LegacyBookFiles.StoreFirst"/> lists the books the phone lacks for the app to fetch. A schema 2 backup has no book files and ignores both.
    /// </summary>
    public static Task<StoryRestoreResult> RestoreAsync(Stream backup, IStoryLibrary library, LegacyBookFiles legacy = LegacyBookFiles.Restore, Action<Story>? onAdded = null) =>
        RestoreAsync(backup, library, MaxBackupBytes, legacy, onAdded);

    internal static async Task<StoryRestoreResult> RestoreAsync(Stream backup, IStoryLibrary library, long maxBytes, LegacyBookFiles legacy = LegacyBookFiles.Restore, Action<Story>? onAdded = null)
    {
        try
        {
            using var zip = new ZipArchive(backup, ZipArchiveMode.Read, leaveOpen: true);
            StoryBackupManifest manifest = await ReadManifestAsync(zip);
            var budget = new ByteBudget(maxBytes);
            if (manifest.SchemaVersion >= 2)
            {
                return await RestoreStateAsync(zip, manifest, library, budget);
            }

            return legacy == LegacyBookFiles.Restore
                ? await RestoreLegacyFilesAsync(zip, library, budget, onAdded)
                : await RestoreLegacyAsync(zip, library, budget);
        }
        catch (Exception ex) when (ex is InvalidDataException or StoryPackageException)
        {
            // no-fallback: rethrown as StoryBackupException, which the page shows
            throw new StoryBackupException("The file is not a valid backup.", ex);
        }
    }

    /// <summary>The settings a backup carries, checked against <see cref="BackupSettings"/>; empty for a schema 1 backup.</summary>
    public static async Task<IReadOnlyDictionary<string, string>> ReadSettingsAsync(Stream backup)
    {
        try
        {
            using var zip = new ZipArchive(backup, ZipArchiveMode.Read, leaveOpen: true);
            return BackupSettings.Sanitize((await ReadManifestAsync(zip)).Settings);
        }
        catch (InvalidDataException ex)
        {
            // no-fallback: rethrown as StoryBackupException, which the page shows
            throw new StoryBackupException("The file is not a valid backup.", ex);
        }
    }

    /// <summary>
    /// Applies the backup's state for the book <paramref name="bookId"/> (its store id) to the copy now on the shelf, after the app downloaded it: its recordings and reading view come back. Returns false when the book is
    /// not on the shelf or the backup holds nothing for it.
    /// </summary>
    public static async Task<bool> ApplyToBookAsync(Stream backup, IStoryLibrary library, string bookId)
    {
        try
        {
            using var zip = new ZipArchive(backup, ZipArchiveMode.Read, leaveOpen: true);
            StoryBackupManifest manifest = await ReadManifestAsync(zip);
            Story? phone = await ShelfCopyAsync(library, bookId);
            if (phone is null)
            {
                return false;
            }

            var budget = new ByteBudget(MaxBackupBytes);
            return manifest.SchemaVersion >= 2
                ? await MergeStateAsync(zip, library, phone, bookId, budget) != Outcome.Skipped
                : await ApplyLegacyAsync(zip, library, phone, bookId, budget);
        }
        catch (Exception ex) when (ex is InvalidDataException or StoryPackageException or StoryFormatException)
        {
            // no-fallback: rethrown as StoryBackupException, which the page shows
            throw new StoryBackupException("The file is not a valid backup.", ex);
        }
    }

    private enum Outcome
    {
        Added,
        Merged,
        Skipped,
        Damaged,
        TooLarge,
    }

    private static async Task<StoryBackupManifest> ReadManifestAsync(ZipArchive zip)
    {
        ZipArchiveEntry entry = zip.GetEntry(StoryBackupManifest.ManifestFileName)
            ?? throw new StoryBackupException("The file is not a Dino Book Reader backup.");
        if (entry.Length > MaxManifestBytes)
        {
            throw new StoryBackupException("The backup manifest is too large.");
        }

        await using Stream stream = new CappedStream(entry.Open(), MaxManifestBytes);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return StoryBackupManifest.Parse(await reader.ReadToEndAsync());
    }

    // The shelf's copy of a backed-up book: the story whose store id (or, failing that, own id) is the backup's id.
    private static async Task<Story?> ShelfCopyAsync(IStoryLibrary library, string id)
    {
        IReadOnlyList<Story> shelf = await library.ListAsync();
        return shelf.FirstOrDefault(s => s.Id == id) ?? shelf.FirstOrDefault(s => s.SourceId == id);
    }

    // ---- schema 2 ----

    private static async Task<StoryRestoreResult> RestoreStateAsync(ZipArchive zip, StoryBackupManifest manifest, IStoryLibrary library, ByteBudget budget)
    {
        IReadOnlyList<BackupShelfEntry> shelf = manifest.Shelf ?? [];
        if (shelf.Count > MaxStories)
        {
            throw new StoryBackupException("The backup has too many stories.");
        }

        var counts = new int[Enum.GetValues<Outcome>().Length];
        var missing = new List<MissingBook>();
        int tooLarge = 0;
        for (int i = 0; i < shelf.Count && tooLarge == 0; i++)
        {
            BackupShelfEntry book = shelf[i];
            Outcome outcome = await RestoreOneStateAsync(zip, library, book, missing, budget);
            counts[(int)outcome]++;
            tooLarge = outcome == Outcome.TooLarge ? shelf.Count - i : 0;
        }

        return ResultOf(counts, tooLarge, missing);
    }

    private static async Task<Outcome> RestoreOneStateAsync(ZipArchive zip, IStoryLibrary library, BackupShelfEntry book, List<MissingBook> missing, ByteBudget budget)
    {
        if (!StoryIds.IsValid(book.Id) || zip.GetEntry(StoryBackupState.StateFile(book.Id)) is null)
        {
            return Outcome.Damaged;
        }

        Story? phone = await ShelfCopyAsync(library, book.Id);
        if (phone is null)
        {
            missing.Add(new MissingBook(book.Id, book.Title));
            return Outcome.Skipped;
        }

        return await MergeStateAsync(zip, library, phone, book.Id, budget);
    }

    private static async Task<Outcome> MergeStateAsync(ZipArchive zip, IStoryLibrary library, Story phone, string id, ByteBudget budget)
    {
        try
        {
            ZipArchiveEntry? stateEntry = StoryIds.IsValid(id) ? zip.GetEntry(StoryBackupState.StateFile(id)) : null;
            if (stateEntry is null || stateEntry.Length > StoryPackageReader.MaxStoryBytes)
            {
                return Outcome.Damaged;
            }

            Story stub = StoryJson.Deserialize(await ReadTextAsync(stateEntry, StoryPackageReader.MaxStoryBytes));
            phone = await WithReadingViewAsync(library, phone, stub);
            Story backed = StoryBackupState.Backed(phone, stub);
            await using FileStream package = TempFile();
            await BuildPackageAsync(zip, package, backed, id, budget);
            package.Position = 0;
            Debug.WriteLine($"Dino Reader restore: merging recordings into {phone.Id}");
            return await StoryPackageReader.MergeRecordingsAsync(package, library, phone, budget) is null ? Outcome.Skipped : Outcome.Merged;
        }
        catch (Exception ex) when (ex is StoryPackageException or StoryFormatException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            // no-fallback: counted in the restore result (Damaged or TooLarge), which the page shows
            return budget.Exhausted ? Outcome.TooLarge : Outcome.Damaged;
        }
    }

    // The reading view comes back only where the phone's copy has no choice of its own yet.
    private static async Task<Story> WithReadingViewAsync(IStoryLibrary library, Story phone, Story stub)
    {
        if (phone.PreferredMode is not null || stub.PreferredMode is null)
        {
            return phone;
        }

        Story updated = phone with { PreferredMode = stub.PreferredMode };
        await library.UpdateAsync(updated);
        return updated;
    }

    // An in-memory-style package (story.json plus the recordings the backup holds), so the existing staged, rolled-back merge does the placing.
    private static async Task BuildPackageAsync(ZipArchive backup, Stream package, Story backed, string id, ByteBudget budget)
    {
        using var zip = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true);
        ZipArchiveEntry story = zip.CreateEntry(StoryPackageManifest.StoryFileName, CompressionLevel.Optimal);
        await using (Stream stream = story.Open())
        {
            await stream.WriteAsync(new UTF8Encoding(false).GetBytes(StoryJson.Serialize(backed)));
        }

        foreach (string name in StoryRecordings.Files(backed).Distinct(StringComparer.Ordinal))
        {
            if (backup.GetEntry(StoryBackupState.RecordingEntry(id, name)) is { } source)
            {
                await using Stream from = new CappedStream(source.Open(), StoryPackageReader.MaxPictureBytes, budget);
                await using Stream to = zip.CreateEntry(name, CompressionLevel.NoCompression).Open();
                await from.CopyToAsync(to, CopyBufferBytes);
            }
        }
    }

    // ---- schema 1 (a .dino per story): recordings and the shelf list are used, the book files are ignored ----

    private static async Task<StoryRestoreResult> RestoreLegacyAsync(ZipArchive zip, IStoryLibrary library, ByteBudget budget)
    {
        List<ZipArchiveEntry> packages = zip.Entries.Where(IsPackage).ToList();
        if (packages.Count > MaxStories)
        {
            throw new StoryBackupException("The backup has too many stories.");
        }

        var counts = new int[Enum.GetValues<Outcome>().Length];
        var missing = new List<MissingBook>();
        int tooLarge = 0;
        for (int i = 0; i < packages.Count && tooLarge == 0; i++)
        {
            Outcome outcome = await RestoreOneLegacyAsync(packages[i], library, missing, budget);
            counts[(int)outcome]++;
            tooLarge = outcome == Outcome.TooLarge ? packages.Count - i : 0;
        }

        return ResultOf(counts, tooLarge, missing);
    }

    private static async Task<Outcome> RestoreOneLegacyAsync(ZipArchiveEntry entry, IStoryLibrary library, List<MissingBook> missing, ByteBudget budget)
    {
        try
        {
            if (entry.Length > MaxPackageBytes)
            {
                return Outcome.Damaged;
            }

            await using FileStream package = TempFile();
            await CopyCappedAsync(entry, package, budget);
            (Story packaged, string id) = await PeekAsync(package);
            Story? phone = await ShelfCopyAsync(library, id) ?? await ShelfCopyAsync(library, packaged.Id);
            if (phone is null)
            {
                missing.Add(new MissingBook(id, packaged.Title));
                return Outcome.Skipped;
            }

            package.Position = 0;
            return await StoryPackageReader.MergeRecordingsAsync(package, library, phone, budget) is null ? Outcome.Skipped : Outcome.Merged;
        }
        catch (Exception ex) when (ex is StoryPackageException or StoryFormatException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            // no-fallback: counted in the restore result (Damaged or TooLarge), which the page shows
            return budget.Exhausted ? Outcome.TooLarge : Outcome.Damaged;
        }
    }

    private static async Task<bool> ApplyLegacyAsync(ZipArchive zip, IStoryLibrary library, Story phone, string bookId, ByteBudget budget)
    {
        foreach (ZipArchiveEntry entry in zip.Entries.Where(IsPackage))
        {
            await using FileStream package = TempFile();
            await CopyCappedAsync(entry, package, budget);
            (Story packaged, string id) = await PeekAsync(package);
            if (id != bookId && packaged.Id != bookId)
            {
                continue;
            }

            package.Position = 0;
            await StoryPackageReader.MergeRecordingsAsync(package, library, phone, budget);
            return true;
        }

        return false;
    }

    // Only top-level packages count; anything else in the zip (folders, macOS metadata, stray files) is ignored.
    private static bool IsPackage(ZipArchiveEntry entry) =>
        !entry.FullName.StartsWith(MacMetadataFolder, StringComparison.Ordinal)
        && entry.FullName == entry.Name
        && entry.Name.EndsWith(StoryPackageManifest.Extension, StringComparison.OrdinalIgnoreCase);

    // The package's story and the id the backup knew it by: the writer keeps a store book's source id as the package id in its manifest.
    private static async Task<(Story Story, string Id)> PeekAsync(Stream package)
    {
        package.Position = 0;
        using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        Story packaged = StoryJson.Deserialize(await ReadTextAsync(
            zip.GetEntry(StoryPackageManifest.StoryFileName) ?? throw new StoryPackageException("The package has no story.json."), StoryPackageReader.MaxStoryBytes));
        StoryPackageManifest manifest = StoryPackageManifest.Parse(await ReadTextAsync(
            zip.GetEntry(StoryPackageManifest.ManifestFileName) ?? throw new StoryPackageException("The package has no manifest."), StoryPackageReader.MaxManifestBytes));
        return (packaged, manifest.Id != packaged.Id ? manifest.Id : packaged.SourceId ?? packaged.Id);
    }

    // ---- shared ----

    private static StoryRestoreResult ResultOf(int[] counts, int tooLarge, List<MissingBook> missing) =>
        new(counts[(int)Outcome.Added], counts[(int)Outcome.Skipped] - missing.Count, counts[(int)Outcome.Damaged], tooLarge, counts[(int)Outcome.Merged], missing.Count > 0 ? missing : null);

    private static async Task<string> ReadTextAsync(ZipArchiveEntry entry, long cap)
    {
        if (entry.Length > cap)
        {
            throw new StoryPackageException("The package is too large.");
        }

        await using Stream stream = new CappedStream(entry.Open(), cap);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
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
}
