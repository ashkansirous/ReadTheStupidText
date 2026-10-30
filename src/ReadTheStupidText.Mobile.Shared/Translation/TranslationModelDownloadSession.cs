using System.Diagnostics;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Translation;

// Not ...Mobile.Shared.Translation: that name would hide ML Kit's own Translation class inside MlKitTranslator.
namespace ReadTheStupidText.Mobile.Shared.ModelDownload;

/// <summary>What the session needs to know about the network (Slice 187); the app's one connectivity watcher answers it, a test fakes it.</summary>
public interface ITranslationNetwork
{
    /// <summary>True when the phone has internet access right now.</summary>
    bool IsOnline { get; }

    /// <summary>The kinds of link that are active (Wi-Fi, cellular...).</summary>
    IReadOnlyCollection<NetworkProfile> Profiles { get; }

    /// <summary>Raised on any thread after the connectivity changed.</summary>
    event EventHandler? Changed;
}

/// <summary>The session's state at one moment, as the wait overlay reads it.</summary>
/// <param name="State">Where the download stands.</param>
/// <param name="Progress">0..1 of the model download when the platform reports it (iOS), else null.</param>
public readonly record struct TranslationDownloadSnapshot(TranslationDownloadState State, double? Progress);

/// <summary>
/// The one owner of a translation model download's state (Slice 187): Downloading, WaitingForInternet, Done, Failed or Cancelled. The platform
/// <c>MlKitTranslator</c> only runs one attempt and reports how it ended (it returns, or throws) and its progress; every move between states is made
/// here, by the pure <see cref="TranslationDownloadRetry"/> rules. A failed attempt with no internet waits; the network coming back restarts it
/// once per change (Wi-Fi or Ethernet only, unless the person went ahead on mobile data); Retry restarts at once on any network; Cancel and leaving
/// the page end it for good. One session per requested language pair, created by the caller and not reused.
/// </summary>
public sealed class TranslationModelDownloadSession : IDisposable
{
    /// <summary>How long a download may show no progress while there is no internet before it counts as a failed attempt and waits.</summary>
    public static readonly TimeSpan DefaultStallLimit = TimeSpan.FromSeconds(30);

    private const string PackKeyPrefix = "mlkit-";
    private const string EnglishCode = "en";
    private const char PackKeyJoiner = '+';
    private const int StallPollDivisor = 4;
    private const string FailedMessage = "The translation model could not be downloaded.";

    private readonly string _packKey;
    private readonly Func<IProgress<double>, CancellationToken, Task> _attempt;
    private readonly ITranslationNetwork _network;
    private readonly bool _mobileDataAllowed;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly object _gate = new();
    private TranslationDownloadState _state = TranslationDownloadState.Downloading;
    private double? _progress;
    private readonly TimeSpan _stallLimit;
    private TaskCompletionSource? _restart;
    private int _started;
    private Exception? _lastError; // guarded by _gate
    private long _lastProgressStamp = Stopwatch.GetTimestamp(); // monotonic: a clock change cannot jump the stall check or the waited time
    private TimeSpan _waited; // guarded by _gate
    private long? _waitStartedStamp; // guarded by _gate

    /// <param name="packKey">The pack named in the Debug log line, for example <c>mlkit-fa</c> (see <see cref="PackKey"/>).</param>
    /// <param name="attempt">One download attempt: returns when the model is there, throws when it is not. Gets a progress sink and the session's cancellation.</param>
    /// <param name="network">The connectivity watcher.</param>
    /// <param name="mobileDataAllowed">True when the person began the download on mobile data, so a network change on mobile data may restart it.</param>
    /// <param name="stallLimit">How long an attempt may show no progress with no internet before it counts as failed (default <see cref="DefaultStallLimit"/>).</param>
    public TranslationModelDownloadSession(string packKey, Func<IProgress<double>, CancellationToken, Task> attempt, ITranslationNetwork network, bool mobileDataAllowed, TimeSpan? stallLimit = null)
    {
        _stallLimit = stallLimit ?? DefaultStallLimit;
        _packKey = packKey;
        _attempt = attempt;
        _network = network;
        _mobileDataAllowed = mobileDataAllowed;
    }

    /// <summary>Raised on any thread whenever the state or the progress changes.</summary>
    public event Action<TranslationDownloadSnapshot>? Changed;

    public TranslationDownloadSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return new TranslationDownloadSnapshot(_state, _progress);
            }
        }
    }

    /// <summary>Total time spent in the waiting-for-internet state so far, so a timing of the download can leave it out.</summary>
    public TimeSpan WaitedTotal
    {
        get
        {
            lock (_gate)
            {
                return _waitStartedStamp is { } started ? _waited + Stopwatch.GetElapsedTime(started) : _waited;
            }
        }
    }

    /// <summary>The name a pack gets in the log: <c>mlkit-</c> and the language codes (English left out when another language is named).</summary>
    public static string PackKey(params string[] languages)
    {
        string[] named = [.. languages.Where(language => language != EnglishCode).Distinct()];
        return PackKeyPrefix + string.Join(PackKeyJoiner, named.Length > 0 ? named : languages.Distinct());
    }

    /// <summary>
    /// Runs the download until it is done. Returns when the model is there; throws <see cref="TranslationModelUnavailableException"/> when it failed
    /// with internet; throws <see cref="OperationCanceledException"/> after Cancel or when <paramref name="cancellationToken"/> is cancelled.
    /// May be called once.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            throw new InvalidOperationException("A download session runs once.");
        }

        using CancellationTokenRegistration linked = cancellationToken.Register(Cancel);
        _network.Changed += OnNetworkChanged;
        try
        {
            await RunAttemptsAsync();
        }
        finally
        {
            _network.Changed -= OnNetworkChanged; // first: nothing restarts after the session ended
        }
    }

    /// <summary>Retry now, on any network. Does nothing (returns false) unless the download is waiting.</summary>
    public bool Retry()
    {
        if (!TryRestart())
        {
            return false;
        }

        PackDownloadLog.ResumeByRetry(_packKey);
        return true;
    }

    /// <summary>Stops the download for good; nothing restarts after this.</summary>
    public void Cancel()
    {
        lock (_gate)
        {
            EndWaitClock();
            _state = TranslationDownloadRetry.AfterCancel(_state);
            _restart?.TrySetCanceled();
        }

        try
        {
            _lifetime.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return; // disposed after the end: nothing left to stop
        }

        RaiseChanged();
    }

    public void Dispose()
    {
        Cancel();
        _lifetime.Dispose();
    }

    private async Task RunAttemptsAsync()
    {
        var progress = new Progress<double>(ReportProgress);
        while (true)
        {
            _lifetime.Token.ThrowIfCancellationRequested();
            AttemptEnd end = await TryAttemptAsync(progress);
            TranslationDownloadState next = Settle(end);
            if (next == TranslationDownloadState.Done)
            {
                return;
            }

            if (next == TranslationDownloadState.Failed)
            {
                throw new TranslationModelUnavailableException(FailedMessage, LastError());
            }

            _lifetime.Token.ThrowIfCancellationRequested();
            await WaitForRestartAsync();
        }
    }

    // What one attempt ended with, and whether the phone had internet at the moment it ended (the first writer wins: a stall that cancels the
    // attempt keeps its own verdict, not the cancellation the attempt throws afterwards).
    private sealed class AttemptEnd
    {
        private readonly object _lock = new();
        private bool _set;

        public bool Succeeded { get; private set; }

        public bool Online { get; private set; }

        public Exception? Error { get; private set; }

        public void Set(bool succeeded, bool online, Exception? error)
        {
            lock (_lock)
            {
                if (_set)
                {
                    return;
                }

                _set = true;
                Succeeded = succeeded;
                Online = online;
                Error = error;
            }
        }
    }

    private async Task<AttemptEnd> TryAttemptAsync(IProgress<double> progress)
    {
        using CancellationTokenSource attemptEnd = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var end = new AttemptEnd();
        Volatile.Write(ref _lastProgressStamp, Stopwatch.GetTimestamp());
        Task watchdog = WatchForStallAsync(attemptEnd, end);
        try
        {
            await _attempt(progress, attemptEnd.Token);
            end.Set(true, _network.IsOnline, null);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"A translation model download attempt failed: {ex.Message}");
            end.Set(false, _network.IsOnline, ex);
        }
        finally
        {
            attemptEnd.Cancel(); // the attempt is over (or stalled): stop its watchdog
            await watchdog;
        }

        return end;
    }

    // A download that shows no progress and has no internet for the stall limit is a failed attempt: the platform may keep its task pending
    // offline instead of failing, which would leave the overlay on a spinner. Ends the attempt by cancelling it (not the session).
    private async Task WatchForStallAsync(CancellationTokenSource attempt, AttemptEnd end)
    {
        long quietSince = Stopwatch.GetTimestamp();
        try
        {
            while (true)
            {
                await Task.Delay(_stallLimit / StallPollDivisor, attempt.Token);
                bool online = _network.IsOnline;
                long now = Stopwatch.GetTimestamp();
                quietSince = online ? now : Math.Max(quietSince, Volatile.Read(ref _lastProgressStamp));
                if (Stopwatch.GetElapsedTime(quietSince, now) >= _stallLimit)
                {
                    end.Set(false, online, new TimeoutException("The download stalled with no internet."));
                    attempt.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // the attempt ended by itself
        }
    }

    private Exception? LastError()
    {
        lock (_gate)
        {
            return _lastError;
        }
    }

    // The attempt's end is turned into the next state in one place, under the lock, so a Cancel or a restart cannot slip in between.
    private TranslationDownloadState Settle(AttemptEnd end)
    {
        TranslationDownloadState next;
        lock (_gate)
        {
            _lastError = end.Error;
            next = TranslationDownloadRetry.AfterAttempt(_state, end.Succeeded, end.Online);
            _state = next;
            if (next == TranslationDownloadState.WaitingForInternet)
            {
                _waitStartedStamp = Stopwatch.GetTimestamp();
                PackDownloadLog.Waiting(_packKey);
                _restart = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        RaiseChanged();
        if (next == TranslationDownloadState.WaitingForInternet && RestartIfNetworkAllows())
        {
            PackDownloadLog.Resume(PackResumeReason.Network, _packKey, 0); // the network came back between the failure and the wait
        }

        return next;
    }

    private async Task WaitForRestartAsync()
    {
        TaskCompletionSource? restart;
        lock (_gate)
        {
            restart = _restart;
        }

        if (restart is not null)
        {
            await restart.Task;
        }
    }

    private void OnNetworkChanged(object? sender, EventArgs e)
    {
        if (RestartIfNetworkAllows())
        {
            PackDownloadLog.Resume(PackResumeReason.Network, _packKey, 0);
        }
    }

    private bool RestartIfNetworkAllows()
    {
        lock (_gate)
        {
            if (!TranslationDownloadRetry.ShouldRestartOnNetwork(_state, _network.IsOnline, _network.Profiles, _mobileDataAllowed))
            {
                return false;
            }
        }

        return TryRestart();
    }

    private bool TryRestart()
    {
        lock (_gate)
        {
            if (!TranslationDownloadRetry.CanRetry(_state) || _restart is null)
            {
                return false;
            }

            EndWaitClock();
            _state = TranslationDownloadState.Downloading;
            TaskCompletionSource restart = _restart;
            _restart = null;
            restart.TrySetResult();
        }

        RaiseChanged();
        return true;
    }

    private void EndWaitClock()
    {
        if (_waitStartedStamp is { } started)
        {
            _waited += Stopwatch.GetElapsedTime(started);
            _waitStartedStamp = null;
        }
    }

    private void ReportProgress(double fraction)
    {
        Volatile.Write(ref _lastProgressStamp, Stopwatch.GetTimestamp());
        lock (_gate)
        {
            if (_state != TranslationDownloadState.Downloading)
            {
                return;
            }

            _progress = Math.Clamp(fraction, 0, 1);
        }

        RaiseChanged();
    }

    private void RaiseChanged()
    {
        try
        {
            Changed?.Invoke(Snapshot);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"A download session subscriber failed: {ex.Message}"); // the display must never change what the download does
        }
    }
}
