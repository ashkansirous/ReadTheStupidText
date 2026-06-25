using System.ComponentModel;

namespace ReadTheStupidText_App;

/// <summary>
/// A row in the activity log window — a view over one
/// <see cref="ReadTheStupidText.Domain.Activity.ActivityEntry"/>. Only
/// <see cref="State"/> changes after creation, so it raises change notification
/// for that property to update the row in place.
/// </summary>
public sealed class ActivityRowVm : INotifyPropertyChanged
{
    private string _state;

    public ActivityRowVm(int id, string time, string source, string text, string state)
    {
        Id = id;
        Time = time;
        Source = source;
        Text = text;
        _state = state;
    }

    public int Id { get; }

    public string Time { get; }

    public string Source { get; }

    public string Text { get; }

    public string State
    {
        get => _state;
        set
        {
            if (_state == value)
            {
                return;
            }

            _state = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
