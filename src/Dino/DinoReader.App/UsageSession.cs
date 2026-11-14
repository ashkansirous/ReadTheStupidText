using System.Globalization;
using System.Security.Cryptography;

namespace DinoReader;

/// <summary>
/// The usage session (Slice 250): one random id made when the app process starts, so counts from one launch can be put back in order. It lives in memory and is written to disk only inside
/// the not-yet-sent counts in the local send queue (gone once they are sent); it is never reused for a later launch and never tied to the install, the device or a person; a new <see cref="UsageSession"/> (a cold start) has a new id. It also numbers the counts it
/// stamps (<see cref="NextSeq"/>) and holds the sampling roll, so a sampled session is complete and an unsampled one sends nothing. Pure, so a test pins it.
/// </summary>
public sealed class UsageSession
{
	private const int IdBytes = 16;

	private int _seq;

	/// <summary>Makes a session with a fresh random id and a fresh sampling roll.</summary>
	public UsageSession()
	{
		Id = Convert.ToHexString(RandomNumberGenerator.GetBytes(IdBytes)).ToLower(CultureInfo.InvariantCulture);
		Roll = Random.Shared.NextDouble();
	}

	/// <summary>32 lower-case hex digits (128 random bits).</summary>
	public string Id { get; }

	/// <summary>The sampling roll for this whole session: from 0 up to but not including 1, compared with the config's sample rate once per session.</summary>
	public double Roll { get; }

	/// <summary>The next sequence number: 1 for the first count of the session, then 2, 3, ...; safe across threads.</summary>
	public int NextSeq() => Interlocked.Increment(ref _seq);
}
