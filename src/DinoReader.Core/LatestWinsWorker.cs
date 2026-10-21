namespace DinoReader.Core;

/// <summary>
/// Serves a wanted value one run at a time, latest wins (Slice 66). Each <see cref="Use"/> records the
/// wanted value and asks for one run; a request that lands while a run is in flight is served by exactly
/// one more run with the value wanted by then, so a failed run is never retried on its own: a retry
/// needs a new <see cref="Use"/>. A run that throws is reported to <c>onError</c> and the loop stays alive.
/// </summary>
public sealed class LatestWinsWorker<T>(Func<T, Task> serve, Action<Exception> onError)
{
    private readonly object _gate = new();
    private T? _wanted;
    private bool _pending;
    private bool _running;

    /// <summary>Records <paramref name="value"/> as wanted and makes sure a run will serve it.</summary>
    public void Use(T value)
    {
        lock (_gate)
        {
            _wanted = value;
            _pending = true;
            if (_running) return;
            _running = true;
        }

        _ = LoopAsync();
    }

    private async Task LoopAsync()
    {
        while (TryTake(out T? target))
        {
            await RunOneAsync(target!);
        }
    }

    private bool TryTake(out T? target)
    {
        lock (_gate)
        {
            target = _wanted;
            if (_pending)
            {
                _pending = false;
                return true;
            }

            _running = false;
            return false;
        }
    }

    private async Task RunOneAsync(T target)
    {
        try
        {
            await serve(target);
        }
        catch (Exception ex)
        {
            onError(ex);
        }
    }
}
