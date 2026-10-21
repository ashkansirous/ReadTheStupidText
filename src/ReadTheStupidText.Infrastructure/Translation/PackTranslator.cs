using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;

namespace ReadTheStupidText.Infrastructure.Translation;

/// <summary>
/// The Windows <see cref="ITranslator"/> (Slice 57): translation runs from packs installed under the
/// packs folder, pivoting through English. The pack-backed inference engine is not part of this build
/// yet (docs/plan/slices/57-windows-localization-translation.md, Translation research), so a pair
/// whose packs are missing, or present but unusable, raises <see cref="TranslationModelUnavailableException"/>
/// and the read falls back to the original text with a message.
/// </summary>
public sealed class PackTranslator : ITranslator
{
    private static readonly string[] Languages = Enum.GetValues<AppLanguage>().Select(AppLanguages.Code).ToArray();

    private readonly Func<string> _packsRoot;

    public PackTranslator(Func<string> packsRoot)
    {
        _packsRoot = packsRoot;
    }

    /// <summary>False until the pack inference engine ships (a follow-up slice): translation stays invisible and inert.</summary>
    public bool IsAvailable => false;

    public IReadOnlyList<string> SupportedLanguages => Languages;

    public Task<bool> IsModelReadyAsync(string source, string target) =>
        Task.FromResult(TranslationPacks.IsInstalled(_packsRoot(), source, target));

    public Task<string> TranslateAsync(string text, string source, string target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string message = TranslationPacks.IsInstalled(_packsRoot(), source, target)
            ? $"No translation engine can run the installed {source}-{target} packs in this build."
            : $"The translation pack for {source} to {target} is not installed.";
        throw new TranslationModelUnavailableException(message);
    }
}
