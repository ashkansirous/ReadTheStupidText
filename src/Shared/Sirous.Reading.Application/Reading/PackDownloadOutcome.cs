namespace Sirous.Reading.Application.Reading;

/// <summary>
/// Slice 357: a pack download's result together with what the server answered when that very run failed (404 not found,
/// 429 too many tries, 5xx ...), null when it did not fail or failed for another reason (offline, a bad zip).
/// </summary>
public sealed record PackDownloadOutcome(PackDownloadResult Status, Net.HttpErrorKind? ServerAnswer = null);
