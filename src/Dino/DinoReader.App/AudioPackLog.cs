using System.Diagnostics;

namespace DinoReader;

/// <summary>The pre-made audio's log line (Slice 246 C2): to the debugger always, and in a Debug build to the test-hook log too, so a device check can prove which path ran.</summary>
internal static class AudioPackLog
{
	public const string Tag = "[AudioPack]";

	public static void Write(string line)
	{
		Debug.WriteLine($"{Tag} {line}");
		Sirous.Reading.Mobile.Shared.Crashes.AppTrace.Line($"{Tag} {line}"); // Slice 267: the release build's rolling log carries the pack and voice lines too (ids and closed words only)
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"{Tag} {line}");
#endif
	}
}
