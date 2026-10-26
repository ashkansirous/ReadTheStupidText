using System.Diagnostics;

namespace ReadTheStupidText.Application.Usage;

/// <summary>
/// The desktop's anonymous usage counts (Slice 140, Decisions 143-145): the queue, config and send logic of Slice 137's phone sender, over
/// <see cref="IUsageStore"/> and <see cref="IUsageTransport"/>. A count is queued and sent in batches of at most
/// <see cref="UsageBatch.MaxEvents"/>. Nothing is queued or sent while the user's switch is off, while the remote config has telemetry off
/// (it stays off until the store declarations match), or when this launch was not picked by the config's sample rate. The queue is cleared
/// only on an explicit off (the switch, or a config that was really fetched or kept), never while no config has been seen. All file and
/// network work runs on the thread pool, so no caller waits for it.
/// </summary>
public sealed class UsageService : IUsageRecorder
{
    private const int MaxQueued = 200;
    private static readonly TimeSpan FlushDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FirstBackoff = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);
    private static readonly TimeSpan ConfigInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ConfigRetry = TimeSpan.FromMinutes(1);

    // The sample rate of a count held before any config was seen; Stamp replaces it with the real one.
    private const double UnstampedRate = 0;

    private enum Queued
    {
        No,
        Yes,
        BatchFull,
    }

    private readonly IUsageStore _store;
    private readonly IUsageTransport _transport;
    private readonly UsageBuild _build;
    private readonly TimeProvider _clock;
    private readonly double _roll;

    // Every read or change of the queue and the config copy happens under this lock, on the in-memory copy, so a send in flight can
    // neither drop counts recorded meanwhile nor bring back counts a switch-off forgot.
    private readonly object _gate = new();
    private readonly SemaphoreSlim _sending = new(1, 1);
    private List<UsageEvent>? _queue;
    private UsageConfig? _config;
    private bool _hasCopy;
    private DateTimeOffset _configNotBefore = DateTimeOffset.MinValue;
    private DateTimeOffset _nextAttempt = DateTimeOffset.MinValue;
    private Func<UsageContext, UsageEvent?>? _pendingOpened;
    private int _flushRequested;
    private int _flushScheduled;
    private int _failures;
    private Task _clearing = Task.CompletedTask;

    public UsageService(IUsageStore store, IUsageTransport transport, UsageBuild build, TimeProvider? clock = null, double? roll = null)
    {
        _store = store;
        _transport = transport;
        _build = build;
        _clock = clock ?? TimeProvider.System;
        _roll = roll ?? Random.Shared.NextDouble();
    }

    /// <summary>The operating system's major version, for the launch count.</summary>
    public int OsMajor => _build.OsMajor;

    public bool Enabled
    {
        get => _store.Enabled;
        set
        {
            try
            {
                _store.Enabled = value;
                if (!value)
                {
                    _clearing = Task.Run(Clear); // file work and the queue lock stay off the caller (the panel's UI thread)
                }
                else
                {
                    _ = FlushAsync(); // switched on mid-session: fetch the config now (if due) so held and new counts are decided
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Usage counts could not change their switch: {ex.Message}");
            }
        }
    }

    public void Record(Func<UsageContext, UsageEvent?> make) => _ = RecordAsync(make);

    internal Task RecordAsync(Func<UsageContext, UsageEvent?> make) => Task.Run(() => RecordCore(make));

    private void RecordCore(Func<UsageContext, UsageEvent?> make)
    {
        try
        {
            switch (Enqueue(make))
            {
                case Queued.BatchFull:
                    _ = FlushAsync();
                    break;
                case Queued.Yes:
                    ScheduleFlush();
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Usage counts could not queue a count: {ex.Message}");
        }
    }

    // BatchFull when this count first filled a batch: only that one starts a send, so a failing send does not spawn one per count.
    // While no config has been seen (first launch, offline) the default "off" is not an answer: the count is held, unstamped, and kept or
    // forgotten by Stamp once a config arrives, as on the phone (Slice 137).
    private Queued Enqueue(Func<UsageContext, UsageEvent?> make)
    {
        if (!Enabled)
        {
            return Queued.No;
        }

        bool held = HasCopy();
        TelemetrySettings telemetry = Config().Telemetry;
        if (held && (!telemetry.Enabled || _roll >= telemetry.SampleRate))
        {
            return Queued.No;
        }

        var context = new UsageContext(_build.Version, _build.Platform, held ? telemetry.SampleRate : UnstampedRate, _clock.GetUtcNow());
        if (make(context) is not { } count)
        {
            return Queued.No;
        }

        lock (_gate)
        {
            List<UsageEvent> queue = Queue();
            queue.Add(count);
            if (queue.Count > MaxQueued)
            {
                queue.RemoveRange(0, queue.Count - MaxQueued);
            }

            Save(queue);
            return queue.Count == UsageBatch.MaxEvents ? Queued.BatchFull : Queued.Yes;
        }
    }

    /// <summary>
    /// Fetches the remote config, counts the launch with <paramref name="opened"/> once a config is held (before that the default is off
    /// and the count would be dropped, so it waits for the config's first arrival), then sends what is queued. Never throws; run at start.
    /// </summary>
    public Task StartAsync(Func<UsageContext, UsageEvent?> opened) => Task.Run(async () =>
    {
        if (!Enabled)
        {
            Clear(); // the switch is off: no config request, nothing queued, nothing sent
            return;
        }

        lock (_gate)
        {
            _pendingOpened = opened;
        }

        await RefreshConfigAsync().ConfigureAwait(false);
        if (HasCopy() && TakePendingOpened() is { } pending)
        {
            RecordCore(pending);
        }

        await FlushOnPoolAsync(true).ConfigureAwait(false);
    });

    private Func<UsageContext, UsageEvent?>? TakePendingOpened()
    {
        lock (_gate)
        {
            Func<UsageContext, UsageEvent?>? opened = _pendingOpened;
            _pendingOpened = null;
            return opened;
        }
    }

    /// <summary>Sends what is queued, a batch at a time, off the caller's thread. Never throws; a batch that could not be delivered stays queued and is tried again after a growing pause. Calls overlap safely.</summary>
    public Task FlushAsync() => Task.Run(() => FlushOnPoolAsync(true));

    /// <summary>A last send when the app quits: waits no longer than <paramref name="wait"/>, so quitting is never held up by the network.</summary>
    public async Task FlushForExitAsync(TimeSpan wait)
    {
        Task flush = Task.Run(() => FlushOnPoolAsync(false)); // the held config is used: a slow config request must not eat the wait
        await Task.WhenAny(flush, Task.Delay(wait)).ConfigureAwait(false);
    }

    private async Task FlushOnPoolAsync(bool refreshConfig)
    {
        if (!await _sending.WaitAsync(0).ConfigureAwait(false))
        {
            // A send is running and may already have passed its last look at the queue: ask it to look once more.
            Interlocked.Exchange(ref _flushRequested, 1);
            return;
        }

        try
        {
            await FlushOnceAsync(refreshConfig).ConfigureAwait(false);
        }
        finally
        {
            _sending.Release();
        }

        if (Interlocked.Exchange(ref _flushRequested, 0) == 1)
        {
            await FlushOnPoolAsync(refreshConfig).ConfigureAwait(false);
        }
    }

    private async Task FlushOnceAsync(bool refreshConfig)
    {
        try
        {
            if (!Enabled)
            {
                Clear();
                return;
            }

            if (refreshConfig)
            {
                await RefreshConfigAsync().ConfigureAwait(false);
            }

            if (!HasCopy())
            {
                return; // no config has ever been seen, so "off" is only the default: keep what is queued
            }

            if (TakePendingOpened() is { } pending)
            {
                Enqueue(pending); // the launch count that waited for the config's first arrival
            }

            if (!Config().Telemetry.Enabled)
            {
                Clear(); // off means off: a fetched or kept config says so, so nothing is kept for later
                return;
            }

            Stamp(Config().Telemetry);

            if (_clock.GetUtcNow() >= _nextAttempt)
            {
                await SendQueuedAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Usage counts could not be sent: {ex.Message}");
            BackOff();
        }
    }

    private async Task SendQueuedAsync()
    {
        while (true)
        {
            List<UsageEvent> batch = Take();
            if (batch.Count == 0)
            {
                return;
            }

            // A post that throws (timeout, quit) leaves the batch out of the queue: it may have arrived, and a double count is worse than a lost one.
            if (await PostAsync(batch).ConfigureAwait(false) == UsageSendResult.Retry)
            {
                Restore(batch);
                BackOff();
                return;
            }

            // A batch the Worker refused for good (old Worker, bad body) is dropped: sending it again could never succeed and would block the rest.
            _failures = 0;
        }
    }

    private async Task<UsageSendResult> PostAsync(IReadOnlyList<UsageEvent> batch)
    {
        using var timeout = new CancellationTokenSource(SendTimeout);
        return await _transport.PostEventsAsync(UsageBatch.ToJson(batch), timeout.Token).ConfigureAwait(false);
    }

    private void BackOff()
    {
        _failures++;
        TimeSpan pause = FirstBackoff * Math.Pow(2, Math.Min(_failures - 1, 6));
        _nextAttempt = _clock.GetUtcNow() + (pause > MaxBackoff ? MaxBackoff : pause);
    }

    // One send a little after the first count of a burst, so a reading session's counts leave together.
    private void ScheduleFlush()
    {
        if (Interlocked.Exchange(ref _flushScheduled, 1) == 1)
        {
            return;
        }

        _ = FlushAfterDelayAsync();
    }

    private async Task FlushAfterDelayAsync()
    {
        await Task.Delay(FlushDelay, _clock).ConfigureAwait(false);
        Interlocked.Exchange(ref _flushScheduled, 0);
        await FlushOnPoolAsync(true).ConfigureAwait(false);
    }

    // ---- remote config ------------------------------------------------------------------------------------------------

    private UsageConfig Config()
    {
        LoadCopyOnce();
        lock (_gate)
        {
            return _config!;
        }
    }

    private bool HasCopy()
    {
        LoadCopyOnce();
        lock (_gate)
        {
            return _hasCopy;
        }
    }

    // The kept copy is read from the store once; the file read stays outside the lock.
    private void LoadCopyOnce()
    {
        lock (_gate)
        {
            if (_config is not null)
            {
                return;
            }
        }

        bool served = UsageConfig.TryParseServed(SafeLoadConfig(), out UsageConfig kept);
        lock (_gate)
        {
            if (_config is null)
            {
                _config = kept;
                _hasCopy = served;
            }
        }
    }

    private string? SafeLoadConfig()
    {
        try
        {
            return _store.LoadConfig();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Usage counts could not read the kept config: {ex.Message}");
            return null;
        }
    }

    // At most once per 15 minutes (one minute after a failure). A failure or a body that is not a served config leaves the copy as it was.
    private async Task RefreshConfigAsync()
    {
        if (!Enabled)
        {
            return;
        }

        LoadCopyOnce();
        lock (_gate)
        {
            if (_clock.GetUtcNow() < _configNotBefore)
            {
                return;
            }

            _configNotBefore = _clock.GetUtcNow() + ConfigRetry;
        }

        try
        {
            using var timeout = new CancellationTokenSource(SendTimeout);
            string body = await _transport.FetchConfigAsync(timeout.Token).ConfigureAwait(false);
            if (UsageConfig.TryParseServed(body, out UsageConfig fetched))
            {
                ApplyFetched(body, fetched);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Usage counts could not load the remote config: {ex.Message}");
        }
    }

    private void ApplyFetched(string body, UsageConfig fetched)
    {
        try
        {
            _store.SaveConfig(body);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Usage counts could not keep the remote config: {ex.Message}");
        }

        lock (_gate)
        {
            _config = fetched;
            _hasCopy = true;
            _configNotBefore = _clock.GetUtcNow() + ConfigInterval;
        }
    }

    // ---- queue --------------------------------------------------------------------------------------------------------

    // The next batch leaves the queue (and the saved file) before it is posted: a post the app quits on may or may not have arrived, and a
    // rare lost batch is better than one counted twice on the next launch. A batch the server asked to retry goes back with Restore.
    private List<UsageEvent> Take()
    {
        lock (_gate)
        {
            List<UsageEvent> queue = Queue();
            List<UsageEvent> batch = queue.Take(UsageBatch.MaxEvents).ToList();
            if (batch.Count > 0)
            {
                queue.RemoveRange(0, batch.Count);
                Save(queue);
            }

            return batch;
        }
    }

    // Puts a batch the server did not take back at the head of the queue, ahead of what was recorded meanwhile; the oldest go first if it overflows.
    private void Restore(List<UsageEvent> batch)
    {
        lock (_gate)
        {
            List<UsageEvent> queue = Queue();
            queue.InsertRange(0, batch);
            if (queue.Count > MaxQueued)
            {
                queue.RemoveRange(0, queue.Count - MaxQueued);
            }

            Save(queue);
        }
    }

    // Counts held before any config was seen carry no sample rate yet: once the config is here they are kept at its rate, or forgotten when
    // this launch was not picked. Call with telemetry on.
    private void Stamp(TelemetrySettings telemetry)
    {
        lock (_gate)
        {
            List<UsageEvent> queue = Queue();
            if (!queue.Exists(count => count.SampleRate == UnstampedRate))
            {
                return;
            }

            if (_roll >= telemetry.SampleRate)
            {
                queue.RemoveAll(count => count.SampleRate == UnstampedRate);
            }
            else
            {
                for (int i = 0; i < queue.Count; i++)
                {
                    if (queue[i].SampleRate == UnstampedRate)
                    {
                        queue[i] = queue[i] with { SampleRate = telemetry.SampleRate };
                    }
                }
            }

            Save(queue);
        }
    }

    private void Clear()
    {
        lock (_gate)
        {
            _queue = [];
            Save(_queue);
        }
    }

    // Call with _gate held.
    private List<UsageEvent> Queue() => _queue ??= UsageBatch.ParseQueue(_store.LoadQueue());

    // Call with _gate held.
    private void Save(IReadOnlyList<UsageEvent> queue) => _store.SaveQueue(queue.Count == 0 ? null : UsageBatch.QueueToJson(queue));

    /// <summary>Completes when the forgetting a switch-off started has finished.</summary>
    internal Task Settled => _clearing;

    internal int QueuedCount
    {
        get
        {
            lock (_gate)
            {
                return Queue().Count;
            }
        }
    }
}
