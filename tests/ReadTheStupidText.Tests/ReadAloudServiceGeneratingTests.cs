using ReadTheStupidText.Application.Activity;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Logging;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Sanitizing;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Domain.Activity;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Domain.Sanitizing;

namespace ReadTheStupidText.Tests;

public class ReadAloudServiceGeneratingTests
{
    [Fact]
    public async Task SpeakAsync_returning_without_ever_playing_fails_after_the_grace()
    {
        var harness = new Harness { Speak = (_, _) => Task.CompletedTask };
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(50);
        var raised = new List<bool>();
        harness.Service.GeneratingChanged += (_, value) => raised.Add(value);

        await harness.Service.ReadFileAsync("a.txt");

        ActivityEntry entry = Assert.Single(harness.Log.Entries);
        Assert.Equal(ActivityState.Failed, entry.State);
        Assert.Equal(ActivityReason.Error, entry.Reason);
        Assert.False(harness.Service.IsGeneratingAudio);
        Assert.Equal(new[] { true, false }, raised);
    }

    [Fact]
    public async Task A_blocked_gate_refuses_a_file_read_and_a_play_click()
    {
        var gate = new FakeGate();
        var harness = new Harness(gate: gate);

        await harness.Service.ReadFileAsync("a.txt");
        await harness.Service.PlayPauseOrReadAsync();

        Assert.Empty(harness.Log.Entries);
        Assert.Equal(new[] { false, false }, gate.Refusals);
    }

    [Fact]
    public async Task A_blocked_gate_refuses_a_selection_and_a_skip_but_a_lifted_gate_reads()
    {
        var gate = new FakeGate();
        var harness = new Harness(gate: gate);

        harness.Monitor.RaiseChanged("selected text");
        await harness.Service.SkipForwardAsync();
        Assert.Empty(harness.Log.Entries);
        Assert.Equal(new[] { true }, gate.Refusals);

        gate.IsBlocked = false;
        await harness.Service.ReadFileAsync("a.txt");
        Assert.Single(harness.Log.Entries);
    }

    [Fact]
    public async Task CancelRead_stops_a_read_still_waiting_for_its_first_audio()
    {
        var harness = new Harness();
        var gate = new TaskCompletionSource();
        harness.Speak = (_, _) => gate.Task;

        Task read = harness.Service.ReadFileAsync("a.txt");
        await Task.Delay(30, TestContext.Current.CancellationToken);
        Assert.True(harness.Service.IsGeneratingAudio);

        harness.Service.CancelRead();

        Assert.False(harness.Service.IsGeneratingAudio);
        Assert.Equal(ActivityState.Interrupted, Assert.Single(harness.Log.Entries).State);
        gate.SetResult();
        await read;
    }

    [Fact]
    public async Task A_read_replaced_during_its_start_does_not_stop_the_newer_read()
    {
        var harness = new Harness();
        var gate = new TaskCompletionSource();
        int stopsAfterReplace = -1;
        Task? second = null;
        int calls = 0;
        harness.Speak = (_, _) =>
        {
            if (++calls == 1)
            {
                second = harness.Service.ReadFileAsync("b.txt"); // the newer read takes over while the first is starting
                stopsAfterReplace = harness.Reader.Stops;
            }

            return gate.Task;
        };

        Task first = harness.Service.ReadFileAsync("a.txt");
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.True(stopsAfterReplace >= 0);
        Assert.Equal(stopsAfterReplace, harness.Reader.Stops);
        gate.SetResult();
        await first;
        await second!;
    }

    [Fact]
    public async Task A_cancelled_read_stops_the_reader_once_it_starts_speaking()
    {
        var harness = new Harness();
        var gate = new TaskCompletionSource();
        harness.Speak = (_, _) =>
        {
            harness.Service.CancelRead();
            return gate.Task;
        };

        Task read = harness.Service.ReadFileAsync("a.txt");
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.True(harness.Reader.Stops >= 2);
        gate.SetResult();
        await read;
    }

    [Fact]
    public void CancelRead_with_nothing_active_does_nothing()
    {
        var harness = new Harness();

        harness.Service.CancelRead();

        Assert.False(harness.Service.IsGeneratingAudio);
        Assert.Empty(harness.Log.Entries);
    }

    [Fact]
    public async Task Reader_that_returns_early_then_plays_within_the_grace_is_not_failed()
    {
        var harness = new Harness();
        harness.Service.FirstAudioGrace = TimeSpan.FromSeconds(10);
        harness.Speak = (_, _) =>
        {
            _ = Task.Delay(100, TestContext.Current.CancellationToken).ContinueWith(_ => harness.Reader.RaiseState(PlaybackState.Playing));
            return Task.CompletedTask;
        };

        Task read = harness.Service.ReadFileAsync("a.txt");
        await Task.Delay(30, TestContext.Current.CancellationToken);
        Assert.True(harness.Service.IsGeneratingAudio);
        await read;

        Assert.Equal(ActivityState.Reading, Assert.Single(harness.Log.Entries).State);
        Assert.False(harness.Service.IsGeneratingAudio);
    }

    [Fact]
    public async Task A_read_counts_its_source_voice_and_first_audio_without_the_text()
    {
        var usage = new RecordingUsage();
        var harness = new Harness(usage: usage);
        harness.Settings.Language = "en";
        harness.Speak = (_, _) =>
        {
            harness.Reader.RaiseState(PlaybackState.Playing);
            return Task.CompletedTask;
        };

        await harness.Service.ReadFileAsync("a.txt");

        UsageEvent started = Assert.Single(usage.Events, e => e.Name == UsageEventName.ReadStarted);
        Assert.Equal(UsageSource.Panel, started.Source);
        Assert.Equal(UsageMode.Original, started.Mode);
        Assert.Equal(UsageEngine.Windows, started.Engine);
        Assert.Equal("en", started.Language);
        Assert.Equal("<200", started.LengthBucket);
        Assert.True(started.VoiceDefault);
        UsageEvent timing = Assert.Single(usage.Events, e => e.Name == UsageEventName.Timing);
        Assert.Equal(UsageStage.FirstAudio, timing.Stage);
        Assert.Equal(UsageOutcome.Done, timing.Outcome);
        Assert.DoesNotContain(usage.Events, e => e.Name != UsageEventName.ReadStarted && e.Name != UsageEventName.Timing);
    }

    [Fact]
    public async Task A_read_that_never_plays_counts_a_failed_first_audio_once()
    {
        var usage = new RecordingUsage();
        var harness = new Harness(usage: usage);
        harness.Settings.Language = "en";
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(30);

        await harness.Service.ReadFileAsync("a.txt");

        UsageEvent timing = Assert.Single(usage.Events, e => e.Name == UsageEventName.Timing);
        Assert.Equal(UsageStage.FirstAudio, timing.Stage);
        Assert.Equal(UsageOutcome.Failed, timing.Outcome);
    }

    [Fact]
    public async Task A_read_replaced_while_it_waits_counts_a_cancelled_first_audio()
    {
        var usage = new RecordingUsage();
        var harness = new Harness(usage: usage);
        harness.Settings.Language = "en";
        harness.Service.FirstAudioGrace = TimeSpan.FromSeconds(10);
        var firstWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Speak = (_, _) =>
        {
            firstWaiting.TrySetResult();
            return Task.CompletedTask;
        };

        Task first = harness.Service.ReadFileAsync("a.txt");
        await firstWaiting.Task;
        harness.Monitor.RaiseCleared();
        await first;

        UsageEvent timing = Assert.Single(usage.Events, e => e.Name == UsageEventName.Timing);
        Assert.Equal(UsageOutcome.Cancelled, timing.Outcome);
    }

    [Fact]
    public void Picking_a_voice_counts_a_voice_change_in_the_voice_language()
    {
        var usage = new RecordingUsage();
        var harness = new Harness(usage: usage);
        harness.Settings.Language = "fr";

        harness.Service.SetVoice("not-installed");
        Assert.Empty(usage.Events);

        harness.Service.SetVoice(SystemVoice.Id);

        UsageEvent changed = Assert.Single(usage.Events);
        Assert.Equal(UsageEventName.VoiceChanged, changed.Name);
        Assert.Equal("fr", changed.Language);
        Assert.Equal(UsageEngine.Windows, changed.Engine);
    }

    [Fact]
    public async Task Translation_on_speaks_the_translation_and_shows_it_in_the_reading_box()
    {
        var spoken = new List<string>();
        var shown = new List<string>();
        var harness = new Harness(settings => TranslationTestKit.Translator(settings, "bonjour", "fr"))
        {
            Speak = (text, _) => { spoken.Add(text); return Task.CompletedTask; },
        };
        harness.Settings.Language = "en";
        harness.Settings.TranslateEnabled = true;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);
        ReadTranslationResult? finished = null;
        harness.Service.TranslationFinished += (_, result) => finished = result;
        harness.Service.ReadTextChanged += (_, read) => shown.Add(read.Text);

        await harness.Service.ReadFileAsync("a.txt");

        Assert.Equal(new[] { "bonjour" }, spoken);
        Assert.Equal(new[] { "bonjour" }, shown);
        Assert.Equal(TranslationOutcome.Translated, finished?.Outcome);
    }

    [Fact]
    public async Task Translation_is_inert_while_the_engine_is_unavailable()
    {
        var spoken = new List<string>();
        var harness = new Harness(settings => TranslationTestKit.Translator(settings, "bonjour", "fr", available: false))
        {
            Speak = (text, _) => { spoken.Add(text); return Task.CompletedTask; },
        };
        harness.Settings.Language = "en";
        harness.Settings.TranslateEnabled = true;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);

        await harness.Service.ReadFileAsync("a.txt");

        Assert.False(harness.Service.CanTranslate);
        Assert.False(harness.Service.TranslateEnabled);
        Assert.Equal(new[] { "some text to read" }, spoken);
    }

    [Fact]
    public async Task Translation_off_speaks_the_original()
    {
        var spoken = new List<string>();
        var harness = new Harness(settings => TranslationTestKit.Translator(settings, "bonjour", "fr"))
        {
            Speak = (text, _) => { spoken.Add(text); return Task.CompletedTask; },
        };
        harness.Settings.Language = "en";
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);

        await harness.Service.ReadFileAsync("a.txt");

        Assert.Single(spoken);
        Assert.NotEqual("bonjour", spoken[0]);
    }

    [Fact]
    public async Task A_read_superseded_while_translating_never_speaks()
    {
        var gate = new TaskCompletionSource<string>();
        var spoken = new List<string>();
        var harness = new Harness(settings => TranslationTestKit.Translator(settings, gate.Task, "fr"))
        {
            Speak = (text, _) => { spoken.Add(text); return Task.CompletedTask; },
        };
        harness.Settings.Language = "en";
        harness.Settings.TranslateEnabled = true;

        Task read = harness.Service.ReadFileAsync("a.txt");
        harness.Monitor.RaiseCleared();
        gate.SetResult("late translation");
        await read;

        Assert.Empty(spoken);
        Assert.Equal(ActivityState.Interrupted, Assert.Single(harness.Log.Entries).State);
    }

    [Fact]
    public async Task A_read_superseded_mid_translation_is_dropped_quietly_not_failed()
    {
        var never = new TaskCompletionSource<string>();
        var harness = new Harness(settings => TranslationTestKit.Translator(settings, never.Task, "fr"));
        harness.Settings.Language = "en";
        harness.Settings.TranslateEnabled = true;

        Task read = harness.Service.ReadFileAsync("a.txt");
        harness.Monitor.RaiseCleared();
        await read.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(ActivityState.Interrupted, Assert.Single(harness.Log.Entries).State);
    }

    [Fact]
    public async Task A_missing_translation_pack_reads_the_original_and_says_why()
    {
        var spoken = new List<string>();
        var harness = new Harness(settings => TranslationTestKit.Translator(settings, "x", "fr", modelReady: false))
        {
            Speak = (text, _) => { spoken.Add(text); return Task.CompletedTask; },
        };
        harness.Settings.Language = "en";
        harness.Settings.TranslateEnabled = true;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);
        ReadTranslationResult? finished = null;
        harness.Service.TranslationFinished += (_, result) => finished = result;

        await harness.Service.ReadFileAsync("a.txt");

        Assert.Single(spoken);
        Assert.NotEqual("x", spoken[0]);
        Assert.Equal(TranslationOutcome.ModelMissing, finished?.Outcome);
    }

    [Fact]
    public async Task Deselect_during_generating_raises_false()
    {
        var gate = new TaskCompletionSource();
        var harness = new Harness { Speak = (_, _) => gate.Task };
        var raised = new List<bool>();
        harness.Service.GeneratingChanged += (_, value) => raised.Add(value);

        Task read = harness.Service.ReadFileAsync("a.txt");
        harness.Monitor.RaiseCleared();

        Assert.False(harness.Service.IsGeneratingAudio);
        Assert.Equal(new[] { true, false }, raised);
        Assert.Equal(ActivityState.Interrupted, Assert.Single(harness.Log.Entries).State);

        gate.SetResult();
        await read;
    }

    [Fact]
    public async Task SpeakAsync_throwing_clears_generating_and_fails_the_entry()
    {
        var harness = new Harness { Speak = (_, _) => throw new InvalidOperationException("boom") };
        var raised = new List<bool>();
        harness.Service.GeneratingChanged += (_, value) => raised.Add(value);

        await harness.Service.ReadFileAsync("a.txt");

        Assert.Equal(ActivityState.Failed, Assert.Single(harness.Log.Entries).State);
        Assert.False(harness.Service.IsGeneratingAudio);
        Assert.Equal(new[] { true, false }, raised);
    }

    [Fact]
    public async Task First_Playing_transition_ends_generating_and_moves_the_entry_to_reading()
    {
        var harness = new Harness();
        harness.Speak = (_, _) =>
        {
            harness.Reader.RaiseState(PlaybackState.Playing);
            return Task.CompletedTask;
        };

        await harness.Service.ReadFileAsync("a.txt");

        Assert.False(harness.Service.IsGeneratingAudio);
        Assert.Equal(ActivityState.Reading, Assert.Single(harness.Log.Entries).State);
    }

    [Fact]
    public async Task PlayPauseOrReadAsync_is_a_noop_while_generating()
    {
        var gate = new TaskCompletionSource();
        var harness = new Harness { Speak = (_, _) => gate.Task };

        Task read = harness.Service.ReadFileAsync("a.txt");
        Assert.True(harness.Service.IsGeneratingAudio);

        await harness.Service.PlayPauseOrReadAsync();

        Assert.Single(harness.Log.Entries);
        Assert.Equal(0, harness.Clipboard.Reads);

        gate.SetResult();
        await read;
    }

    [Fact]
    public async Task Auto_read_prepares_the_head_start_before_the_settle_delay_ends()
    {
        var harness = new Harness();
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(10);
        harness.Service.LoneSelectSettleMs = 30_000; // the read cannot start while the prepare is awaited

        harness.Monitor.RaiseChanged("Some selected text.");

        try
        {
            await harness.HeadStart.FirstPrepare.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(["Some selected text."], harness.HeadStart.Prepared);
        }
        finally
        {
            harness.Service.Dispose(); // cancels the pending settle so no timer outlives the test
        }
    }

    [Fact]
    public async Task A_drag_burst_prepares_only_the_text_the_selection_settles_on()
    {
        var spoken = new TaskCompletionSource();
        var harness = new Harness { Speak = (_, _) => { spoken.TrySetResult(); return Task.CompletedTask; } };
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(10);

        harness.Monitor.RaiseChanged("Some");
        harness.Monitor.RaiseChanged("Some selected");
        harness.Monitor.RaiseChanged("Some selected text.");

        await spoken.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(["Some selected text."], harness.HeadStart.Prepared);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\u200B")]
    [InlineData("\uFEFF \u00AD")]
    public async Task A_blank_selection_starts_no_entry_and_reads_nothing(string blank)
    {
        var harness = new Harness();
        harness.Service.LoneSelectSettleMs = 10;

        harness.Monitor.RaiseChanged(blank);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Empty(harness.Log.Entries);
        Assert.Empty(harness.Reader.Spoken);
        Assert.Empty(harness.HeadStart.Prepared);
    }

    [Fact]
    public async Task A_blank_selection_cancels_the_pending_read_of_the_earlier_selection()
    {
        var harness = new Harness();
        harness.Service.LoneSelectSettleMs = 400;

        harness.Monitor.RaiseChanged("Some selected text.");
        harness.Monitor.RaiseChanged("   ");
        await Task.Delay(900, TestContext.Current.CancellationToken);

        Assert.Empty(harness.Reader.Spoken);
    }

    [Fact]
    public async Task A_blank_selection_never_stops_an_auto_read_already_generating()
    {
        var harness = new Harness();
        harness.Service.LoneSelectSettleMs = 10;
        var gate = new TaskCompletionSource();
        harness.Speak = (_, _) => gate.Task;

        harness.Monitor.RaiseChanged("Some selected text.");
        await Task.Delay(200, TestContext.Current.CancellationToken);
        int stops = harness.Reader.Stops;
        harness.Monitor.RaiseChanged("   ");
        harness.Monitor.RaiseChanged("\u200B");

        Assert.Equal(stops, harness.Reader.Stops);
        Assert.Equal(ActivityState.GeneratingAudio, Assert.Single(harness.Log.Entries).State);
        gate.SetResult();
    }

    [Fact]
    public async Task A_blank_selection_never_stops_a_file_read_already_generating()
    {
        var harness = new Harness();
        var gate = new TaskCompletionSource();
        harness.Speak = (_, _) => gate.Task;

        Task read = harness.Service.ReadFileAsync("a.txt");
        await Task.Delay(30, TestContext.Current.CancellationToken);
        int stops = harness.Reader.Stops;
        harness.Monitor.RaiseChanged("  	 ");

        Assert.Equal(stops, harness.Reader.Stops);
        Assert.True(harness.Service.IsGeneratingAudio);
        gate.SetResult();
        await read;
    }

    [Fact]
    public async Task Deselect_before_the_selection_is_stable_prepares_nothing()
    {
        var harness = new Harness();

        harness.Monitor.RaiseChanged("Some selected text.");
        harness.Monitor.RaiseCleared();
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Empty(harness.HeadStart.Prepared);
    }

    [Fact]
    public void Deselect_cancels_the_head_start_prepare()
    {
        var harness = new Harness();

        harness.Monitor.RaiseCleared();

        Assert.Equal(1, harness.HeadStart.Cancels);
    }

    [Fact]
    public async Task A_failing_head_start_never_affects_the_read()
    {
        var spoken = new TaskCompletionSource();
        var harness = new Harness { Speak = (_, _) => { spoken.TrySetResult(); return Task.CompletedTask; } };
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(10);
        harness.HeadStart.Fail = true;

        harness.Monitor.RaiseChanged("Some selected text.");

        await spoken.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_read_of_the_text_auto_read_prepared_reuses_the_early_translation()
    {
        var translator = new FakeTranslator("bonjour");
        var harness = new Harness(settings => new ReadTranslator(translator, new FakeDetector("fr"), settings, new SilentLog()));
        harness.Settings.Language = "en";
        harness.Settings.TranslateEnabled = true;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);
        harness.Reader.Streaming = true;
        harness.DocumentText = LongText;
        harness.Service.LoneSelectSettleMs = 30_000; // the auto-read itself must not start; only its early work

        harness.Monitor.RaiseChanged(LongText);
        await WaitForAsync(() => translator.Calls >= 1 && harness.HeadStart.Prepared.Count >= 1);
        await harness.Service.ReadFileAsync("a.txt");

        // The early call translated the first block; the read translated only the rest (two calls in all, not three).
        Assert.Equal(2, translator.Calls);
        harness.Service.Dispose();
    }

    [Fact]
    public async Task With_translate_on_the_early_original_voicing_prepares_only_the_head()
    {
        var translator = new FakeTranslator("bonjour");
        var harness = new Harness(settings => new ReadTranslator(translator, new FakeDetector("fr"), settings, new SilentLog()));
        harness.Settings.Language = "en";
        harness.Settings.TranslateEnabled = true;
        harness.Service.LoneSelectSettleMs = 30_000;

        harness.Monitor.RaiseChanged(LongText);
        await WaitForAsync(() => translator.Calls >= 1);

        Assert.NotEmpty(harness.HeadStart.Prepared);
        Assert.Equal(EarlyStartPlan.OriginalHead(LongText), harness.HeadStart.Prepared[0]);
        Assert.True(LongText.Length > harness.HeadStart.Prepared[0].Length);
        harness.Service.Dispose();
    }

    [Fact]
    public async Task A_read_of_other_text_drops_the_early_translation()
    {
        var translator = new FakeTranslator("bonjour");
        var harness = new Harness(settings => new ReadTranslator(translator, new FakeDetector("fr"), settings, new SilentLog()));
        harness.Settings.Language = "en";
        harness.Settings.TranslateEnabled = true;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);
        harness.Reader.Streaming = true;
        harness.DocumentText = LongText;
        harness.Service.LoneSelectSettleMs = 30_000; // the auto-read itself must not start; only its early work

        harness.Monitor.RaiseChanged("Another text. It has two sentences. And three. And four.");
        await WaitForAsync(() => translator.Calls >= 1);
        await harness.Service.ReadFileAsync("a.txt");

        Assert.Equal(3, translator.Calls); // the early one, then the read's two blocks
        harness.Service.Dispose();
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        DateTime end = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < end)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private const string LongText = "One is first. Two is second. Three is third. Four is fourth. Five is fifth.";

    [Fact]
    public async Task A_streamed_translation_speaks_the_first_block_then_appends_the_rest_in_order()
    {
        var harness = StreamingHarness(new ScriptedTranslator(), out ScriptedTranslator _);
        harness.Reader.Streaming = true;
        harness.DocumentText = LongText;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);
        ReadTranslationResult? finished = null;
        harness.Service.TranslationFinished += (_, result) => finished = result;
        var shown = new List<string>();
        harness.Service.ReadTextChanged += (_, read) => shown.Add(read.Text);

        await harness.Service.ReadFileAsync("a.txt");

        Assert.Empty(harness.Reader.Spoken);
        Assert.Equal(["<One is first. Two is second.>"], harness.Reader.Streamed);
        Assert.Equal(["<Three is third. Four is fourth. Five is fifth.>"], harness.Reader.Appended);
        Assert.Equal(1, harness.Reader.Completes);
        Assert.Equal(TranslationOutcome.Translated, finished?.Outcome);
        Assert.Equal("<One is first. Two is second.> <Three is third. Four is fourth. Five is fifth.>", shown[^1]);
    }

    [Fact]
    public async Task A_streamed_read_starts_before_the_rest_is_translated()
    {
        var gate = new TaskCompletionSource<string>();
        var translator = new ScriptedTranslator { BlockGates = { [1] = gate } };
        var harness = StreamingHarness(translator, out _);
        harness.Reader.Streaming = true;
        harness.DocumentText = LongText;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);

        Task read = harness.Service.ReadFileAsync("a.txt");
        await harness.Reader.FirstStream.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(["<One is first. Two is second.>"], harness.Reader.Streamed);
        Assert.Empty(harness.Reader.Appended);
        Assert.Equal(0, harness.Reader.Completes);

        gate.SetResult("<rest>");
        await read;
        Assert.Equal(["<rest>"], harness.Reader.Appended);
        Assert.Equal(1, harness.Reader.Completes);
    }

    [Fact]
    public async Task A_block_that_fails_reads_the_rest_in_the_original()
    {
        var translator = new ScriptedTranslator { FailFromBlock = 1 };
        var harness = StreamingHarness(translator, out _);
        harness.Reader.Streaming = true;
        harness.DocumentText = LongText;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);
        ReadTranslationResult? finished = null;
        harness.Service.TranslationFinished += (_, result) => finished = result;

        await harness.Service.ReadFileAsync("a.txt");

        Assert.Equal(["<One is first. Two is second.>"], harness.Reader.Streamed);
        Assert.Equal(["Three is third. Four is fourth. Five is fifth."], harness.Reader.Appended);
        Assert.Equal(1, harness.Reader.Completes);
        Assert.Equal(TranslationOutcome.Failed, finished?.Outcome);
    }

    [Fact]
    public async Task A_streamed_read_that_faults_stops_translating_the_rest()
    {
        var gate = new TaskCompletionSource<string>();
        var translator = new ScriptedTranslator { BlockGates = { [1] = gate } };
        var harness = StreamingHarness(translator, out _);
        harness.Reader.Streaming = true;
        harness.Reader.StreamFault = new InvalidOperationException("device lost");
        harness.DocumentText = LongText;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);

        await harness.Service.ReadFileAsync("a.txt").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(translator.SawCancellation, "the in-flight block translation was not cancelled");
        Assert.Empty(harness.Reader.Appended);
    }

    [Fact]
    public async Task A_reader_that_cannot_start_a_stream_gets_the_whole_translated_text_instead()
    {
        var harness = StreamingHarness(new ScriptedTranslator(), out _);
        harness.Reader.Streaming = true;
        harness.Reader.StreamFault = new StreamingUnavailableException();
        harness.Reader.StreamingOffAfterFault = true;
        harness.DocumentText = LongText;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);

        await harness.Service.ReadFileAsync("a.txt").WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(["<One is first. Two is second. Three is third. Four is fourth. Five is fifth.>"], harness.Reader.Spoken);
    }

    [Fact]
    public async Task The_first_streamed_block_is_marked_untranslated_when_it_failed_to_translate()
    {
        var translator = new ScriptedTranslator { FailFromBlock = 0 };
        var harness = StreamingHarness(translator, out _);
        harness.Reader.Streaming = true;
        harness.DocumentText = LongText;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);
        var shown = new List<ReadText>();
        harness.Service.ReadTextChanged += (_, read) => shown.Add(read);

        await harness.Service.ReadFileAsync("a.txt");

        Assert.False(shown[0].Translated);
        Assert.Equal("One is first. Two is second.", shown[0].Text);
    }

    [Fact]
    public async Task A_reader_that_cannot_stream_gets_the_whole_translated_text()
    {
        var harness = StreamingHarness(new ScriptedTranslator(), out _);
        harness.DocumentText = LongText;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);

        await harness.Service.ReadFileAsync("a.txt");

        Assert.Single(harness.Reader.Spoken);
        Assert.Empty(harness.Reader.Streamed);
    }

    [Fact]
    public async Task A_single_block_text_is_not_streamed()
    {
        var harness = StreamingHarness(new ScriptedTranslator(), out _);
        harness.Reader.Streaming = true;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);

        await harness.Service.ReadFileAsync("a.txt");

        Assert.Empty(harness.Reader.Streamed);
        Assert.Single(harness.Reader.Spoken);
    }

    [Fact]
    public async Task A_streamed_read_superseded_while_translating_stops_appending()
    {
        var gate = new TaskCompletionSource<string>();
        var translator = new ScriptedTranslator { BlockGates = { [1] = gate } };
        var harness = StreamingHarness(translator, out _);
        harness.Reader.Streaming = true;
        harness.DocumentText = LongText;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);

        Task read = harness.Service.ReadFileAsync("a.txt");
        await harness.Reader.FirstStream.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        harness.Monitor.RaiseCleared();
        gate.SetResult("<late>");
        await read;

        Assert.Empty(harness.Reader.Appended);
    }

    [Fact]
    public async Task A_missing_pack_for_a_streamed_read_reads_the_original_and_starts_the_download()
    {
        var packs = new RecordingPackService();
        var translator = new ScriptedTranslator { ModelReady = false };
        var harness = StreamingHarness(translator, out _, new TranslationPackInstaller(packs, translator, new SilentLog()));
        harness.Reader.Streaming = true;
        harness.DocumentText = LongText;
        harness.Service.FirstAudioGrace = TimeSpan.FromMilliseconds(20);
        ReadTranslationResult? finished = null;
        harness.Service.TranslationFinished += (_, result) => finished = result;

        await harness.Service.ReadFileAsync("a.txt");
        await packs.Downloaded.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal([LongText], harness.Reader.Spoken);
        Assert.Equal(TranslationOutcome.PackDownloading, finished?.Outcome);
        Assert.Equal(["translation-fr-en"], packs.Requested);
    }

    [Fact]
    public void Turning_translation_on_downloads_the_users_language_pack()
    {
        var packs = new RecordingPackService();
        var translator = new ScriptedTranslator { ModelReady = false };
        var harness = StreamingHarness(translator, out _, new TranslationPackInstaller(packs, translator, new SilentLog()));
        harness.Settings.Language = "ru";
        harness.Settings.TranslateEnabled = false;

        harness.Service.TranslateEnabled = true;
        packs.Downloaded.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(["translation-en-ru"], packs.Requested);
    }

    private static Harness StreamingHarness(ScriptedTranslator translator, out ScriptedTranslator used, TranslationPackInstaller? installer = null)
    {
        used = translator;
        var harness = new Harness(settings => new ReadTranslator(translator, new FakeDetector("fr"), settings, new SilentLog(), installer));
        harness.Settings.Language = "en";
        harness.Settings.TranslateEnabled = true;
        return harness;
    }

    /// <summary>Translates "x" to "&lt;x&gt;", per block, with optional gates and failures by block number.</summary>
    private sealed class ScriptedTranslator : ITranslator
    {
        private int _calls;

        public Dictionary<int, TaskCompletionSource<string>> BlockGates { get; } = new();

        public int? FailFromBlock { get; set; }

        public bool ModelReady { get; set; } = true;

        public bool SawCancellation { get; private set; }

        public IReadOnlyList<string> SupportedLanguages => ["en", "fr", "ru"];

        public IReadOnlyList<string> RequiredPackIds(string source, string target) => [$"translation-{source}-{target}"];

        public Task<bool> IsModelReadyAsync(string source, string target) => Task.FromResult(ModelReady);

        public async Task<string> TranslateAsync(string text, string source, string target, CancellationToken cancellationToken)
        {
            int block = Interlocked.Increment(ref _calls) - 1;
            if (FailFromBlock is { } from && block >= from)
            {
                throw new InvalidOperationException("boom");
            }

            if (BlockGates.TryGetValue(block, out TaskCompletionSource<string>? gate))
            {
                try
                {
                    return await gate.Task.WaitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    SawCancellation = true;
                    throw;
                }
            }

            return $"<{text}>";
        }
    }

    private sealed class RecordingPackService : IVoicePackService
    {
        private readonly TaskCompletionSource _downloaded = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Requested { get; } = [];

        public Task Downloaded => _downloaded.Task;

        public Task<PackCatalogResult> GetCatalogAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PackCatalogResult> GetCatalogCachedAsync(TimeSpan maxAge, CancellationToken cancellationToken) => throw new NotSupportedException();

        public string? GetInstalledPath(string packId) => null;

        public IReadOnlyList<InstalledVoicePack> GetInstalledVoicePacks() => [];

        public IReadOnlyList<InstalledVoicePack> GetInstalledOcrPacks() => [];

        public Task<PackDownloadResult> DownloadAsync(string packId, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            Requested.Add(packId);
            _downloaded.TrySetResult();
            return Task.FromResult(PackDownloadResult.Ok);
        }
    }

    private sealed class FakeHeadStart : IHeadStart
    {
        private readonly TaskCompletionSource _firstPrepare = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Prepared { get; } = [];
        public Task FirstPrepare => _firstPrepare.Task;
        public int Cancels { get; private set; }
        public bool Fail { get; set; }
        public bool ReadInProgress => false;

        public Task PrepareAsync(string text)
        {
            Prepared.Add(text);
            _firstPrepare.TrySetResult();
            return Fail ? Task.FromException(new InvalidOperationException("boom")) : Task.CompletedTask;
        }

        public void CancelPrepare() => Cancels++;
    }

    private sealed class Harness
    {
        public Func<string, int?, Task> Speak { get; set; } = (_, _) => Task.CompletedTask;

        public FakeReader Reader { get; }

        public FakeClipboard Clipboard { get; } = new();

        public ActivityLog Log { get; } = new();

        public ReadAloudService Service { get; }

        public FakeSelectionMonitor Monitor { get; } = new();

        public FakeHeadStart HeadStart { get; } = new();

        public TestSettings Settings { get; } = new();

        public string DocumentText { get; set; } = "some text to read";

        public Harness(Func<TestSettings, ReadTranslator>? translation = null, IUsageRecorder? usage = null, IReadGate? gate = null)
        {
            Reader = new FakeReader(this);
            Service = new ReadAloudService(
                Reader, Clipboard, new FakeHotkey(), new FakeCopier(), Monitor,
                new FakeClipboardMonitor(), new FakeForeground(), new FakeVoices(), new FakeModel(),
                new FakeDocuments(this), Log, Settings, new FakeSanitizer(), new SilentLog(), HeadStart, translation?.Invoke(Settings), usage, gate);
        }
    }

    private sealed class FakeGate : IReadGate
    {
        public bool IsBlocked { get; set; } = true;

        public List<bool> Refusals { get; } = new();

        public void ReadRefused(bool automatic) => Refusals.Add(automatic);
    }

    // Runs each count's builder at once with a fixed context, so a test reads what would have been queued.
    private sealed class RecordingUsage : IUsageRecorder
    {
        private static readonly UsageContext Context = new("1.4.0", "windows", 1, new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));

        public List<UsageEvent> Events { get; } = [];

        public bool Enabled { get; set; } = true;

        public void Record(Func<UsageContext, UsageEvent?> make)
        {
            if (make(Context) is { } count)
            {
                lock (Events)
                {
                    Events.Add(count);
                }
            }
        }
    }

    private sealed class FakeReader(Harness harness) : ISpeechReader
    {
        public event EventHandler<PlaybackState>? StateChanged;
        public event EventHandler? Completed { add { } remove { } }
        public event EventHandler<double>? ProgressChanged { add { } remove { } }
        public event EventHandler<ReadTiming>? TimingChanged { add { } remove { } }
        public event EventHandler<ReadChunk>? ChunkChanged { add { } remove { } }

        public PlaybackState State { get; private set; } = PlaybackState.Idle;

        public void RaiseState(PlaybackState state)
        {
            State = state;
            StateChanged?.Invoke(this, state);
        }

        private readonly TaskCompletionSource _firstStream = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource _streamEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool Streaming { get; set; }
        public Exception? StreamFault { get; set; }
        public bool StreamingOffAfterFault { get; set; }
        public List<string> Spoken { get; } = [];
        public List<string> Streamed { get; } = [];
        public List<string> Appended { get; } = [];
        public int Completes { get; private set; }
        public Task FirstStream => _firstStream.Task;

        public bool SupportsStreaming => Streaming && !(StreamingOffAfterFault && Streamed.Count > 0);

        public Task SpeakAsync(string text, int? activityId = null)
        {
            Spoken.Add(text);
            return harness.Speak(text, activityId);
        }

        // A streamed read ends only when the service completes the stream, as the real reader does.
        public Task SpeakStreamAsync(string firstText, int? activityId = null)
        {
            Streamed.Add(firstText);
            _firstStream.TrySetResult();
            return StreamFault is { } fault ? Task.FromException(fault) : _streamEnded.Task;
        }

        public void AppendText(string text, int? activityId = null) => Appended.Add(text);

        public void CompleteStream(int? activityId = null)
        {
            Completes++;
            _streamEnded.TrySetResult();
        }
        public Task WarmUpAsync() => Task.CompletedTask;
        public void Pause() { }
        public void Resume() { }
        public void Stop() => Stops++;
        public int Stops { get; private set; }
        public void SetSpeed(PlaybackRate speed) { }
        public void SetVoice(string voiceId) { }
        public Task SkipForward() => Task.CompletedTask;
        public Task SkipBackward() => Task.CompletedTask;
    }

    private sealed class FakeClipboard : IClipboardReader
    {
        public int Reads { get; private set; }

        public Task<string?> GetTextAsync()
        {
            Reads++;
            return Task.FromResult<string?>("clipboard text");
        }
    }

    private sealed class FakeHotkey : IHotkeyService
    {
        public event EventHandler? Pressed { add { } remove { } }
        public bool Register(nint windowHandle) => true;
        public void Unregister() { }
        public void Dispose() { }
    }

    private sealed class FakeCopier : ISelectionCopier
    {
        public Task CopyAsync() => Task.CompletedTask;
    }

    private sealed class FakeSelectionMonitor : ISelectionMonitor
    {
        public event EventHandler<string>? SelectionChanged;
        public event EventHandler? SelectionCleared;
        public void RaiseChanged(string text) => SelectionChanged?.Invoke(this, text);
        public void RaiseCleared() => SelectionCleared?.Invoke(this, EventArgs.Empty);
        public bool IsRunning => false;
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class FakeClipboardMonitor : IClipboardMonitor
    {
        public event EventHandler? ContentChanged { add { } remove { } }
        public bool IsRegistered => false;
        public void Register(nint windowHandle) { }
        public void Unregister() { }
        public void Dispose() { }
    }

    private sealed class FakeForeground : IForegroundWindow
    {
        public WindowSource? Capture() => null;
    }

    private sealed class FakeVoices : IVoiceCatalog
    {
        public IReadOnlyList<VoiceInfo> InstalledVoices { get; } = [new VoiceInfo(SystemVoice.Id, "Windows voice", string.Empty)];
        public VoiceInfo? DefaultVoice => null;
    }

    private sealed class FakeModel : IVoiceModelService
    {
        public bool IsReady => false;
        public VoiceModelPaths? Paths => null;
        public event EventHandler? ReadyChanged { add { } remove { } }
        public Task InitializeAsync(IProgress<double>? progress = null) => Task.CompletedTask;
    }

    private sealed class FakeDocuments(Harness harness) : IDocumentTextExtractor
    {
        public bool CanHandle(string extension) => true;
        public Task<string> ExtractTextAsync(string filePath) => Task.FromResult(harness.DocumentText);
    }

    private sealed class FakeSanitizer : ITextSanitizer
    {
        public string Sanitize(string text) => text;
    }
}
