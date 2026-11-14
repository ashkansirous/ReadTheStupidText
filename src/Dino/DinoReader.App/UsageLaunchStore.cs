namespace DinoReader;

/// <summary>Keeps the launch history (Slice 351) in local preferences; it is never read by anything that sends.</summary>
public sealed class PreferencesLaunchStore : ILaunchStore
{
	private const string FirstDayKey = "dino.usage.firstday";
	private const string LaunchesKey = "dino.usage.launches";

	public string? FirstDay => Preferences.Default.Get<string?>(FirstDayKey, null);

	public int Launches => Preferences.Default.Get(LaunchesKey, 0);

	public void Save(string firstDay, int launches)
	{
		Preferences.Default.Set(FirstDayKey, firstDay);
		Preferences.Default.Set(LaunchesKey, launches);
	}
}
