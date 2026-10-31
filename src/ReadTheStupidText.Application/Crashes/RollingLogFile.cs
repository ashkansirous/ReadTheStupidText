using System.Text;

namespace ReadTheStupidText.Application.Crashes;

/// <summary>
/// The phone's small rolling log (Slice 227): two files of at most <see cref="DefaultFileBytes"/> in one folder, swapped when the current one is full,
/// so the newest 128 to 256 KB of log lines are always on disk. One line per call; the writer is flushed on Warning and Error, and by
/// <see cref="Flush"/> from the crash handler. Never throws: a log that cannot be written must not break the app. The tail goes out with a crash report.
/// </summary>
public sealed class RollingLogFile
{
    public const int DefaultFileBytes = 128 * 1024;
    public const int DefaultTailLines = 300;
    private const string FirstName = "log-a.txt";
    private const string SecondName = "log-b.txt";

    private readonly object _gate = new();
    private readonly string[] _paths;
    private readonly int _maxFileBytes;
    private StreamWriter? _writer;
    private int _current = -1;
    private long _currentBytes;

    public RollingLogFile(string folder, int maxFileBytes = DefaultFileBytes)
    {
        _paths = [Path.Combine(folder, FirstName), Path.Combine(folder, SecondName)];
        _maxFileBytes = maxFileBytes;
    }

    /// <summary>Appends one line (no line break inside); <paramref name="flush"/> pushes it to disk at once.</summary>
    public void Append(string line, bool flush)
    {
        try
        {
            lock (_gate)
            {
                StreamWriter writer = Writer();
                writer.WriteLine(line);
                _currentBytes += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
                if (flush)
                {
                    writer.Flush();
                }

                if (_currentBytes >= _maxFileBytes)
                {
                    Swap();
                }
            }
        }
        catch (Exception)
        {
            // A broken log must not break the app; the next call tries again.
            CloseQuietly();
        }
    }

    /// <summary>Writes whatever is buffered; called from the crash handler. Never throws.</summary>
    public void Flush()
    {
        try
        {
            lock (_gate)
            {
                _writer?.Flush();
            }
        }
        catch (Exception)
        {
            // Nothing more can be done in a crash handler.
        }
    }

    /// <summary>Closes the log and deletes both files (the person turned crash reports off, or the build may not send). A later line starts a fresh file. Never throws.</summary>
    public void DeleteFiles()
    {
        try
        {
            lock (_gate)
            {
                CloseQuietly();
                foreach (string path in _paths)
                {
                    File.Delete(path);
                }

                _current = -1;
                _currentBytes = 0;
            }
        }
        catch (Exception)
        {
            // A file that cannot be deleted now is deleted at the next check.
        }
    }

    /// <summary>The last <paramref name="maxLines"/> lines (at most <paramref name="maxBytes"/> bytes), oldest first, across both files. Reads only the end of the files. Never throws.</summary>
    public string Tail(int maxLines = DefaultTailLines, int maxBytes = CrashReportLimits.LogTailBytes)
    {
        try
        {
            return ReadTail(Snapshot(), maxLines, maxBytes);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Like <see cref="Tail"/>, but the files' extents are taken now and the end is read on a background thread: lines written after this call are not part of it,
    /// so the previous run's tail can be read at start-up without blocking the UI thread and without the new run's lines getting in. Never throws.
    /// </summary>
    public Task<string> TailInBackground(int maxLines = DefaultTailLines, int maxBytes = CrashReportLimits.LogTailBytes)
    {
        try
        {
            Extent[] extents = Snapshot();
            return Task.Run(() => ReadTailQuietly(extents, maxLines, maxBytes));
        }
        catch (Exception)
        {
            return Task.FromResult(string.Empty);
        }
    }

    // The files as they are now, older first: flushed, and their lengths taken under the lock so a later append is outside them.
    private Extent[] Snapshot()
    {
        lock (_gate)
        {
            _writer?.Flush();
            int newest = _writer is not null ? _current : NewestIndex();
            int older = newest == 0 ? 1 : 0;
            return [new Extent(_paths[older], LengthOf(_paths[older])), new Extent(_paths[newest], LengthOf(_paths[newest]))];
        }
    }

    private static long LengthOf(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

    private static string ReadTailQuietly(Extent[] extents, int maxLines, int maxBytes)
    {
        try
        {
            return ReadTail(extents, maxLines, maxBytes);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    // Reads at most maxBytes from the end of the newest file and, only if that is the whole file and room is left, from the end of the older one.
    private static string ReadTail(Extent[] extents, int maxLines, int maxBytes)
    {
        Extent older = extents[0];
        Extent newest = extents[1];
        List<string> lines = ReadEndLines(newest, maxBytes, out bool wholeFile, out int used);
        if (wholeFile && used < maxBytes)
        {
            lines.InsertRange(0, ReadEndLines(older, maxBytes - used, out _, out _));
        }

        string tail = string.Join('\n', lines.Skip(Math.Max(0, lines.Count - maxLines)));
        return TextCap.Tail(tail, maxBytes);
    }

    // The complete lines in the last wantBytes bytes of the first extent.Length bytes of a file (a partial first line is dropped unless it starts the file).
    private static List<string> ReadEndLines(Extent extent, int wantBytes, out bool wholeFile, out int used)
    {
        wholeFile = true;
        used = 0;
        var lines = new List<string>();
        if (extent.Length <= 0 || wantBytes <= 0 || !File.Exists(extent.Path))
        {
            return lines;
        }

        long start = Math.Max(0, extent.Length - wantBytes);
        wholeFile = start == 0;
        var buffer = new byte[extent.Length - start];
        using (var stream = new FileStream(extent.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            stream.Seek(start, SeekOrigin.Begin);
            used = stream.Read(buffer, 0, buffer.Length);
        }

        string text = Encoding.UTF8.GetString(buffer, 0, used);
        lines.AddRange(text.Split('\n').Select(line => line.TrimEnd('\r')));
        if (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (!wholeFile && lines.Count > 0)
        {
            lines.RemoveAt(0); // starts mid-line
        }

        return lines;
    }

    private readonly record struct Extent(string Path, long Length);

    // Call with _gate held.
    private StreamWriter Writer()
    {
        if (_writer is not null)
        {
            return _writer;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_paths[0])!);
        _current = NewestIndex();
        var stream = new FileStream(_paths[_current], FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _currentBytes = stream.Length;
        return _writer = new StreamWriter(stream, new UTF8Encoding(false));
    }

    // Call with _gate held: close the full file, start the other one empty.
    private void Swap()
    {
        CloseQuietly();
        int next = _current == 0 ? 1 : 0;
        using (new FileStream(_paths[next], FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
        {
        }

        _current = next;
        _currentBytes = 0;
        _writer = new StreamWriter(new FileStream(_paths[next], FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false));
    }

    private void CloseQuietly()
    {
        try
        {
            _writer?.Dispose();
        }
        catch (Exception)
        {
            // Closing a broken writer can fail; it is dropped either way.
        }

        _writer = null;
    }

    // The file written last is the current one; with none, the first.
    private int NewestIndex()
    {
        DateTime first = File.Exists(_paths[0]) ? File.GetLastWriteTimeUtc(_paths[0]) : DateTime.MinValue;
        DateTime second = File.Exists(_paths[1]) ? File.GetLastWriteTimeUtc(_paths[1]) : DateTime.MinValue;
        return second > first ? 1 : 0;
    }
}
