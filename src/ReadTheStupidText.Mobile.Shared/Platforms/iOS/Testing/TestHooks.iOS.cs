#if DEBUG
using Foundation;
using Microsoft.Maui.LifecycleEvents;

namespace ReadTheStupidText.Mobile.Shared.Testing;

/// <summary>
/// iOS side of the Debug-only test hooks (Slice 162): a URL such as <c>rtst-test://skip-first-run?lang=en</c>, which
/// Maestro's <c>openLink</c> opens. The scheme is declared by <c>Testing/TestHooks.Rtst.plist</c> or <c>TestHooks.Dino.plist</c>, merged into the
/// app's Info.plist in Debug builds only (see <c>TestHooks.props</c>); the handler is registered by <c>AddTestHooks</c>.
/// </summary>
public static partial class TestHooks
{
	private static readonly string[] LaunchArgumentKeys = [TestHookCommand.NameKey, TestHookDispatcher.LanguageKey];
	private static bool _launchHookRun;

	// Mid-flow: the test URL (Maestro openLink). At launch: the flow's launchApp arguments, which iOS exposes as user defaults ("-cmd skip-first-run").
	static partial void AddLaunchHandlers(MauiAppBuilder builder, string urlScheme) =>
		builder.ConfigureLifecycleEvents(events => events.AddiOS(ios => ios
			.OpenUrl((_, url, _) => HandleUrl(url?.AbsoluteString, urlScheme))
			.OnActivated(_ => RunLaunchArguments())));

	private static void RunLaunchArguments()
	{
		if (_launchHookRun)
		{
			return;
		}

		// Only the keys a command uses, never every default in the app's settings domain.
		NSUserDefaults defaults = NSUserDefaults.StandardUserDefaults;
		var args = new List<KeyValuePair<string, string>>();
		foreach (string key in LaunchArgumentKeys)
		{
			if (defaults.StringForKey(key) is { } value)
			{
				args.Add(new KeyValuePair<string, string>(key, value));
			}
		}

		// Done only once a command was handed on (queued or dispatched); an activation without one leaves the flag clear.
		_launchHookRun = RunLaunchHook(args);
	}

	private static bool HandleUrl(string? url, string urlScheme)
	{
		TestHookCommand? command = TestHookCommand.FromUrl(url, urlScheme);
		if (command is null)
		{
			return false;
		}

		Submit(command);
		return true;
	}
}
#endif
