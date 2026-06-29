namespace ReadTheStupidText.Application.Logging;

/// <summary>
/// Exposes the on-disk folder the daily log files are written to, so the UI can
/// open it for the user (the Activity-Log window's "Open logs" button).
/// </summary>
public interface ILogFolder
{
    /// <summary>Absolute path to the directory holding the input/system log files.</summary>
    string Path { get; }
}
