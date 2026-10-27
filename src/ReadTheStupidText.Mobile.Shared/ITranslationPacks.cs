namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// The on-device translation language packs, apart from translating (Slice 159, Decision 164): Dino Reader asks before a pack
/// downloads, so an engine that implements this can be told to stop fetching packs by itself. Kept off the Application
/// <c>ITranslator</c> so Read The Stupid Text does not rebuild.
/// </summary>
public interface ITranslationPacks
{
    /// <summary>Whether translating fetches a missing pack by itself (the default, as Read The Stupid Text expects). Dino turns it off.</summary>
    bool AutoDownload { get; set; }

    /// <summary>Whether the pack for <paramref name="language"/> (a language code) is on the device.</summary>
    Task<bool> IsPackReadyAsync(string language);

    /// <summary>Fetches the pack for <paramref name="language"/>. <paramref name="progress"/> gets 0..1 when the engine knows it.</summary>
    /// <exception cref="ReadTheStupidText.Application.Translation.TranslationModelUnavailableException">The pack could not be downloaded.</exception>
    Task DownloadPackAsync(string language, IProgress<double>? progress, CancellationToken cancellationToken);
}
