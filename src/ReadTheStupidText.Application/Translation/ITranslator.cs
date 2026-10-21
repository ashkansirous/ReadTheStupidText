namespace ReadTheStupidText.Application.Translation;

/// <summary>
/// Translates text on the device (Slice 54). The engine may download a language model on first
/// use; the text itself never leaves the device.
/// </summary>
public interface ITranslator
{
    /// <summary>Lower-case primary language codes (for example "fa") the engine can translate from and to.</summary>
    IReadOnlyList<string> SupportedLanguages { get; }

    /// <summary>Whether the models for this pair are already on the device, so translating needs no download.</summary>
    Task<bool> IsModelReadyAsync(string source, string target);

    /// <exception cref="TranslationModelUnavailableException">A model is missing and could not be downloaded.</exception>
    Task<string> TranslateAsync(string text, string source, string target, CancellationToken cancellationToken);
}
