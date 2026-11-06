using System.Diagnostics;
using System.Globalization;
using DinoReader.Core;

namespace DinoReader.BookBuilder;

/// <summary>
/// Slice 281: one listening track per store book and language, <c>listen-&lt;lang&gt;-v&lt;n&gt;.m4a</c>, for smart speakers (Alexa, Sonos), which fetch audio from a public HTTPS address and cannot play the app's zip.
/// The pack's clips are joined in the app's read order (<see cref="ListenTrackPlan"/>) with the app's sentence gap and a longer gap at a page turn. The join is ONE ffmpeg process per track using the
/// <b>concat demuxer</b> (an ffconcat list file: one <c>file</c> line per clip or silence), never a filter graph: no command-line argument grows with the book and only one file is open at a time, so a book of
/// thousands of clips is the same command as one of ten. Each distinct gap is a small silence <c>.m4a</c> made first with the clips' own sample rate, so the demuxer sees one format throughout (every clip is checked to share one sample rate and channel count first, see <see cref="ReadFormat"/>); the list is encoded once as AAC
/// (ffmpeg's native <c>aac</c>), mono, 48 kb/s, <c>+faststart</c>, the encoder settings <c>tools/premade-audio/encode.py</c> uses for the clips. The length is read back with ffprobe (a missing or non-numeric
/// duration fails the track). ffmpeg and ffprobe are found through <c>$FFMPEG</c>/<c>$FFPROBE</c> or the PATH. The track's version is the audio pack's.
/// </summary>
internal static class ListenTrack
{
    public const string FfmpegEnv = "FFMPEG";
    public const string FfprobeEnv = "FFPROBE";
    private const string DefaultFfmpeg = "ffmpeg";
    private const string DefaultFfprobe = "ffprobe";
    private const int OutputSampleRate = 48000;
    private const string Bitrate = "48k";
    private const int ProcessTimeoutMs = 600_000;
    private const string TrackTempFile = "track.m4a";
    private const string ListFile = "list.ffconcat";
    private const string LanguageSeparator = ",";
    private const string DurationNotAvailable = "N/A";
    private const int HeaderBytes = 65536;
    private const int BoxHeaderBytes = 8; // 4-byte size, 4-byte type
    private const int StsdPrefixBytes = 8; // stsd is a full box: version and flags, then the entry count; the first entry follows
    private const string Mp4aType = "mp4a";
    private static readonly string[] SampleEntryPath = ["moov", "trak", "mdia", "minf", "stbl", "stsd"];
    private const int Mp4aChannelsOffset = 20; // from the start of the "mp4a" tag: size-less entry = 6 reserved + 2 index + 8 reserved, then the channel count
    private const int Mp4aRateOffset = 28; // after channel count, sample size and 4 reserved bytes: the 16.16 sample rate, integer part first

    /// <summary><c>build-listen &lt;stem&gt; --lang &lt;code&gt;[,&lt;code&gt;...] [--clips &lt;folder&gt;] [--out &lt;folder&gt;]</c>: run after <c>build-audio</c> for the same book and languages. Joins the clips into
    /// <c>artifacts/store/&lt;id&gt;/listen-&lt;lang&gt;-v&lt;n&gt;.m4a</c> (n = the pack version recorded in audio-cast.json) and records the track's length there, which the next <c>build</c> lists in meta.json.
    /// The story is built once for all the languages. A failing language is named and does not stop the others; the exit code is 1 when any failed.</summary>
    public static int Run(string stem, string languages, string? clipsFolder)
    {
        BookFolder book = Books.Folder(stem);
        Story story = SentenceDump.StoryOf(stem);
        var failed = new List<string>();
        foreach (string language in languages.Split(LanguageSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryBuildOne(stem, book, story, language, clipsFolder))
            {
                failed.Add(language);
            }
        }

        return failed.Count == 0 ? 0 : Fail(stem, failed);
    }

    private static int Fail(string stem, IReadOnlyList<string> failed)
    {
        Console.Error.WriteLine($"listening track failed: {stem} {string.Join(' ', failed)}");
        return 1;
    }

    /// <summary>Builds one language; any failure except a cancel (a missing clip, a full disk, a bad manifest, ffmpeg) names the language and lets the next one go on.</summary>
    private static bool TryBuildOne(string stem, BookFolder book, Story story, string language, string? clipsFolder)
    {
        try
        {
            AudioPackRecord record = AudioPacks.RecordsIn(book.Dir).GetValueOrDefault(language)
                ?? throw new InvalidOperationException($"{stem} has no audio pack record for '{language}'; run build-audio first.");
            string clips = clipsFolder ?? AudioPacks.ClipsFolderOf(book, language);
            int seconds = Build(book, story, language, clips, record.Version, Path.Combine(StoreBuilder.StoreFolder(), book.Info.Id));
            AudioPacks.SaveRecord(book.Dir, language, record with { ListenSeconds = seconds });
            Console.WriteLine($"built {stem} listening track {language} v{record.Version} ({seconds} s)");
            return true;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Console.Error.WriteLine($"::error::The listening track of {stem} [{language}] could not be built: {error.GetType().Name}: {error.Message}");
            return false;
        }
    }

    /// <summary>Builds the track of one book language into <paramref name="outFolder"/> and returns its length in whole seconds (rounded up). Fails on a missing clip, a missing ffmpeg or an ffmpeg failure.</summary>
    public static int Build(BookFolder book, Story story, string language, string clipsFolder, int version, string outFolder)
    {
        SentenceListing listing = SentenceDump.Build(book.Stem, story, language);
        IReadOnlyList<ListenStep> steps = Plan(story, listing, language, clipsFolder);
        Directory.CreateDirectory(outFolder);
        return Join(steps, clipsFolder, Path.Combine(outFolder, ListenTrackPlan.FileName(language, version)));
    }

    private static IReadOnlyList<ListenStep> Plan(Story story, SentenceListing listing, string language, string clipsFolder)
    {
        var rules = LanguageRules.For(UiLanguage.Resolve(story.Language));
        IReadOnlyList<SentenceSpan> spans = StorySentences.Build(story.Text, rules, story.BreakMap());
        IReadOnlyList<int> pages = ListenTrackPlan.PagesOfSentences(story, spans);
        return ListenTrackPlan.Steps(ListenTrackPlan.Clips(listing, pages, AudioPacks.ClipsByIndex(clipsFolder, language)));
    }

    /// <summary>Joins the steps into <paramref name="target"/> with one ffmpeg run and returns the track's length in whole seconds (rounded up). The file is written beside the target and moved into place.</summary>
    public static int Join(IReadOnlyList<ListenStep> steps, string clipsFolder, string target)
    {
        string temp = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(target))!, $".{Guid.NewGuid():N}.tmp");
        Directory.CreateDirectory(temp);
        try
        {
            RequireClips(steps, clipsFolder);
            string list = WriteList(steps, clipsFolder, temp);
            string produced = Path.Combine(temp, TrackTempFile);
            RunTool(Tool(FfmpegEnv, DefaultFfmpeg), ArgumentsOf(list, produced), temp);
            int seconds = (int)Math.Ceiling(DurationSeconds(Probe(["-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", produced]), target));
            File.Move(produced, target, overwrite: true);
            return seconds;
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static void RequireClips(IReadOnlyList<ListenStep> steps, string clipsFolder)
    {
        foreach (string clip in steps.Where(s => s.File is not null).Select(s => Path.Combine(clipsFolder, s.File!)))
        {
            if (!File.Exists(clip))
            {
                throw new InvalidOperationException($"The listening track needs {clip} but the file is not there.");
            }
        }
    }

    /// <summary>Writes the silences the steps need and the ffconcat list; returns the list's path.</summary>
    private static string WriteList(IReadOnlyList<ListenStep> steps, string clipsFolder, string temp)
    {
        (int Rate, int Channels) clipFormat = RequireOneFormat(steps, clipsFolder) ?? (OutputSampleRate, 1);
        foreach (int milliseconds in ListenTrackPlan.SilenceLengths(steps))
        {
            MakeSilence(Path.Combine(temp, ListenTrackPlan.SilenceName(milliseconds)), milliseconds, clipFormat);
        }

        string list = Path.Combine(temp, ListFile);
        File.WriteAllText(list, ListenTrackPlan.ConcatList(steps, clipsFolder, temp));
        return list;
    }

    /// <summary>The guard for the concat demuxer, which joins clips as if they shared one format and plays a clip of another sample rate too fast or slow without any error: reads every clip's sample rate and channel count
    /// (a header read, no process) and fails the track naming the first clip that differs from the first one. Returns the shared sample rate, or null when the steps hold no clip.</summary>
    private static (int Rate, int Channels)? RequireOneFormat(IReadOnlyList<ListenStep> steps, string clipsFolder)
    {
        (int Rate, int Channels)? first = null;
        string? firstName = null;
        foreach (string name in steps.Where(s => s.File is not null).Select(s => s.File!).Distinct())
        {
            (int Rate, int Channels) format = ReadFormat(Path.Combine(clipsFolder, name));
            first ??= format;
            firstName ??= name;
            if (format != first)
            {
                throw new InvalidOperationException($"{name} is {format.Rate} Hz, {format.Channels} ch but {firstName} is {first.Value.Rate} Hz, {first.Value.Channels} ch; the clips of one track must share a format (re-encode the pack).");
            }
        }

        return first;
    }

    /// <summary>Sample rate and channel count of an m4a clip, read by walking the MP4 boxes <c>moov/trak/mdia/minf/stbl/stsd</c> to the <c>mp4a</c> sample entry in the first 64 KB (clips are encoded with <c>+faststart</c>,
    /// so the header is there); a file whose walk does not reach that entry (moov at the end, a truncated or odd file) is asked of ffprobe instead.</summary>
    public static (int Rate, int Channels) ReadFormat(string clip)
    {
        byte[] head = new byte[HeaderBytes];
        int length;
        using (FileStream stream = File.OpenRead(clip))
        {
            length = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        }

        int entry = FindSampleEntry(head, 0, length, 0);
        return entry >= 0
            ? (System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(head.AsSpan(entry + Mp4aRateOffset)), System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(head.AsSpan(entry + Mp4aChannelsOffset)))
            : ProbeFormat(clip);
    }

    /// <summary>The offset of the first <c>mp4a</c> sample entry reached through <see cref="SampleEntryPath"/> inside <c>data[start..end)</c>, or -1. A box that runs past the buffer is read as far as the buffer goes.</summary>
    private static int FindSampleEntry(byte[] data, int start, int end, int depth)
    {
        long position = start;
        while (position + BoxHeaderBytes <= end)
        {
            int at = (int)position;
            long size = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at));
            int header = BoxHeaderBytes;
            if (size == 1)
            {
                header += 8;
                size = at + header <= end ? (long)System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(data.AsSpan(at + BoxHeaderBytes)) : 0;
            }
            else if (size == 0)
            {
                size = end - position;
            }

            if (size < header)
            {
                return -1;
            }

            int boxEnd = (int)Math.Min(end, position + size);
            if (System.Text.Encoding.ASCII.GetString(data, at + 4, 4) == SampleEntryPath[depth])
            {
                int hit = depth == SampleEntryPath.Length - 1 ? FirstEntryOf(data, at + header + StsdPrefixBytes, boxEnd) : FindSampleEntry(data, at + header, boxEnd, depth + 1);
                if (hit >= 0)
                {
                    return hit;
                }
            }

            position += size;
        }

        return -1;
    }

    /// <summary>The offset of the <c>mp4a</c> entry that starts at <paramref name="at"/> when it is whole enough to hold the channel count and rate; else -1.</summary>
    private static int FirstEntryOf(byte[] data, int at, int end) =>
        at + 4 + Mp4aRateOffset + 2 <= end && System.Text.Encoding.ASCII.GetString(data, at + 4, 4) == Mp4aType ? at + 4 : -1;

    private static (int Rate, int Channels) ProbeFormat(string clip)
    {
        string[] fields = Probe(["-v", "error", "-select_streams", "a:0", "-show_entries", "stream=sample_rate,channels", "-of", "csv=p=0", clip]).Trim().Split(',');
        return fields.Length == 2 && int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rate) && rate > 0 && int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int channels)
            ? (rate, channels)
            : throw new InvalidOperationException($"ffprobe gave no sample rate and channels for {clip} ('{string.Join(',', fields)}').");
    }

    /// <summary>One silence with the clips' own sample rate and channel count, so the concat demuxer sees one format (a stereo pack gets stereo silences).</summary>
    private static void MakeSilence(string path, int milliseconds, (int Rate, int Channels) format)
    {
        string length = string.Create(CultureInfo.InvariantCulture, $"{milliseconds / 1000.0}");
        string channels = format.Channels.ToString(CultureInfo.InvariantCulture);
        string layout = format.Channels switch { 1 => "mono", 2 => "stereo", _ => $"{channels}c" };
        RunTool(Tool(FfmpegEnv, DefaultFfmpeg), ["-hide_banner", "-loglevel", "error", "-nostats", "-y", "-f", "lavfi", "-i", $"anullsrc=r={format.Rate.ToString(CultureInfo.InvariantCulture)}:cl={layout}",
            "-t", length, "-c:a", "aac", "-b:a", Bitrate, "-ac", channels, path], Path.GetDirectoryName(path)!);
    }

    /// <summary>The one encode: the concat list in, the track out. The argument list does not depend on the book's length.</summary>
    public static IReadOnlyList<string> ArgumentsOf(string list, string produced) =>
        ["-hide_banner", "-loglevel", "error", "-nostats", "-y", "-f", "concat", "-safe", "0", "-i", list,
            "-c:a", "aac", "-b:a", Bitrate, "-ac", "1", "-ar", OutputSampleRate.ToString(CultureInfo.InvariantCulture), "-movflags", "+faststart", produced];

    /// <summary>The length in seconds from ffprobe's <c>format=duration</c> output. A missing, <c>N/A</c>, non-numeric or non-positive value fails the track.</summary>
    public static double DurationSeconds(string probeOutput, string target)
    {
        string text = probeOutput.Trim();
        if (text == DurationNotAvailable || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) || !double.IsFinite(seconds) || seconds <= 0)
        {
            throw new InvalidOperationException($"ffprobe gave no duration for {target} ('{text}'); the listening track is not built.");
        }

        return seconds;
    }

    private static string Probe(IReadOnlyList<string> arguments) =>
        RunTool(ProbeTool(), arguments, Environment.CurrentDirectory);

    /// <summary>ffprobe: <c>$FFPROBE</c>, else the file named ffprobe beside <c>$FFMPEG</c> when that is set, else <c>ffprobe</c> on the PATH.</summary>
    private static string ProbeTool()
    {
        if (Environment.GetEnvironmentVariable(FfprobeEnv) is { Length: > 0 } named)
        {
            return named;
        }

        return Environment.GetEnvironmentVariable(FfmpegEnv) is { Length: > 0 } ffmpeg && Path.GetDirectoryName(ffmpeg) is { Length: > 0 } folder
            ? Path.Combine(folder, DefaultFfprobe + Path.GetExtension(ffmpeg))
            : DefaultFfprobe;
    }

    private static string Tool(string variable, string fallback) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } named ? named : fallback;

    private static string RunTool(string tool, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var start = new ProcessStartInfo(tool) { WorkingDirectory = workingDirectory, RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = StartOrFail(start, tool);
        Task<string> error = process.StandardError.ReadToEndAsync();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(ProcessTimeoutMs))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"{tool} did not finish within {ProcessTimeoutMs / 1000} s.");
        }

        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{tool} failed ({process.ExitCode}): {error.Result.Trim()}");
        }

        return output.Result;
    }

    private static Process StartOrFail(ProcessStartInfo start, string tool)
    {
        try
        {
            return Process.Start(start) ?? throw new InvalidOperationException($"{tool} did not start.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException($"{tool} was not found: install ffmpeg (it brings ffprobe), put both on the PATH or set ${FfmpegEnv} and ${FfprobeEnv} to their paths.");
        }
    }
}
