namespace ReadTheStupidText.Application.Translation;

/// <summary>
/// The pure decisions of the phone's review page, wizard page 2 (Slice 101): whether the translate
/// checkbox shows, whether the big button translates, and which language the text is then read in.
/// </summary>
public static class ReviewDecisions
{
    /// <summary>
    /// The checkbox shows only when the text's detected language differs from the app's and the device can
    /// translate that pair. An unknown detection (null) never shows it.
    /// </summary>
    public static bool ShowTranslateOption(string? detected, string target, bool canTranslatePair) =>
        canTranslatePair && TranslationFlow.ShouldTranslate(true, detected, target);

    /// <summary>The big button translates only when the checkbox is on screen and ticked; otherwise it just reads.</summary>
    public static bool TranslatesOnRead(bool optionShown, bool ticked) => optionShown && ticked;

    /// <summary>
    /// Play before the language was looked at (Slice 211): detection runs at once instead of being skipped, so the same text
    /// reads the same however fast Play is tapped. Not needed without a detector, without text, or once a pass has finished
    /// for this text (a failed pass counts: it falls back to the plain read, as before).
    /// </summary>
    public static bool NeedsDetectionAtRead(bool hasDetector, bool detectionSettled, string? text) =>
        hasDetector && !detectionSettled && !string.IsNullOrWhiteSpace(text);

    /// <summary>
    /// The language the read text is spoken in, handed to the main page so its voice follows at once: the
    /// target when the text was translated, else what the detector found (null when it was unsure).
    /// </summary>
    public static string? ReadLanguage(bool translated, string? detected, string target) =>
        translated ? target : detected;
}

/// <summary>
/// The text on the review page and, once it holds a translation, the text it came from, so "Show original"
/// restores it exactly (Slice 101). Immutable: every change returns a new state.
/// </summary>
public sealed record ReviewTextState(string Text, string? Original = null)
{
    /// <summary>Whether <see cref="Text"/> is a translation that can be undone.</summary>
    public bool HasOriginal => Original is not null;

    /// <summary>The state after translating: the current text becomes the original (an older original is kept).</summary>
    public ReviewTextState Translated(string translation) => new(translation, Original ?? Text);

    /// <summary>The state with the original text back and nothing left to restore.</summary>
    public ReviewTextState ShowOriginal() => new(Original ?? Text);

    /// <summary>The state after the user edited the translation: the original stays.</summary>
    public ReviewTextState Edited(string text) => this with { Text = text };
}
