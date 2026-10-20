namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// The outcome of a pack download (Slice 50). The UI composes the user-facing
/// message from this; the service never returns copy.
/// </summary>
public enum PackDownloadResult
{
    Ok,
    Offline,
    HashMismatch,
    Failed,

    /// <summary>The pack arrived intact but its contents are wrong; retrying cannot help.</summary>
    InvalidPack,
}
