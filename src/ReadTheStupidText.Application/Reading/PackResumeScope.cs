namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Marks the async flow of an automatic restart (Slice 182), so a pack download that starts inside it can tell it was not the user's
/// own tap: the notification question (Slice 183) is only asked for a download the user started.
/// </summary>
public static class PackResumeScope
{
    private static readonly AsyncLocal<bool> Automatic = new();

    public static bool IsAutomatic => Automatic.Value;

    /// <summary>Marks the current flow (and tasks started from it) as an automatic restart.</summary>
    public static void Enter() => Automatic.Value = true;
}
