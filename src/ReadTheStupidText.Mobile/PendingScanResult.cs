namespace ReadTheStupidText.Mobile;

/// <summary>
/// One-shot hand-off for a <see cref="ScanPage"/> OCR result on its way back to
/// <see cref="ReaderPage"/>. Not a Shell query parameter: MAUI Shell's
/// <c>ShellContent.GetOrCreateContent()</c> re-applies whatever query parameters
/// were last stashed for a route on <i>every</i> subsequent call to that route's
/// content — not just the one navigation that sent them — so a
/// <c>[QueryProperty]</c>-based hand-off silently replays the old scan text back
/// into the reader's text box on any later Shell navigation (e.g. opening and
/// leaving the voice picker). A singleton value that is cleared the instant it
/// is read has no state left to replay.
/// </summary>
public sealed class PendingScanResult
{
    private string? _text;

    public void Set(string text) => _text = text;

    public string? TakeAndClear()
    {
        string? text = _text;
        _text = null;
        return text;
    }
}
