#if DEBUG || DINO_TEST_BOOKS // Debug app builds and the DinoReader.Tests link (it defines DINO_TEST_BOOKS); never in a Release app
using System.Globalization;

namespace DinoReader.Testing;

/// <summary>
/// Pure part of the Debug <c>sound-pill state=getting|wifi|preparing|nosound|off [percent=&lt;0-100&gt;] [lang=&lt;code&gt;]</c> hook (Slice 452): reads the arguments into the pill state a
/// device check holds on the open reading page, so Slice 290's pill can be screenshotted next to Batch 50 frames 197-199.
/// </summary>
internal static class SoundPillHookPlan
{
	internal const string GettingName = "getting";
	internal const string WifiName = "wifi";
	internal const string PreparingName = "preparing";
	internal const string NoSoundName = "nosound";
	internal const string OffName = "off";
	internal const string StateKey = "state";
	internal const string PercentKey = "percent";

	/// <summary>The launch-argument names the hook reads; the app adds them to <c>TestHookDispatcher.AppLaunchKeys</c> so the iOS entry point passes them on.</summary>
	internal static readonly string[] LaunchKeys = [StateKey, PercentKey];
	internal const int DefaultPercent = 40;
	internal const int PercentTotal = 100;

	/// <summary>The pill kind for a state word, or null for <c>off</c> and for anything else (the caller tells them apart with <see cref="IsOff"/>).</summary>
	internal static SoundStatusKind? KindOf(string state) => state switch
	{
		GettingName => SoundStatusKind.Getting,
		WifiName => SoundStatusKind.WaitingForInternet,
		PreparingName => SoundStatusKind.Preparing,
		NoSoundName => SoundStatusKind.NoSound,
		_ => null,
	};

	internal static bool IsOff(string state) => state == OffName;

	/// <summary>False (with the reason) when <paramref name="percent"/> is given and is not a whole number from 0 to 100; an empty value means <see cref="DefaultPercent"/>.</summary>
	internal static bool TryParsePercent(string percent, out int value, out string error)
	{
		value = DefaultPercent;
		error = string.Empty;
		if (percent.Length > 0 && (!int.TryParse(percent, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value < 0 || value > PercentTotal))
		{
			error = $"bad percent {percent}";
			return false;
		}

		return true;
	}

	/// <summary>The state the pill is held in: only Getting carries the count (the percent as position over 100, as the reading page does).</summary>
	internal static SoundStatusPillState StateFor(SoundStatusKind kind, int percent) =>
		kind == SoundStatusKind.Getting ? new SoundStatusPillState(kind, percent, PercentTotal) : new SoundStatusPillState(kind);
}
#endif
