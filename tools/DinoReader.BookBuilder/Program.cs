using DinoReader.BookBuilder;

// The Dino book builder (Slice 157, Decision 162): content/dino/<stem>/ in, .dino packages and artifacts/store/<id>/ out.
const string OutFlag = "--out";
const string KeepVersionFlag = "--keep-version";
const string Usage = """
    usage: DinoReader.BookBuilder <command> [<stem>...]
      build [<stem>...] [--out <folder>]   write artifacts/store/<id>/ for each book and rewrite committed starters whose content changed
      check                                fail when a book changed without a version bump or a committed starter is stale
      stamp [<stem>...] [--keep-version]   raise the version of changed books and store their content hash (no stem: every book); --keep-version is for one-off seeding only
      dump-pieces [<stem>...]              write translations/pieces.json (the texts a book needs translated)
      dump-voices [--out <file>]           cast every book mechanically, as cast entries for the writers
      voice-listen --out <file>            write the English roster and the Ugly Duckling cast for tools/voice-listen
    """;

string[] arguments = args;
string command = arguments.FirstOrDefault() ?? string.Empty;
string? Option(string name) => Array.IndexOf(arguments, name) is var at and >= 0 && at + 1 < arguments.Length ? arguments[at + 1] : null;
string? outPath = Option(OutFlag);
List<string> stems = arguments.Skip(1).Where(a => !a.StartsWith("--") && a != outPath).ToList();

try
{
    switch (command)
    {
        case "build":
            StoreBuilder.OutputFolder = outPath;
            return await BookCommands.BuildAsync(stems);
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
