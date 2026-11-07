namespace DinoReader;

/// <summary>What one read waits to count: the read it belongs to, the reading language taken when the read started, and the read's own payload (the story, pieces and why).</summary>
internal sealed record PendingVoice<TPayload>(int ReadId, string Reading, TPayload Payload);

/// <summary>
/// Slice 297: the pure part of the read_voice count, apart from <c>ReadingSession</c> (which needs the MAUI main thread) so a unit test can drive it.
/// A read records what to count when it starts; its first clip takes that record once; a stop, a failure or the end of the read drops it, so a read that fails before any audio counts nothing
/// and a stopped read keeps nothing alive. The once-per-book key (book and reading language) is claimed here too.
/// </summary>
internal sealed class PendingVoiceCount<TPayload>
{
	private PendingVoice<TPayload>? _pending;
	private readonly object _claimLock = new();
	private string? _countedFor;

	/// <summary>Records what read <paramref name="readId"/> will count at its first clip; it replaces any earlier record.</summary>
	public void Begin(int readId, string reading, TPayload payload) => Volatile.Write(ref _pending, new PendingVoice<TPayload>(readId, reading, payload));

	/// <summary>Takes the record of read <paramref name="readId"/> once; null when there is none, it belongs to another read, or it was already taken.</summary>
	public PendingVoice<TPayload>? Take(int readId)
	{
		PendingVoice<TPayload>? pending = Volatile.Read(ref _pending);
		if (pending is null || pending.ReadId != readId)
		{
			return null;
		}

		return ReferenceEquals(Interlocked.CompareExchange(ref _pending, null, pending), pending) ? pending : null;
	}

	/// <summary>Drops the record (a stop, a failure, the end of the read).</summary>
	public void Clear() => Volatile.Write(ref _pending, null);

	/// <summary>Drops the record only when it belongs to read <paramref name="readId"/>, so a late end of an earlier read never clears the record of the read that replaced it.</summary>
	public void ClearIfRead(int readId)
	{
		PendingVoice<TPayload>? pending = Volatile.Read(ref _pending);
		if (pending is not null && pending.ReadId == readId)
		{
			Interlocked.CompareExchange(ref _pending, null, pending);
		}
	}

	/// <summary>True when the book and reading language are already counted (nothing is claimed).</summary>
	public bool IsBookClaimed(string bookId, string reading)
	{
		lock (_claimLock)
		{
			return _countedFor == $"{bookId}|{reading}";
		}
	}

	/// <summary>True once per book and reading language: the first call for a key takes it, later calls for the same key return false.</summary>
	public bool ClaimBook(string bookId, string reading)
	{
		string key = $"{bookId}|{reading}";
		lock (_claimLock)
		{
			if (_countedFor == key)
			{
				return false;
			}

			_countedFor = key;
			return true;
		}
	}
}
