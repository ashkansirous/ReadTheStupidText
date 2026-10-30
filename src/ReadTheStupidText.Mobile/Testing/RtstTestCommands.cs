#if DEBUG
using System.Runtime.CompilerServices;
using ReadTheStupidText.Mobile.Shared.Testing;

namespace ReadTheStupidText.Mobile.Testing;

/// <summary>
/// Read The Stupid Text's own Debug test command (Slice 211): <c>hold-detection</c> keeps the Review page's language detection from running after typing
/// (the idle delay becomes ten minutes), so a flow can tap Play before detection and always reach the detect-now path. It registers itself with the
/// dispatcher, so no start-up code changes; Release has none of it. Outcome line: <c>HOOK hold-detection ok</c>.
/// </summary>
internal static class RtstTestCommands
{
	private const string HoldDetectionName = "hold-detection";

	/// <summary>True once a launch asked to hold detection; read by the Review page when it schedules a typed-text detection.</summary>
	internal static volatile bool HoldDetection;

	// A module initializer on purpose: the command registers itself, so the app's start-up code stays untouched.
#pragma warning disable CA2255
	[ModuleInitializer]
#pragma warning restore CA2255
	internal static void Register() => TestHookDispatcher.AppCommands = Run;

	private static string? Run(TestHookCommand command)
	{
		if (command.Name != HoldDetectionName)
		{
			return null;
		}

		HoldDetection = true;
		return TestHookDispatcher.Ok;
	}
}
#endif
