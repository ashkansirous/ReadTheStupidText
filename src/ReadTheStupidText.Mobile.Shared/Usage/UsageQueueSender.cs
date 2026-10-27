using System.Diagnostics;
using System.Net;
using System.Text;

namespace ReadTheStupidText.Mobile.Shared.Usage;

/// <summary>How a <see cref="UsageQueueSender{TEvent}"/> queues, encodes and addresses one app's counts.</summary>
/// <param name="BaseAddress">The app's own host; no redirect is ever followed.</param>
/// <param name="EnabledKey">The preference key of the user's switch (on by default).</param>
/// <param name="QueueKey">The preference key that keeps counts not sent yet.</param>
/// <param name="LogName">The app's name in debug lines.</param>
/// <param name="MaxBatch">The most counts in one request (the Worker refuses more).</param>
/// <param name="Telemetry">The remote config's switch and sample rate now in force.</param>
/// <param name="TelemetryKnown">True once a real config backs <paramref name="Telemetry"/>; false while it is only the default, when the queue is kept, not cleared.</param>
/// <param name="ToJson">The request body for a batch.</param>
/// <param name="ParseQueue">Reads the stored queue (never throws).</param>
/// <param name="QueueToJson">Writes the queue for storage.</param>
public sealed record UsageQueueOptions<TEvent>(
    string BaseAddress,
    string EnabledKey,
    string QueueKey,
    string LogName,
    int MaxBatch,
    Func<(bool Enabled, double SampleRate)> Telemetry,
    Func<bool> TelemetryKnown,
    Func<IReadOnlyList<TEvent>, string> ToJson,
    Func<string?, List<TEvent>> ParseQueue,
    Func<IReadOnlyList<TEvent>, string> QueueToJson)
    where TEvent : class;

/// <summary>
/// The queue, batch and send logic behind an app's anonymous usage counts (Dino Reader Slice 133, Decisions 140-142; shared with Read The
/// Stupid Text in Slice 137). A count is queued in preferences and sent in batches of at most <see cref="UsageQueueOptions{TEvent}.MaxBatch"/>
/// to <c>POST /api/events</c> on the app's host. Nothing is queued or sent when the user's switch is off, when the remote config has
/// telemetry off, or when this launch was not picked by the config's sample rate; the Worker drops everything again when telemetry is off.
/// </summary>
public sealed class UsageQueueSender<TEvent>(UsageQueueOptions<TEvent> options)
    where TEvent : class
{
    private const string EventsPath = "/api/events";
    private const int MaxQueued = 200;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    // Only the app's own host, no redirects, no cookies.
    private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

    private static readonly TimeSpan FirstBackoff = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

    // Every read or change of the queue happens under this lock, on the in-memory copy, so a send that is in flight can neither
    // drop events recorded meanwhile nor bring back events a user switched off.
    private readonly object _queueLock = new();
    private readonly SemaphoreSlim _sending = new(1, 1);
    private readonly double _roll = Random.Shared.NextDouble();
    private List<TEvent>? _queue;
    private int _flushRequested;
    private int _failures;
    private DateTimeOffset _nextAttempt = DateTimeOffset.MinValue;

    private enum SendResult { Sent, Rejected, Retry }

    /// <summary>Whether counts may be sent at all; turning it off also forgets what was waiting.</summary>
    public bool Enabled
    {
        get => Preferences.Default.Get(options.EnabledKey, true);
        set
        {
            try
            {
                Preferences.Default.Set(options.EnabledKey, value);
                if (!value)
                {
                    Clear();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"{options.LogName} could not change the counts switch: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Queues the count <paramref name="make"/> builds from the sample rate and the time, unless counting is off for this launch (the
    /// function may answer null for a count that does not apply). Counting must never change what the user's action does: every failure
    /// here is logged and swallowed. Returns whether a count was queued.
    /// </summary>
    public bool Enqueue(Func<double, DateTimeOffset, TEvent?> make)
    {
        try
        {
            (bool queued, bool batchFull) = EnqueueCore(make);
            if (batchFull)
            {
                _ = FlushAsync();
            }

            return queued;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{options.LogName} could not queue a count: {ex.Message}");
            return false;
        }
    }

    private (bool Queued, bool BatchFull) EnqueueCore(Func<double, DateTimeOffset, TEvent?> make)
    {
        (bool enabled, double sampleRate) = options.Telemetry();
        if (!Enabled || !enabled || _roll >= sampleRate || make(sampleRate, DateTimeOffset.UtcNow) is not { } count)
        {
            return (false, false);
        }

        lock (_queueLock)
        {
            List<TEvent> queue = Queue();
            queue.Add(count);
            if (queue.Count > MaxQueued)
            {
                queue.RemoveRange(0, queue.Count - MaxQueued);
            }

            Save(queue);
            // Only the count that first fills a batch starts a send: while a send keeps failing the queue stays full and must not spawn one per count.
            return (true, queue.Count == options.MaxBatch);
        }
    }

    /// <summary>Sends what is queued, a batch at a time. Never throws; a batch that could not be delivered stays queued and is tried again after a growing pause. Calls overlap safely.</summary>
    public Task FlushAsync()
    {
        // Off the caller's thread: on Android a request begun on the main thread throws NetworkOnMainThreadException (the queue and preferences work is also kept off it).
        return Task.Run(FlushOnPoolAsync);
    }

    private async Task FlushOnPoolAsync()
    {
        if (!await _sending.WaitAsync(0))
        {
            // A send is running and may already have passed its last look at the queue: ask it to look once more.
            Interlocked.Exchange(ref _flushRequested, 1);
            return;
        }

        try
        {
            await FlushOnceAsync();
        }
        finally
        {
            _sending.Release();
        }

        // A request that arrived while this send ran, even just before the release, is served now rather than at the next count or launch.
        if (Interlocked.Exchange(ref _flushRequested, 0) == 1)
        {
            await FlushOnPoolAsync();
        }
    }

    private async Task FlushOnceAsync()
    {
        try
        {
            if (!Enabled)
            {
                Clear();
                return;
            }

            if (!options.TelemetryKnown())
            {
                // No config has ever been fetched, so "off" is only the default and says nothing: keep what is queued and try at the next flush.
                return;
            }

            if (!options.Telemetry().Enabled)
            {
                // Off means off: when the user's switch or a fetched remote config says so, whatever is queued is discarded, not kept for later.
                Clear();
                return;
            }

            if (DateTimeOffset.UtcNow >= _nextAttempt)
            {
                await SendQueuedAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"{options.LogName} could not send its counts: {ex.Message}");
            BackOff();
        }
    }

    private async Task SendQueuedAsync()
    {
        while (true)
        {
            List<TEvent> batch = Peek();
            if (batch.Count == 0)
            {
                return;
            }

            SendResult result = await PostAsync(batch);
            if (result == SendResult.Retry)
            {
                BackOff();
                return;
            }

            // A batch the Worker refused for good (old Worker, bad body) is dropped: sending it again could never succeed and would block the rest.
            _failures = 0;
            Remove(batch);
        }
    }

    private void BackOff()
    {
        _failures++;
        TimeSpan pause = FirstBackoff * Math.Pow(2, Math.Min(_failures - 1, 6));
        _nextAttempt = DateTimeOffset.UtcNow + (pause > MaxBackoff ? MaxBackoff : pause);
    }

    private async Task<SendResult> PostAsync(IReadOnlyList<TEvent> batch)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        string json = options.ToJson(batch);
#if DEBUG
        Debug.WriteLine($"{options.LogName} usage batch of {batch.Count}: {json}"); // counts hold no id or text, so the body is safe to log in a debug build
#endif
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await Http.PostAsync(new Uri(new Uri(options.BaseAddress), EventsPath), body, timeout.Token);
        return Classify(response.StatusCode);
    }

    // 2xx is delivered; 408 and 429 and every 5xx (the Worker's 503 when counting is not set up yet) are worth trying again; any other status is final.
    private static SendResult Classify(HttpStatusCode status)
    {
        int code = (int)status;
        if (code is >= 200 and < 300)
        {
            return SendResult.Sent;
        }

        return code is 408 or 429 or >= 500 ? SendResult.Retry : SendResult.Rejected;
    }

    private List<TEvent> Peek()
    {
        lock (_queueLock)
        {
            return Queue().Take(options.MaxBatch).ToList();
        }
    }

    private void Remove(IReadOnlyList<TEvent> sent)
    {
        lock (_queueLock)
        {
            List<TEvent> queue = Queue();
            // By reference, not by position or value: Enqueue may have trimmed the head meanwhile, and two counts can be equal.
            var delivered = new HashSet<TEvent>(sent, ReferenceEqualityComparer.Instance);
            if (queue.RemoveAll(delivered.Contains) > 0)
            {
                Save(queue);
            }
        }
    }

    private void Clear()
    {
        lock (_queueLock)
        {
            _queue = [];
            Save(_queue);
        }
    }

    // Call with _queueLock held.
    private List<TEvent> Queue() => _queue ??= options.ParseQueue(Preferences.Default.Get(options.QueueKey, (string?)null));

    // Call with _queueLock held.
    private void Save(IReadOnlyList<TEvent> queue)
    {
        if (queue.Count == 0)
        {
            Preferences.Default.Remove(options.QueueKey);
            return;
        }

        Preferences.Default.Set(options.QueueKey, options.QueueToJson(queue));
    }
}
