using System.Windows.Automation;
using System.Windows.Automation.Text;
using ReadTheStupidText.Application.Input;

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
    private string? _lastText;
    private bool _running;

    public UiaSelectionMonitor() => _handler = OnTextSelectionChanged;

    public event EventHandler<string>? SelectionChanged;

    public bool IsRunning => _running;

    public void Start()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        Task.Run(() => Automation.AddAutomationEventHandler(
            TextPattern.TextSelectionChangedEvent,
            AutomationElement.RootElement,
            TreeScope.Subtree,
            _handler));
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
            return;
        }

        string? text = TryReadSelection(element);
        if (string.IsNullOrWhiteSpace(text) || text == _lastText)
        {
            return;
        }

        _lastText = text;
        SelectionChanged?.Invoke(this, text);
    }

    private static string? TryReadSelection(AutomationElement element)
    {
        try
        {
            if (!element.TryGetCurrentPattern(TextPattern.Pattern, out object pattern))
            {
                return null;
            }

            TextPatternRange[] ranges = ((TextPattern)pattern).GetSelection();
            return ranges.Length == 0 ? null : ranges[0].GetText(MaxTextLength);
        }
        catch (Exception)
        {
            // UI Automation cross-process calls can throw transiently (element
            // gone, timeout); treat as "nothing to read".
            return null;
        }
    }

    public void Dispose() => Stop();
}
