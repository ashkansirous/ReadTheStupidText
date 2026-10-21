namespace ReadTheStupidText.Application.Translation;

/// <summary>
/// Translates text on the device (Slice 54). The engine may download a language model on first
/// use; the text itself never leaves the device.
/// </summary>
public interface ITranslator
{
    /// <summary>
    /// Whether this engine can translate at all in this build (on Windows: into the user's language). When false the UI hides every translate
    /// control and reads are never translated.
    /// </summary>
    bool IsAvailable => true;

    /// <summary>Lower-case primary language codes (for example "fa") the engine can translate from and to.</summary>
    IReadOnlyList<string> SupportedLanguages { get; }

    /// <summary>
    /// Whether the engine has a model for this pair at all (Slice 75): an engine whose languages are not
    /// all reachable from each other (Windows has no Persian, and no English to Japanese) overrides it.
    /// </summary>
    bool SupportsPair(string source, string target) =>
        TranslationFlow.IsPairSupported(SupportedLanguages.ToArray(), source, target);

    /// <summary>The ids of the downloadable packs the pair needs (Windows packs, Slice 75); none when models are not packs.</summary>
    IReadOnlyList<string> RequiredPackIds(string source, string target) => [];

    /// <summary>Whether the models for this pair are already on the device, so translating needs no download.</summary>
    Task<bool> IsModelReadyAsync(string source, string target);

    /// <exception cref="TranslationModelUnavailableException">A model is missing and could not be downloaded.</exception>
    Task<string> TranslateAsync(string text, string source, string target, CancellationToken cancellationToken);
}
