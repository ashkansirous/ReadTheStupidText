using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile.Shared.Waiting;

/// <summary>
/// Drives one <see cref="WaitOverlay"/> from the pure <see cref="WaitGate"/> (Slice 143, Decision 149): a wait shows the
/// overlay 500 ms after it begins and hides it the moment it ends, so a short wait never flashes. Use it from the UI thread.
/// Either call <see cref="Begin"/> / <see cref="End"/> around a wait whose end is an event (first audio), or wrap a task in
/// <see cref="RunAsync{T}"/>, which also turns Cancel into a <see cref="CancellationToken"/>.
/// </summary>
public sealed class WaitController
{
    private readonly WaitOverlay _overlay;
    private readonly WaitGate _gate = new();
    private readonly TimeProvider _clock;
    private string _line = string.Empty;
    private string? _detail;
    private bool _canCancel;
    private CancellationTokenSource? _cancellation;
    private int _wait;

    public WaitController(WaitOverlay overlay, TimeProvider? clock = null)
    {
        _overlay = overlay;
        _clock = clock ?? TimeProvider.System;
        _overlay.CancelTapped += (_, _) => OnCancelTapped();
    }

    /// <summary>The user tapped Cancel.</summary>
    public event Action? CancelRequested;

    /// <summary>Raised with true when a wait begins and false when it ends (the page can ignore touches meanwhile, even before the overlay shows).</summary>
    public event Action<bool>? ActiveChanged;

    /// <summary>Raised with true when the overlay actually appears (after 500 ms) and false when it goes, so a page can dim itself only for a wait the user sees.</summary>
    public event Action<bool>? ShownChanged;

    public bool IsActive => _gate.IsActive;

    public WaitStage Stage => _gate.Stage;

    /// <summary>The token of the wait begun with <see cref="BeginCancellable"/>, cancelled by Cancel; none when no such wait runs.</summary>
    public CancellationToken Token => _cancellation?.Token ?? CancellationToken.None;

    /// <summary>Starts (or, while a wait runs, re-words) a wait. The overlay shows once it has lasted 500 ms.</summary>
    public void Begin(WaitStage stage, string line, bool canCancel, string? detail = null)
    {
        bool starting = !_gate.IsActive;
        _gate.Begin(stage, _clock.GetUtcNow());
        _line = line;
        _detail = detail;
        _canCancel = canCancel;
        if (starting)
        {
            _ = ShowWhenDueAsync(++_wait);
            ActiveChanged?.Invoke(true);
        }
        else
        {
            ShowIfDue();
        }
    }

    /// <summary>Starts a wait with Cancel; Cancel cancels the returned token. <see cref="End"/> disposes it.</summary>
    public CancellationToken BeginCancellable(WaitStage stage, string line)
    {
        _cancellation?.Cancel(); // an earlier cancellable wait still holding the old token is stopped, not orphaned
        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        Begin(stage, line, canCancel: true);
        return _cancellation.Token;
    }

    /// <summary>Changes the second line (a count) of the wait in progress.</summary>
    public void Report(string? detail)
    {
        _detail = detail;
        ShowIfDue();
    }

    /// <summary>Ends the wait and hides the overlay. Returns how long the overlay showed, null when it never did.</summary>
    public TimeSpan? End()
    {
        _wait++;
        _cancellation?.Dispose();
        _cancellation = null;
        bool wasActive = _gate.IsActive;
        TimeSpan? shown = _gate.End(_clock.GetUtcNow());
        bool wasShown = _overlay.IsVisible;
        _overlay.Hide();
        if (wasShown)
        {
            ShownChanged?.Invoke(false);
        }

        if (wasActive)
        {
            ActiveChanged?.Invoke(false);
        }

        return shown;
    }

    /// <summary>Runs <paramref name="work"/> behind the overlay. Cancel (when <paramref name="canCancel"/>) cancels the token it gets.</summary>
    public async Task<T> RunAsync<T>(WaitStage stage, string line, bool canCancel, Func<CancellationToken, Task<T>> work)
    {
        CancellationToken token = canCancel ? BeginCancellable(stage, line) : BeginUncancellable(stage, line);
        try
        {
            return await work(token);
        }
        finally
        {
            End();
        }
    }

    private CancellationToken BeginUncancellable(WaitStage stage, string line)
    {
        Begin(stage, line, canCancel: false);
        return CancellationToken.None;
    }

    private void OnCancelTapped()
    {
        _cancellation?.Cancel();
        CancelRequested?.Invoke();
    }

    private async Task ShowWhenDueAsync(int wait)
    {
        await Task.Delay(WaitGate.ShowAfter, _clock);
        if (wait == _wait)
        {
            ShowIfDue();
        }
    }

    private void ShowIfDue()
    {
        if (!_gate.IsVisible(_clock.GetUtcNow()))
        {
            return;
        }

        bool announce = !_overlay.IsVisible;
        _overlay.Show(_line, _detail, _canCancel);
        if (announce)
        {
            SemanticScreenReader.Announce(_line);
            ShownChanged?.Invoke(true);
        }
    }
}
