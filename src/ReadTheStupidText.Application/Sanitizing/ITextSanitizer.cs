namespace ReadTheStupidText.Application.Sanitizing;

/// <summary>
/// Rewrites "noise" in intercepted text — URLs, secrets, emails, long numbers,
/// file paths, identifiers and markup — into short spoken-friendly summaries
/// before the text is read aloud or written to the diagnostic logs, so a secret
/// is never spoken or persisted. Which categories run is read live from settings;
/// an all-disabled configuration returns the text unchanged.
/// </summary>
public interface ITextSanitizer
{
    /// <summary>Returns the text with every enabled category rewritten.</summary>
    string Sanitize(string text);
}
