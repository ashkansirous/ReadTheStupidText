namespace DinoReader.Core;

/// <summary>
/// One "going back" at a time (Slice 235 part B): while a back navigation is under way, a second Esc (a key repeat, or a second press) does nothing. <see cref="TryBegin"/> takes the single
/// slot, <see cref="End"/> gives it back when the navigation has finished.
/// </summary>
public sealed class BackInFlight
{
	private bool _active;

	/// <summary>True when a back navigation is under way.</summary>
	public bool IsActive => _active;

	/// <summary>Takes the slot; false when a back is already in flight (the caller then does nothing).</summary>
	public bool TryBegin()
	{
		if (_active)
		{
			return false;
		}

		_active = true;
		return true;
	}

	/// <summary>Frees the slot once the navigation finished.</summary>
	public void End() => _active = false;
}
