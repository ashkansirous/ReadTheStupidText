#if DEBUG
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;

namespace ReadTheStupidText.Mobile.Shared.Testing;

/// <summary>
/// Runs a <see cref="TestHookCommand"/> against the app's state (Slice 162), so a flow never taps through first-run
/// screens to reach what it tests. Both platforms' entry points (Android broadcast, iOS URL) call <see cref="Dispatch"/>.
/// Commands: <c>skip-first-run [lang=en]</c>, <c>stop-read</c>, <c>state</c>. Debug builds only.
/// </summary>
public sealed class TestHookDispatcher
{
	public const string SkipFirstRun = "skip-first-run";
	public const string StopRead = "stop-read";
	public const string State = "state";
	public const string DefaultLanguage = "en";
	public const string LanguageKey = "lang";
	public const string Ok = "ok";
	public const string Unknown = "unknown";
	public const string Failed = "failed";
	public const int WindowRetryMilliseconds = 250;
	public const int WindowWaitMilliseconds = 15000;

	private readonly ISettingsStore _settings;
	private readonly ISpeechReader _reader;
	private readonly Func<string, bool> _completeFirstRun;
	private bool _skipPending;

	/// <param name="settings">The app's settings store.</param>
	/// <param name="reader">The app's speech reader.</param>
	/// <param name="completeFirstRun">The app's own first-run completion for a language code (culture, pack, root); false when it could not run.</param>
	public TestHookDispatcher(ISettingsStore settings, ISpeechReader reader, Func<string, bool> completeFirstRun)
	{
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

		string outcome = command.Name switch
		{
			StopRead => Stop(),
			State => Report(),
			_ => Unknown,
		};
		TestHookLog.Write($"{TestHookLog.HookPrefix} {command.Name} {outcome}");
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

	private string Report()
	{
		TestHookLog.Write($"STATE language={_settings.Language ?? "none"} reader={_reader.State} speed={_settings.Speed.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
		return Ok;
	}
}
#endif
