namespace DinoReader;

/// <summary>
/// The "Download now" consent and the single-flight run guard, held under one lock (Slice 197). A tap records consent and starts a run in one step;
/// a run ending checks consent and releases the guard in one step. Either the tap sees the guard free and starts a run, or the ending run sees the
/// consent and goes round again, so no tap is ever dropped between the two. MAUI-free so the interleavings are unit-tested.
/// </summary>
public sealed class StorytellerConsent
{
	private readonly object _lock = new();
	private bool _given;
	private bool _active;

	/// <summary>The grown-up tapped "Download now" and the download has not completed or been cancelled.</summary>
	public bool IsGiven
	{
		get
		{
			lock (_lock)
			{
				return _given;
			}
		}
	}

	/// <summary>A run holds the guard.</summary>
	public bool IsActive
	{
		get
		{
			lock (_lock)
			{
				return _active;
			}
		}
	}

	/// <summary>A start that is not a tap. True when the caller now owns the guard and must run; false when a run is already in flight.</summary>
	public bool TryBegin()
	{
		lock (_lock)
		{
			if (_active)
			{
				return false;
			}

			_active = true;
			return true;
		}
	}

	/// <summary>The "Download now" tap: records consent, then behaves as <see cref="TryBegin"/>. When it returns false the run in flight will see the consent as it ends.</summary>
	public bool RecordConsentAndTryBegin()
	{
		lock (_lock)
		{
			_given = true;
			if (_active)
			{
				return false;
			}

			_active = true;
			return true;
		}
	}

	/// <summary>
	/// A run is ending. A pack that is installed ends the consent. Returns true (guard kept) when the run stopped at the Wi-Fi wait and consent is given: run once more.
	/// Otherwise releases the guard in the same step and returns false.
	/// </summary>
	public bool EndRun(bool endedWaitingForWifi, bool packInstalled)
	{
		lock (_lock)
		{
			if (packInstalled)
			{
				_given = false;
			}

			if (endedWaitingForWifi && _given)
			{
				return true;
			}

			_active = false;
			return false;
		}
	}

	/// <summary>A run faulted before it could end normally: release the guard so later starts are not refused for good.</summary>
	public void Abort()
	{
		lock (_lock)
		{
			_active = false;
		}
	}

	/// <summary>Done or Cancel: the consent ends.</summary>
	public void Clear()
	{
		lock (_lock)
		{
			_given = false;
		}
	}
}
