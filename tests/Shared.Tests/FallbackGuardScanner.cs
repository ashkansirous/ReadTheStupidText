using System.Text;
using System.Text.RegularExpressions;

namespace ReadTheStupidText.Tests;

/// <summary>One catch clause the guard looked at: where it is and whether it passes the Slice 273 part E rule.</summary>
internal sealed record CatchFinding(int Line, bool Passes);

/// <summary>
/// Slice 273 part E: finds every <c>catch</c> in a C# source text and decides whether it reports a fallback,
/// carries the <c>// no-fallback: &lt;reason&gt;</c> marker as the first thing in its block, or only rethrows.
/// A small lexer masks comments, strings and chars so braces and words inside them are never read as code.
/// </summary>
internal static class FallbackGuardScanner
{
    internal const string Marker = "no-fallback:";

    private static readonly Regex CatchWord = new(@"\bcatch\b", RegexOptions.Compiled);

    internal const string RecordFallbackCall = @"\bRecordFallback\s*\(";
    internal const string RecordGeneralFallbackCall = @"\bRecordGeneralFallback\s*\(";
    internal const string ReportGeneralFallbackCall = @"\bReportGeneralFallback\s*\(";
    // Any method call (`.` or `?.`) whose argument list names a FallbackArea constant: every sink/reporter call takes one,
    // so no receiver name is read. The argument text stops at `;`, `{` or `}` so it cannot reach the next statement.
    internal const string AreaArgumentCall = @"\??\.\s*\w+\s*(?:<[^<>;]*>)?\s*\([^;{}]*?\bFallbackArea\.";
    // AudioPackStore's own pack-usage report: Report(bookId, language, AudioPackUsage.PackFailed(...)); it passes no FallbackArea.
    internal const string PackUsageReportCall = @"\bReport\s*\(\s*\w+\s*,\s*\w+\s*,\s*AudioPackUsage\.";
    // Part D helper methods that wrap a fallback report (each reports inside); the call site shows no FallbackArea.
    internal const string ReportHelperCalls =
        @"\b(?:ReportRecordingUnplayable|ReportFallback|ReportFontFallbackOnce|ReportConfigFallback)\s*\(";

    private static readonly Regex ReportCall = new(
        $"({RecordFallbackCall}|{RecordGeneralFallbackCall}|{ReportGeneralFallbackCall}|{AreaArgumentCall}|{PackUsageReportCall}|{ReportHelperCalls})",
        RegexOptions.Compiled);

    private static readonly Regex ControlExit = new(@"\b(?:return|break|continue|goto)\b", RegexOptions.Compiled);

    private static readonly Regex MarkerLine = new(@"\A\s*//\s*no-fallback:[ \t]*\S", RegexOptions.Compiled);

    public static IReadOnlyList<CatchFinding> Scan(string source)
    {
        var masked = Mask(source);
        var findings = new List<CatchFinding>();
        foreach (Match match in CatchWord.Matches(masked))
        {
            var open = FindBlockOpen(masked, match.Index + match.Length);
            if (open < 0)
            {
                continue;
            }

            var close = FindMatchingBrace(masked, open);
            var body = masked.Substring(open + 1, close - open - 1);
            var rawFirst = source.Substring(open + 1, Math.Min(close, source.Length) - open - 1);
            var ownBody = BlankNested(body);
            var passes = HasMarker(rawFirst) || ReportCall.IsMatch(ownBody) || EndsInThrow(ownBody);
            findings.Add(new CatchFinding(LineOf(source, match.Index), passes));
        }

        return findings;
    }

    public static int UnmarkedCount(string source) => Scan(source).Count(f => !f.Passes);

    private static bool HasMarker(string rawBlockStart) => MarkerLine.IsMatch(rawBlockStart);

    // Best effort: only a final `throw` statement counts as a rethrow. A conditional throw (`if (x) { throw; }`),
    // `x ?? throw ...` or a throw in an else branch is not understood and needs a report or a marker.
    private static bool EndsInThrow(string maskedBody)
    {
        var trimmed = maskedBody.TrimEnd();
        if (trimmed.Length < 2 || !trimmed.EndsWith(';'))
        {
            return false;
        }

        var boundary = trimmed.LastIndexOfAny([';', '{', '}'], trimmed.Length - 2);
        var last = trimmed[(boundary + 1)..].TrimStart();
        var rethrows = last.StartsWith("throw;", StringComparison.Ordinal) || Regex.IsMatch(last, @"^throw\s+\S");
        return rethrows && !ControlExit.IsMatch(trimmed[..(boundary + 1)]);
    }

    /// <summary>Blanks nested catch blocks and lambda bodies: a report or rethrow there does not belong to this catch.</summary>
    private static string BlankNested(string body)
    {
        var chars = body.ToCharArray();
        foreach (Match match in CatchWord.Matches(body))
        {
            var open = FindBlockOpen(body, match.Index + match.Length);
            if (open >= 0)
            {
                BlankRange(chars, match.Index, FindMatchingBrace(body, open) + 1);
            }
        }

        var arrow = body.IndexOf("=>", StringComparison.Ordinal);
        while (arrow >= 0)
        {
            var end = LambdaEnd(body, arrow + 2);
            BlankRange(chars, arrow, end);
            arrow = body.IndexOf("=>", Math.Max(end, arrow + 2), StringComparison.Ordinal);
        }

        return new string(chars);
    }

    private static void BlankRange(char[] chars, int from, int to)
    {
        for (var i = from; i < Math.Min(to, chars.Length); i++)
        {
            chars[i] = chars[i] == '\n' || chars[i] == '\r' ? chars[i] : ' ';
        }
    }

    private static int LambdaEnd(string body, int from)
    {
        var i = SkipSpace(body, from);
        if (i < body.Length && body[i] == '{')
        {
            return FindMatchingBrace(body, i) + 1;
        }

        var depth = 0;
        for (; i < body.Length; i++)
        {
            if (body[i] is '(' or '[' or '{')
            {
                depth++;
            }
            else if (body[i] is ')' or ']' or '}')
            {
                if (--depth < 0)
                {
                    return i;
                }
            }
            else if ((body[i] == ';' || body[i] == ',') && depth == 0)
            {
                return i;
            }
        }

        return body.Length;
    }

    private static int LineOf(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static int FindBlockOpen(string masked, int from)
    {
        var i = SkipSpace(masked, from);
        if (i < masked.Length && masked[i] == '(')
        {
            i = SkipSpace(masked, FindMatchingParen(masked, i) + 1);
        }

        if (string.CompareOrdinal(masked, i, "when", 0, 4) == 0 && i + 4 < masked.Length && !char.IsLetterOrDigit(masked[i + 4]))
        {
            i = SkipSpace(masked, i + 4);
            if (i < masked.Length && masked[i] == '(')
            {
                i = SkipSpace(masked, FindMatchingParen(masked, i) + 1);
            }
        }

        return i < masked.Length && masked[i] == '{' ? i : -1;
    }

    private static int SkipSpace(string text, int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i;
    }

    private static int FindMatchingParen(string masked, int open) => FindMatching(masked, open, '(', ')');

    private static int FindMatchingBrace(string masked, int open) => FindMatching(masked, open, '{', '}');

    private static int FindMatching(string masked, int open, char up, char down)
    {
        var depth = 0;
        for (var i = open; i < masked.Length; i++)
        {
            if (masked[i] == up)
            {
                depth++;
            }
            else if (masked[i] == down && --depth == 0)
            {
                return i;
            }
        }

        return masked.Length - 1;
    }

    /// <summary>Same length as the input; comment, string and char contents become spaces (newlines stay).</summary>
    internal static string Mask(string s)
    {
        var sb = new StringBuilder(s.Length);
        var i = 0;
        while (i < s.Length)
        {
            i = MaskStep(s, i, sb);
        }

        return sb.ToString();
    }

    private static int MaskStep(string s, int i, StringBuilder sb)
    {
        var c = s[i];
        var next = i + 1 < s.Length ? s[i + 1] : '\0';
        if (c == '/' && next == '/')
        {
            return Blank(s, sb, i, EndOfLine(s, i));
        }

        if (c == '/' && next == '*')
        {
            var end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
            return Blank(s, sb, i, end < 0 ? s.Length : end + 2);
        }

        if (c == '"' || ((c == '$' || c == '@') && IsStringStart(s, i)))
        {
            return MaskString(s, i, sb);
        }

        if (c == '\'')
        {
            return MaskChar(s, i, sb);
        }

        sb.Append(c);
        return i + 1;
    }

    private static bool IsStringStart(string s, int i)
    {
        var j = i;
        while (j < s.Length && (s[j] == '$' || s[j] == '@'))
        {
            j++;
        }

        return j < s.Length && s[j] == '"';
    }

    private static int EndOfLine(string s, int i)
    {
        var end = s.IndexOf('\n', i);
        return end < 0 ? s.Length : end;
    }

    private static int MaskChar(string s, int i, StringBuilder sb)
    {
        var j = i + 1;
        while (j < s.Length && s[j] != '\'' && s[j] != '\n')
        {
            j += s[j] == '\\' ? 2 : 1;
        }

        return Blank(s, sb, i, Math.Min(j + 1, s.Length));
    }

    private static int MaskString(string s, int i, StringBuilder sb)
    {
        var j = i;
        var verbatim = false;
        var dollars = 0;
        while (s[j] != '"')
        {
            verbatim |= s[j] == '@';
            dollars += s[j] == '$' ? 1 : 0;
            j++;
        }

        var quotes = 0;
        while (j + quotes < s.Length && s[j + quotes] == '"')
        {
            quotes++;
        }

        if (quotes >= 3 && !verbatim)
        {
            return MaskRaw(s, i, j, quotes, sb);
        }

        return MaskQuoted(s, i, j + 1, verbatim, dollars > 0, sb);
    }

    private static int MaskRaw(string s, int start, int quoteStart, int quotes, StringBuilder sb)
    {
        var close = s.IndexOf(new string('"', quotes), quoteStart + quotes, StringComparison.Ordinal);
        return Blank(s, sb, start, close < 0 ? s.Length : close + quotes);
    }

    private static int MaskQuoted(string s, int start, int j, bool verbatim, bool interpolated, StringBuilder sb)
    {
        while (j < s.Length)
        {
            if (interpolated && s[j] == '{' && j + 1 < s.Length && s[j + 1] == '{')
            {
                j += 2;
            }
            else if (interpolated && s[j] == '{')
            {
                j = SkipHole(s, j);
            }
            else if (!verbatim && s[j] == '\\')
            {
                j += 2;
            }
            else if (s[j] == '"' && verbatim && j + 1 < s.Length && s[j + 1] == '"')
            {
                j += 2;
            }
            else if (s[j] == '"')
            {
                return Blank(s, sb, start, j + 1);
            }
            else
            {
                j++;
            }
        }

        return Blank(s, sb, start, s.Length);
    }

    /// <summary>Skips an interpolation hole, including nested strings and braces; returns the index after its brace.</summary>
    private static int SkipHole(string s, int open)
    {
        var depth = 0;
        var scratch = new StringBuilder();
        var j = open;
        while (j < s.Length)
        {
            var c = s[j];
            if (c == '"' || c == '\'' || ((c == '$' || c == '@') && IsStringStart(s, j)))
            {
                j = MaskStep(s, j, scratch);
                continue;
            }

            j++;
            depth += c == '{' ? 1 : c == '}' ? -1 : 0;
            if (c == '}' && depth == 0)
            {
                return j;
            }
        }

        return s.Length;
    }

    private static int Blank(string s, StringBuilder sb, int from, int to)
    {
        for (var k = from; k < to; k++)
        {
            sb.Append(s[k] == '\n' || s[k] == '\r' ? s[k] : ' ');
        }

        return to;
    }
}
