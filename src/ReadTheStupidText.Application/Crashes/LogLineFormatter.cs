using System.Text;
using System.Text.RegularExpressions;

namespace ReadTheStupidText.Application.Crashes;

/// <summary>
/// Turns one system-log call into one line of the rolling log file (Slice 227). The file goes out with a crash report, so a line holds what the
/// code wrote plus an exception's type, a scrubbed message and its stack. In a message every quoted piece of text ('...', "...", «...», “...”) becomes
/// <c>&lt;text&gt;</c> and every other file path <c>&lt;path&gt;</c>, and the message is cut to <see cref="MaxMessageChars"/>: a library error often quotes the
/// text it choked on (a sentence being read, a book title, a file name the person chose). Log calls themselves must never carry typed or read text (audited per call site).
/// </summary>
public static partial class LogLineFormatter
{
    /// <summary>One line is never longer than this many characters.</summary>
    public const int MaxLineChars = 2000;

    /// <summary>A scrubbed exception message is never longer than this many characters, in a log line and in a report.</summary>
    public const int MaxMessageChars = 200;

    private const int MaxInnerExceptions = 3;
    private const string PathPlaceholder = "<path>";
    private const string TextPlaceholder = "<text>";
    private const string LineJoin = " | ";

    // A quoted piece of text in any of the four quote styles. A straight apostrophe counts only at a word edge, so "can't" is not a quote.
    [GeneratedRegex(@"""[^""\r\n]*""|(?<![\p{L}\p{N}])'[^'\r\n]*'(?![\p{L}\p{N}])|«[^»\r\n]*»|“[^”\r\n]*”")]
    private static partial Regex QuotedText();

    // A file path: a file:// URL, a Windows drive or UNC path, any token with a backslash, or an absolute Unix path of two or more segments that does not continue
    // a word or a URL (so "HTTP/1.1", "and/or", "1/2", "https://host/a/b" and version strings stay readable).
    [GeneratedRegex(@"file://[^\s""'()<>]*|(?<![\w])[A-Za-z]:[\\/][^\s""'()<>]*|\\\\[^\s""'()<>]*|[^\s""'()<>]*\\[^\s""'()<>]*|(?<![\w/:.\-])/[^\s""'()<>/]+(?:/[^\s""'()<>/]*)+")]
    private static partial Regex BarePath();

    /// <summary>The line for a call: time, level letter, optional activity id, the message and the exception detail; no line break inside.</summary>
    public static string Format(DateTimeOffset time, char level, string message, int? activityId, Exception? error)
    {
        var line = new StringBuilder();
        line.Append(time.UtcDateTime.ToString("HH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture)).Append(' ').Append(level).Append(' ');
        if (activityId is { } id)
        {
            line.Append('[').Append(id).Append("] ");
        }

        line.Append(message);
        if (error is not null)
        {
            line.Append(" (").Append(Describe(error)).Append(')');
        }

        return OneLine(line.ToString());
    }

    /// <summary>The exception as one scrubbed string: type and message for it and its inner exceptions, then the stack of the outermost.</summary>
    public static string Describe(Exception error)
    {
        var text = new StringBuilder();
        Exception? current = error;
        for (int depth = 0; current is not null && depth <= MaxInnerExceptions; depth++, current = current.InnerException)
        {
            text.Append(depth == 0 ? string.Empty : " <- ").Append(current.GetType().FullName).Append(": ").Append(ScrubMessage(current.Message));
        }

        if (!string.IsNullOrEmpty(error.StackTrace))
        {
            text.Append(LineJoin).Append(error.StackTrace);
        }

        return text.ToString();
    }

    /// <summary>The message with every quoted piece of text and every file path replaced by a placeholder, cut to <paramref name="maxChars"/> characters.</summary>
    public static string ScrubMessage(string? message, int maxChars = MaxMessageChars)
    {
        string unquoted = QuotedText().Replace(message ?? string.Empty, TextPlaceholder);
        string scrubbed = BarePath().Replace(unquoted, PathPlaceholder);
        return CutChars(scrubbed, maxChars);
    }

    // Cut on a whole character: never between the two halves of a surrogate pair.
    private static string CutChars(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }

        int end = char.IsHighSurrogate(text[maxChars - 1]) ? maxChars - 1 : maxChars;
        return text[..end];
    }

    /// <summary>Joins the lines of <paramref name="text"/> with a separator and cuts the result to <see cref="MaxLineChars"/>.</summary>
    public static string OneLine(string text)
    {
        string joined = text.Replace("\r\n", LineJoin).Replace("\n", LineJoin).Replace("\r", LineJoin);
        return CutChars(joined, MaxLineChars);
    }

    /// <summary>The kind of file a path names (its extension), never its name: the only thing a log may say about a file the person chose.</summary>
    public static string FileKind(string? path)
    {
        string extension = Path.GetExtension(path ?? string.Empty);
        return extension.Length == 0 ? "no extension" : extension.ToLowerInvariant();
    }
}
