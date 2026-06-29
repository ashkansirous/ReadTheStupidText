using System.Text.RegularExpressions;
using ReadTheStupidText.Domain.Sanitizing;

namespace ReadTheStupidText.Infrastructure.Sanitizing;

/// <summary>
/// The pure regex rule set behind <see cref="TextSanitizer"/>. Ordered so
/// structural markup is unwrapped first (a URL inside a markdown link is then
/// seen by the URL rule), then URLs, emails, file paths, secrets, identifiers
/// and finally bare numbers — each rule narrowing what the later ones can match.
/// Every replacement is a short phrase a narrator can read naturally.
/// </summary>
public static class SanitizationRules
{
    private static readonly Regex MarkdownImage = new(@"!\[([^\]]*)\]\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex MarkdownLink = new(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex HtmlTag = new(@"</?[a-zA-Z][^>]*>", RegexOptions.Compiled);
    private static readonly Regex Emphasis = new(@"(\*\*|__|`)", RegexOptions.Compiled);
    private static readonly Regex ControlChars = new("[\u0000-\u0008\u000B\u000C\u000E-\u001F]", RegexOptions.Compiled);

    private static readonly Regex Url = new(@"\b(?:https?://|www\.)[^\s<>""')\]]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Email = new(@"\b[^\s@]+@[^\s@]+\.[^\s@]+\b", RegexOptions.Compiled);

    private static readonly Regex WindowsPath = new(@"[A-Za-z]:\\[^\s""<>|]+", RegexOptions.Compiled);
    private static readonly Regex UncPath = new(@"\\\\[^\s""<>|]+", RegexOptions.Compiled);

    private static readonly Regex KeyedSecret = new(
        @"(?i)\b(password|passwd|pwd|secret|api[_-]?key|apikey|access[_-]?token|token|auth)\b\s*[:=]\s*\S+",
        RegexOptions.Compiled);
    private static readonly Regex BearerToken = new(@"(?i)\bBearer\s+[A-Za-z0-9._\-]+", RegexOptions.Compiled);
    private static readonly Regex EntropyToken = new(@"\b[A-Za-z0-9+/=_\-]{24,}\b", RegexOptions.Compiled);

    private static readonly Regex Guid = new(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.Compiled);
    private static readonly Regex Hash = new(@"\b[0-9a-fA-F]{7,40}\b", RegexOptions.Compiled);

    private static readonly Regex LongNumber = new(@"\b\d[\d \-]{5,}\d\b", RegexOptions.Compiled);
    private static readonly Regex MultiSpace = new(@"[ \t]{2,}", RegexOptions.Compiled);

    /// <summary>The rules in application order. A rule's category gates whether it runs.</summary>
    public static IReadOnlyList<SanitizationRule> All { get; } =
    [
        new(SanitizerCategory.Markup, StripMarkup),
        new(SanitizerCategory.Urls, s => Url.Replace(s, m => DescribeUrl(m.Value))),
        new(SanitizerCategory.Emails, s => Email.Replace(s, "an email address")),
        new(SanitizerCategory.FilePaths, s => UncPath.Replace(WindowsPath.Replace(s, m => FileNameOf(m.Value)), m => FileNameOf(m.Value))),
        new(SanitizerCategory.Secrets, s => BearerToken.Replace(KeyedSecret.Replace(s, DescribeKeyedSecret), "a secret token")),
        new(SanitizerCategory.Identifiers, s => Hash.Replace(Guid.Replace(s, "an identifier"), DescribeHash)),
        new(SanitizerCategory.Secrets, s => EntropyToken.Replace(s, DescribeEntropyToken)),
        new(SanitizerCategory.LongNumbers, s => LongNumber.Replace(s, DescribeNumber)),
    ];

    private static string StripMarkup(string text)
    {
        text = MarkdownImage.Replace(text, "$1");
        text = MarkdownLink.Replace(text, "$1");
        text = HtmlTag.Replace(text, " ");
        text = Emphasis.Replace(text, string.Empty);
        return ControlChars.Replace(text, string.Empty);
    }

    // "page on host": the host (sans leading www.) plus the last path segment,
    // de-slugged and with its extension dropped. Query/scheme are discarded.
    private static string DescribeUrl(string raw)
    {
        string trimmed = raw.TrimEnd('.', ',', ';', ':', '!', '?', ')');
        string withScheme = trimmed.Contains("://") ? trimmed : "http://" + trimmed;
        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out Uri? uri) || uri.Host.Length == 0)
        {
            return "a link";
        }

        string host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        string? page = LastPathSegment(uri.AbsolutePath);
        return page is { Length: > 0 } ? $"{page} on {host}" : host;
    }

    private static string? LastPathSegment(string path)
    {
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        string last = Uri.UnescapeDataString(segments[^1]);
        int dot = last.LastIndexOf('.');
        if (dot > 0)
        {
            last = last[..dot];
        }

        return last.Replace('-', ' ').Replace('_', ' ');
    }

    private static string FileNameOf(string path)
    {
        string[] segments = path.Split('\\', '/');
        string name = segments[^1];
        return name.Length > 0 ? name : "a file path";
    }

    private static string DescribeKeyedSecret(Match match)
    {
        string keyword = match.Groups[1].Value.ToLowerInvariant();
        return keyword.Contains("pass") || keyword == "pwd" ? "a password" : "a secret token";
    }

    // Only a value that mixes hex letters with digits is treated as a hash/SHA, so
    // plain words (no digit) and bare numbers (handled by LongNumber) are left alone.
    private static string DescribeHash(Match match)
    {
        string value = match.Value;
        bool hasHexLetter = value.Any(c => Uri.IsHexDigit(c) && !char.IsDigit(c));
        bool hasDigit = value.Any(char.IsDigit);
        return hasHexLetter && hasDigit ? "an identifier" : value;
    }

    private static string DescribeEntropyToken(Match match)
    {
        string value = match.Value;
        bool mixed = value.Any(char.IsDigit) && value.Any(char.IsLetter);
        return mixed ? "a secret token" : value;
    }

    // Distinguishes a card-length run from a phone-length one by digit count.
    private static string DescribeNumber(Match match)
    {
        int digits = match.Value.Count(char.IsDigit);
        if (digits < 7)
        {
            return match.Value;
        }

        return digits >= 13 ? "a card number" : "a phone number";
    }

    /// <summary>Collapses runs of spaces/tabs left by replacements into one and
    /// trims the ends, so a stripped tag or markup marker leaves no ragged gap.</summary>
    public static string CollapseSpaces(string text) => MultiSpace.Replace(text, " ").Trim();
}
