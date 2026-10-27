#if DEBUG
using Android.Content;
using Microsoft.Maui.LifecycleEvents;

namespace ReadTheStupidText.Mobile.Shared.Testing;

/// <summary>
/// Android side of the Debug-only test hooks (Slice 162): a receiver registered at run time (so the manifest, and
/// Release, stay untouched) for the action <see cref="BroadcastActionFor"/>. Drive it with
/// <c>adb shell am broadcast -a uk.sirous.rtst-test.TEST_HOOK --es cmd skip-first-run --es lang en</c> (Dino: <c>uk.sirous.dino-test.TEST_HOOK</c>).
/// </summary>
public static partial class TestHooks
{
	private const string BroadcastActionPrefix = "uk.sirous.";
	private const string BroadcastActionSuffix = ".TEST_HOOK";

	/// <summary>The broadcast action of the app with this test scheme: <c>uk.sirous.rtst-test.TEST_HOOK</c>, <c>uk.sirous.dino-test.TEST_HOOK</c>, so one broadcast never reaches both apps.</summary>
	public static string BroadcastActionFor(string urlScheme) => BroadcastActionPrefix + urlScheme + BroadcastActionSuffix;

	private static HookReceiver? _receiver;

	// A flow's launchApp arguments arrive as intent extras (Maestro: --es key value) on the first start and on a relaunch.
	static partial void AddLaunchHandlers(MauiAppBuilder builder, string urlScheme) =>
		builder.ConfigureLifecycleEvents(events => events.AddAndroid(android => android
			.OnPostCreate((activity, savedState) => RunOnLaunch(activity.Intent, savedState))
			.OnNewIntent((_, intent) => RunLaunchHook(Extras(intent)))));

	// A launch hook runs once per launch: an activity recreated (rotation, theme, locale) comes back with a saved state and the same intent.
	private static void RunOnLaunch(Intent? intent, global::Android.OS.Bundle? savedState)
	{
		if (savedState is null)
		{
			RunLaunchHook(Extras(intent));
		}
	}

	static partial void InstallPlatform(TestHookDispatcher dispatcher, string urlScheme)
	{
		Context context = global::Android.App.Application.Context;
		_receiver = new HookReceiver();
		var filter = new IntentFilter(BroadcastActionFor(urlScheme));
		if (OperatingSystem.IsAndroidVersionAtLeast(33))
		{
			context.RegisterReceiver(_receiver, filter, ReceiverFlags.Exported);
		}
		else
		{
			context.RegisterReceiver(_receiver, filter);
		}

		TestHookLog.Write("HOOKS installed");
	}

	private sealed class HookReceiver : BroadcastReceiver
	{
		public override void OnReceive(Context? context, Intent? intent)
		{
			TestHookCommand? command = TestHookCommand.Parse(intent?.GetStringExtra(TestHookCommand.NameKey), Extras(intent));
			if (command is null)
			{
				TestHookLog.Write($"{TestHookLog.HookPrefix} missing-cmd");
				return;
			}

			TestHooks.Submit(command);
		}
	}

	private static IEnumerable<KeyValuePair<string, string>> Extras(Intent? intent)
	{
		if (intent?.Extras is not { } extras)
		{
			yield break;
		}

		foreach (string key in extras.KeySet() ?? [])
		{
			yield return new KeyValuePair<string, string>(key, extras.GetString(key) ?? string.Empty);
		}
	}
}
#endif
