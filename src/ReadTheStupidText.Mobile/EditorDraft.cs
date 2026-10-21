namespace ReadTheStupidText.Mobile;

/// <summary>
/// Holds the reader's editor text across a language change (Slice 53), which rebuilds the shell and
/// so the <see cref="ReaderPage"/>. The old page stores its text, the new one takes it once.
/// </summary>
public sealed class EditorDraft
{
    private string? _text;

    public void Store(string? text) => _text = text;

    /// <summary>The stored text, cleared so a later fresh page does not restore a stale draft.</summary>
    public string? Take()
    {
        string? text = _text;
        _text = null;
        return text;
    }
}
