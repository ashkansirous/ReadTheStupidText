using System.ComponentModel;
using DinoReader.Core;

namespace DinoReader;

/// <summary>Whether the shelf is unlocked (Slice 80, Slice 418): the grown-up's ••• menus and the Store and Settings tabs. Never persisted: a new
/// process, or leaving the app, starts locked. <see cref="DinoApp"/> owns the reset on leaving; <see cref="Clock"/> is the 30-second countdown that locks it again (<see cref="LockTimer"/> ticks it).</summary>
public sealed class GrownUpMode : INotifyPropertyChanged
{
	private bool _isGrownUp;
	private LockReason? _pendingMessage; // the reason of a lock the gallery has not told about yet (another page was on top when it happened)

	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>The shelf locked itself or was locked by hand (not by leaving the app): the gallery shows the short message.</summary>
	public event Action<LockReason>? Locked;

	public bool IsGrownUp => _isGrownUp;

	/// <summary>The lock's clock (Slice 418): unlocked while it runs, locked at zero.</summary>
	public LockClock Clock { get; } = new();

	/// <summary>The open tab (Slice 325 part A). Never persisted; entering and leaving both put it back on Books.</summary>
	public CornerTab Tab { get; private set; } = CornerTab.Books;

	public void Enter()
	{
		ResetTab();
		_pendingMessage = null;
		Clock.Unlock();
		Set(true);
		RaiseCountdown();
	}

	/// <summary>Locks without a message: the app left the foreground, a new window opened, or the update page took over.</summary>
	public void Leave()
	{
		ResetTab();
		Clock.Lock(LockReason.ByHand);
		Set(false);
		RaiseCountdown();
	}

	/// <summary>Locks and says why (Slice 418): the lock was tapped, or the 30 seconds ran out. The reason stays pending until the gallery shows its message.</summary>
	public void Lock(LockReason reason)
	{
		bool wasUnlocked = _isGrownUp;
		Leave();
		if (wasUnlocked)
		{
			_pendingMessage = reason;
			Locked?.Invoke(reason);
		}
	}

	/// <summary>The seconds on the countdown chip changed (raised on the thread that changed them: the timer's, a tap's, a hold's end).</summary>
	public event Action? CountdownChanged;

	/// <summary>Counts <paramref name="elapsed"/> down; when the clock runs out, locks with <see cref="LockReason.Timer"/>.</summary>
	public void Tick(TimeSpan elapsed)
	{
		if (_isGrownUp && Clock.Tick(elapsed))
		{
			Lock(LockReason.Timer);
		}

		RaiseCountdown();
	}

	/// <summary>A grown-up tap (Slice 418 item 5): the time starts again. Called from <c>UiUsage.Tap</c>, so every control a grown-up presses is covered.</summary>
	public void GrownUpTap()
	{
		Clock.GrownUpTap();
		RaiseCountdown();
	}

	/// <summary>A Debug hook (<c>lock-seconds</c>): the seconds a grown-up has after the last grown-up tap, for the flows.</summary>
	public void SetTotalSeconds(int seconds)
	{
		Clock.SetTotalSeconds(seconds);
		RaiseCountdown();
	}

	/// <summary>Raises <see cref="CountdownChanged"/> when the whole seconds differ from the last time it was raised.</summary>
	public void RaiseCountdown()
	{
		int seconds = Clock.SecondsLeft;
		if (Interlocked.Exchange(ref _lastSeconds, seconds) != seconds)
		{
			CountdownChanged?.Invoke();
		}
	}

	private int _lastSeconds = -1;

	/// <summary>The reason of a lock the gallery has not shown yet, once; null when there is none.</summary>
	public LockReason? TakePendingMessage()
	{
		LockReason? reason = _pendingMessage;
		_pendingMessage = null;
		return reason;
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
