namespace ReadTheStupidText.Application.Reading;

/// <summary>What a downloadable language pack contains (Decision 62).</summary>
public enum PackKind
{
    Voice,
    Ocr,
    Translation,

    /// <summary>
    /// Dino's storyteller voices (Slice 114, Decision 118). A kind of its own, not <see cref="Voice"/>, so a
    /// language lookup for the text reader (RTST) never picks it; an older app skips the unknown kind in the catalog.
    /// </summary>
    Storyteller,
}
