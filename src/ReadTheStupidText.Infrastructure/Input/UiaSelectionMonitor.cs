using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using ReadTheStupidText.Application.Input;
using ReadTheStupidText.Application.Logging;

namespace ReadTheStupidText.Infrastructure.Input;

/// <summary>
/// Detects text-selection changes across the desktop using UI Automation's
/// <see cref="TextPattern.TextSelectionChangedEvent"/>, reads the selected text
/// from the source element's <see cref="TextPattern"/>, and raises it. Handler
/// (de)registration runs off the UI thread because subscribing on the desktop
/// root can block, and the callbacks arrive on UI Automation threads anyway.
/// </summary>
public sealed class UiaSelectionMonitor : ISelectionMonitor
{
    private const int MaxTextLength = 10_000;

    private readonly AutomationEventHandler _handler;
    private readonly ISystemLog _log;
    private string? _lastText;
    private bool _running;

    public UiaSelectionMonitor(ISystemLog log)
    {
        _log = log;
        _handler = OnTextSelectionChanged;
    }

    public event EventHandler<string>? SelectionChanged;

    public event EventHandler? SelectionCleared;

    public bool IsRunning => _running;

    public void Start()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        Task.Run(() =>
        {
            try
            {
                Automation.AddAutomationEventHandler(
                    TextPattern.TextSelectionChangedEvent,
                    AutomationElement.RootElement,
                    TreeScope.Subtree,
                    _handler);
                _log.Info("UIA selection handler registered on root subtree");
            }
            catch (Exception ex)
            {
                _log.Error("UIA failed to register selection handler", error: ex);
            }
        });
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _lastText = null;
        Task.Run(() => Automation.RemoveAutomationEventHandler(
            TextPattern.TextSelectionChangedEvent,
            AutomationElement.RootElement,
            _handler));
    }

    private void OnTextSelectionChanged(object? sender, AutomationEventArgs e)
    {
        if (sender is not AutomationElement element)
        {
            _log.Debug("UIA event fired but sender was not an AutomationElement");
            return;
        }

        SelectionOutcome outcome = TryReadSelection(element, out string text);
        _log.Debug($"UIA selection event from {DescribeSource(element)} outcome={outcome} len={text.Length}");

        switch (outcome)
        {
            case SelectionOutcome.Unavailable:
                // A transient cross-process UIA failure, NOT a deselect. Leave
                // _lastText alone so a re-fired event mid-selection can't masquerade
                // as a clear and ignore/interrupt the read the user actually wants.
                return;

            case SelectionOutcome.Empty:
                // A genuine deselect: signal once on the transition from a selection
                // to none so an in-progress read can be interrupted.
                if (_lastText is not null)
                {
                    _lastText = null;
                    SelectionCleared?.Invoke(this, EventArgs.Empty);
                }

                return;

            default: // SelectionOutcome.HasText
                if (text == _lastText)
                {
                    return;
                }

                _lastText = text;
                SelectionChanged?.Invoke(this, text);
                return;
        }
    }

    // Diagnostic only: best-effort process name + control type of the element that
    // raised the event, so the log shows whether Chrome/Terminal are emitting at all.
    private static string DescribeSource(AutomationElement element)
    {
        try
        {
            int pid = element.Current.ProcessId;
            string process = Process.GetProcessById(pid).ProcessName;
            return $"{process} (pid {pid}, {element.Current.ControlType.ProgrammaticName})";
        }
        catch (Exception ex)
        {
            return $"<unknown source: {ex.GetType().Name}>";
        }
    }

    // Distinguishes a real, empty selection (deselect) from a failed read so the
    // two are never conflated — a transient failure must not look like a deselect.
    private static SelectionOutcome TryReadSelection(AutomationElement element, out string text)
    {
        text = string.Empty;
        try
        {
            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out object pattern))
            {
                return SelectionOutcome.Unavailable;
            }

            TextPatternRange[] ranges = ((TextPattern)pattern).GetSelection();
            if (ranges.Length == 0)
            {
                return SelectionOutcome.Empty;
            }

            text = ranges[0].GetText(MaxTextLength);
            return string.IsNullOrWhiteSpace(text) ? SelectionOutcome.Empty : SelectionOutcome.HasText;
        }
        catch (Exception)
        {
            // UI Automation cross-process calls can throw transiently (element
            // busy, gone, timeout). This is "couldn't read", not "no selection".
            return SelectionOutcome.Unavailable;
        }
    }

    // The three distinguishable results of reading an element's text selection.
    private enum SelectionOutcome
    {
        /// <summary>A non-empty selection was read.</summary>
        HasText,

        /// <summary>The element genuinely has no (or whitespace-only) selection — a deselect.</summary>
        Empty,

        /// <summary>The element exposes no text pattern, or the read failed transiently.</summary>
        Unavailable,
    }

    public void Dispose() => Stop();
}
