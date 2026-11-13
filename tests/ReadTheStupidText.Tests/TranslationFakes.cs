using Sirous.Reading.Application.Translation;

namespace ReadTheStupidText.Tests;

// Slice 368: moved out of ReadTranslatorTests.cs (RTST-only) so FallbackSinkTests, now in Shared.Tests, can use them too.
// The file is linked into both test projects.
internal sealed class FakeTranslator(string result) : ITranslator
{
    private static readonly string[] Languages = ["en", "fa", "fr", "es", "pt", "zh", "ar", "ru", "ja"];

    public IReadOnlyList<string> SupportedLanguages => Languages;

    public bool IsAvailable { get; set; } = true;

    public bool ModelReady { get; set; } = true;

    public Exception? Failure { get; set; }

    public TaskCompletionSource<string>? Gate { get; set; }

    public int Calls { get; private set; }

    public string? LastSource { get; private set; }

    public IReadOnlyList<string> Packs { get; set; } = [];

    public IReadOnlyList<string> RequiredPackIds(string source, string target) => Packs;

    public Task<bool> IsModelReadyAsync(string source, string target) => Task.FromResult(ModelReady);

    public async Task<string> TranslateAsync(string text, string source, string target, CancellationToken cancellationToken)
    {
        Calls++;
        LastSource = source;
        if (Failure is not null)
        {
            throw Failure;
        }

        if (Gate is not null)
        {
            return await Gate.Task.WaitAsync(cancellationToken);
        }

        return result;
    }
}

internal sealed class FakeDetector(string? tag) : ILanguageDetector
{
    public Task<DetectedLanguage?> DetectAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult(tag is null ? null : new DetectedLanguage(tag, 0.95f));
}
