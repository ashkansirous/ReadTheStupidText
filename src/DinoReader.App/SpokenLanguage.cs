using DinoReader.Core;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Mobile.Shared;

namespace DinoReader;

/// <summary>
/// Points the voice at the grown-up's language (Slice 66): the neural reader follows the stored language setting, so picking a language
/// activates (or starts downloading) that voice pack, and the phone voice reads until it is ready.
/// Only a grown-up screen switches it; the reading screen and the cast screen never do. The child's story language (Slice 173) is the
/// reading screen's own, passed to everything the read uses, and never reaches this setting.
/// A switch made while an install is still running is not lost: a single worker serves the latest wanted language after the current run, once per change.
/// </summary>
public sealed class SpokenLanguage
{
	private readonly ISettingsStore _settings;
	private readonly VoicePackInstaller _installer;
	private readonly LatestWinsWorker<WantedLanguage> _worker;
	private readonly object _gate = new();
	private CancellationTokenSource _stop = new();
	private bool _cleared;

	public SpokenLanguage(ISettingsStore settings, VoicePackInstaller installer)
	{
		_settings = settings;
		_installer = installer;
		_worker = new LatestWinsWorker<WantedLanguage>(ServeAsync, ex => System.Diagnostics.Debug.WriteLine($"SpokenLanguage: {ex}"));
	}

	/// <summary>The language the voice speaks now (the stored setting, English when none).</summary>
	public string Current => UiLanguage.Resolve(_settings.Language);

	/// <summary>True once the worker has stored <paramref name="language"/> as the setting (a state read before that is stale).</summary>
	public bool HasStored(string language) =>
		string.Equals(_settings.Language, UiLanguage.Resolve(language), StringComparison.OrdinalIgnoreCase);

	// A language to serve and why: a grown-up's own tap is User (it lifts a Cancel hold); anything else must pass Automatic (Slice 204).
	private readonly record struct WantedLanguage(string Code, PackStartReason Reason);

	/// <summary>Uses <paramref name="language"/> for the voice, and starts that language's pack unless it is the one in use and ready. <paramref name="reason"/> is User only for the grown-up's own language tap.</summary>
	public void Use(string language, PackStartReason reason)
	{
		string code = UiLanguage.Resolve(language);
		bool same = string.Equals(_settings.Language, code, StringComparison.OrdinalIgnoreCase);
		if (same && _installer.State == PackInstallState.Ready)
		{
			return;
		}

		lock (_gate)
		{
			_cleared = false;
		}

		_worker.Use(new WantedLanguage(code, reason));
	}

	/// <summary>Forgets the stored language (the first run's "Pick another"): a switch still queued or waiting on a download is dropped, never stored.</summary>
	public void Clear()
	{
		lock (_gate)
		{
			_cleared = true;
			_settings.Language = null;
			CancellationTokenSource old = _stop;
			_stop = new CancellationTokenSource();
			old.Cancel();
		}
	}

	// One run per wanted language: store it (the installer starts a run for the change itself), then
	// join that run: StartAsync completes when the run in flight settles, so the language is never
	// reported served while its pack is still resolving. A Clear drops the run and stops waiting.
	private async Task ServeAsync(WantedLanguage wanted)
	{
		string code = wanted.Code;
		CancellationToken stop = CurrentStop();
		await WhileBusyAsync(stop);
		if (!TryStore(code, stop))
		{
			return;
		}

		await WaitAsync(_installer.StartAsync(wanted.Reason), stop);
		await WhileBusyAsync(stop);
	}

	private CancellationToken CurrentStop()
	{
		lock (_gate)
		{
			return _stop.Token;
		}
	}

	private bool TryStore(string code, CancellationToken stop)
	{
		lock (_gate)
		{
			if (_cleared || stop.IsCancellationRequested)
			{
				return false;
			}

			_settings.Language = code;
			return true;
		}
	}

	private static async Task WaitAsync(Task work, CancellationToken stop)
	{
		try
		{
			await work.WaitAsync(stop);
		}
		catch (OperationCanceledException) when (stop.IsCancellationRequested)
		{
			// Cleared meanwhile: the download may carry on in the background; its result is ignored.
		}
	}

	// A published state can still be queued for the main thread when StartAsync returns; wait it out.
	private async Task WhileBusyAsync(CancellationToken stop)
	{
		while (!stop.IsCancellationRequested && _installer.State is PackInstallState.Resolving or PackInstallState.Downloading)
		{
			var changed = new TaskCompletionSource();
			EventHandler handler = (_, _) => changed.TrySetResult();
			_installer.Changed += handler;
			try
			{
				if (_installer.State is PackInstallState.Resolving or PackInstallState.Downloading)
				{
					await WaitAsync(changed.Task, stop);
				}
			}
			finally
			{
				_installer.Changed -= handler;
			}
		}
	}
}
