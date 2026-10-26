using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Tests;

public class EarlyStartPlanTests
{
    [Fact]
    public void Original_head_is_the_first_two_sentences()
    {
        Assert.Equal("The first one is here. The second one follows.", EarlyStartPlan.OriginalHead("The first one is here. The second one follows. The third one is here. The fourth one."));
    }

    [Fact]
    public void A_last_sentence_still_being_typed_is_not_part_of_a_head()
    {
        Assert.Equal("A. B.", EarlyStartPlan.TranslationSpan("A. B. C is still being typ").Head);
        Assert.Equal("One.", EarlyStartPlan.OriginalHead("One. Two is still"));
    }

    [Fact]
    public void Original_head_of_a_short_text_is_all_of_it()
    {
        Assert.Equal("Only one sentence here.", EarlyStartPlan.OriginalHead("  Only one sentence here.  "));
    }

    [Fact]
    public void Original_head_without_a_sentence_ending_is_cut_at_a_word_near_120_characters()
    {
        string text = string.Join(' ', Enumerable.Repeat("word", 60));

        string head = EarlyStartPlan.OriginalHead(text);

        Assert.InRange(head.Length, 100, EarlyStartPlan.FallbackChars);
        Assert.StartsWith("word word", head);
        Assert.EndsWith("word", head);
    }

    [Fact]
    public void Original_head_never_crosses_a_paragraph_break()
    {
        Assert.Equal("A heading without a stop", EarlyStartPlan.OriginalHead("A heading without a stop\n\nThen a sentence. And another."));
    }

    [Fact]
    public void A_first_sentence_that_is_too_long_is_cut_at_a_word()
    {
        string text = string.Join(' ', Enumerable.Repeat("long", 80)) + ". Next.";

        string head = EarlyStartPlan.OriginalHead(text);

        Assert.InRange(head.Length, 100, EarlyStartPlan.FallbackChars);
    }

    [Fact]
    public void Chinese_sentences_split_at_the_ideographic_full_stop_without_spaces()
    {
        Assert.Equal("今天天气非常好，我们一起去公园玩耍。我们在公园里开心地跑步和唱歌。", EarlyStartPlan.OriginalHead("今天天气非常好，我们一起去公园玩耍。我们在公园里开心地跑步和唱歌。然后我们一起回家吃好吃的晚饭。"));
    }

    [Fact]
    public void Unspaced_cjk_without_a_terminator_is_cut_at_the_limit()
    {
        string head = EarlyStartPlan.OriginalHead(new string('字', 400));

        Assert.Equal(EarlyStartPlan.FallbackChars, head.Length);
    }

    [Fact]
    public void Persian_sentences_split_at_the_question_mark_and_the_full_stop()
    {
        Assert.Equal("سلام، حال شما چطور است؟ من خوبم، ممنون از شما.", EarlyStartPlan.OriginalHead("سلام، حال شما چطور است؟ من خوبم، ممنون از شما. امروز هوا خوب است."));
    }

    [Fact]
    public void Translation_span_is_the_first_three_sentences_and_the_rest_follows()
    {
        EarlySplit split = EarlyStartPlan.TranslationSpan("Un. Deux. Trois. Quatre. Cinq.");

        Assert.Equal("Un. Deux. Trois.", split.Head);
        Assert.Equal(" ", split.Gap);
        Assert.Equal("Quatre. Cinq.", split.Remainder);
    }

    [Fact]
    public void Translation_span_stops_before_a_sentence_that_would_pass_300_characters()
    {
        string sentence = string.Join(' ', Enumerable.Repeat("mot", 30)) + "."; // 119 characters
        EarlySplit split = EarlyStartPlan.TranslationSpan($"{sentence} {sentence} {sentence} {sentence}");

        Assert.Equal($"{sentence} {sentence}", split.Head);
        Assert.Equal($"{sentence} {sentence}", split.Remainder);
    }

    [Fact]
    public void A_long_first_sentence_is_translated_whole_not_cut_mid_clause()
    {
        string sentence = string.Join(' ', Enumerable.Repeat("mot", 120)) + "."; // 479 characters
        EarlySplit split = EarlyStartPlan.TranslationSpan($"{sentence} Court.");

        Assert.Equal(sentence, split.Head);
        Assert.Equal("Court.", split.Remainder);
    }

    [Fact]
    public void A_decimal_point_and_a_missing_space_are_not_sentence_ends_just_as_for_the_chunker()
    {
        Assert.Equal("Version 3.14 is out. See Dr.Smith for the details.", EarlyStartPlan.OriginalHead("Version 3.14 is out. See Dr.Smith for the details. A third sentence follows here."));
    }

    [Fact]
    public void A_line_break_ends_a_sentence_as_it_does_for_the_chunker()
    {
        Assert.Equal("Line one wrapped\nline two wrapped", EarlyStartPlan.OriginalHead("Line one wrapped\nline two wrapped\nline three"));
    }

    [Fact]
    public void Translation_span_of_a_short_text_leaves_nothing_over()
    {
        EarlySplit split = EarlyStartPlan.TranslationSpan("Bonjour. Merci.");

        Assert.Equal("Bonjour. Merci.", split.Head);
        Assert.Equal(string.Empty, split.Remainder);
    }

    [Fact]
    public void The_head_and_the_rest_put_back_together_are_the_text()
    {
        string text = "Un. Deux. Trois.\n\nQuatre.";

        EarlySplit split = EarlyStartPlan.TranslationSpan(text);

        Assert.Equal("Un. Deux. Trois.", split.Head);
        Assert.Equal("\n\n", split.Gap);
        Assert.Equal(text, split.Head + split.Gap + split.Remainder);
    }

    [Fact]
    public void Voiced_translation_is_the_first_two_sentences()
    {
        Assert.Equal("One is here today. Two is here too.", EarlyStartPlan.VoicedTranslation("One is here today. Two is here too. Three is here too."));
    }

    [Fact]
    public void Join_keeps_a_paragraph_break_and_otherwise_a_space()
    {
        Assert.Equal("A. B.\n\nC.", EarlyStartPlan.Join("A. B.", " C. ", "\n\n"));
        Assert.Equal("A. B. C.", EarlyStartPlan.Join("A. B.", "C.", " "));
        Assert.Equal("A. B.", EarlyStartPlan.Join("A. B.", "  ", " "));
    }
}

public class EarlyStartCoordinatorTests
{
    private const string French = "Un. Deux. Trois. Quatre.";

    private sealed class Probe
    {
        public List<string> VoicedOriginal { get; } = [];

        public List<string> VoicedTranslated { get; } = [];

        public List<string> Translated { get; } = [];

        public CancellationToken OriginalToken { get; private set; }

        public CancellationToken TranslationToken { get; private set; }

        public TaskCompletionSource OriginalGate { get; set; } = new();

        public bool HoldOriginal { get; set; }

        public EarlyStartJobs Jobs(bool translate = true) => new(
            async (text, token) =>
            {
                OriginalToken = token;
                VoicedOriginal.Add(text);
                if (HoldOriginal)
                {
                    await OriginalGate.Task.WaitAsync(token);
                }
            },
            translate ? (split, token) =>
            {
                TranslationToken = token;
                Translated.Add(split.Head);
                return Task.FromResult<string?>("The translated head is here. The second one is here. The third one is here.");
            }
            : null,
            (text, _) =>
            {
                VoicedTranslated.Add(text);
                return Task.CompletedTask;
            });
    }

    [Fact]
    public async Task Both_sides_start_and_the_translation_keeps_the_text_it_was_made_for()
    {
        var probe = new Probe();
        var early = new EarlyStartCoordinator();

        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.Zero);
        EarlyTranslation? result = await early.ChooseTranslate();

        Assert.Equal(["Un. Deux."], probe.VoicedOriginal); // two sentences: the read takes them as its first chunk
        Assert.Equal(["Un. Deux. Trois."], probe.Translated);
        Assert.NotNull(result);
        Assert.True(result!.Matches(French, "fr", "en"));
        Assert.False(result.Matches(French + "!", "fr", "en"));
        Assert.Equal("Quatre.", result.Split.Remainder);
        Assert.Equal("The translated head is here. The second one is here. The third one is here. Quatre en.", result.Joined("Quatre en."));
    }

    [Fact]
    public async Task A_tap_during_the_original_voice_keeps_it_running_and_settling_waits_for_it()
    {
        var probe = new Probe { HoldOriginal = true };
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.Zero);

        early.ChooseRead();
        early.CancelAll(); // the page is left
        Task settled = early.SettleChosenAsync();
        await Task.Delay(50);

        Assert.False(probe.OriginalToken.IsCancellationRequested);
        Assert.False(settled.IsCompleted);
        probe.OriginalGate.SetResult();
        await settled;
        Assert.False(probe.OriginalToken.IsCancellationRequested);
    }

    [Fact]
    public async Task A_restart_after_the_read_tap_keeps_the_chosen_voice_and_settling_waits_for_it()
    {
        var probe = new Probe { HoldOriginal = true };
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.FromMinutes(1));
        early.ChooseRead(); // the voice starts at once, the idle rest is skipped

        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.Zero); // a late detection or toggle restarts the early work
        Task settled = early.SettleChosenAsync();
        await Task.Delay(50);

        Assert.Equal(["Un. Deux."], probe.VoicedOriginal); // one voicing, not cancelled and started again
        Assert.False(probe.OriginalToken.IsCancellationRequested);
        Assert.False(settled.IsCompleted);
        probe.OriginalGate.SetResult();
        await settled;
    }

    [Fact]
    public async Task A_restart_before_the_read_tap_then_a_read_during_the_restarted_voice_waits_for_it()
    {
        var probe = new Probe { HoldOriginal = true };
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.Zero);
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.Zero); // the toggle restarts the early work
        await Task.Delay(20);

        early.ChooseRead(); // the restarted voice is in flight
        Task settled = early.SettleChosenAsync();
        await Task.Delay(50);

        Assert.False(probe.OriginalToken.IsCancellationRequested);
        Assert.False(settled.IsCompleted);
        probe.OriginalGate.SetResult();
        await settled;
    }

    [Fact]
    public async Task A_tap_on_translate_settles_after_the_translated_voice_and_never_cancels_it()
    {
        var voiceGate = new TaskCompletionSource();
        CancellationToken voiceToken = default;
        var jobs = new EarlyStartJobs(
            (_, _) => Task.CompletedTask,
            (_, _) => Task.FromResult<string?>("The translated head is here. The second one is here. The third one is here."),
            async (_, token) =>
            {
                voiceToken = token;
                await voiceGate.Task.WaitAsync(token);
            });
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", jobs, TimeSpan.Zero);

        await early.ChooseTranslate();
        early.CancelAll();
        Task settled = early.SettleChosenAsync();
        await Task.Delay(50);

        Assert.False(settled.IsCompleted);
        Assert.False(voiceToken.IsCancellationRequested);
        voiceGate.SetResult();
        await settled;
    }

    [Fact]
    public async Task Settling_with_nothing_chosen_returns_at_once()
    {
        var early = new EarlyStartCoordinator();

        await early.SettleChosenAsync();
        early.Restart(French, "fr", "en", new Probe().Jobs(), TimeSpan.Zero);
        await early.SettleChosenAsync();
    }

    [Fact]
    public async Task Choosing_read_does_not_wait_for_the_idle_rest_of_the_original()
    {
        var probe = new Probe();
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.FromSeconds(30));

        early.ChooseRead();
        await early.SettleChosenAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["Un. Deux."], probe.VoicedOriginal); // two sentences: the read takes them as its first chunk
    }

    [Fact]
    public async Task Discarding_after_a_choice_stops_the_chosen_side_too()
    {
        var probe = new Probe { HoldOriginal = true };
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.Zero);
        await Task.Delay(50);
        early.ChooseRead();

        early.Discard();

        Assert.True(probe.OriginalToken.IsCancellationRequested);
        await early.SettleChosenAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Choosing_translate_does_not_wait_for_the_idle_rest()
    {
        var probe = new Probe();
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.FromSeconds(30));

        EarlyTranslation? result = await early.ChooseTranslate().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(result);
        Assert.Equal(["Un. Deux. Trois."], probe.Translated);
    }

    [Fact]
    public async Task Choosing_translate_cancels_the_original_at_once_and_voices_the_translated_head()
    {
        var probe = new Probe { HoldOriginal = true };
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.Zero);

        EarlyTranslation? result = await early.ChooseTranslate();
        await Task.Delay(50);

        Assert.True(probe.OriginalToken.IsCancellationRequested);
        Assert.NotNull(result);
        Assert.Equal(["The translated head is here. The second one is here."], probe.VoicedTranslated);
    }

    [Fact]
    public async Task The_translated_head_is_not_voiced_while_the_original_still_is()
    {
        var probe = new Probe { HoldOriginal = true };
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.Zero);
        await Task.Delay(50);

        Assert.Empty(probe.VoicedTranslated);

        probe.OriginalGate.SetResult();
        await Task.Delay(50);

        Assert.Equal(["The translated head is here. The second one is here."], probe.VoicedTranslated);
    }

    [Fact]
    public async Task Choosing_read_cancels_the_translation_and_leaves_the_original_running()
    {
        var probe = new Probe { HoldOriginal = true };
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        early.ChooseRead();
        early.CancelAll(); // the page is left after Read: the chosen side is not stopped

        Assert.True(probe.TranslationToken.IsCancellationRequested);
        Assert.False(probe.OriginalToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Leaving_without_a_choice_cancels_both()
    {
        var probe = new Probe { HoldOriginal = true };
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);

        early.CancelAll();

        Assert.True(probe.OriginalToken.IsCancellationRequested);
        Assert.True(probe.TranslationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task New_text_cancels_everything_and_starts_again_even_after_a_choice()
    {
        var probe = new Probe { HoldOriginal = true };
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.FromMilliseconds(1));
        await Task.Delay(50);
        early.ChooseRead();
        CancellationToken first = probe.OriginalToken;

        early.Restart("Autre texte. Fin.", "fr", "en", probe.Jobs(), TimeSpan.Zero);
        await Task.Delay(50);

        Assert.True(first.IsCancellationRequested);
        Assert.Contains("Autre texte. Fin.", probe.VoicedOriginal);
    }

    [Fact]
    public async Task A_text_replaced_during_the_idle_wait_never_starts_its_work()
    {
        var probe = new Probe();
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.FromMilliseconds(300));

        early.Restart("Autre texte. Fin.", "fr", "en", probe.Jobs(), TimeSpan.Zero);
        await Task.Delay(400);

        Assert.Equal(["Autre texte. Fin."], probe.VoicedOriginal);
        Assert.Equal(["Autre texte. Fin."], probe.Translated);
    }

    [Fact]
    public async Task Without_a_translation_job_only_the_original_is_voiced()
    {
        var probe = new Probe();
        var early = new EarlyStartCoordinator();

        early.Restart(French, null, null, probe.Jobs(translate: false), TimeSpan.Zero);

        Assert.Null(await early.ChooseTranslate());
        Assert.Empty(probe.Translated);
        Assert.Single(probe.VoicedOriginal);
    }

    [Fact]
    public async Task A_translation_that_fails_or_waits_for_the_tap_is_none()
    {
        var early = new EarlyStartCoordinator();
        var failing = new EarlyStartJobs((_, _) => Task.CompletedTask, (_, _) => throw new InvalidOperationException("boom"), null);
        early.Restart(French, "fr", "en", failing, TimeSpan.Zero);
        Assert.Null(await early.ChooseTranslate());

        var waiting = new EarlyStartJobs((_, _) => Task.CompletedTask, (_, _) => Task.FromResult<string?>(null), null);
        early.Restart(French, "fr", "en", waiting, TimeSpan.Zero);
        Assert.Null(await early.ChooseTranslate());
    }

    [Fact]
    public async Task Choosing_translate_after_read_withdrew_the_translation_gives_none()
    {
        var probe = new Probe();
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.Zero);
        early.ChooseRead();

        Assert.Null(await early.ChooseTranslate());
    }

    [Fact]
    public async Task A_restart_after_a_choice_runs_both_sides_again()
    {
        var probe = new Probe();
        var early = new EarlyStartCoordinator();
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.Zero);
        early.ChooseRead();

        early.Discard(); // the page withdrew the choice (Cancel on the wait overlay) before starting over
        early.Restart(French, "fr", "en", probe.Jobs(), TimeSpan.Zero);

        Assert.NotNull(await early.ChooseTranslate());
        Assert.Equal(2, probe.VoicedOriginal.Count);
    }

    [Fact]
    public async Task A_failing_voice_job_never_reaches_the_caller()
    {
        var early = new EarlyStartCoordinator();
        var jobs = new EarlyStartJobs((_, _) => throw new InvalidOperationException("no engine"), null, null);

        early.Restart(French, null, null, jobs, TimeSpan.Zero);

        Assert.Null(await early.ChooseTranslate());
    }
}

public class EarlyStartVoiceTests
{
    private static EarlyStartVoice With(bool prepared) =>
        new() { Handler = (_, _, _) => Task.FromResult(prepared) };

    [Fact]
    public async Task A_head_that_was_prepared_counts_for_a_read_that_starts_with_it_once()
    {
        EarlyStartVoice voice = With(true);
        await voice.VoiceAsync("Un. Deux.", "fr", CancellationToken.None);

        Assert.True(voice.TakeEarly("  Un. Deux. Trois.", "fr"));
        Assert.False(voice.TakeEarly("Un. Deux. Trois.", "fr"));
    }

    [Fact]
    public async Task A_handler_that_prepared_nothing_does_not_count()
    {
        EarlyStartVoice voice = With(false);
        await voice.VoiceAsync("Un. Deux.", "fr", CancellationToken.None);

        Assert.False(voice.TakeEarly("Un. Deux. Trois.", "fr"));
    }

    [Fact]
    public async Task A_cancelled_preparation_and_a_reset_do_not_count()
    {
        EarlyStartVoice voice = With(true);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await voice.VoiceAsync("Un.", "fr", cts.Token);
        Assert.False(voice.TakeEarly("Un. Deux.", "fr"));

        await voice.VoiceAsync("Un.", "fr", CancellationToken.None);
        voice.Reset();
        Assert.False(voice.TakeEarly("Un. Deux.", "fr"));
    }

    [Fact]
    public async Task A_head_prepared_in_another_language_does_not_count()
    {
        EarlyStartVoice voice = With(true);
        await voice.VoiceAsync("Un. Deux.", "fr", CancellationToken.None);

        Assert.False(voice.TakeEarly("Un. Deux. Trois.", "en"));
    }

    [Fact]
    public async Task Either_prepared_head_counts_and_a_retired_page_stops_being_the_handler()
    {
        EarlyStartVoice voice = With(true);
        await voice.VoiceAsync("Un.", "fr", CancellationToken.None);
        await voice.VoiceAsync("One.", "en", CancellationToken.None);
        Assert.True(voice.TakeEarly("One. Two.", "en"));

        Func<string, string, CancellationToken, Task<bool>> other = (_, _, _) => Task.FromResult(true);
        voice.Clear(other);
        Assert.NotNull(voice.Handler);
        voice.Clear(voice.Handler!);
        Assert.Null(voice.Handler);
    }

    [Fact]
    public async Task The_handler_survives_the_page_disappearing_under_the_review_page()
    {
        int calls = 0;
        Func<string, string, CancellationToken, Task<bool>> page = (_, _, _) => { calls++; return Task.FromResult(true); };
        var voice = new EarlyStartVoice { Handler = page }; // the reader page's constructor
        // OnDisappearing (the review page opens on top) no longer clears the handler; OnAppearing registers it again.
        voice.Handler = page;

        await voice.VoiceAsync("Un.", "fr", CancellationToken.None);

        Assert.Equal(1, calls);
        Assert.True(voice.TakeEarly("Un. Deux.", "fr"));
        voice.Clear(page); // only a retired page lets go
        Assert.Null(voice.Handler);
    }
}
