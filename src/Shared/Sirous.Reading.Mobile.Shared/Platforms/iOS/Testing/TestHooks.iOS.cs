#if DEBUG
using Foundation;
using Microsoft.Maui.LifecycleEvents;

namespace Sirous.Reading.Mobile.Shared.Testing;

/// <summary>
/// iOS side of the Debug-only test hooks (Slice 162): a URL such as <c>rtst-test://skip-first-run?lang=en</c>, which
/// Maestro's <c>openLink</c> opens. The scheme is declared by <c>Testing/TestHooks.Rtst.plist</c> or <c>TestHooks.Dino.plist</c>, merged into the
/// app's Info.plist in Debug builds only (see <c>TestHooks.props</c>); the handler is registered by <c>AddTestHooks</c>.
/// </summary>
public static partial class TestHooks
{
	private static bool _launchHookRun;

	// Slice 379 part B: under the UIKit scene lifecycle (required by the iOS 27 SDK) the application-level OpenUrl and OnActivated no longer fire, so the scene events carry them.
	// Mid-flow: the test URL (Maestro openLink, SceneOpenUrl). A URL that launches the app arrives in the scene's connection options (SceneWillConnect; MAUI does not replay it).
	// At launch: the flow's launchApp arguments, which iOS exposes as user defaults ("-cmd skip-first-run"), read when the scene becomes active.
	static partial void AddLaunchHandlers(MauiAppBuilder builder, string urlScheme) =>
		builder.ConfigureLifecycleEvents(events => events.AddiOS(ios => ios
			.SceneOpenUrl((_, contexts) => HandleUrlContexts(contexts, urlScheme))
			.SceneWillConnect((_, _, options) => HandleUrlContexts(options.UrlContexts, urlScheme))
			.SceneOnActivated(_ => RunLaunchArguments())));

	private static bool HandleUrlContexts(NSSet<UIKit.UIOpenUrlContext>? contexts, string urlScheme)
	{
		bool handled = false;
		foreach (UIKit.UIOpenUrlContext context in contexts?.ToArray() ?? [])
		{
			handled |= HandleUrl(context.Url?.AbsoluteString, urlScheme);
		}

		return handled;
	}

	private static void RunLaunchArguments()
	{
		if (_launchHookRun)
		{
			return;
		}

		// No command name, nothing else is read; with one, only TestHookDispatcher.LaunchKeys, never every default in the app's settings domain.
		NSUserDefaults defaults = NSUserDefaults.StandardUserDefaults;
		var args = new List<KeyValuePair<string, string>>();
		if (defaults.StringForKey(TestHookCommand.NameKey) is null)
		{
			return;
		}

		foreach (string key in TestHookDispatcher.LaunchKeys)
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
