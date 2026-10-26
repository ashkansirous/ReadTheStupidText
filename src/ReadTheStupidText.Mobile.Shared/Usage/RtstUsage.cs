using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace ReadTheStupidText.Mobile.Shared.Usage;

/// <summary>
/// Read The Stupid Text's anonymous usage counts on the phone (Slice 137, Decisions 143-145): the shared queue and config client
/// (<see cref="UsageQueueSender{TEvent}"/>, <see cref="RemoteConfigClient{TConfig}"/>) pointed at <c>readthestupidtext.sirous.uk</c> with
/// this app's own preference keys. A count is a name plus closed-list values (<see cref="UsageEvents"/>): no id, no text, nothing finer than
/// the day. Counts are queued and sent in batches a short while after the first one, and when the app next starts. Nothing is queued or
/// sent while the user's switch is off, while the remote config has telemetry off (it stays off until the store privacy declarations match,
/// Decision 145), or when this launch was not picked by the config's sample rate.
/// </summary>
public sealed class RtstUsage
{
    public const string BaseAddress = "https://readthestupidtext.sirous.uk";

    /// <summary>The user's switch (Settings, "Help improve the app"). On by default (Decision 143).</summary>
    public const string EnabledKey = "rtst.usage.enabled";

    /// <summary>Set once the person has been told about the counts (the first-run language screen, or the one-time notice an update shows).</summary>
    public const string NoticeSeenKey = "rtst.usage.noticeSeen";

    private const string QueueKey = "rtst.usage.queue";
    private const string ConfigCacheKey = "rtst.appconfig.json";
    private const string LogName = "Read The Stupid Text";
    private static readonly TimeSpan FlushDelay = TimeSpan.FromSeconds(30);

    private readonly RemoteConfigClient<UsageConfig> _config = new(new RemoteConfigOptions<UsageConfig>(
        BaseAddress, ConfigCacheKey, LogName, UsageConfig.Default, UsageConfig.Parse, UsageConfig.TryParseServed));

    private readonly UsageQueueSender<UsageEvent> _sender;
    private int _flushScheduled;
    private Func<UsageContext, UsageEvent?>? _skippedOpened; // this session's launch count, held while the switch was off

    public RtstUsage()
    {
        _sender = new UsageQueueSender<UsageEvent>(new UsageQueueOptions<UsageEvent>(
            BaseAddress, EnabledKey, QueueKey, LogName, UsageBatch.MaxEvents, CurrentTelemetry, () => _config.HasCopy,
            UsageBatch.ToJson, UsageBatch.ParseQueue, UsageBatch.QueueToJson));
    }

    /// <summary>Whether counts may be sent at all; turning it off also forgets what was waiting.</summary>
    public bool Enabled
    {
        get => _sender.Enabled;
        set
        {
            bool turnedOn = value && !_sender.Enabled;
            _sender.Enabled = value;
            if (turnedOn)
            {
                Func<UsageContext, UsageEvent?>? opened = Interlocked.Exchange(ref _skippedOpened, null);
                _ = FetchConfigAndFlushAsync(opened); // switched on mid-session: StartAsync made no fetch while it was off
            }
        }
    }

    /// <summary>
    /// Queues the count <paramref name="make"/> builds (null for a count that does not apply), unless counting is off. Never throws:
    /// counting must not change what the person's action does.
    /// </summary>
    public void Record(Func<UsageContext, UsageEvent?> make)
    {
        bool queued = _sender.Enqueue((sampleRate, now) => make(new UsageContext(UsageEnvironment.InstalledVersion, UsageEnvironment.Platform, sampleRate, now)));
        if (queued)
        {
            ScheduleFlush();
        }
    }

    /// <summary>Counts every wait overlay that showed (<c>wait_shown</c>) and every Cancel on one (<c>wait_cancelled</c>), Slice 148. Call once at start-up.</summary>
    public void CountWaits()
    {
        WaitController.Finished -= RecordWait;
        WaitController.Finished += RecordWait;
    }

    private void RecordWait(WaitFinish finish)
    {
        Record(context => UsageEvents.WaitShown(context, finish.Stage, finish.Shown));
        if (finish.Cancelled)
        {
            Record(context => UsageEvents.WaitCancelled(context, finish.Stage, finish.Shown));
        }
    }

    /// <summary>True once, the first time it is asked on an install that was never told about the counts (one that updated from before Slice 137); marks the notice seen.</summary>
    public static bool TakeUnseenNotice()
    {
        try
        {
            if (Preferences.Default.Get(NoticeSeenKey, false))
            {
                return false;
            }

            Preferences.Default.Set(NoticeSeenKey, true);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Fetches the remote config, counts the launch with <paramref name="opened"/> (only once a config is held: before that the default is
    /// off and the count would be dropped; if the fetch failed, the count waits for the config's first arrival), then sends what is queued.
    /// Does nothing while the switch is off. Never throws; run it at app start.
    /// </summary>
    public async Task StartAsync(Func<UsageContext, UsageEvent?> opened)
    {
        if (!Enabled)
        {
            _skippedOpened = opened; // the switch is off: no config fetch, no count, no send (Slice 148); kept so turning it on counts this session's launch once
            return;
        }

        await FetchConfigAndFlushAsync(opened).ConfigureAwait(false);
    }

    private async Task FetchConfigAndFlushAsync(Func<UsageContext, UsageEvent?>? opened)
    {
        try
        {
            await _config.RefreshAsync().ConfigureAwait(false);
            if (opened is not null)
            {
                RecordLaunch(opened);
            }

            await _sender.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Usage start failed: {ex.Message}"); // counting must never change what the app does
        }
    }

    private void RecordLaunch(Func<UsageContext, UsageEvent?> opened)
    {
        if (_config.HasCopy)
        {
            Record(opened);
        }
        else
        {
            RecordWhenConfigArrives(opened);
        }
    }

    // The fetch failed (offline cold start) and its retries run on their own: count the launch when one finally brings a config (a Changed
    // arrives on the main thread; a config equal to the default means counting is off anyway, so nothing is lost by not recording then).
    private void RecordWhenConfigArrives(Func<UsageContext, UsageEvent?> opened)
    {
        void OnChanged(object? sender, EventArgs e)
        {
            _config.Changed -= OnChanged;
            Record(opened);
            _ = _sender.FlushAsync();
        }

        _config.Changed += OnChanged;
    }

    private (bool Enabled, double SampleRate) CurrentTelemetry()
    {
        TelemetrySettings telemetry = _config.Current.Telemetry;
        return (telemetry.Enabled, telemetry.SampleRate);
    }

    // One send a little after the first count of a burst, so a reading session's counts leave together.
    private void ScheduleFlush()
    {
        if (!Enabled || Interlocked.Exchange(ref _flushScheduled, 1) == 1)
        {
            return;
        }

        _ = FlushAfterDelayAsync();
    }

    private async Task FlushAfterDelayAsync()
    {
        await Task.Delay(FlushDelay).ConfigureAwait(false);
        Interlocked.Exchange(ref _flushScheduled, 0);
        await _sender.FlushAsync().ConfigureAwait(false);
    }
}
