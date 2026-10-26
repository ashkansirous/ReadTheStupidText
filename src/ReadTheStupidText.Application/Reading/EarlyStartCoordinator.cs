namespace ReadTheStupidText.Application.Reading;

/// <summary>An early translation of a text's head, kept so the full translation translates only the rest.</summary>
public sealed record EarlyTranslation(string FullText, string Source, string Target, EarlySplit Split, string TranslatedHead)
{
    /// <summary>Whether this is the translation of exactly this text and pair.</summary>
    public bool Matches(string text, string source, string target) =>
        FullText == text && Source == source && Target == target;

    /// <summary>The whole translation, given the translation of what follows the head (empty when nothing does).</summary>
    public string Joined(string translatedRemainder) => EarlyStartPlan.Join(TranslatedHead, translatedRemainder, Split.Gap);
}

/// <summary>
/// The work behind one text, supplied by the page (Slice 139). <see cref="TranslateHead"/> is null when nothing is translated
/// (same language, no model for the pair, no translator); it returns null when it chose not to translate yet (a model that
/// would have to download over mobile data waits for the tap).
/// </summary>
public sealed record EarlyStartJobs(
    Func<string, CancellationToken, Task> VoiceOriginal,
    Func<EarlySplit, CancellationToken, Task<string?>>? TranslateHead,
    Func<string, CancellationToken, Task>? VoiceTranslated);

/// <summary>
/// Runs the two early jobs of one text, voicing the head of the original and translating then voicing the head of the
/// translation, and lets the tap decide (Decision 147): choosing one side cancels the other at once and leaves the chosen side
/// running even after the page is left. New text restarts everything. Not thread-safe: called from the UI thread. All the
/// work is best effort, so no job's failure reaches the caller; a failed or cancelled translation is simply null.
/// </summary>
public sealed class EarlyStartCoordinator
{
    private Run? _run;

    private sealed class Run
    {
        public string Text { get; init; } = string.Empty;

        public CancellationTokenSource OriginalCts { get; } = new();

        public CancellationTokenSource TranslationCts { get; } = new();

        /// <summary>Cancelled by the tap on Translate and read: the translation no longer waits for the typing to rest.</summary>
        public CancellationTokenSource SkipTranslationDelay { get; } = new();

        /// <summary>Cancelled by the tap on Read: the original's voicing no longer waits for the typing to rest.</summary>
        public CancellationTokenSource SkipOriginalDelay { get; } = new();

        public Task OriginalTask { get; set; } = Task.CompletedTask;

        public Task<EarlyTranslation?> TranslationTask { get; set; } = Task.FromResult<EarlyTranslation?>(null);

        public bool OriginalCommitted { get; set; }

        public bool TranslationCommitted { get; set; }

        public Task VoiceTask { get; set; } = Task.CompletedTask;

        /// <summary>Frees the three sources once every task that reads their tokens has ended (a source is not freed while one runs).</summary>
        public void DisposeWhenDone() => _ = DisposeAfterTasksAsync();

        private async Task DisposeAfterTasksAsync()
        {
            await Task.WhenAll(OriginalTask, TranslationTask);
            await VoiceTask; // set by the translation, so read after it ended
            OriginalCts.Dispose();
            TranslationCts.Dispose();
            SkipTranslationDelay.Dispose();
            SkipOriginalDelay.Dispose();
        }
    }

    /// <summary>Cancels what was running and starts both jobs for <paramref name="text"/> after <paramref name="delay"/>.</summary>
    public void Restart(string text, string? source, string? target, EarlyStartJobs jobs, TimeSpan delay) =>
        RestartDeferred(text, source, target, jobs, delay)();

    /// <summary>
    /// As <see cref="Restart"/>, but the cancelling of the previous work is returned instead of run: cancelling runs registered
    /// callbacks (and maybe continuations) inline, so a caller holding a lock runs it after releasing the lock.
    /// </summary>
    public Action RestartDeferred(string text, string? source, string? target, EarlyStartJobs jobs, TimeSpan delay)
    {
        if (_run is { } chosen && KeepsChoice(chosen, text))
        {
            return NoOp; // the tap already chose a side for this very text: a late detection or toggle must not replace the chosen work
        }

        Action cancelPrevious = CancelAndDispose(_run);
        var run = new Run { Text = text };
        _run = run;
        run.OriginalTask = RunOriginalAsync(text, jobs, delay, run);
        if (jobs.TranslateHead is not null && source is not null && target is not null)
        {
            run.TranslationTask = RunTranslationAsync(text, source, target, jobs, delay, run);
        }

        return cancelPrevious;
    }

    private static void NoOp()
    {
    }

    private static Action CancelAndDispose(Run? run) =>
        run is null
            ? NoOp
            : () =>
            {
                Cancel(run, force: true);
                run.DisposeWhenDone();
            };

    // A side the tap chose, still running for the same text, is the work the read waits for and reuses: restarting would drop it.
    private static bool KeepsChoice(Run run, string text) =>
        run.Text == text
        && ((run.OriginalCommitted && !run.OriginalCts.IsCancellationRequested)
            || (run.TranslationCommitted && !run.TranslationCts.IsCancellationRequested));

    /// <summary>Stops every job that no tap has chosen (the page was left without choosing).</summary>
    public void CancelAll() => Cancel(_run, force: false);

    /// <summary>Stops every job, chosen or not: the text changed, so nothing prepared for the old one is wanted.</summary>
    public void Discard() => Cancel(_run, force: true);

    /// <summary>As <see cref="Discard"/>, but the cancelling is returned for the caller to run outside its lock (see <see cref="RestartDeferred"/>).</summary>
    public Action DiscardDeferred()
    {
        Run? run = _run;
        return () => Cancel(run, force: true);
    }

    /// <summary>The person chose Translate and read: the original's work stops now. Returns the early translation, null if there is none.</summary>
    public Task<EarlyTranslation?> ChooseTranslate()
    {
        if (_run is not { } run)
        {
            return Task.FromResult<EarlyTranslation?>(null);
        }

        if (run.TranslationCts.IsCancellationRequested)
        {
            return Task.FromResult<EarlyTranslation?>(null); // Read withdrew it earlier: the whole text is translated
        }

        run.TranslationCommitted = true;
        run.SkipTranslationDelay.Cancel();
        run.OriginalCts.Cancel();
        return run.TranslationTask;
    }

    /// <summary>The person chose Read: the translation's work stops now.</summary>
    public void ChooseRead()
    {
        if (_run is not { } run)
        {
            return;
        }

        run.OriginalCommitted = true;
        run.SkipOriginalDelay.Cancel();
        run.TranslationCts.Cancel();
    }

    /// <summary>
    /// Ends when the chosen side's early work has: the voice of the original after Read, the translation and then the voice of
    /// the translated head after Translate and read. The page waits for it before handing the text to the reader, because the
    /// read's own start cancels a preparation still running; ended work is in the sentence cache and the read reuses it.
    /// </summary>
    public async Task SettleChosenAsync()
    {
        if (_run is not { } run)
        {
            return;
        }

        if (run.OriginalCommitted)
        {
            await run.OriginalTask;
        }

        if (run.TranslationCommitted)
        {
            await run.TranslationTask;
            await run.VoiceTask; // set by the translation, so read after it ended
        }
    }

    private static void Cancel(Run? run, bool force)
    {
        if (run is null)
        {
            return;
        }

        if (force || !run.OriginalCommitted)
        {
            run.OriginalCts.Cancel();
        }

        if (force || !run.TranslationCommitted)
        {
            run.TranslationCts.Cancel();
        }
    }

    private static async Task RunOriginalAsync(string text, EarlyStartJobs jobs, TimeSpan delay, Run run)
    {
        CancellationToken token = run.OriginalCts.Token;
        try
        {
            await WaitUnlessChosenAsync(delay, run.SkipOriginalDelay.Token, token);
            string head = EarlyStartPlan.OriginalHead(text);
            if (head.Length > 0)
            {
                await jobs.VoiceOriginal(head, token);
            }
        }
        catch (Exception)
        {
            // Best effort: cancelled by a newer text or a tap, or failed; the read does its own work.
        }
    }

    private static async Task<EarlyTranslation?> RunTranslationAsync(string text, string source, string target, EarlyStartJobs jobs, TimeSpan delay, Run run)
    {
        CancellationToken token = run.TranslationCts.Token;
        try
        {
            await WaitUnlessChosenAsync(delay, run.SkipTranslationDelay.Token, token);
            EarlySplit split = EarlyStartPlan.TranslationSpan(text);
            string? translated = split.Head.Length > 0 ? (await jobs.TranslateHead!(split, token))?.Trim() : null;
            if (string.IsNullOrEmpty(translated))
            {
                return null;
            }

            run.VoiceTask = VoiceTranslatedAsync(translated, jobs, run, token);
            return new EarlyTranslation(text, source, target, split, translated);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // One preparation at a time on the reader: the translated head waits for the original's to end (or be cancelled by the tap).
    private static async Task VoiceTranslatedAsync(string translatedHead, EarlyStartJobs jobs, Run run, CancellationToken token)
    {
        try
        {
            await run.OriginalTask;
            string voiced = EarlyStartPlan.VoicedTranslation(translatedHead);
            if (jobs.VoiceTranslated is { } voice && voiced.Length > 0)
            {
                await voice(voiced, token);
            }
        }
        catch (Exception)
        {
            // Best effort, as above.
        }
    }

    // The idle rest, ended early (not failed) by the tap on that side; a cancel of the job itself still throws.
    private static async Task WaitUnlessChosenAsync(TimeSpan delay, CancellationToken skip, CancellationToken token)
    {
        try
        {
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(skip, token);
            await WaitAsync(delay, linked.Token);
        }
        catch (OperationCanceledException) when (skip.IsCancellationRequested && !token.IsCancellationRequested)
        {
            // The person tapped this side: start now.
        }
    }

    private static Task WaitAsync(TimeSpan delay, CancellationToken token) =>
        delay > TimeSpan.Zero ? Task.Delay(delay, token) : Task.CompletedTask;
}
