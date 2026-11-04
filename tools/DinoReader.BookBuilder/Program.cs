using DinoReader.BookBuilder;

// The Dino book builder (Slice 157, Decision 162): content/dino/<stem>/ in, .dino packages and artifacts/store/<id>/ out.
const string OutFlag = "--out";
const string KeepVersionFlag = "--keep-version";
const string LiveFlag = "--live";
const string LangFlag = "--lang";
const string ClipsFlag = "--clips";
const string Usage = """
    usage: DinoReader.BookBuilder <command> [<stem>...]
      build [<stem>...] [--out <folder>] [--live <file>]   write artifacts/store/<id>/ for each book and rewrite committed starters whose content changed;
                                           --live (publish-store) is the live store's id to {version, contentHash}, so a changed store book is built as live + 1, and each store book is
                                           validated first: an invalid one is skipped and named, the valid ones are built, and the exit code is 2
      check                                fail when a starter changed without a version bump or is stale, or a store book does not build
      stamp [<stem>...] [--keep-version]   raise the version of changed starters and store their content hash (store books need none); --keep-version is for one-off seeding only
      dump-pieces [<stem>...]              write translations/pieces.json (the texts a book needs translated)
      dump-voices [--out <file>]           cast every book mechanically, as cast entries for the writers
      voice-listen --out <file>            write the English roster and the Ugly Duckling cast for tools/voice-listen
      dump-sentences <stem> --lang <code> [--out <file>]   print every piece the app reads aloud for a store book or a bundled starter in a language (index, highlight sentence, spoken text, speaker, narrator) as JSON, for tools/premade-audio
      build-audio <stem> --lang <code> [--clips <folder>] [--out <folder>]   pack the clips folder (default content/dino/<stem>/audio/<code>, Git LFS: git lfs pull --include it first; manifest.json v3 + its clips) into artifacts/store/<id>/audio-<code>-v<n>.zip and record
                                           the pack in the book's audio-cast.json (audioPacks); the version rises only when the content hash changes; the next build lists it in meta.json
    """;

string[] arguments = args;
string command = arguments.FirstOrDefault() ?? string.Empty;
string? Option(string name) => Array.IndexOf(arguments, name) is var at and >= 0 && at + 1 < arguments.Length ? arguments[at + 1] : null;
string? outPath = Option(OutFlag);
string? livePath = Option(LiveFlag);
string? language = Option(LangFlag);
string? clipsPath = Option(ClipsFlag);
List<string> stems = arguments.Skip(1).Where(a => !a.StartsWith("--") && a != outPath && a != livePath && a != language && a != clipsPath).ToList();

try
{
    switch (command)
    {
        case "build":
            if (Array.IndexOf(arguments, LiveFlag) >= 0 && livePath is null)
            {
                return Fail("build --live needs the path of the live store state file; refusing to build without validation");
            }

            StoreBuilder.OutputFolder = outPath;
            return await BookCommands.BuildAsync(stems, livePath);
        case "check":
            return await BookCommands.CheckAsync();
        case "stamp":
            return BookCommands.Stamp(stems, arguments.Contains(KeepVersionFlag));
        case "dump-pieces":
            return await BookCommands.DumpPiecesAsync(stems);
        case "dump-voices":
            return await VoiceDump.RunAsync(outPath);
        case "voice-listen":
            return outPath is null ? Fail("voice-listen needs --out <file>") : VoiceListenExport.Run(outPath);
        case "dump-sentences":
            return SentenceDump.Run(stems.FirstOrDefault(), language, outPath);
        case "build-audio":
            StoreBuilder.OutputFolder = outPath;
            return stems.Count != 1 || language is null ? Fail("build-audio needs one book and --lang <code>") : BookCommands.BuildAudio(stems[0], language, clipsPath);
        default:
            return Fail(Usage);
    }
}
catch (Exception e) when (e is InvalidOperationException or System.Text.Json.JsonException or IOException)
{
    return Fail(e is InvalidOperationException ? e.Message : $"{e.GetType().Name}: {e.Message}");
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}
