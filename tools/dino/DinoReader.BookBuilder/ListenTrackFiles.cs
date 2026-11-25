namespace DinoReader.BookBuilder;

/// <summary>
/// Slice 409: the listening track is built on the PC (<see cref="ListenTrack"/>, ffmpeg) and committed beside the clips in Git LFS, <c>content/dino/&lt;book&gt;/audio/&lt;lang&gt;/listen-&lt;lang&gt;-v&lt;n&gt;.m4a</c>,
/// where n is the pack version in the book's <c>audio-cast.json</c> record. CI needs no ffmpeg: it only checks the track is there (<see cref="Problems"/>) and stages the committed file for upload (<see cref="Stage"/>).
/// </summary>
internal static class ListenTrackFiles
{
    private const string LanguageSeparator = ",";

    /// <summary>
    /// <c>build-listen-all [&lt;stem&gt;...]</c>: the repeatable PC step for every book (all with an audio-cast.json when none is named): for each language that has a manifest, <c>build-audio</c> (the pack record) then
    /// one <c>build-listen</c> per book. Needs the clips pulled from Git LFS and ffmpeg. Prints a WARNING when a pack's version moved (its clips differ from the committed record, so the live pack is stale too).
    /// A language whose track is already current (the record lists a length, the track file is there and the clips still hash to the record) is skipped, so a rerun adds no new Git LFS object (ffmpeg's output is not
    /// byte-stable); <paramref name="force"/> (<c>--force</c>) rebuilds every language. A language whose clips are still LFS pointers is skipped with a warning. Exit 1 when any language failed.
    /// </summary>
    public static int BuildAll(IReadOnlyList<string> stems, bool force = false)
    {
        List<BookFolder> books = (stems.Count > 0 ? stems.Select(Books.Folder) : Books.All.Where(b => File.Exists(Path.Combine(b.Dir, AudioPacks.CastFile)))).ToList();
        var failed = new List<string>();
        foreach (BookFolder book in books)
        {
            failed.AddRange(BuildBook(book, force));
        }

        if (failed.Count > 0)
        {
            Console.Error.WriteLine($"build-listen-all failed: {string.Join(' ', failed)}");
        }

        // The same checks CI runs: a language whose build failed after build-audio moved its record, or a track that does not match its clips, shows up here instead of in a later CI run.
        int check = AudioAlignment.Run(books.Select(b => b.Stem).ToList());
        return failed.Count == 0 && check == 0 ? 0 : 1;
    }

    /// <summary>Builds one book's languages; returns the failures as <c>stem/language</c> (a track failure names every language of the book's track run).</summary>
    private static List<string> BuildBook(BookFolder book, bool force)
    {
        IReadOnlyDictionary<string, AudioPackRecord> before = AudioPacks.RecordsIn(book.Dir);
        var built = new List<string>();
        var failures = new List<string>();
        foreach (string language in AudioCoverage.OfferedLanguages.Where(l => HasPulledManifest(book, l)))
        {
            if (!force && IsUpToDate(before.GetValueOrDefault(language), AudioPacks.ClipsFolderOf(book, language), language))
            {
                Console.WriteLine($"{book.Stem} {language}: listening track is current, skipped (--force rebuilds it)");
            }
            else if (TryBuildPack(book.Stem, language, before))
            {
                built.Add(language);
            }
            else
            {
                failures.Add($"{book.Stem}/{language}");
            }
        }

        if (built.Count > 0 && ListenTrack.Run(book.Stem, string.Join(LanguageSeparator, built), null) != 0)
        {
            failures.AddRange(built.Select(language => $"{book.Stem}/{language}(track)"));
        }

        return failures;
    }

    /// <summary>True when the language has a manifest whose clips were pulled; a manifest that is still an LFS pointer is named and skipped.</summary>
    internal static bool HasPulledManifest(BookFolder book, string language)
    {
        string manifest = Path.Combine(AudioPacks.ClipsFolderOf(book, language), AudioPacks.ManifestFile);
        if (!File.Exists(manifest))
        {
            return false;
        }

        if (AudioPacks.IsLfsPointer(manifest))
        {
            Console.WriteLine($"WARNING: {book.Stem} {language} skipped: its clips are Git LFS pointers (git lfs pull --include \"content/dino/{book.Stem}/audio/**\" --exclude \"\").");
            return false;
        }

        return true;
    }

    /// <summary>True when the record lists the track, the track file is there and the clips on disk still hash to the record, so rebuilding would only rewrite the same track.</summary>
    internal static bool IsUpToDate(AudioPackRecord? record, string clipsFolder, string language) =>
        record is not null && HashOrNull(clipsFolder, language) == record.Hash && !Problems(record, language, clipsFolder).Any();

    /// <summary>build-audio for one language; a failure is named and counted by the caller instead of ending the whole command.</summary>
    internal static bool TryBuildPack(string stem, string language, IReadOnlyDictionary<string, AudioPackRecord> before)
    {
        try
        {
            BookCommands.BuildAudio(stem, language, null);
            WarnWhenMoved(stem, language, before);
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Console.Error.WriteLine($"::error::The audio pack of {stem} [{language}] could not be built: {error.GetType().Name}: {error.Message}");
            return false;
        }
    }

    private static void WarnWhenMoved(string stem, string language, IReadOnlyDictionary<string, AudioPackRecord> before)
    {
        if (AudioPacks.RecordsIn(Books.Folder(stem).Dir).TryGetValue(language, out AudioPackRecord? now)
            && before.TryGetValue(language, out AudioPackRecord? was) && was.Version != now.Version)
        {
            Console.WriteLine($"WARNING: {stem} {language} pack version moved {was.Version} -> {now.Version}: its clips differ from the committed record, so this pack is behind the live store too.");
        }
    }

    /// <summary>Fails when the clips on disk no longer hash to the pack record, so a track is never named for a pack version whose clips changed (run <c>build-audio</c> first).</summary>
    public static void RequireCurrentRecord(AudioPackRecord record, string clipsFolder, string language)
    {
        string hash = AudioPacks.ContentHash(clipsFolder, AudioPacks.PackFiles(clipsFolder, language));
        if (hash != record.Hash)
        {
            throw new InvalidOperationException($"The clips in {clipsFolder} changed since the pack record (version {record.Version}) was written; run build-audio for this language first.");
        }
    }

    /// <summary>Deletes the tracks of this language that belong to another pack version, so the folder holds only the current one.</summary>
    public static void RemoveOlder(string clipsFolder, string language, int version)
    {
        string current = ListenTrackPlan.FileName(language, version);
        foreach (string path in Directory.EnumerateFiles(clipsFolder, $"listen-{language}-v*.m4a").Where(p => Path.GetFileName(p) != current))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// What check-audio says about one language's listening track (empty when it is fine): the record lists a length, the file named for the record's version is there and not empty (an LFS pointer counts by the size it records,
    /// so CI, which pulls no clips, checks it too), and, where the clips are real files (a PC checkout), they still hash to the record, i.e. the track is not older than its clips.
    /// </summary>
    public static IEnumerable<string> Problems(AudioPackRecord record, string language, string folder)
    {
        string name = ListenTrackPlan.FileName(language, record.Version);
        long? size = AudioCoverage.SizeInFolder(folder, name);
        if (!(record.ListenSeconds > 0))
        {
            yield return $"{AudioPacks.CastFile} lists no listening track length for this language: run build-audio, then build-listen";
        }

        if (size is null)
        {
            yield return $"the listening track audio/{language}/{name} is missing: run build-listen on the PC and commit it";
        }
        else if (size == 0)
        {
            yield return $"the listening track audio/{language}/{name} is empty: run build-listen on the PC and commit it";
        }

        if (HashOrNull(folder, language) is { } hash && hash != record.Hash)
        {
            yield return $"the clips changed since the pack record (version {record.Version}) and its listening track were made: run build-audio, then build-listen";
        }
    }

    // Null when the clips are not on disk (LFS pointers, as in CI) or the folder is not a pack: the staleness check then cannot run, and the other checks still do.
    private static string? HashOrNull(string folder, string language)
    {
        try
        {
            return AudioPacks.ContentHash(folder, AudioPacks.PackFiles(folder, language));
        }
        catch (Exception error) when (error is InvalidOperationException or System.Text.Json.JsonException or IOException)
        {
            return null;
        }
    }

    /// <summary><c>stage-listen &lt;stem&gt; --lang &lt;code&gt;[,&lt;code&gt;...] [--clips &lt;folder&gt;] [--out &lt;folder&gt;]</c>: copies each language's committed track (after <c>git lfs pull</c> of it) into
    /// <c>artifacts/store/&lt;id&gt;/</c> for the publish step. No ffmpeg. A track that is missing, an LFS pointer, or recorded with no length fails that language; the exit code is 1 when any failed.</summary>
    public static int Stage(string stem, string languages, string? clipsFolder, string? storeFolder = null)
    {
        BookFolder book = Books.Folder(stem);
        var failed = new List<string>();
        foreach (string language in languages.Split(LanguageSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryStageOne(book, language, clipsFolder ?? AudioPacks.ClipsFolderOf(book, language), storeFolder ?? StoreBuilder.StoreFolder()))
            {
                failed.Add(language);
            }
        }

        if (failed.Count > 0)
        {
            Console.Error.WriteLine($"listening track not staged: {stem} {string.Join(' ', failed)}");
        }

        return failed.Count == 0 ? 0 : 1;
    }

    internal static bool TryStageOne(BookFolder book, string language, string clips, string storeFolder)
    {
        try
        {
            AudioPackRecord record = AudioPacks.RecordsIn(book.Dir).GetValueOrDefault(language)
                ?? throw new InvalidOperationException($"{book.Stem} has no audio pack record for '{language}'.");
            StageTrack(record, language, clips, Path.Combine(storeFolder, book.Info.Id));
            Console.WriteLine($"staged {book.Stem} listening track {language} v{record.Version}");
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"::error::The listening track of {book.Stem} [{language}] could not be staged: {error.Message}");
            return false;
        }
    }

    /// <summary>Copies the committed track named for the record's version into <paramref name="targetFolder"/>. Throws when the record lists no length, the file is missing, or it is an LFS pointer (never pulled), so a pointer is never uploaded as the track.</summary>
    internal static void StageTrack(AudioPackRecord record, string language, string clips, string targetFolder)
    {
        string name = ListenTrackPlan.FileName(language, record.Version);
        string source = Path.Combine(clips, name);
        if (!(record.ListenSeconds > 0) || !File.Exists(source) || AudioPacks.IsLfsPointer(source))
        {
            throw new InvalidOperationException($"{source} is not a committed, pulled listening track (expected the file {name}, i.e. content/dino/<book>/audio/{language}/listen-{language}-v*.m4a; record length {record.ListenSeconds?.ToString() ?? "none"}); build it on the PC with build-listen, commit it and git lfs pull it.");
        }

        Directory.CreateDirectory(targetFolder);
        File.Copy(source, Path.Combine(targetFolder, name), overwrite: true);
    }
}
