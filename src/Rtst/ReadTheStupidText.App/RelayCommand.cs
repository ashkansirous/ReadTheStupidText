using System.Windows.Input;

namespace ReadTheStupidText_App;

/// <summary>
/// Minimal <see cref="ICommand"/> used to wire tray menu items. H.NotifyIcon's
/// default (PopupMenu) context-menu mode renders a native Win32 menu and invokes
/// each item's <c>Command</c> — it never raises the WinUI <c>Click</c> event — so
/// the menu must be driven through commands rather than click handlers.
/// </summary>
internal sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;

    public RelayCommand(Action<object?> execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute(parameter);
}
