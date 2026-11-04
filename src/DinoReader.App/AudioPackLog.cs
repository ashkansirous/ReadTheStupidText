using System.Diagnostics;

namespace DinoReader;

/// <summary>The pre-made audio's log line (Slice 246 C2): to the debugger always, and in a Debug build to the test-hook log too, so a device check can prove which path ran.</summary>
internal static class AudioPackLog
{
	public const string Tag = "[AudioPack]";

	public static void Write(string line)
	{
		Debug.WriteLine($"{Tag} {line}");
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"{Tag} {line}");
#endif
	}
}
