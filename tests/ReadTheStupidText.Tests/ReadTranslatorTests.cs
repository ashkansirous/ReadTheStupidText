using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Domain.Sanitizing;

namespace ReadTheStupidText.Tests;

public class ReadTranslatorTests
{
    private const string Original = "Ceci est un texte.";

    [Fact]
    public async Task Toggle_off_returns_the_original_without_touching_the_engine()
    {
        var translator = new FakeTranslator("translated");
        ReadTranslator subject = Subject(translator, "fr", enabled: false);

        ReadTranslationResult result = await subject.PrepareAsync(Original, 1, CancellationToken.None);

        Assert.Equal(TranslationOutcome.NotRequested, result.Outcome);
        Assert.Equal(Original, result.Text);
        Assert.Equal(0, translator.Calls);
    }

    [Fact]
    public async Task No_target_language_yet_returns_the_original()
    {
        ReadTranslator subject = Subject(new FakeTranslator("translated"), "fr", language: null);

        ReadTranslationResult result = await subject.PrepareAsync(Original, 1, CancellationToken.None);

        Assert.Equal(TranslationOutcome.NotRequested, result.Outcome);
    }

    [Fact]
    public async Task A_detected_foreign_text_is_translated()
    {
        ReadTranslator subject = Subject(new FakeTranslator("This is a text."), "fr");

        ReadTranslationResult result = await subject.PrepareAsync(Original, 1, CancellationToken.None);

        Assert.Equal(TranslationOutcome.Translated, result.Outcome);
        Assert.Equal("This is a text.", result.Text);
        Assert.Equal("fr", result.Source);
        Assert.Equal("en", result.Target);
    }

    [Fact]
    public async Task The_source_the_user_picked_beats_detection()
    {
        var translator = new FakeTranslator("translated");
        ReadTranslator subject = Subject(translator, "fr", source: "es");

        ReadTranslationResult result = await subject.PrepareAsync(Original, 1, CancellationToken.None);

        Assert.Equal("es", result.Source);
        Assert.Equal("es", translator.LastSource);
    }

    [Fact]
    public async Task Text_already_in_the_target_language_is_left_alone()
    {
        ReadTranslator subject = Subject(new FakeTranslator("translated"), "en");

        ReadTranslationResult result = await subject.PrepareAsync("Hello there", 1, CancellationToken.None);

        Assert.Equal(TranslationOutcome.SameLanguage, result.Outcome);
        Assert.False(result.IsNotable);
    }

    [Fact]
    public async Task An_undetectable_language_reads_the_original()
    {
        ReadTranslator subject = Subject(new FakeTranslator("translated"), null);

        ReadTranslationResult result = await subject.PrepareAsync(Original, 1, CancellationToken.None);

        Assert.Equal(TranslationOutcome.SourceUnknown, result.Outcome);
        Assert.Equal(Original, result.Text);
    }

    [Fact]
    public async Task A_pair_the_engine_does_not_cover_reads_the_original()
    {
        ReadTranslator subject = Subject(new FakeTranslator("translated"), "de");

        ReadTranslationResult result = await subject.PrepareAsync(Original, 1, CancellationToken.None);

        Assert.Equal(TranslationOutcome.PairUnsupported, result.Outcome);
    }

    [Fact]
    public async Task A_pack_that_is_not_installed_reads_the_original()
    {
        ReadTranslator subject = Subject(new FakeTranslator("translated") { ModelReady = false }, "fr");

        ReadTranslationResult result = await subject.PrepareAsync(Original, 1, CancellationToken.None);

        Assert.Equal(TranslationOutcome.ModelMissing, result.Outcome);
        Assert.Equal(Original, result.Text);
    }

    [Fact]
    public async Task A_model_exception_during_translation_reads_the_original()
    {
        var translator = new FakeTranslator("translated") { Failure = new TranslationModelUnavailableException("gone") };
        ReadTranslator subject = Subject(translator, "fr");

        ReadTranslationResult result = await subject.PrepareAsync(Original, 1, CancellationToken.None);

        Assert.Equal(TranslationOutcome.ModelMissing, result.Outcome);
    }

    [Fact]
    public async Task Any_other_engine_failure_reads_the_original()
    {
        var translator = new FakeTranslator("translated") { Failure = new InvalidOperationException("boom") };
        ReadTranslator subject = Subject(translator, "fr");

        ReadTranslationResult result = await subject.PrepareAsync(Original, 1, CancellationToken.None);

        Assert.Equal(TranslationOutcome.Failed, result.Outcome);
        Assert.Equal(Original, result.Text);
    }

    [Fact]
    public async Task An_empty_translation_counts_as_a_failure()
    {
        ReadTranslator subject = Subject(new FakeTranslator("   "), "fr");

        ReadTranslationResult result = await subject.PrepareAsync(Original, 1, CancellationToken.None);

        Assert.Equal(TranslationOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task A_slow_translation_times_out_and_reads_the_original()
    {
        var translator = new FakeTranslator("translated") { Gate = new TaskCompletionSource<string>() };
        ReadTranslator subject = Subject(translator, "fr");
        subject.Budget = TimeSpan.FromMilliseconds(50);

        ReadTranslationResult result = await subject.PrepareAsync(Original, 1, CancellationToken.None);

        Assert.Equal(TranslationOutcome.TimedOut, result.Outcome);
        Assert.Equal(Original, result.Text);
    }

    [Fact]
    public async Task The_callers_own_cancellation_is_not_swallowed()
    {
        var translator = new FakeTranslator("translated") { Gate = new TaskCompletionSource<string>() };
        ReadTranslator subject = Subject(translator, "fr");
        using var cts = new CancellationTokenSource();

        Task<ReadTranslationResult> pending = subject.PrepareAsync(Original, 1, cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    private static ReadTranslator Subject(FakeTranslator translator, string? detected, bool enabled = true, string? language = "en", string? source = null)
    {
        var settings = new TestSettings { TranslateEnabled = enabled, Language = language, TranslateSourceLanguage = source };
        return new ReadTranslator(translator, new FakeDetector(detected), settings, new SilentLog());
    }
}

/// <summary>Fakes for the translation flow, shared with the read-service tests.</summary>
internal static class TranslationTestKit
{
    public static ReadTranslator Translator(ISettingsStore settings, string translation, string detectedTag, bool modelReady = true, bool available = true) =>
        new(new FakeTranslator(translation) { ModelReady = modelReady, IsAvailable = available }, new FakeDetector(detectedTag), settings, new SilentLog());

    /// <summary>A translator whose result is only released when <paramref name="translation"/> completes.</summary>
    public static ReadTranslator Translator(ISettingsStore settings, Task<string> translation, string detectedTag)
    {
        var gate = new TaskCompletionSource<string>();
        _ = translation.ContinueWith(t => gate.TrySetResult(t.Result), TaskScheduler.Default);
        return new(new FakeTranslator("unused") { Gate = gate }, new FakeDetector(detectedTag), settings, new SilentLog());
    }
}

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
