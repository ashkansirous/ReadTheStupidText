using System.ComponentModel;

namespace ReadTheStupidText_App;

/// <summary>
/// A row in the activity log window — a view over one
/// <see cref="ReadTheStupidText.Domain.Activity.ActivityEntry"/>. Only
/// <see cref="State"/> and <see cref="Reason"/> change after creation, so it
/// raises change notification for those to update the row in place; the trigger
/// and originating window <see cref="Source"/> are fixed.
/// </summary>
public sealed class ActivityRowVm : INotifyPropertyChanged
{
    private string _state;
    private string _reason;

    public ActivityRowVm(int id, string time, string trigger, string source, string text, string state, string reason)
    {
        Id = id;
        Time = time;
        Trigger = trigger;
        Source = source;
        Text = text;
        _state = state;
        _reason = reason;
    }

    public int Id { get; }

    public string Time { get; }

    /// <summary>How the read was triggered (auto-read / hotkey / manual / clipboard).</summary>
    public string Trigger { get; }

    /// <summary>The window the text came from, e.g. "Chrome — Inbox - Gmail".</summary>
    public string Source { get; }

    public string Text { get; }

    public string State
    {
        get => _state;
        set => Set(ref _state, value, nameof(State));
    }

    public string Reason
    {
        get => _reason;
        set => Set(ref _reason, value, nameof(Reason));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set(ref string field, string value, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
