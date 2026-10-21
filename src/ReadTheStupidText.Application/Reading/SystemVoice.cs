namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The id of the picker entry that reads with the operating system's own voice even after a
/// neural pack is installed (Decision 61). Its user-facing label is platform-specific and lives
/// with each platform's voice picker. Stored as the voice id.
/// </summary>
public static class SystemVoice
{
    public const string Id = "system";
}
