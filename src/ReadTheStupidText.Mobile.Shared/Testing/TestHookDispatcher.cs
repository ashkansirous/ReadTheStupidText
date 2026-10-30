#if DEBUG
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Application.Translation;
using ReadTheStupidText.Domain.Reading;

namespace ReadTheStupidText.Mobile.Shared.Testing;

/// <summary>
/// Runs a <see cref="TestHookCommand"/> against the app's state (Slice 162), so a flow never taps through first-run
/// screens to reach what it tests. Both platforms' entry points (Android broadcast, iOS URL) call <see cref="Dispatch"/>.
/// Commands: <c>skip-first-run [lang=en]</c>, <c>delete-translation-model from=.. [to=..]</c> (Slice 187), <c>stop-read</c>, <c>state</c>, <c>read text=... [speed=1.5]</c> (Slice 165: reads a fixed text on the app's own reader, for the real-phone timing script).
/// An app adds its own commands through <see cref="AppCommands"/> (Slice 175: Dino's <c>open-book id=... [page=n] [play=1]</c>). Debug builds only.
/// </summary>
public sealed class TestHookDispatcher
{
	public const string SkipFirstRun = "skip-first-run";
	public const string StopRead = "stop-read";
	public const string State = "state";
	public const string CancelPackDownloads = "cancel-pack-downloads";
	public const string Read = "read";
	public const string TextKey = "text";
	public const string SpeedKey = "speed";
	public const string DeleteTranslationModel = "delete-translation-model";
	public const string FromKey = "from";
	public const string ToKey = "to";
	public const int PackWaitSeconds = 300;
	public const int PackIdleGraceSeconds = 10;
	public const string DefaultLanguage = "en";
	public const string LanguageKey = "lang";
	public const string Ok = "ok";
	public const string Unknown = "unknown";
	public const string Failed = "failed";
	public const int WindowRetryMilliseconds = 250;
	public const int WindowWaitMilliseconds = 15000;

	/// <summary>
	/// Commands only one app has (Slice 175), set once by that app's own Debug code before the first command arrives. Returns the outcome text
	/// (<see cref="Ok"/>, <see cref="Failed"/>), or null when the name is not its command, which then logs <see cref="Unknown"/>.
	/// </summary>
	public static Func<TestHookCommand, string?>? AppCommands { get; set; }

	/// <summary>The launch-argument names the app's own commands take (iOS reads only listed keys), set next to <see cref="AppCommands"/>.</summary>
	public static IReadOnlyList<string> AppLaunchKeys { get; set; } = [];

	/// <summary>
	/// Every launch-argument name a hook needs, built in one place and at call time (the app sets <see cref="AppLaunchKeys"/> after startup):
	/// the command name first, then the keys this dispatcher's own commands read, then the app's. A platform that filters launch arguments reads exactly these.
	/// </summary>
	public static IReadOnlyList<string> LaunchKeys =>
		TestHookLaunchKeys.Build(TestHookCommand.NameKey, [LanguageKey, TextKey, SpeedKey, FromKey, ToKey], AppLaunchKeys);

	private readonly ISettingsStore _settings;
	private readonly ISpeechReader _reader;
	private readonly Func<string, bool> _completeFirstRun;
	private readonly Func<ITranslationPacks?>? _translationPacks;
	private readonly VoicePackInstaller? _installer;
	private bool _skipPending;

	/// <param name="settings">The app's settings store.</param>
	/// <param name="reader">The app's speech reader.</param>
	/// <param name="completeFirstRun">The app's own first-run completion for a language code (culture, pack, root); false when it could not run.</param>
	/// <param name="translationPacks">The app's translation engine, when it manages packs (ML Kit), looked up when first needed.</param>
	/// <param name="installer">The voice-pack installer, so <c>delete-translation-model</c> waits for the voice pack first.</param>
	public TestHookDispatcher(ISettingsStore settings, ISpeechReader reader, Func<string, bool> completeFirstRun,
		Func<ITranslationPacks?>? translationPacks = null, VoicePackInstaller? installer = null)
	{
		_translationPacks = translationPacks;
		_installer = installer;
		_settings = settings;
		_reader = reader;
		_completeFirstRun = completeFirstRun;
	}

	/// <summary>Runs the command on the main thread; logs <c>HOOK name ok</c> or <c>HOOK name unknown</c>.</summary>
	public void Dispatch(TestHookCommand command) =>
		MainThread.BeginInvokeOnMainThread(() => Run(command));

	// One skip at a time, on the main thread: a second skip-first-run while one is waiting for the first-run page is ignored.
	private void StartSkipOnce(TestHookCommand command)
	{
		if (_skipPending)
		{
			TestHookLog.Write($"{TestHookLog.HookPrefix} {command.Name} duplicate");
			return;
		}

		_skipPending = true;
		_ = SkipWhenWindowReady(command);
	}

	// A first run that cannot complete yet (the window is not up) is retried on the main thread until it can, for a bounded time; it runs once.
	private async Task SkipWhenWindowReady(TestHookCommand command)
	{
		string outcome = Failed;
		for (int waited = 0; waited < WindowWaitMilliseconds; waited += WindowRetryMilliseconds)
		{
			if (Skip(command))
			{
				outcome = Ok;
				break;
			}

			await Task.Delay(WindowRetryMilliseconds);
		}

		_skipPending = false;
		TestHookLog.Write($"{TestHookLog.HookPrefix} {command.Name} {outcome}");
	}

	private void Run(TestHookCommand command)
	{
		if (command.Name == SkipFirstRun)
		{
			StartSkipOnce(command);
			return;
		}

		if (command.Name == DeleteTranslationModel)
		{
			_ = DeleteModelsAndLog(command);
			return;
		}

		string outcome = command.Name switch
		{
			StopRead => Stop(),
			State => Report(),
			CancelPackDownloads => CancelDownloads(),
			Read => StartRead(command),
			_ => AppCommands?.Invoke(command) ?? Unknown,
		};
		TestHookLog.Write($"{TestHookLog.HookPrefix} {command.Name} {outcome}");
	}

	// delete-translation-model from=<lang> [to=<lang>] (Slice 187): waits (bounded) for the voice pack, then removes only those ML Kit models, so an offline
	// Read reaches the translation download and not the voice pack. Logs ok, or failed with the reason.
	private async Task DeleteModelsAndLog(TestHookCommand command)
	{
		string outcome;
		try
		{
			await WaitForVoicePack();
			outcome = await DeleteModels(command);
		}
		catch (Exception ex)
		{
			outcome = $"{Failed} {TestHookLog.FaultSummary(ex)}";
		}

		TestHookLog.Write($"{TestHookLog.HookPrefix} {command.Name} {outcome}");
	}

	private async Task<string> DeleteModels(TestHookCommand command)
	{
		ITranslationPacks? packs = _translationPacks?.Invoke();
		string[] languages = [command.Get(FromKey, string.Empty), command.Get(ToKey, string.Empty)];
		if (packs is null || languages.All(l => l.Length == 0))
		{
			return Failed;
		}

		foreach (string language in languages.Where(l => l.Length > 0).Distinct())
		{
			await packs.DeletePackAsync(language);
		}

		return Ok;
	}

	// Ready ends the wait; so does a stopped download state, and an installer that never started (Idle) after a short grace (pack already on the device).
	private async Task WaitForVoicePack()
	{
		if (_installer is null)
		{
			return;
		}

		for (int waited = 0; waited < PackWaitSeconds * 1000; waited += WindowRetryMilliseconds)
		{
			bool inProgress = _installer.State is PackInstallState.Resolving or PackInstallState.Downloading
				|| (_installer.State is PackInstallState.Idle && waited < PackIdleGraceSeconds * 1000);
			if (!inProgress)
			{
				return;
			}

			await Task.Delay(WindowRetryMilliseconds);
		}
	}

	// Runs the app's own first-run completion, so the culture, the voice pack and the root end up as after a real first run.
	// Already past first run is a no-op that still counts as done; a first run that could not complete (no window yet) is a failure.
	private bool Skip(TestHookCommand command) =>
		_settings.Language is not null || _completeFirstRun(command.Get(LanguageKey, DefaultLanguage));

	private string Stop()
	{
		_reader.Stop();
		return Ok;
	}

	// Sets the speed when given (the reader's own SetSpeed, not the stored setting), then reads the text; the READ_START / CHUNK / READ_END markers follow.
	private string StartRead(TestHookCommand command)
	{
		string text = command.Get(TextKey, string.Empty);
		if (text.Length == 0)
		{
			return Failed;
		}

		string speedText = command.Get(SpeedKey, string.Empty);
		if (speedText.Length > 0)
		{
			if (!double.TryParse(speedText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double speed))
			{
				return Failed;
			}

			_reader.SetSpeed(new PlaybackRate(speed));
		}

		TestHookLog.NoteReadRequested();
		TestHookLog.Write($"{TestHookLog.HookPrefix} {Read} start");
		_ = LogIfFaulted(_reader.SpeakAsync(text));
		return Ok;
	}

	// A faulting read is logged (type and top stack frame, never the text) instead of vanishing with the dropped task.
	private static async Task LogIfFaulted(Task read)
	{
		try
		{
			await read;
		}
		catch (Exception ex)
		{
			TestHookLog.Write($"{TestHookLog.HookPrefix} {Read} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	// Slice 186: the notification's Cancel (the service is not exported, so adb cannot tap it); Failed where the app has no such notification.
	private static string CancelDownloads()
	{
		if (PackDownloadHost.CancelAllActive is not { } cancel)
		{
			return Failed;
		}

		cancel();
		return Ok;
	}

	private string Report()
	{
		TestHookLog.Write($"STATE language={_settings.Language ?? "none"} reader={_reader.State} speed={_settings.Speed.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
		return Ok;
	}
}
#endif
