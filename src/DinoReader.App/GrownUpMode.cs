using System.ComponentModel;

namespace DinoReader;

/// <summary>Whether the gallery shows its grown-up edit overlay (Slice 80). Never persisted: a new
/// process, or leaving the app, starts in kids' mode. <see cref="DinoApp"/> owns the reset on leaving.</summary>
public sealed class GrownUpMode : INotifyPropertyChanged
{
	private bool _isGrownUp;

	public event PropertyChangedEventHandler? PropertyChanged;

	public bool IsGrownUp => _isGrownUp;

	public void Enter() => Set(true);

	public void Leave() => Set(false);

	private void Set(bool value)
	{
		if (_isGrownUp != value)
		{
			_isGrownUp = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsGrownUp)));
		}
	}
}
