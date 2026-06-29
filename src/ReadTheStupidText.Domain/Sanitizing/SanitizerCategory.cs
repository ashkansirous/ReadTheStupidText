namespace ReadTheStupidText.Domain.Sanitizing;

/// <summary>
/// The kinds of "noise" the text sanitizer can strip before a read (and before
/// logging). A flags enum so each category is toggled independently;
/// <see cref="All"/> — the default — enables every category. Persisted as its
/// integer value in settings.
/// </summary>
[Flags]
public enum SanitizerCategory
{
    /// <summary>Nothing is rewritten — text is read verbatim.</summary>
    None = 0,

    /// <summary>http(s):// and www. links → "&lt;page&gt; on &lt;host&gt;".</summary>
    Urls = 1 << 0,

    /// <summary>Email addresses → "an email address".</summary>
    Emails = 1 << 1,

    /// <summary>password=/token= assignments, bearer tokens and high-entropy keys.</summary>
    Secrets = 1 << 2,

    /// <summary>Long digit runs → "a card number" / "a phone number".</summary>
    LongNumbers = 1 << 3,

    /// <summary>Windows / UNC file paths → the file name.</summary>
    FilePaths = 1 << 4,

    /// <summary>GUIDs, commit SHAs and hashes → "an identifier".</summary>
    Identifiers = 1 << 5,

    /// <summary>Markdown / HTML markup and control characters → their plain text.</summary>
    Markup = 1 << 6,

    /// <summary>Every category enabled — the default for a fresh install.</summary>
    All = Urls | Emails | Secrets | LongNumbers | FilePaths | Identifiers | Markup,
}
