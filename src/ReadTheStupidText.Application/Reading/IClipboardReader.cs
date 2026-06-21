namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Reads the current clipboard text. Abstracted so the Application layer can
/// orchestrate a clipboard read without depending on any OS clipboard API.
/// </summary>
public interface IClipboardReader
{
    /// <summary>
    /// Returns the clipboard's text content, or <c>null</c> when the clipboard
    /// holds no readable text.
    /// </summary>
    Task<string?> GetTextAsync();
}
