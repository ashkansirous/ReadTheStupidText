namespace DinoReader.Core;

/// <summary>
/// Slice 352: the one owner of which screen the app is on. A modal screen (the gate, the close question, the drop-language sheet, a wizard step) remembers the screen beneath it, so when the modal closes
/// and the page below raises no appearing event the screen is put back by <see cref="Restore"/>, instead of every later count carrying the modal's name. Pure: no UI, no counting.
/// </summary>
public sealed class UiScreenTracker
{
    private readonly Stack<(string Screen, object? Owner)> _beneath = new();
    private string _current = UiScreens.None;
    private object? _currentOwner;

    /// <summary>The screen now shown, or <see cref="UiScreens.None"/> before the first page.</summary>
    public string Current => _current;

    /// <summary>Makes <paramref name="screen"/> current, shown by <paramref name="owner"/> (the page; a modal one is matched against the page that closes in <see cref="Restore"/>); true when that is a change (the caller counts <c>screen:shown</c> then).</summary>
    public bool Show(string screen, object? owner = null)
    {
        if (_current == screen)
        {
            _currentOwner = owner ?? _currentOwner;
            return false;
        }

        if (!IsModal(screen))
        {
            _beneath.Clear();
        }
        else if (_current != UiScreens.None && !(IsIntro(screen) && IsIntro(_current)))
        {
            _beneath.Push((_current, _currentOwner));
        }

        _current = screen;
        _currentOwner = owner;
        return true;
    }

    /// <summary>When the modal page <paramref name="closed"/> closes: puts back the screen beneath it and returns it. Returns null (nothing restored) when the current screen is not a modal one, or was not shown by <paramref name="closed"/> (another modal closed, or the page beneath already showed itself).</summary>
    public string? Restore(object? closed)
    {
        if (!IsModal(_current) || _beneath.Count == 0 || closed is null || !ReferenceEquals(_currentOwner, closed))
        {
            return null;
        }

        (_current, _currentOwner) = _beneath.Pop();
        return _current;
    }

    private static bool IsModal(string screen) => screen is UiScreens.Gate or UiScreens.CloseQuestion or UiScreens.DropLanguage || IsIntro(screen);

    private static bool IsIntro(string screen) => screen is UiScreens.Intro1 or UiScreens.Intro2 or UiScreens.Intro3 or UiScreens.Intro4;
}

/// <summary>
/// Slice 352: the once-per-launch rule for the first <c>screen:shown</c>. A screen shown before the config says counting is on is dropped by the counter, so the launch count adds it afterwards,
/// but only when no <c>screen:shown</c> was recorded yet. Pure and thread-safe.
/// </summary>
public sealed class ShownCountedThisLaunch
{
    private readonly object _gate = new();
    private bool _counted;

    /// <summary>A <c>screen:shown</c> was actually recorded (not dropped).</summary>
    public void Recorded()
    {
        lock (_gate)
        {
            _counted = true;
        }
    }

    /// <summary>True exactly once, and only while no <c>screen:shown</c> was recorded: the caller then counts the current screen.</summary>
    public bool TryClaim()
    {
        lock (_gate)
        {
            bool claim = !_counted;
            _counted = true;
            return claim;
        }
    }
}

/// <summary>
/// Slice 352: pairs the app's background and resume counts. A resume is a return only after a background was counted, so the resume some platforms raise at cold start is not counted. Pure.
/// </summary>
public sealed class UiLifecycle
{
    private bool _away;

    /// <summary>The app went to the background; always counted.</summary>
    public void Backgrounded() => _away = true;

    /// <summary>True when this resume is a return from a counted background (and so is counted).</summary>
    public bool Resumed()
    {
        bool returned = _away;
        _away = false;
        return returned;
    }
}
