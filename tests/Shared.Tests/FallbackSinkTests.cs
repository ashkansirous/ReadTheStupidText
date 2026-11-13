using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Tests;

/// <summary>Slice 273 part B: the shared <see cref="IFallbackSink"/>, and the fallback points of the shared translator and silent-output guard that report to it.</summary>
public sealed class FallbackSinkTests
{
    private const string FrenchText = "Ceci est un texte.";
    private const int Rate = 24000;

    private sealed class RecordingSink : IFallbackSink
    {
        public List<(string Area, string Reason, string? Language)> Reports { get; } = [];

        public void Report(string area, string reason, string? language = null) => Reports.Add((area, reason, language));
    }

    private sealed class FakeRecorder : IUsageRecorder
    {
        private static readonly UsageContext Context = new("1.4.0", "android", 1, new DateTimeOffset(2026, 11, 6, 10, 0, 0, TimeSpan.Zero));

        public List<UsageEvent> Recorded { get; } = [];

        public bool Enabled { get; set; } = true;

        public void Record(Func<UsageContext, UsageEvent?> make)
        {
            if (make(Context) is { } count)
            {
                Recorded.Add(count);
            }
        }
    }

    [Fact]
    public void The_default_sink_does_nothing_and_never_throws() =>
        NoFallbackSink.Instance.Report(FallbackArea.Speech, FallbackReason.NeuralNotReady, "fa");

    [Fact]
    public void The_reporter_is_a_sink_that_queues_a_speech_fallback_once_per_session_and_language()
    {
        var recorder = new FakeRecorder();
        IFallbackSink sink = new FallbackReporter(recorder);

        sink.Report(FallbackArea.Speech, FallbackReason.NeuralNotReady, "fa");
        sink.Report(FallbackArea.Speech, FallbackReason.NeuralNotReady, "fa");

        UsageEvent only = Assert.Single(recorder.Recorded);
        Assert.Equal(("speech", "neural_not_ready", 1, "fa"), (only.FallbackArea, only.FallbackReason, only.Count, only.Language));
    }

    [Fact]
    public void A_language_with_and_without_a_region_is_one_kind_so_a_repeat_is_not_a_second_first_row()
    {
        var recorder = new FakeRecorder();
        var reporter = new FallbackReporter(recorder);

        reporter.Report(FallbackArea.Speech, FallbackReason.NeuralNotReady, "en-US");
        reporter.Report(FallbackArea.Speech, FallbackReason.NeuralNotReady, "en");

        UsageEvent only = Assert.Single(recorder.Recorded);
        Assert.Equal("en", only.Language);
    }

    [Fact]
    public void Every_part_B_area_and_reason_is_accepted_by_the_count()
    {
        var recorder = new FakeRecorder();
        var reporter = new FallbackReporter(recorder);

        foreach (string reason in FallbackReason.All)
        {
            reporter.Report(FallbackArea.Speech, reason);
            reporter.Report(FallbackArea.Translation, reason);
        }

        Assert.Equal(FallbackReason.All.Count * 2, recorder.Recorded.Count);
    }

    [Fact]
    public async Task A_translation_that_times_out_reports_translation_timeout()
    {
        var sink = new RecordingSink();
        var translator = new FakeTranslator("x") { Gate = new TaskCompletionSource<string>() };
        ReadTranslator subject = Subject(translator, sink);
        subject.Budget = TimeSpan.FromMilliseconds(50);

        await subject.PrepareAsync(FrenchText, 1, CancellationToken.None);

        Assert.Equal([(FallbackArea.Translation, FallbackReason.TranslationTimeout, "en")], sink.Reports);
    }

    [Fact]
    public async Task A_translation_pack_that_is_not_there_reports_translation_pack_missing()
    {
        var sink = new RecordingSink();
        ReadTranslator subject = Subject(new FakeTranslator("x") { ModelReady = false }, sink);

        await subject.PrepareAsync(FrenchText, 1, CancellationToken.None);

        Assert.Equal([(FallbackArea.Translation, FallbackReason.TranslationPackMissing, "en")], sink.Reports);
    }

    [Fact]
    public async Task A_translation_that_throws_reports_translation_error()
    {
        var sink = new RecordingSink();
        ReadTranslator subject = Subject(new FakeTranslator("x") { Failure = new InvalidOperationException("boom") }, sink);

        await subject.PrepareAsync(FrenchText, 1, CancellationToken.None);

        Assert.Equal([(FallbackArea.Translation, FallbackReason.TranslationError, "en")], sink.Reports);
    }

    [Fact]
    public async Task A_good_translation_reports_nothing()
    {
        var sink = new RecordingSink();
        ReadTranslator subject = Subject(new FakeTranslator("This is a text."), sink);

        await subject.PrepareAsync(FrenchText, 1, CancellationToken.None);

        Assert.Empty(sink.Reports);
    }

    [Fact]
    public async Task A_streamed_block_that_fails_reports_stream_partial_once()
    {
        var sink = new RecordingSink();
        ReadTranslator subject = Subject(new FakeTranslator("x") { Failure = new InvalidOperationException("boom") }, sink);
        TranslationStream stream = (await subject.PlanStreamAsync("One. Two. Three. Four.", 1, CancellationToken.None))!.Stream!;

        await foreach (string _ in stream.BlocksAsync(CancellationToken.None))
        {
        }

        Assert.Equal(TranslationOutcome.Failed, stream.Outcome);
        Assert.Equal([(FallbackArea.Translation, FallbackReason.StreamPartial, "en")], sink.Reports);
    }

    [Fact]
    public async Task A_silent_take_rescued_by_a_retake_reports_silent_retake()
    {
        var sink = new RecordingSink();
        var takes = new Queue<SpokenTake>([Silence(), Tone()]);

        await SilentOutputGuard.RescueAsync("Ann.", "kokoro:am_liam", 1.0, () => Task.FromResult(takes.Dequeue()), _ => { }, fallbacks: sink);

        Assert.Equal([(FallbackArea.Speech, FallbackReason.SilentRetake, (string?)null)], sink.Reports);
    }

    [Fact]
    public async Task A_voice_that_stays_silent_is_rescued_by_its_sibling_and_reported()
    {
        var sink = new RecordingSink();

        await SilentOutputGuard.RescueAsync(
            "Ann.", "kokoro:am_liam", 1.0, () => Task.FromResult(Silence()), _ => { }, fallback: () => Task.FromResult<SpokenTake?>(Tone()), fallbacks: sink);

        Assert.Equal([(FallbackArea.Speech, FallbackReason.SilentSibling, (string?)null)], sink.Reports);
    }

    [Fact]
    public async Task A_voice_that_stays_silent_with_no_sibling_reports_silent_gave_up()
    {
        var sink = new RecordingSink();

        RescueResult result = await SilentOutputGuard.RescueAsync("Ann.", "kokoro:am_liam", 1.0, () => Task.FromResult(Silence()), _ => { }, fallbacks: sink);

        Assert.True(result.GaveUp);
        Assert.Equal([(FallbackArea.Speech, FallbackReason.SilentGaveUp, (string?)null)], sink.Reports);
    }

    [Fact]
    public async Task A_voiced_first_take_reports_nothing()
    {
        var sink = new RecordingSink();

        await SilentOutputGuard.RescueAsync("Ann.", "kokoro:am_liam", 1.0, () => Task.FromResult(Tone()), _ => { }, fallbacks: sink);

        Assert.Empty(sink.Reports);
    }

    private static ReadTranslator Subject(FakeTranslator translator, IFallbackSink sink)
    {
        var settings = new TestSettings { TranslateEnabled = true, Language = "en" };
        return new ReadTranslator(translator, new FakeDetector("fr"), settings, new SilentLog(), fallbacks: sink);
    }

    private static SpokenTake Silence() => new(new float[Rate / 2], Rate);

    private static SpokenTake Tone()
    {
        float[] samples = new float[Rate / 2];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.5f * MathF.Sin(2 * MathF.PI * 220 * i / Rate);
        }

        return new SpokenTake(samples, Rate);
    }
}
