namespace ReadTheStupidText.Application.Usage;

/// <summary>
/// A translator that can fetch its language model apart from translating (ML Kit on the phone), so the wait for a download
/// and the translation itself are timed as two stages (Slice 137). Without it the whole wait counts as translating.
/// </summary>
public interface IModelPreparer
{
    /// <summary>Returns once the models for the pair are on the device, downloading them when they are not.</summary>
    /// <exception cref="Translation.TranslationModelUnavailableException">A model could not be downloaded.</exception>
    Task EnsureModelAsync(string source, string target, CancellationToken cancellationToken);
}
