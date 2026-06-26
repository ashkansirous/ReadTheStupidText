using System.ComponentModel;

namespace ReadTheStupidText_App;

/// <summary>
/// A row in the activity log window — a view over one
/// <see cref="ReadTheStupidText.Domain.Activity.ActivityEntry"/>. The
/// <see cref="State"/>, <see cref="Reason"/>, and the timing columns change after
/// creation (timings arrive when audio first plays), so those raise change
/// notification to update the row in place; the trigger and originating window
/// <see cref="Source"/> are fixed.
/// </summary>
public sealed class ActivityRowVm : INotifyPropertyChanged
{
    private string _state;
    private string _reason;
    private string _firstAudio;
    private string _synth;

    public ActivityRowVm(int id, string time, string trigger, string source, string text, string state, string reason)
    {
        Id = id;
        Time = time;
        Trigger = trigger;
        Source = source;
        Text = text;
        _state = state;
        _reason = reason;
        _firstAudio = string.Empty;
        _synth = string.Empty;
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

    /// <summary>Time-to-first-audio (entry → first audio), e.g. "412 ms"; empty
    /// until the read plays. Local diagnostic only.</summary>
    public string FirstAudio
    {
        get => _firstAudio;
        set => Set(ref _firstAudio, value, nameof(FirstAudio));
    }

    /// <summary>Synthesis duration before audio began, e.g. "180 ms"; empty until
    /// the read plays. Local diagnostic only.</summary>
    public string Synth
    {
        get => _synth;
        set => Set(ref _synth, value, nameof(Synth));
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
