using System.Globalization;
using System.Text;

namespace ReadTheStupidText.Domain.Reading;

/// <summary>
/// The rules sherpa-onnx's Piper reader applies to a pack's <c>tokens.txt</c> (Slice 201). Its
/// <c>ReadTokens</c> (sherpa-onnx piper-phonemize-lexicon.cc, last read at v1.13.8) calls <c>SHERPA_ONNX_EXIT(-1)</c> on
/// a token of more than one code point, on a duplicate token, and on trailing junk after the id, and
/// looks up <c>_</c>, <c>^</c> and <c>$</c> unconditionally. It aborts the whole process natively, so
/// the app checks the file first. A line is <c>&lt;one code point&gt; &lt;id&gt;</c>; a line holding only
/// an id is the space token. A byte-order mark makes the first token two code points, so it fails too, and a <c>&lt;BLNK&gt;</c> line is skipped; an id may be signed.
/// </summary>
public static class PiperTokens
{
    private static readonly string[] RequiredTokens = ["_", "^", "$"];

    private const string BlankToken = "<BLNK>";

    // Strict UTF-8 that keeps a byte-order mark as U+FEFF, as sherpa-onnx's reader sees the raw bytes.
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The first rule the raw file <paramref name="bytes"/> break, or null; a BOM or invalid UTF-8 counts as sherpa sees it.</summary>
    public static string? ProblemInBytes(byte[] bytes)
    {
        try
        {
            return Problem(StrictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            return "the file is not valid UTF-8";
        }
    }

    /// <summary><see cref="ProblemInBytes"/> for the file at <paramref name="path"/>.</summary>
    public static string? ProblemInFile(string path) => ProblemInBytes(File.ReadAllBytes(path));

    /// <summary>True when sherpa-onnx's Piper reader accepts <paramref name="content"/> without aborting.</summary>
    public static bool IsValid(string content) => Problem(content) is null;

    /// <summary>The first rule the file breaks (with its line number), or null when it is sound.</summary>
    public static string? Problem(string content)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        string[] lines = content.Split('\n');
        int count = lines.Length > 0 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
        for (int i = 0; i < count; i++)
        {
            string? problem = LineProblem(lines[i].TrimEnd('\r'), seen);
            if (problem is not null)
            {
                return $"line {i + 1}: {problem}";
            }
        }

        string? missing = RequiredTokens.FirstOrDefault(t => !seen.Contains(t));
        return missing is null ? null : $"missing required token '{missing}'";
    }

    private static string? LineProblem(string line, HashSet<string> seen)
    {
        if (!TrySplit(line, out string token, out string id))
        {
            return "expected '<token> <id>'";
        }

        if (!int.TryParse(id, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
        {
            return "the id is not a number";
        }

        if (token == BlankToken)
        {
            return null; // sherpa skips a Coqui-style <BLNK> line before the duplicate check
        }

        if (token.EnumerateRunes().Count() != 1)
        {
            return $"token '{token}' is not exactly one code point";
        }

        return seen.Add(token) ? null : $"duplicate token '{token}'";
    }

    // "<token> <id>", or "<id>" alone for the space token (sherpa reads a one-field line as a space).
    private static bool TrySplit(string line, out string token, out string id)
    {
        token = " ";
        id = string.Empty;
        string trimmed = line.TrimEnd(' ', '\t');
        int cut = trimmed.LastIndexOf(' ');
        if (trimmed.Length == 0)
        {
            return false;
        }

        if (cut < 0)
        {
            id = trimmed;
            return true;
        }

        id = trimmed[(cut + 1)..];
        string head = trimmed[..cut];
        token = head.Trim(' ', '\t').Length == 0 ? " " : head;
        return true;
    }
}
