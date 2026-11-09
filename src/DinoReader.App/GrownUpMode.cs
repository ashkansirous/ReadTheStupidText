using System.ComponentModel;
using DinoReader.Core;

namespace DinoReader;

/// <summary>Whether the gallery shows its grown-up edit overlay (Slice 80). Never persisted: a new
/// process, or leaving the app, starts in kids' mode. <see cref="DinoApp"/> owns the reset on leaving.</summary>
public sealed class GrownUpMode : INotifyPropertyChanged
{
	private bool _isGrownUp;

	public event PropertyChangedEventHandler? PropertyChanged;

	public bool IsGrownUp => _isGrownUp;

	/// <summary>The corner's tab (Slice 325 part A). Never persisted; entering and leaving both put it back on Books.</summary>
	public CornerTab Tab { get; private set; } = CornerTab.Books;

	public void Enter()
	{
		ResetTab();
		Set(true);
	}

	public void Leave()
	{
		ResetTab();
		Set(false);
	}

	/// <summary>Shows <paramref name="tab"/>; raises <c>PropertyChanged("Tab")</c> when it changed.</summary>
	public void SelectTab(CornerTab tab)
	{
		if (Tab != tab)
		{
			Tab = tab;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tab)));
		}
	}

	// Through SelectTab, so the views hear of a reset even when IsGrownUp itself does not change (the debug hook, a second window).
	private void ResetTab() => SelectTab(CornerTab.Books);

	private void Set(bool value)
	{
		if (_isGrownUp != value)
		{
			_isGrownUp = value;
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsGrownUp)));
		}
	}
}
