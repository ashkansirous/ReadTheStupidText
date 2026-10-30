#if DEBUG
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Mobile.Shared.Testing;

/// <summary>
/// Debug-only entry point of the scripted device flows (Slice 162, Decision 165): an app calls
/// <see cref="Install"/> once after building its services, inside <c>#if DEBUG</c>. None of the Testing code
/// is compiled into Release (the csproj drops the folders, and every file is inside <c>#if DEBUG</c>).
/// </summary>
public static partial class TestHooks
{
	private static TestMarkers? _markers;
	private static TestHookDispatcher? _dispatcher;
	private static readonly object PendingLock = new();
	private static List<TestHookCommand>? _pending = [];

	/// <param name="services">The built service provider.</param>
	/// <param name="urlScheme">The iOS URL scheme this app answers to (<c>rtst-test</c>, <c>dino-test</c>); Android uses an intent action.</param>
	/// <param name="completeFirstRun">The app's own first-run completion for a language code (see <see cref="TestHookDispatcher"/>).</param>
	public static void Install(IServiceProvider services, string urlScheme, Func<string, bool> completeFirstRun)
	{
		ISpeechReader reader = services.GetRequiredService<ISpeechReader>();
		_markers = new TestMarkers(reader, services.GetRequiredService<VoicePackInstaller>());
		_dispatcher = new TestHookDispatcher(services.GetRequiredService<ISettingsStore>(), reader, completeFirstRun,
			() => services.GetService<ReadTheStupidText.Application.Translation.ITranslator>() as ITranslationPacks,
			services.GetRequiredService<VoicePackInstaller>());
		InstallPlatform(_dispatcher, urlScheme);
		FlushPending(_dispatcher);
	}

	/// <summary>
	/// Runs a command from a platform entry point. One that arrives before <see cref="Install"/> is queued and runs once
	/// at install (never dropped, never run twice).
	/// </summary>
	internal static bool Submit(TestHookCommand command)
	{
		lock (PendingLock)
		{
			if (_pending is not null)
			{
				_pending.Add(command);
				TestHookLog.Write($"{TestHookLog.HookPrefix} {command.Name} queued");
				return false;
			}
		}

		_dispatcher!.Dispatch(command);
		return true;
	}

	private static void FlushPending(TestHookDispatcher dispatcher)
	{
		List<TestHookCommand> queued;
		lock (PendingLock)
		{
			queued = _pending ?? [];
			_pending = null;
		}

		foreach (TestHookCommand command in queued)
		{
			dispatcher.Dispatch(command);
		}
	}

	/// <summary>
	/// Registers the launch-time entry points on the MAUI builder; call before Build, inside <c>#if DEBUG</c>. A flow's
	/// <c>launchApp</c> <c>arguments</c> (cmd, lang, ...) arrive as intent extras on Android and as launch arguments on iOS,
	/// and iOS also answers to the app's test URL scheme.
	/// </summary>
	public static MauiAppBuilder AddTestHooks(this MauiAppBuilder builder, string urlScheme)
	{
		AddLaunchHandlers(builder, urlScheme);
		return builder;
	}

	/// <summary>Runs the hook named by a launch's arguments, if there is one (the platform files call this); true when there was one (queued or dispatched).</summary>
	private static bool RunLaunchHook(IEnumerable<KeyValuePair<string, string>> launchArgs)
	{
		var args = launchArgs.ToList();
		string? name = args.FirstOrDefault(a => a.Key == TestHookCommand.NameKey).Value;
		if (TestHookCommand.Parse(name, args) is not { } command)
		{
			return false;
		}

		Submit(command);
		return true;
	}

	static partial void AddLaunchHandlers(MauiAppBuilder builder, string urlScheme);

	static partial void InstallPlatform(TestHookDispatcher dispatcher, string urlScheme);
}
#endif
