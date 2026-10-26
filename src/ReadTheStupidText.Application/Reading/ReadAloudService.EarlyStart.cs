using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Domain.Activity;

namespace ReadTheStupidText.Application.Reading;

// Slice 142 (Decision 147 on Windows): the quiet moment between a selection event and its read translates the
// head of the text and voices the part the read will start with. The pieces are the phone's (EarlyStartPlan, EarlyStartCoordinator);
// the read decides at once which side it wants (the translate toggle), so nothing waits for a tap. Early work records no usage and
// shows no wait UI of its own: the read's own wait indicator covers the time the read spends joining it.
public sealed partial class ReadAloudService
{
    /// <summary>The longest a read waits for the early translation and the early voice together before it goes on alone.</summary>
    internal static readonly TimeSpan EarlySettleCap = TimeSpan.FromSeconds(10);

    private readonly EarlyStartCoordinator _early = new();

    // The coordinator is not thread-safe: every call into it (and the run record) goes through this gate, because early work is
    // started from the pool (after its waits) while reads, cancels and settings changes arrive on other threads.
    private readonly object _earlyGate = new();

    // The text (and language pair, null when only the original is voiced) the running early work is for; null when there is none.
    private volatile EarlyRun? _earlyRun;

    // Counts the voicings started, so a cancelled voicing cancels the reader's preparation only while it is still the latest.
    private long _voiceEpoch;

    private volatile bool _waitingOnEarlyVoice;

    private sealed record EarlyRun(string Text, string? Source, string? Target);

    /// <summary>True while a read waits for its early translated head to be voiced (the wait indicator then says it is preparing the voice).</summary>
    public bool WaitingOnEarlyVoice => _waitingOnEarlyVoice;

    /// <summary>Raised when <see cref="WaitingOnEarlyVoice"/> changes; may fire on any thread.</summary>
    public event EventHandler? WaitingOnEarlyVoiceChanged;

    // Starts the early work for text that has been stable. The original's voicing starts at once (as the head start always did);
    // when a translation pair is then planned (detection and the model check are async) it replaces that with the translation's.
    private async Task StartEarlyWorkAsync(IHeadStart headStart, string text, bool requirePending, TimeSpan delay, CancellationToken token)
    {
        StartEarlyRun(headStart, text, null, delay, keepAnyRunForText: true);
        if (_translation is null || !TranslateEnabled)
        {
            return;
        }

        (string Source, string Target)? pair = await _translation.PlanEarlyAsync(text, token);
        token.ThrowIfCancellationRequested();
        if (pair is null)
        {
            return; // nothing to translate: the original's voicing started above stays
        }

        if (requirePending && !IsStillPending(text))
        {
            DiscardEarlyWorkFor(text); // the read started meanwhile and speaks a translation: the original's voicing must not hold the reader
            return;
        }

        StartEarlyRun(headStart, text, pair, delay, keepAnyRunForText: false);
    }

    private bool IsStillPending(string text) => _activeEntry is { State: ActivityState.Pending } && _lastTriggeredText == text;

    private void StartEarlyRun(IHeadStart headStart, string text, (string Source, string Target)? pair, TimeSpan delay, bool keepAnyRunForText)
    {
        Action cancelPrevious;
        lock (_earlyGate)
        {
            bool same = _earlyRun is { } current && current.Text == text
                && (keepAnyRunForText || (current.Source == pair?.Source && current.Target == pair?.Target));
            if (same)
            {
                return; // work is already under way (or done) for this text
            }

            _earlyRun = new EarlyRun(text, pair?.Source, pair?.Target);
            cancelPrevious = _early.RestartDeferred(text, pair?.Source, pair?.Target, BuildEarlyJobs(headStart, text, pair), delay);
        }

        cancelPrevious(); // outside the gate: cancelling runs callbacks inline
    }

    // With a translation the original is not voiced (the read speaks the translation); without one the head start prepares the
    // original (as it did before the early start), which the reader then plans as the read's first chunk.
    private EarlyStartJobs BuildEarlyJobs(IHeadStart headStart, string text, (string Source, string Target)? pair) =>
        new(
            (_, ct) => pair is null ? PrepareVoiceAsync(headStart, OriginalVoicedText(text), ct) : Task.CompletedTask,
            pair is { } p ? (span, ct) => _translation!.TranslateEarlyAsync(span.Head, p.Source, p.Target, ct) : null,
            pair is null ? null : (voiced, ct) => PrepareVoiceAsync(headStart, voiced, ct));

    // With the translate toggle on a translation pair is about to replace the original's voicing, and voicing the whole original
    // meanwhile slows the translation (it shares the CPU): only the head (the read's first chunk) is prepared then.
    internal string OriginalVoicedText(string text) =>
        _translation is not null && TranslateEnabled ? EarlyStartPlan.OriginalHead(text) : text;

    // The head start has no token: cancelling stops the preparation, but only while this voicing is still the latest one, so a
    // late cancel of an old voicing never stops the preparation of newer text.
    private async Task PrepareVoiceAsync(IHeadStart headStart, string text, CancellationToken token)
    {
        long epoch = Interlocked.Increment(ref _voiceEpoch);
        Task prepare = headStart.PrepareAsync(text);
        try
        {
            await prepare.WaitAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _ = prepare.ContinueWith(
                static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            if (Interlocked.Read(ref _voiceEpoch) == epoch)
            {
                headStart.CancelPrepare();
            }

            throw;
        }
    }

    /// <summary>
    /// For a read about to translate <paramref name="text"/>: joins the early translation of its head and the voicing of it, and
    /// returns the translation (null when there is none to reuse). The whole join is bounded by <see cref="EarlySettleCap"/>;
    /// past it the early work is dropped and the read translates alone. Only the caller's cancel throws.
    /// </summary>
    private async Task<EarlyTranslation?> TakeEarlyTranslationAsync(string text, CancellationToken token)
    {
        if (!TryChooseEarlyTranslation(text, out Task<EarlyTranslation?>? translation, out Task? settle))
        {
            return null;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(EarlySettleCap);
        SetWaitingOnEarlyVoice(false);
        EarlyTranslation? early;
        try
        {
            early = await translation.WaitAsync(deadline.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            _systemLog.Debug("early translation too slow; the read translates alone");
            DiscardEarlyWork();
            return null;
        }

        await SettleEarlyVoiceAsync(settle, deadline.Token, token);
        return early?.FullText == text ? early : null;
    }

    // The early run is for this text, the translate toggle is on and the run's target is still the app language (a run for another
    // target would only delay the read, so it is dropped); then the read chooses the translation side. Checked and chosen under one
    // gate, so the run cannot change in between: both tasks belong to the run that was checked.
    private bool TryChooseEarlyTranslation(string text, out Task<EarlyTranslation?> translation, out Task settle)
    {
        translation = Task.FromResult<EarlyTranslation?>(null);
        settle = Task.CompletedTask;
        Action? cancelStale = null;
        try
        {
            lock (_earlyGate)
            {
                EarlyRun? run = _earlyRun;
                if (run is not { Source: not null } || run.Text != text || !TranslateEnabled)
                {
                    return false;
                }

                if (run.Target != TranslationFlow.NormalizeTag(_settings.Language))
                {
                    _earlyRun = null;
                    cancelStale = _early.DiscardDeferred();
                    return false;
                }

                translation = _early.ChooseTranslate();
                settle = _early.SettleChosenAsync(); // reads this same run before its first await
                return true;
            }
        }
        finally
        {
            cancelStale?.Invoke();
        }
    }

    private async Task SettleEarlyVoiceAsync(Task settle, CancellationToken deadline, CancellationToken caller)
    {
        if (settle.IsCompleted)
        {
            return; // nothing left to wait for (also a second join of the same read): no wait indicator
        }

        SetWaitingOnEarlyVoice(true);
        try
        {
            await settle.WaitAsync(deadline);
        }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested)
        {
            _systemLog.Debug("early voice still running; the read goes on");
        }
        finally
        {
            SetWaitingOnEarlyVoice(false);
        }
    }

    private void SetWaitingOnEarlyVoice(bool waiting)
    {
        if (_waitingOnEarlyVoice != waiting)
        {
            _waitingOnEarlyVoice = waiting;
            WaitingOnEarlyVoiceChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // New text, a toggle or source change, a cancelled read or disposal: nothing prepared for the old state is wanted.
    private void DiscardEarlyWork() => DiscardEarlyWorkWhere(_ => true);

    private void DiscardEarlyWorkFor(string text) => DiscardEarlyWorkWhere(run => run.Text == text);

    // A read of text other than the early work's drops it; a read of the very same text keeps it and joins it.
    private void DiscardEarlyWorkUnlessFor(string text) => DiscardEarlyWorkWhere(run => run.Text != text);

    // The run is detached under the gate and cancelled after it: cancelling runs callbacks inline (the voicing's catch calls
    // into the reader), which must not happen while the gate is held.
    private void DiscardEarlyWorkWhere(Func<EarlyRun, bool> unwanted)
    {
        Action cancel;
        lock (_earlyGate)
        {
            if (_earlyRun is { } run && !unwanted(run))
            {
                return;
            }

            _earlyRun = null;
            cancel = _early.DiscardDeferred();
        }

        cancel();
    }

    /// <summary>The panel changed the translate source language: early work for the old pick is dropped.</summary>
    public void NotifyTranslateSettingsChanged() => DiscardEarlyWork();
}
