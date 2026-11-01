namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Lets the host refuse every read before it starts (Slice 217: while a forced update is pending, reading is paused).
/// <see cref="ReadAloudService"/> asks it on each read path, so a new trigger cannot bypass the pause.
/// </summary>
public interface IReadGate
{
    /// <summary>True while reads must not start.</summary>
    bool IsBlocked { get; }

    /// <summary>Called when a read was refused; <paramref name="automatic"/> is true for a selection or copy that fired on its own.</summary>
    void ReadRefused(bool automatic);
}
