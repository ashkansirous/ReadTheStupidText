namespace DinoReader.Core;

/// <summary>
/// The one queue every shelf rebuild and chrome re-layout goes through (Slice 325, "Rebuild/chrome design after the third strike"): one pass runs at a time; a request made while a pass runs
/// replaces (is merged into) the single pending one and is served by one pass after it; a pass that throws is handed to <c>onError</c> and the queue goes on with the next request.
/// There is no retry, no skip-if-unchanged memory and no exception leaves the queue.
/// </summary>
public sealed class LatestWinsQueue<TRequest>
{
    private readonly Func<TRequest, TRequest, TRequest> _merge;
    private readonly Func<TRequest, Task<bool>> _pass;
    private readonly Action<Exception> _onError;
    private readonly object _lock = new();
    private bool _running;
    private bool _hasPending;
    private TRequest? _pending;
    private TaskCompletionSource<bool>? _pendingDone;

    /// <param name="merge">Combines the pending request with a newer one (the page ORs its flags, so a forced rebuild is never downgraded).</param>
    /// <param name="pass">Does the work for one request; true when it succeeded.</param>
    /// <param name="onError">Reports an exception a pass threw (the app's fallback reporting).</param>
    public LatestWinsQueue(Func<TRequest, TRequest, TRequest> merge, Func<TRequest, Task<bool>> pass, Action<Exception> onError)
    {
        _merge = merge;
        _pass = pass;
        _onError = onError;
    }

    /// <summary>
    /// Runs <paramref name="request"/> now when idle (synchronously up to the pass's first real await), else makes it the pending request. The task never faults; it holds the result of the pass that served
    /// this request, and ends once the queue has drained when this call started the draining.
    /// </summary>
    public Task<bool> Request(TRequest request)
    {
        lock (_lock)
        {
            if (_running)
            {
                _pending = _hasPending ? _merge(_pending!, request) : request;
                _hasPending = true;
                _pendingDone ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                return _pendingDone.Task;
            }

            _running = true;
        }

        return DrainAsync(request);
    }

    private async Task<bool> DrainAsync(TRequest first)
    {
        bool result = await RunPassAsync(first);
        while (TakePending(out TRequest next, out TaskCompletionSource<bool>? done))
        {
            bool ok = await RunPassAsync(next);
            done?.TrySetResult(ok);
        }

        return result;
    }

    private bool TakePending(out TRequest next, out TaskCompletionSource<bool>? done)
    {
        lock (_lock)
        {
            if (!_hasPending)
            {
                _running = false;
                next = default!;
                done = null;
                return false;
            }

            next = _pending!;
            done = _pendingDone;
            _pending = default;
            _pendingDone = null;
            _hasPending = false;
            return true;
        }
    }

    private async Task<bool> RunPassAsync(TRequest request)
    {
        try
        {
            return await _pass(request);
        }
        catch (Exception ex)
        {
            // no-fallback: handed to onError, which is the page's fallback reporting (Core has no reporter of its own)
            return ReportSafely(ex);
        }
    }

    private bool ReportSafely(Exception ex)
    {
        try
        {
            _onError(ex);
        }
        catch
        {
            // no-fallback: the reporter itself failed; the queue must go on with the next request
        }

        return false;
    }
}
