namespace ReadTheStupidText.Mobile.Shared.Waiting;

/// <summary>
/// The extra look of a translation model download wait (Slice 187, frame R12): while <paramref name="Waiting"/> the spinner becomes a no-Wi-Fi icon and
/// the bar goes grey, with a Retry button next to Cancel. A bar shows whenever <paramref name="Progress"/> (0..1) is known. A waiting state, not an error: no red.
/// </summary>
/// <param name="Waiting">True when there is no internet and the download carries on by itself.</param>
/// <param name="Progress">0..1 of the download when the platform reports it, else null (no bar).</param>
/// <param name="RetryText">The Retry button's text; null shows no Retry.</param>
public sealed record WaitLook(bool Waiting, double? Progress, string? RetryText);
