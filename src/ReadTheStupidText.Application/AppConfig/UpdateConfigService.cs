using System.Diagnostics;
using ReadTheStupidText.Application.Usage;

namespace ReadTheStupidText.Application.AppConfig;

/// <summary>
/// The desktop's forced-update check (Slice 217): fetches the remote config over <see cref="IUsageTransport"/>, keeps the last good copy in
/// <see cref="IUsageStore"/> (the same body the usage counts keep) and answers <see cref="State"/> from it, whether or not the usage switch is on.
/// A refresh runs at most every 15 minutes (one minute after a failure) and never twice at once; a failure, or a body that is not a served
/// config, leaves the copy in force; with no copy at all there is no update. <see cref="Changed"/> is raised on a pool thread. A
/// <c>testConfigJson</c> (the <see cref="TestConfigVariable"/> of a test run) replaces the fetched config and lets a test build be asked to update;
/// nothing is fetched or kept then, so a test never reads or changes the live config.
/// </summary>
public sealed class UpdateConfigService
{
    /// <summary>The environment variable a test run sets to a config body, so a scripted check never depends on the live config file (Slice 217).</summary>
    public const string TestConfigVariable = "READTHESTUPIDTEXT_TEST_APP_CONFIG";

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(10);

    private readonly IUsageStore _store;
    private readonly IUsageTransport _transport;
    private readonly AppPlatform _platform;
    private readonly string _installed;
    private readonly bool _buildMayUpdate;
    private readonly TimeProvider _clock;
    private readonly UsageConfig? _testConfig;

    private readonly object _gate = new();
    private UsageConfig? _current;
    private Task? _inFlight;
    private DateTimeOffset _notBefore = DateTimeOffset.MinValue;

    public UpdateConfigService(IUsageStore store, IUsageTransport transport, AppPlatform platform, string installed, bool buildMayUpdate, string? testConfigJson = null, TimeProvider? clock = null)
    {
        _store = store;
        _transport = transport;
        _platform = platform;
        _installed = installed;
        _buildMayUpdate = buildMayUpdate;
        _clock = clock ?? TimeProvider.System;
        _testConfig = string.IsNullOrWhiteSpace(testConfigJson) ? null : UsageConfig.Parse(testConfigJson);
    }

    /// <summary>Raised when a fetch brought a config that differs from the one held (on a pool thread).</summary>
    public event EventHandler? Changed;

    /// <summary>True once a fetch has brought a served config in this run.</summary>
    public bool HasFetched { get; private set; }

    /// <summary>The config in force: the test config, else the last good copy, else the default.</summary>
    public UsageConfig Config => _testConfig ?? Held();

    /// <summary>What the config in force asks of this build.</summary>
    public UpdateState State => UpdateDecision.Evaluate(Config, _platform, _installed, _buildMayUpdate || _testConfig is not null);

    /// <summary>The newest release the config names for this platform (what a dismissal is remembered against).</summary>
    public string LatestVersion => Config.For(_platform).LatestVersion;

    /// <summary>Fetches the config and keeps it, at most every 15 minutes. Never throws.</summary>
    public Task RefreshAsync()
    {
        if (_testConfig is not null)
        {
            return Task.CompletedTask;
        }

        lock (_gate)
        {
            if (_inFlight is { IsCompleted: false } running)
            {
                return running;
            }

            if (_clock.GetUtcNow() < _notBefore)
            {
                return Task.CompletedTask;
            }

            _notBefore = _clock.GetUtcNow() + RetryInterval;
            return _inFlight = Task.Run(FetchAndApplyAsync);
        }
    }

    private async Task FetchAndApplyAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(FetchTimeout);
            string body = await _transport.FetchConfigAsync(timeout.Token).ConfigureAwait(false);
            if (UsageConfig.TryParseServed(body, out UsageConfig fetched))
            {
                Apply(body, fetched);
            }
            else
            {
                Debug.WriteLine("Update check ignored a remote config body that is not a config.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check could not load the remote config: {ex.Message}");
        }
    }

    private void Apply(string body, UsageConfig fetched)
    {
        Held(); // load the kept copy before it is overwritten, so a change is measured against it
        TryKeep(body);
        bool changed;
        lock (_gate)
        {
            changed = !Equals(_current, fetched);
            _current = fetched;
            HasFetched = true;
            _notBefore = _clock.GetUtcNow() + Interval;
        }

        if (changed)
        {
            RaiseChanged();
        }
    }

    // A handler that throws must not fault the pool task.
    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check: a change handler failed: {ex.Message}");
        }
    }

    private void TryKeep(string body)
    {
        try
        {
            _store.SaveConfig(body);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check could not keep the remote config: {ex.Message}");
        }
    }

    private UsageConfig Held()
    {
        lock (_gate)
        {
            if (_current is not null)
            {
                return _current;
            }
        }

        UsageConfig kept = ReadKept();
        lock (_gate)
        {
            return _current ??= kept;
        }
    }

    // File I/O stays outside the lock; only a body that is a served config counts as a kept copy.
    private UsageConfig ReadKept()
    {
        try
        {
            return UsageConfig.TryParseServed(_store.LoadConfig(), out UsageConfig kept) ? kept : UsageConfig.Default;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check could not read the kept config: {ex.Message}");
            return UsageConfig.Default;
        }
    }
}
