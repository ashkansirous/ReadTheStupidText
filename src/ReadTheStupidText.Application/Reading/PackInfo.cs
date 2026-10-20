namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// One entry of the <c>packs.json</c> catalog (Decision 62). <see cref="Languages"/>
/// lists every language code the pack serves (a multilingual voice serves several).
/// </summary>
public sealed record PackInfo(
    string Id,
    IReadOnlyList<string> Languages,
    PackKind Kind,
    string Version,
    long SizeBytes,
    string Sha256,
    string License,
    string Url);
