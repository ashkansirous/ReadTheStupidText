using DinoReader.BookBuilder;

// The Dino book builder (Slice 157, Decision 162): content/dino/<stem>/ in, .dino packages and artifacts/store/<id>/ out.
const string OutFlag = "--out";
const string KeepVersionFlag = "--keep-version";
const string LiveFlag = "--live";
const string LangFlag = "--lang";
const string ClipsFlag = "--clips";
const string VerdictFlag = "--verdict";
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
      check-audio [<stem>...] [--live] [--verdict <file>]   fail unless every book has a complete audio pack in every language the app offers (Slice 272, 277): a pack exists, every sentence the app reads has its clip, no clip
                                           is for text that is gone, every clip file is there and not empty; needs the manifests pulled from Git LFS. --live checks what the live store serves instead (downloads
                                           its packs; read only). --verdict writes a JSON verdict per book (stem, id, complete, gaps) for the publish job, which uploads only complete books
    """;

string[] arguments = args;
string command = arguments.FirstOrDefault() ?? string.Empty;
string? Option(string name) => Array.IndexOf(arguments, name) is var at and >= 0 && at + 1 < arguments.Length ? arguments[at + 1] : null;
string? outPath = Option(OutFlag);
string? livePath = Option(LiveFlag);
string? language = Option(LangFlag);
string? clipsPath = Option(ClipsFlag);
string? verdictPath = Option(VerdictFlag);
bool liveCheck = command == "check-audio" && Array.IndexOf(arguments, LiveFlag) >= 0; // for check-audio --live is a switch, not a file
List<string> stems = arguments.Skip(1).Where(a => !a.StartsWith("--") && a != outPath && (liveCheck || a != livePath) && a != language && a != clipsPath && a != verdictPath).ToList();

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
        case "check-audio":
            return AudioAlignment.Run(stems, liveCheck, verdictPath);
        case "build-audio":
            StoreBuilder.OutputFolder = outPath;
            return stems.Count != 1 || language is null ? Fail("build-audio needs one book and --lang <code>") : BookCommands.BuildAudio(stems[0], language, clipsPath);
        default:
            return Fail(Usage);
    }
}
catch (Exception e) when (e is InvalidOperationException or System.Text.Json.JsonException or IOException or HttpRequestException or TaskCanceledException)
{
    return Fail(e is InvalidOperationException ? e.Message : $"{e.GetType().Name}: {e.Message}");
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}
