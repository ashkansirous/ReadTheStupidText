namespace ReadTheStupidText.Mobile;

/// <summary>
/// Holds the reader's editor text across a language change (Slice 53), which rebuilds the shell and
/// so the <see cref="ReaderPage"/>. The old page stores its text, the new one takes it once.
/// </summary>
public sealed class EditorDraft
{
    private string? _text;
    private string? _original;
    private Action? _prepareForRebuild;

    /// <summary>
    /// Registered by the live reader page, so the Settings page (which cannot see it) can ask it to stop
    /// reading and store its text before a language change rebuilds the shell (Slice 99).
    /// </summary>
    public void RegisterRebuildHook(Action prepareForRebuild) => _prepareForRebuild = prepareForRebuild;

    /// <summary>Runs the registered hook once and drops it, so the retired page is not kept alive or called again.</summary>
    public void PrepareForRebuild()
    {
        Action? hook = _prepareForRebuild;
        _prepareForRebuild = null;
        hook?.Invoke();
    }

    public void Store(string? text, string? original = null)
    {
        _text = text;
        _original = original;
    }

    /// <summary>The text a stored translation came from (Slice 101), cleared like the text; call after <see cref="Take"/>.</summary>
    public string? TakeOriginal()
    {
        string? original = _original;
        _original = null;
        return original;
    }

    /// <summary>The stored text, cleared so a later fresh page does not restore a stale draft.</summary>
    public string? Take()
    {
        string? text = _text;
        _text = null;
        return text;
    }
}
