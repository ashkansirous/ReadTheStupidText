using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// This launch's usage context (Slice 351): the install-age and launch-count buckets, worked out once at start from the first-launch day and launch count kept on the device
/// (never sent, no id), and the seconds since the launch started. One per process, like <see cref="UsageSession"/>. The two stored values go through <see cref="ILaunchStore"/>, so a test uses memory.
/// </summary>
public sealed class UsageLaunch
{
	private readonly Func<long> _tickMilliseconds;
	private readonly long _startedAt;

	/// <summary>Counts this launch: reads the stored first day and count, saves the new ones and keeps the buckets.</summary>
	public UsageLaunch(ILaunchStore store, DateOnly today, Func<long> tickMilliseconds)
	{
		_tickMilliseconds = tickMilliseconds;
		_startedAt = tickMilliseconds();
		InstallAge = UsageSource.None;
		LaunchNo = UsageSource.None;
		try
		{
			LaunchHistory.Launch launch = LaunchHistory.Begin(store.FirstDay, store.Launches, today);
			InstallAge = launch.InstallAge;
			LaunchNo = launch.LaunchNo;
			store.Save(launch.FirstDay, launch.Number);
		}
		catch (Exception)
		{
			// no-fallback: the failure is kept in StoreFailed and UsageCounter reports it once it is built (a preference that cannot be read leaves the buckets "none", never stops the counter)
			StoreFailed = true;
		}
	}

	/// <summary>True when the stored launch history could not be read or saved (Slice 351): the buckets are then "none" or unsaved, and the counter reports it.</summary>
	public bool StoreFailed { get; private set; }

	/// <summary>A <see cref="UsageContext"/> install-age bucket.</summary>
	public string InstallAge { get; private set; }

	/// <summary>A <see cref="UsageContext"/> launch-count bucket.</summary>
	public string LaunchNo { get; private set; }

	/// <summary>Whole seconds since this launch started, capped at a day.</summary>
	public int SinceLaunch() => UsageContext.SinceLaunchOf(TimeSpan.FromMilliseconds(_tickMilliseconds() - _startedAt));
}

/// <summary>Where the first-launch day and the launch count live between launches (preferences on a device).</summary>
public interface ILaunchStore
{
	/// <summary>The day of the first launch (yyyy-MM-dd), or null before any launch.</summary>
	string? FirstDay { get; }

	/// <summary>How many launches were counted so far (0 before any).</summary>
	int Launches { get; }

	void Save(string firstDay, int launches);
}
