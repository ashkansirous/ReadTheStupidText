using System.Collections.Concurrent;
using System.Globalization;
using Foundation;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared.Network;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// iOS's <see cref="IPackBodyFetcher"/> (Slice 183): a pack body downloads through a background <c>NSURLSession</c>, so the
/// system keeps it going with the phone locked or the app suspended, and relaunches the app when it is done. The bytes land in
/// the same <c>.partial</c> file the HTTP path uses (appended after a Range request), so the verify/install path that follows
/// is unchanged. When the app was killed meanwhile, the session delegate still moves the finished body into the partial file
/// at the next launch (under the same per-file lock the next run reads the length with); the next run (the Slice 182 resume)
/// then finds the body complete and only verifies it. A task that drops or is paused keeps its Apple resume data beside the
/// partial file, so no fetched byte is lost. A download that cannot go on (no network, Wi-Fi gone while mobile data is not
/// allowed) fails as an offline transport error, so Slice 182's waiting line and automatic resume work here too. iOS shows
/// no permission question for the download itself; when the run ends unfinished (a terminal transport error, after the service's retries) while the
/// app is in the background, a quiet "Download paused" notice is posted by the host (Slice 208, <see cref="IosPackNotification"/>), which this class hands
/// over as its <see cref="IPackPausedNotifier"/>.
/// </summary>
public sealed class IosBackgroundDownloader : NSUrlSessionDownloadDelegate, IPackBodyFetcher, IPackPausedNotifier
{
    private const char DescriptionSeparator = '|';
    private const int PartialContent = 206;
    private const int Ok = 200;
    private const string RangeHeader = "Range";
    private const string ResumeSuffix = ".resume";
    private const long NoTask = -1;
    private const int StuckChecksBeforeOffline = 3;
    private const int ResumeHeaderBytes = sizeof(long) + 1;

    private static readonly TimeSpan MonitorInterval = TimeSpan.FromSeconds(5);
    private static readonly Lazy<IosBackgroundDownloader> Shared = new(() => new IosBackgroundDownloader());
    private static readonly ConcurrentDictionary<string, object> PartialLocks = new();
    private static readonly object CompletionGate = new();
    private static Action? _systemCompletion;
    private static bool _eventsFinished;
    private static int _eventsRound; // numbers each early report, so a stale one is cleared after a short wait
    private static readonly TimeSpan EarlyEventsWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CancelWait = TimeSpan.FromSeconds(10);

    private readonly NSUrlSession _session;
    private readonly ConcurrentDictionary<string, Waiter> _waiters = new(); // by partial file: one transfer per file
    private readonly ConcurrentDictionary<nuint, string> _failures = new();

    private sealed class Waiter(string packId, TaskCompletionSource done, IProgress<double>? progress, long offset, long total, bool allowsCellular)
    {
        public string PackId { get; } = packId;

        public TaskCompletionSource Done { get; } = done;

        public IProgress<double>? Progress { get; } = progress;

        public long Offset { get; } = offset;

        public long Total { get; } = total;

        public bool AllowsCellular { get; } = allowsCellular;

        public long TaskId { get; set; } = NoTask;

        public volatile bool WaitingForConnectivity;

        public volatile bool Stuck; // the watcher ended this transfer as an offline failure
    }

    private IosBackgroundDownloader()
    {
        // Created at start-up with the same identifier every launch, so a session the system relaunched the app for reconnects.
        string id = (NSBundle.MainBundle.BundleIdentifier ?? "app") + ".packs";
        using NSUrlSessionConfiguration config = NSUrlSessionConfiguration.CreateBackgroundSessionConfiguration(id);
        config.Discretionary = false; // the user asked for it
        config.SessionSendsLaunchEvents = true;
        config.AllowsCellularAccess = true; // the Wi-Fi rule is set on each request (see CreateTask)
        _session = NSUrlSession.FromConfiguration(config, this, null);
    }

    public static IosBackgroundDownloader Instance => Shared.Value;

    private static readonly IosPackNotification Notice = new();

    public void Post(string packId, double? fraction) => Notice.Post(packId, fraction);

    public void Clear(string packId) => Notice.Clear(packId);

    /// <summary>A task in the background session still writes this partial: one a waiter here runs, or one the system kept across a relaunch (no waiter yet).</summary>
    public async Task<bool> HasLiveTransferAsync(string partial)
    {
        if (_waiters.ContainsKey(partial))
        {
            return true;
        }

        NSUrlSessionActiveTasks active = await _session.GetTasksAsync().ConfigureAwait(false);
        return active.DownloadTasks.OfType<NSUrlSessionDownloadTask>().Any(t =>
            Parse(t.TaskDescription).Partial == partial && t.State is NSUrlSessionTaskState.Running or NSUrlSessionTaskState.Suspended);
    }

    /// <summary>The scan deleted the partial: its Apple resume data goes with it, or a later task would continue onto a file that is gone.</summary>
    public void DiscardLeftovers(string partial) => DeleteResumeData(partial);

    /// <summary>The app delegate's <c>HandleEventsForBackgroundUrl</c> hands the system's completion handler here.</summary>
    public static void HandleEventsForBackgroundUrl(Action completionHandler)
    {
        Action? ready = null;
        lock (CompletionGate)
        {
            if (_eventsFinished)
            {
                _eventsFinished = false;
                ready = completionHandler; // the session reported its events before the handler arrived
            }
            else
            {
                _systemCompletion = completionHandler; // set before the session can report: touching Instance creates it
            }
        }

        _ = Instance;
        if (ready is not null)
        {
            NSOperationQueue.MainQueue.AddOperation(ready);
        }
    }

    public async Task FetchAsync(PackInfo pack, string partial, long offset, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        long start = CurrentLength(partial); // the relaunch may have appended the finished body since the caller read the length
        if (start >= pack.SizeBytes)
        {
            return;
        }

        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiter = new Waiter(pack.Id, done, progress, start, pack.SizeBytes, PackResumeWatcher.CellularOnlyKnown());
        _ = IosPackNotification.EnsureProvisionalAuthorizationAsync(); // Slice 208: asked once, the first time a pack download starts; provisional, so no prompt
        _waiters[partial] = waiter; // before any task is looked up or created, so no completion can be missed
        using var monitorCancel = new CancellationTokenSource();
        try
        {
            await RunTransferAsync(pack, partial, waiter, monitorCancel.Token, cancellationToken).ConfigureAwait(false);
            if (CurrentLength(partial) < pack.SizeBytes)
            {
                throw new IOException("The pack download ended short of the whole file.");
            }
        }
        finally
        {
            monitorCancel.Cancel();
            _waiters.TryRemove(new KeyValuePair<string, Waiter>(partial, waiter));
        }
    }

    private async Task RunTransferAsync(PackInfo pack, string partial, Waiter waiter, CancellationToken monitorToken, CancellationToken cancellationToken)
    {
        string description = Describe(partial, waiter.Offset, pack.SizeBytes);
        NSUrlSessionDownloadTask task = await FindRunningAsync(description, partial, waiter).ConfigureAwait(false) ?? CreateTask(pack, partial, waiter, description);
        waiter.TaskId = (long)task.TaskIdentifier;
        using CancellationTokenRegistration registration = cancellationToken.Register(() => _ = PauseAsync(task, partial, waiter.Offset));
        _ = WatchNetworkAsync(waiter, task, partial, monitorToken);
        if (task.State == NSUrlSessionTaskState.Suspended)
        {
            task.Resume();
        }

        PackDownloadHost.LogService("background-session", pack.Id);
        await waiter.Done.Task.ConfigureAwait(false);
    }

    // A task the previous process started for the same file and offset carries on; a second one would fetch the same bytes twice.
    // A task that is still cancelling (state Canceling) is never taken. A task whose mobile-data setting differs from this run's is
    // cancelled (its resume data kept) and waited for, and the caller makes a new one: a running task keeps the setting it was made with.
    private async Task<NSUrlSessionDownloadTask?> FindRunningAsync(string description, string partial, Waiter waiter)
    {
        NSUrlSessionActiveTasks active = await _session.GetTasksAsync().ConfigureAwait(false);
        NSUrlSessionDownloadTask? found = active.DownloadTasks.OfType<NSUrlSessionDownloadTask>().FirstOrDefault(t => t.TaskDescription == description
            && (t.State == NSUrlSessionTaskState.Running || t.State == NSUrlSessionTaskState.Suspended));
        if (found is null || CellularOf(found) == waiter.AllowsCellular)
        {
            return found;
        }

        await PauseAsync(found, partial, waiter.Offset).ConfigureAwait(false);
        return null;
    }

    private static bool CellularOf(NSUrlSessionTask task) => task.OriginalRequest?.AllowsCellularAccess ?? true;

    // Wi-Fi rule: a download that started on Wi-Fi never moves to mobile data when Wi-Fi drops; one that started on mobile data only was
    // allowed to by the caller (first run, or the grown-up's "Download now"), so it may use it.
    private NSUrlSessionDownloadTask CreateTask(PackInfo pack, string partial, Waiter waiter, string description)
    {
        NSUrlSessionDownloadTask task;
        if (ReadResumeData(partial, waiter.Offset, waiter.AllowsCellular) is { } resumeData)
        {
            task = _session.CreateDownloadTaskFromResumeData(resumeData, null);
        }
        else
        {
            using var request = new NSMutableUrlRequest(new NSUrl(pack.Url)) { AllowsCellularAccess = waiter.AllowsCellular };
            if (waiter.Offset > 0)
            {
                request.Headers = NSDictionary.FromObjectsAndKeys(new NSObject[] { new NSString($"bytes={waiter.Offset}-") }, new NSObject[] { new NSString(RangeHeader) });
            }

            task = _session.CreateDownloadTask(request);
        }

        task.TaskDescription = description;
        return task;
    }

    // Stops the task but keeps what it fetched as Apple resume data, so the next task continues instead of starting over. The task
    // returned completes when the cancel has finished (the resume data is saved), so a retry never races it.
    private static Task PauseAsync(NSUrlSessionDownloadTask task, string partial, long offset)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool cellular = CellularOf(task);
        task.Cancel(data =>
        {
            try
            {
                SaveResumeData(partial, offset, cellular, data);
            }
            catch (Exception ex)
            {
                LogDelegateFailure(nameof(PauseAsync), ex);
            }
            finally
            {
                finished.TrySetResult();
            }
        });
        return finished.Task.WaitAsync(CancelWait).ContinueWith(_ => { }, TaskScheduler.Default);
    }

    private static void LogDelegateFailure(string where, Exception ex) =>
        PackDownloadHost.LogService("ios-delegate-failed", $"{where}:{ex.GetType().Name}");

    // No progress is not an error for a background session (it waits for the network), so a stuck transfer is detected here and
    // ended as an offline failure: Slice 182's waiting line and its restart when the network or Wi-Fi is back then apply.
    private async Task WatchNetworkAsync(Waiter waiter, NSUrlSessionDownloadTask task, string partial, CancellationToken token)
    {
        int stuck = 0;
        using var timer = new PeriodicTimer(MonitorInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                stuck = IsStuck(waiter) ? stuck + 1 : 0;
                if (stuck >= StuckChecksBeforeOffline)
                {
                    waiter.Stuck = true; // DidCompleteWithError then reports this offline failure, not a plain cancel
                    await PauseAsync(task, partial, waiter.Offset).ConfigureAwait(false);
                    waiter.Done.TrySetException(new HttpRequestException(HttpRequestError.ConnectionError, "The pack download is waiting for the network."));
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // the transfer ended
        }
    }

    private static bool IsStuck(Waiter waiter) =>
        waiter.WaitingForConnectivity
        || Connectivity.Current.NetworkAccess == Microsoft.Maui.Networking.NetworkAccess.None
        || (!waiter.AllowsCellular && PackResumeWatcher.CellularOnly());

    public override void TaskIsWaitingForConnectivity(NSUrlSession session, NSUrlSessionTask task)
    {
        try
        {
            if (WaiterOf(task) is { } waiter)
            {
                waiter.WaitingForConnectivity = true;
            }
        }
        catch (Exception ex)
        {
            LogDelegateFailure(nameof(TaskIsWaitingForConnectivity), ex);
        }
    }

    public override void DidWriteData(NSUrlSession session, NSUrlSessionDownloadTask downloadTask, long bytesWritten, long totalBytesWritten, long totalBytesExpectedToWrite)
    {
        try
        {
            if (WaiterOf(downloadTask) is { Total: > 0 } waiter)
            {
                waiter.WaitingForConnectivity = false;
                waiter.Progress?.Report(Math.Min(1.0, (waiter.Offset + totalBytesWritten) / (double)waiter.Total));
            }
        }
        catch (Exception ex)
        {
            LogDelegateFailure(nameof(DidWriteData), ex); // a progress handler or an odd task description must never unwind into native dispatch
        }
    }

    // The file at location is deleted when this returns: the move into the partial file is finished in here.
    public override void DidFinishDownloading(NSUrlSession session, NSUrlSessionDownloadTask downloadTask, NSUrl location)
    {
        try
        {
            (string partial, long offset, long size) = Parse(downloadTask.TaskDescription);
            int status = downloadTask.Response is NSHttpUrlResponse response ? (int)response.StatusCode : (offset > 0 ? PartialContent : Ok);
            lock (LockFor(partial))
            {
                StoreBody(location.Path!, partial, offset, size, status);
                DeleteResumeData(partial);
            }
        }
        catch (Exception ex)
        {
            LogDelegateFailure(nameof(DidFinishDownloading), ex);
            _failures[downloadTask.TaskIdentifier] = ex.Message;
        }
    }

    private static void StoreBody(string downloaded, string partial, long offset, long size, int status)
    {
        long current = File.Exists(partial) ? new FileInfo(partial).Length : 0;
        if (current >= size)
        {
            return; // a relaunch already stored this body: the partial is complete, keep it
        }

        if (status == Ok)
        {
            File.Move(downloaded, partial, overwrite: true); // the server ignored Range: the whole body
            return;
        }

        if (status == PartialContent && current == offset)
        {
            using var target = new FileStream(partial, FileMode.Append, FileAccess.Write);
            using var source = new FileStream(downloaded, FileMode.Open, FileAccess.Read);
            source.CopyTo(target);
            return;
        }

        if (File.Exists(partial))
        {
            File.Delete(partial); // a stale partial: the next attempt starts clean
        }

        throw new IOException($"The pack download answered {status}.");
    }

    public override void DidCompleteWithError(NSUrlSession session, NSUrlSessionTask task, NSError? error)
    {
        try
        {
            CompleteTask(task, error);
        }
        catch (Exception ex)
        {
            LogDelegateFailure(nameof(DidCompleteWithError), ex);
        }
    }

    private void CompleteTask(NSUrlSessionTask task, NSError? error)
    {
        _failures.TryRemove(task.TaskIdentifier, out string? failure);
        bool cancelled = error is not null && error.Code == (nint)NSUrlError.Cancelled;
        TryKeepOrDropResumeData(task, error); // a disk error here must not leave the waiter hanging
        if (WaiterOf(task) is not { } waiter || (waiter.TaskId == NoTask && cancelled))
        {
            return; // no run is waiting (the app was relaunched): the body, if it came, is already in the partial file
        }

        if (error is null && failure is null)
        {
            waiter.Done.TrySetResult();
        }
        else if (cancelled && waiter.Stuck)
        {
            waiter.Done.TrySetException(new HttpRequestException(HttpRequestError.ConnectionError, "The pack download is waiting for the network."));
        }
        else if (cancelled)
        {
            waiter.Done.TrySetCanceled();
        }
        else
        {
            waiter.Done.TrySetException(ToException(error, failure));
        }
    }

    private static void TryKeepOrDropResumeData(NSUrlSessionTask task, NSError? error)
    {
        try
        {
            KeepOrDropResumeData(task, error);
        }
        catch (Exception ex)
        {
            LogDelegateFailure(nameof(KeepOrDropResumeData), ex);
        }
    }

    // Apple hands the bytes it fetched back as resume data on a failure; without it a stale resume file must not be tried again.
    private static void KeepOrDropResumeData(NSUrlSessionTask task, NSError? error)
    {
        if (error is null || string.IsNullOrEmpty(task.TaskDescription))
        {
            return;
        }

        (string partial, long offset, _) = Parse(task.TaskDescription);
        if (error.UserInfo?[TaskResumeDataKey] is NSData data)
        {
            SaveResumeData(partial, offset, CellularOf(task), data);
        }
        else if (error.Code != (nint)NSUrlError.Cancelled)
        {
            DeleteResumeData(partial);
        }
    }

    // Transport errors become an offline failure (the pack service then reports Offline); anything else is a plain failure.
    private static HttpRequestException ToException(NSError? error, string? failure)
    {
        if (error is not null && TransportErrors.Contains(error.Code))
        {
            return new HttpRequestException(HttpRequestError.ConnectionError, error.LocalizedDescription);
        }

        return new HttpRequestException(failure ?? error?.LocalizedDescription ?? "The pack download failed.");
    }

    private static readonly HashSet<nint> TransportErrors =
    [
        (nint)NSUrlError.NotConnectedToInternet,
        (nint)NSUrlError.NetworkConnectionLost,
        (nint)NSUrlError.TimedOut,
        (nint)NSUrlError.CannotConnectToHost,
        (nint)NSUrlError.CannotFindHost,
        (nint)NSUrlError.DNSLookupFailed,
        (nint)NSUrlError.InternationalRoamingOff,
        (nint)NSUrlError.DataNotAllowed,
    ];

    public override void DidFinishEventsForBackgroundSession(NSUrlSession session)
    {
        try
        {
            Action? completion;
            int round = 0;
            lock (CompletionGate)
            {
                completion = _systemCompletion;
                _systemCompletion = null;
                _eventsFinished = completion is null; // the handler has not arrived yet: it is called when it does
                if (_eventsFinished)
                {
                    round = ++_eventsRound;
                }
            }

            if (completion is not null)
            {
                NSOperationQueue.MainQueue.AddOperation(completion);
            }
            else
            {
                _ = ForgetEarlyEventsAsync(round);
            }
        }
        catch (Exception ex)
        {
            LogDelegateFailure(nameof(DidFinishEventsForBackgroundSession), ex);
        }
    }

    // The early report belongs to one background delivery: one that no handler claims soon was foreground use and must not
    // make a later delivery's handler fire before that delivery's callbacks.
    private static async Task ForgetEarlyEventsAsync(int round)
    {
        await Task.Delay(EarlyEventsWindow).ConfigureAwait(false);
        lock (CompletionGate)
        {
            if (_eventsRound == round)
            {
                _eventsFinished = false;
            }
        }
    }

    private Waiter? WaiterOf(NSUrlSessionTask task)
    {
        if (string.IsNullOrEmpty(task.TaskDescription) || !_waiters.TryGetValue(Parse(task.TaskDescription).Partial, out Waiter? waiter))
        {
            return null;
        }

        return waiter.TaskId == NoTask || waiter.TaskId == (long)task.TaskIdentifier ? waiter : null;
    }

    private static string Describe(string partial, long offset, long size) =>
        string.Join(DescriptionSeparator, partial, offset.ToString(CultureInfo.InvariantCulture), size.ToString(CultureInfo.InvariantCulture));

    private static (string Partial, long Offset, long Size) Parse(string? description)
    {
        string[] parts = (description ?? string.Empty).Split(DescriptionSeparator);
        return (parts[0], long.Parse(parts[1], CultureInfo.InvariantCulture), long.Parse(parts[2], CultureInfo.InvariantCulture));
    }

    private static object LockFor(string partial) => PartialLocks.GetOrAdd(partial, _ => new object());

    private static long CurrentLength(string partial)
    {
        lock (LockFor(partial))
        {
            return File.Exists(partial) ? new FileInfo(partial).Length : 0;
        }
    }

    // The resume file is the offset the task started at (8 bytes), whether it may use mobile data (1 byte), and then Apple's resume data.
    private static void SaveResumeData(string partial, long offset, bool cellular, NSData? data)
    {
        if (data is null || data.Length == 0)
        {
            return;
        }

        lock (LockFor(partial))
        {
            using var stream = new FileStream(partial + ResumeSuffix, FileMode.Create, FileAccess.Write);
            stream.Write(BitConverter.GetBytes(offset));
            stream.WriteByte(cellular ? (byte)1 : (byte)0);
            stream.Write(data.ToArray());
        }
    }

    private static NSData? ReadResumeData(string partial, long offset, bool cellular)
    {
        lock (LockFor(partial))
        {
            string path = partial + ResumeSuffix;
            if (!File.Exists(path))
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length > ResumeHeaderBytes && BitConverter.ToInt64(bytes, 0) == offset && (bytes[sizeof(long)] == 1) == cellular)
            {
                return NSData.FromArray(bytes[ResumeHeaderBytes..]);
            }

            File.Delete(path); // taken for another offset, or under another mobile-data setting (a resumed task keeps the one it was made with)
            return null;
        }
    }

    private static void DeleteResumeData(string partial)
    {
        lock (LockFor(partial))
        {
            File.Delete(partial + ResumeSuffix);
        }
    }
}
