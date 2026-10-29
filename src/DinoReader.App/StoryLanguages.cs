using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared;
using ReadTheStupidText.Mobile.Shared.Network;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>
/// The languages the child's stories can be in (Slice 173, Decision 168): the app language plus up to two side languages the parent picked
/// (<see cref="StoryLanguageSet"/> holds the rules), and the one language the child reads in. The side languages and the child's pick are kept in
/// preferences; the app language is <see cref="LanguageChoice.Current()"/>. The child's pick reaches the reading screen only, which holds its reading
/// language itself and passes it to everything a read uses; no grown-up screen or shelf title sees it. The usage events of a book that was read (open, finish, a switch)
/// do carry the language it was read in, a closed-list value like the app language.
/// </summary>
public static class StoryLanguages
{
	private const string SidesKey = "dino.story.sides";
	private const string PickKey = "dino.story.reading";
	private const string NoPackKey = "dino.story.nopack";
	private const string WantedKey = "dino.story.wanted";
	private const char PairSeparator = ',';
	private const char FlagSeparator = ':';

	/// <summary>Raised after the side languages were saved (from Settings, the app-language swap or the intro), with the set now in force: whatever waited for a language that left it can stop.</summary>
	public static event Action<StoryLanguageSet>? SidesSaved;

	/// <summary>The languages the catalogue has no voice pack for (kept so they stay choosable after a restart; the phone's own voice reads them).</summary>
	public static IEnumerable<string> LoadNoPack() => StoryLanguageSet.Split(Preferences.Default.Get(NoPackKey, (string?)null));

	public static void SaveNoPack(IEnumerable<string> languages) => Preferences.Default.Set(NoPackKey, StoryLanguageSet.Join(languages));

	/// <summary>
	/// Remembers that a book in <paramref name="language"/> was read with the phone's voice because that language's pack is not installed (Slice 173): Settings > Story
	/// languages then shows the pack as missing, with its Download. Nothing on the child's side says or offers anything.
	/// </summary>
	public static void NoteWanted(string language)
	{
		string code = UiLanguage.Resolve(language);
		List<string> wanted = [.. StoryLanguageSet.Split(Preferences.Default.Get(WantedKey, (string?)null))];
		if (!wanted.Contains(code))
		{
			wanted.Add(code);
			Preferences.Default.Set(WantedKey, StoryLanguageSet.Join(wanted));
		}
	}

	/// <summary>Whether a book in <paramref name="language"/> was read with the phone's voice for want of a pack (see <see cref="NoteWanted"/>).</summary>
	public static bool IsWanted(string language) =>
		StoryLanguageSet.Split(Preferences.Default.Get(WantedKey, (string?)null)).Contains(UiLanguage.Resolve(language));

	/// <summary>The app language and the saved side languages.</summary>
	public static StoryLanguageSet Current() => StoryLanguageSet.Create(LanguageChoice.Current(), StoryLanguageSet.Split(Preferences.Default.Get(SidesKey, (string?)null)));

	/// <summary>Saves the side languages of <paramref name="set"/> (its app language is the app's own pick). The child's pick is forgotten when it left the set.</summary>
	public static void SaveSides(StoryLanguageSet set)
	{
		Preferences.Default.Set(SidesKey, StoryLanguageSet.Join(set.Sides));
		if (!set.Contains(Preferences.Default.Get(PickKey, (string?)null)))
		{
			Preferences.Default.Remove(PickKey);
		}

		SidesSaved?.Invoke(set);
	}

	/// <summary>The language the child picked (remembered across books and restarts), the app language when none or when it left the set.</summary>
	public static string Pick() => Current().Reading(Preferences.Default.Get(PickKey, (string?)null));

	public static void SetPick(string language) => Preferences.Default.Set(PickKey, UiLanguage.Resolve(language));

	/// <summary>The country code whose flag stands for <paramref name="language"/> (one fixed flag per language, kept in the strings file so it can change); empty when none.</summary>
	public static string FlagOf(string language)
	{
		string code = UiLanguage.Resolve(language);
		foreach (string pair in AppResources.StoryLanguages_FlagMap.Split(PairSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			string[] parts = pair.Split(FlagSeparator);
			if (parts.Length == 2 && parts[0] == code)
			{
				return parts[1];
			}
		}

		return string.Empty;
	}

	/// <summary>The languages in <paramref name="languages"/> by their names in their own script, like "English, فارسی".</summary>
	public static string Names(IEnumerable<string> languages) => string.Join(", ", languages.Select(LanguageNames.NativeName));
}

/// <summary>
/// Downloads the voice packs of the story languages (Slice 173), one run per language and on its own: the app's spoken-language setting is not touched, so a
/// side language's voice arrives while the child keeps reading in another. A singleton, so a download started in the intro keeps going and its progress shows
/// in Settings. The reader switches to the language's voice itself when the child picks it (<see cref="SpokenLanguage"/>). Each run's end is counted
/// (<see cref="UsageEventName.VoicePackDownload"/>).
/// </summary>
public sealed class StoryLanguagePacks
{
	private const int PercentScale = 100;
	private static readonly TimeSpan CatalogTimeout = TimeSpan.FromSeconds(8);

	private readonly IVoicePackService _packs;
	private readonly IPlatformServices _platform;
	private readonly UsageCounter _usage;
	private readonly object _gate = new();
	private readonly Dictionary<string, CancellationTokenSource> _runs = [];
	private readonly Dictionary<string, double> _progress = [];
	private readonly HashSet<string> _failed = [];
	private readonly HashSet<string> _noPack = [.. StoryLanguages.LoadNoPack()]; // persisted: a language without a pack must stay pickable after a restart
	private readonly HashSet<string> _waiting = [];
	private readonly Dictionary<string, PackDownloadResult> _lastResult = []; // Slice 182: how each language's last run ended, for the automatic restart
	private readonly Dictionary<string, double> _waitingInternet = []; // stopped Offline/Failed and waiting to restart by itself: the share it reached
	private readonly HashSet<string> _downloadNow = []; // started with "Download now" (or Retry): may restart over mobile data
	private readonly PackResumeWatcher _resume;
	private int _watchingNetwork;

	public StoryLanguagePacks(IVoicePackService packs, IPlatformServices platform, UsageCounter usage, PackResumeWatcher resume)
	{
		_resume = resume;
		_packs = packs;
		_platform = platform;
		_usage = usage;
		PackDownloadHost.CancelRequested += OnNotificationCancel;
		PackDownloadHost.NotificationText = fraction => AppResources.Format(AppResources.StoryLanguages_Getting, (int)(fraction * 100)); // Slice 183: the notification shows the app's own status line
		PackDownloadHost.CancelLabel = () => AppResources.Common_Cancel;
		StoryLanguages.SidesSaved += DropWaitingOutside; // a language that left the set (Settings, the app-language swap, the intro) no longer waits for Wi-Fi
	}

	// Slice 183: Cancel on the download notification stops the run of that pack's language exactly as the in-app Cancel does.
	// Every language that waits for or downloads a pack, since languages can share one pack: Cancel is final for each of them.
	private readonly Dictionary<string, HashSet<string>> _packLanguages = new();

	private void OnNotificationCancel(string packId)
	{
		string[] codes;
		lock (_gate)
		{
			if (!_packLanguages.TryGetValue(packId, out HashSet<string>? languages))
			{
				return;
			}

			codes = [.. languages];
		}

		foreach (string code in codes)
		{
			Cancel(code);
		}
	}

	private void TrackPack(string packId, string code)
	{
		lock (_gate)
		{
			if (!_packLanguages.TryGetValue(packId, out HashSet<string>? languages))
			{
				_packLanguages[packId] = languages = [];
			}

			languages.Add(code);
		}
	}

	// A run that ended no longer waits for a notification Cancel; an emptied pack entry goes too.
	private void UntrackLanguage(string code)
	{
		lock (_gate)
		{
			if (_runs.ContainsKey(code))
			{
				return; // a newer run of this language started while the old one unwound: it tracks itself
			}

			foreach ((string packId, HashSet<string> languages) in _packLanguages.ToArray())
			{
				languages.Remove(code);
				if (languages.Count == 0)
				{
					_packLanguages.Remove(packId);
				}
			}
		}
	}

	/// <summary>Raised on the main thread whenever a run starts, moves on or ends.</summary>
	public event EventHandler? Changed;

	/// <summary>Where <paramref name="language"/>'s voice stands: installed (or not needed here), on its way, or not fetched.</summary>
	public LanguageVoiceState StateOf(string language)
	{
		string code = UiLanguage.Resolve(language);
		lock (_gate)
		{
			if (_runs.ContainsKey(code))
			{
				return LanguageVoiceState.Downloading;
			}

			if (_noPack.Contains(code) || !_platform.SupportsNeuralVoice)
			{
				return LanguageVoiceState.Ready;
			}
		}

		return LanguageChoice.IsInstalled(_packs, code) ? LanguageVoiceState.Ready : LanguageVoiceState.NotInstalled;
	}

	/// <summary>The download's progress, 0 to 1, while <see cref="StateOf"/> says Downloading.</summary>
	public double ProgressOf(string language)
	{
		lock (_gate)
		{
			string code = UiLanguage.Resolve(language);
			return _progress.TryGetValue(code, out double live) ? live : _waitingInternet.GetValueOrDefault(code);
		}
	}

	/// <summary>Whether <paramref name="language"/> is ticked but waits for Wi-Fi (the grown-up chose "Wait for Wi-Fi" on mobile data).</summary>
	public bool IsWaitingForWifi(string language)
	{
		lock (_gate)
		{
			return _waiting.Contains(UiLanguage.Resolve(language));
		}
	}

	/// <summary>Whether the download of <paramref name="language"/> stopped for want of a connection and carries on by itself (Slice 182, mock frame 95): shown as a grey bar at <see cref="ProgressOf"/>.</summary>
	public bool IsWaitingForInternet(string language)
	{
		lock (_gate)
		{
			return _waitingInternet.ContainsKey(UiLanguage.Resolve(language));
		}
	}

	/// <summary>Whether the automatic retry of <paramref name="language"/> failed too, so only the grown-up's Retry starts it again (Slice 182): the row shows Retry, not "carries on by itself".</summary>
	public bool NeedsRetry(string language) => _resume.StateOf(ResumeKey(UiLanguage.Resolve(language))) == PackResumeState.NeedsUserRetry;

	/// <summary>Whether the waiting line should say "Waiting for Wi-Fi": the phone is on mobile data only and the grown-up did not choose "Download now".</summary>
	public bool WaitingNeedsWifi(string language)
	{
		lock (_gate)
		{
			return !_downloadNow.Contains(UiLanguage.Resolve(language)) && PackResumeWatcher.CellularOnly();
		}
	}

	/// <summary>Whether the last run for <paramref name="language"/> failed (offline, a bad file).</summary>
	public bool HasFailed(string language)
	{
		lock (_gate)
		{
			return _failed.Contains(UiLanguage.Resolve(language));
		}
	}

	/// <summary>
	/// Gets the voices of <paramref name="languages"/> that are missing. On mobile data (Android and iOS) the grown-up is asked first, naming each language
	/// with its size: "Download now" starts them, "Wait for Wi-Fi" starts them when Wi-Fi is there. Windows and Wi-Fi start at once. Only a grown-up screen calls this.
	/// The sizes come from the catalogue, fetched behind <paramref name="wait"/> (shown after 500 ms, with Cancel, which drops the whole request) and given up after a
	/// timeout, when the names are shown without sizes.
	/// </summary>
	public async Task RequestAsync(Page? host, IEnumerable<string> languages, WaitController? wait = null)
	{
		List<string> missing = [.. languages.Select(UiLanguage.Resolve).Distinct().Where(code => StateOf(code) == LanguageVoiceState.NotInstalled)];
		if (missing.Count == 0)
		{
			return;
		}

		if (host is not null)
		{
			// Slice 183: the grown-up is asked once about notifications before the first big download; then the Wi-Fi question, then the download.
			await PackNotificationAsk.AskOnceAsync(() => host.DisplayAlertAsync(AppResources.Notify_Title, AppResources.Notify_Body, AppResources.Notify_Continue, AppResources.Notify_NotNow));
		}

		if (!OnMobileDataOnly())
		{
			missing.ForEach(Start);
			return;
		}

		string? described = await DescribeAsync(missing, wait);
		if (described is null)
		{
			return; // Cancel on the wait: nothing starts, nothing waits
		}

		bool now = host is not null && await host.DisplayAlertAsync(
			AppResources.StoryLanguages_WifiTitle, AppResources.Format(AppResources.StoryLanguages_WifiBody, described),
			AppResources.StoryLanguages_WifiNow, AppResources.StoryLanguages_WifiWait);
		if (now)
		{
			missing.ForEach(code => Start(code, downloadNow: true));
			return;
		}

		WaitForWifi(missing);
	}

	// Cellular is all there is (offline is not asked about: the download would only fail).
	private static bool OnMobileDataOnly() =>
		(OperatingSystem.IsAndroid() || OperatingSystem.IsIOS()) && Connectivity.Current.ConnectionProfiles.Any() && !StorytellerDefault.WifiCapable();

	// "فارسی (62 MB), Svenska (58 MB)": each language the question is about, with its size when the catalogue says. Null when the grown-up cancelled the wait.
	private async Task<string?> DescribeAsync(IReadOnlyList<string> codes, WaitController? wait)
	{
		CancellationToken cancel = wait?.BeginCancellable(WaitStage.PreparingVoice, AppResources.Wait_GettingVoices) ?? CancellationToken.None;
		try
		{
			PackCatalogResult catalog = await TryCatalogAsync(cancel);
			return string.Join(", ", codes.Select(code => Described(code, catalog)));
		}
		catch (OperationCanceledException) when (cancel.IsCancellationRequested)
		{
			return null;
		}
		finally
		{
			wait?.End();
		}
	}

	private static string Described(string code, PackCatalogResult catalog)
	{
		string name = LanguageNames.NativeName(code);
		PackInfo? pack = catalog.Status == PackDownloadResult.Ok ? VoicePackSelector.Select(catalog.Packs, code) : null;
		return pack is null ? name : $"{name} ({StorytellerOffer.Megabytes(pack.SizeBytes)} MB)";
	}

	// A catalogue that takes longer than the timeout is given up on (the names show without sizes); only the grown-up's own Cancel is passed on.
	private async Task<PackCatalogResult> TryCatalogAsync(CancellationToken userCancel)
	{
		using var limit = CancellationTokenSource.CreateLinkedTokenSource(userCancel);
		limit.CancelAfter(CatalogTimeout);
		try
		{
			return await _packs.GetCatalogAsync(limit.Token);
		}
		catch (Exception ex) when (!userCancel.IsCancellationRequested)
		{
			Debug.WriteLine($"Dino Reader could not read the voice catalogue: {ex.Message}");
			return new PackCatalogResult(PackDownloadResult.Failed, []);
		}
	}

	private void DropWaitingOutside(StoryLanguageSet set)
	{
		bool dropped;
		lock (_gate)
		{
			dropped = _waiting.RemoveWhere(code => !set.Contains(code)) > 0;
		}

		if (dropped)
		{
			RaiseChanged();
		}
	}

	private void WaitForWifi(IEnumerable<string> codes)
	{
		lock (_gate)
		{
			_waiting.UnionWith(codes);
		}

		if (Interlocked.Exchange(ref _watchingNetwork, 1) == 0)
		{
			_resume.NetworkChanged += (_, _) => StartWaiting();
		}

		RaiseChanged();
	}

	// A phone that joins Wi-Fi starts the languages that waited for it.
	private void StartWaiting()
	{
		if (!StorytellerDefault.WifiCapable())
		{
			return;
		}

		StoryLanguageSet current = StoryLanguages.Current();
		string[] ready;
		lock (_gate)
		{
			_waiting.RemoveWhere(code => !current.Contains(code)); // never fetch a language the grown-up no longer has
			ready = [.. _waiting];
		}

		Array.ForEach(ready, Start);
	}

	/// <summary>Starts the download unless the voice is there or a run is already going.</summary>
	public void Start(string language) => Start(language, downloadNow: false);

	/// <summary>As <see cref="Start(string)"/>; <paramref name="downloadNow"/> lets a stopped download restart by itself over mobile data too (the grown-up chose "Download now").</summary>
	public void Start(string language, bool downloadNow) => TryStart(language, downloadNow);

	// True when this call began a run; false when the voice is there or a run is already going (that run reports its own end).
	private bool TryStart(string language, bool downloadNow)
	{
		string code = UiLanguage.Resolve(language);
		lock (_gate)
		{
			_waiting.Remove(code);
			if (downloadNow)
			{
				_downloadNow.Add(code);
			}
		}

		if (StateOf(code) != LanguageVoiceState.NotInstalled)
		{
			return false;
		}

		var cancel = new CancellationTokenSource();
		long epoch;
		lock (_gate)
		{
			if (!_runs.TryAdd(code, cancel))
			{
				cancel.Dispose();
				return false;
			}

			epoch = _resume.EpochOf(ResumeKey(code));
			_failed.Remove(code);
			_lastResult.Remove(code); // a run that ends in a plain exception must not reuse the previous run's Offline/Failed
			_progress[code] = _waitingInternet.GetValueOrDefault(code); // a restart keeps counting from the partial file's share
			_waitingInternet.Remove(code);
		}

		RaiseChanged();
		_ = Task.Run(() => RunAsync(code, cancel, epoch));
		return true;
	}

	/// <summary>
	/// Stops the download of <paramref name="language"/>; a partial file is kept for next time. The run is let go of at once, so a tick right after starts a fresh one
	/// even while the old one is still unwinding.
	/// </summary>
	public void Cancel(string language)
	{
		string code = UiLanguage.Resolve(language);
		CancellationTokenSource? cancel;
		lock (_gate)
		{
			_waiting.Remove(code);
			_downloadNow.Remove(code);
			_waitingInternet.Remove(code);
			_failed.Remove(code); // a deliberate Cancel is not a failure: the unticked row shows no failure line
			if (_runs.Remove(code, out cancel))
			{
				_progress.Remove(code);
			}

			// Cancel is final: the coordinator forgets the key and drops the Offline/Failed end of any run that started before this (under the lock, so no run starts in between with the old count).
			_resume.Report(ResumeKey(code), PackRunEnd.Cancelled, () => Task.FromResult(false));
		}

		RaiseChanged();

		try
		{
			cancel?.Cancel();
		}
		catch (ObjectDisposedException)
		{
			// The run just ended.
		}
	}

	private async Task RunAsync(string code, CancellationTokenSource cancel, long epoch)
	{
		string? outcome; // null: another run (the first-run installer) fetched the pack while this one waited for it, so this one fetched nothing and counts nothing
		try
		{
			outcome = await DownloadAsync(code, cancel);
		}
		catch (OperationCanceledException)
		{
			outcome = UsageDetail.PackCancelled;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not get the {code} voice: {ex}");
			// a dropped connection is offline (keep ticked and wait); anything else is a plain failure
			NoteResult(code, PackFailureDecision.IsNetworkLoss(ex) ? PackDownloadResult.Offline : PackDownloadResult.Failed);

			outcome = UsageDetail.PackFailed;
		}

		(bool current, PackDownloadResult last) = SettleRun(code, cancel, outcome);
		UntrackLanguage(code);
		cancel.Dispose();
		if (current)
		{
			ReportEnd(code, last, epoch); // after the run is out of _runs, so an immediate restart is never swallowed; a cancelled or replaced run reports nothing here
			if (outcome == UsageDetail.PackFailed)
			{
				SettleFailure(code, last);
			}
		}

		if (outcome is not null)
		{
			_usage.RecordGeneral(UsageEventName.VoicePackDownload, code, outcome);
		}

		RaiseChanged();
	}

	// Takes the run out of the books and settles the row's state: waiting (Offline/Failed) or done; false when a newer run or a Cancel already took over.
	private (bool Current, PackDownloadResult Last) SettleRun(string code, CancellationTokenSource cancel, string? outcome)
	{
		PackDownloadResult last = PackDownloadResult.Ok;
		lock (_gate)
		{
			if (!IsCurrent(code, cancel))
			{
				return (false, last);
			}

			_runs.Remove(code);
			if (outcome == UsageDetail.PackFailed)
			{
				_failed.Add(code);
				last = _lastResult.GetValueOrDefault(code, PackDownloadResult.Ok);
				if (PackResumePolicy.IsResumableResult(last))
				{
					_waitingInternet[code] = _progress.GetValueOrDefault(code);
				}
			}

			if (!PackResumePolicy.IsResumableResult(last))
			{
				_downloadNow.Remove(code); // "Download now" lasts until the download finishes or the grown-up cancels
			}

			_progress.Remove(code);
			return (true, last);
		}
	}

	// Offline (or a retry still to come) stays ticked and waiting; a failure nothing will retry unticks the language so the row shows the failure with Retry.
	// Decided and applied under the lock; a newer run (a restart or Retry in between) is left alone.
	private void SettleFailure(string code, PackDownloadResult last)
	{
		PackFailureAction action;
		lock (_gate)
		{
			if (_runs.ContainsKey(code))
			{
				return;
			}

			action = PackFailureDecision.Decide(last, NeedsRetry(code));
			if (action == PackFailureAction.UntickAndShowRetry)
			{
				Cancel(code); // forgets the coordinator's restart and the waiting state (the lock is re-entrant)
				_failed.Add(code);
			}
		}

		PackDownloadLog.LanguageFailure(code, last, action);
		if (action == PackFailureAction.UntickAndShowRetry)
		{
			UntickAfterFailure(code);
		}
	}

	/// <summary>Stops any run of <paramref name="language"/>, marks it failed and unticks it (saved at once); its row then shows the failure with Retry. For a download that could not even start.</summary>
	public void GiveUp(string language)
	{
		string code = UiLanguage.Resolve(language);
		Cancel(code);
		lock (_gate)
		{
			_failed.Add(code);
		}

		UntickAfterFailure(code);
	}

	private void UntickAfterFailure(string code)
	{
		MainThread.BeginInvokeOnMainThread(() => StoryLanguages.SaveSides(StoryLanguages.Current().WithoutSide(code)));
		RaiseChanged();
	}
	private static string ResumeKey(string code) => $"story-voice:{code}";

	// Slice 182: the run reports its end once; Offline or Failed starts again by itself when the network is back or the app returns (Wi-Fi/Ethernet unless "Download now").
	private void ReportEnd(string code, PackDownloadResult last, long epoch)
	{
		bool now;
		lock (_gate)
		{
			now = _downloadNow.Contains(code);
		}

		_resume.Report(ResumeKey(code), IPackResume.EndOf(last), () => Task.FromResult(ResumeStart(code, now)), downloadNow: now, epoch: epoch);
	}

	// The coordinator's restart: false when a run is already going (it reports its own end); a voice that arrived by another route is reported finished.
	private bool ResumeStart(string code, bool downloadNow)
	{
		if (TryStart(code, downloadNow))
		{
			return true;
		}

		if (StateOf(code) == LanguageVoiceState.Ready)
		{
			_resume.Report(ResumeKey(code), PackRunEnd.Finished, () => Task.FromResult(false));
		}

		return false;
	}

	private void NoteResult(string code, PackDownloadResult result)
	{
		lock (_gate)
		{
			_lastResult[code] = result;
		}
	}

	private bool IsCurrent(string code, CancellationTokenSource run) => _runs.TryGetValue(code, out CancellationTokenSource? current) && ReferenceEquals(current, run);

	private async Task<string?> DownloadAsync(string code, CancellationTokenSource run)
	{
		CancellationToken token = run.Token;
		PackCatalogResult catalog = await _packs.GetCatalogAsync(token);
		NoteResult(code, catalog.Status);
		if (catalog.Status != PackDownloadResult.Ok)
		{
			return UsageDetail.PackFailed;
		}

		PackInfo? pack = VoicePackSelector.Select(catalog.Packs, code);
		if (pack is null)
		{
			lock (_gate)
			{
				_noPack.Add(code); // no voice pack for this language: the phone's own voice reads it
				StoryLanguages.SaveNoPack(_noPack);
			}

			return UsageDetail.PackDone;
		}

		TrackPack(pack.Id, code); // before waiting for the gate: a Cancel on the notification reaches every language sharing the pack
		if (await FetchedByAnotherRunAsync(pack.Id, code, token))
		{
			return null;
		}

		var progress = new InlineProgress(value => Report(code, run, value));

		PackDownloadResult result = await _packs.DownloadAsync(pack.Id, progress, token);
		NoteResult(code, result);
		return result == PackDownloadResult.Ok ? UsageDetail.PackDone : UsageDetail.PackFailed;
	}

	// A download of the same pack in flight (the voice installer's) holds the gate: wait for it, and when the voice is there afterwards this run has nothing to fetch.
	private async Task<bool> FetchedByAnotherRunAsync(string packId, string code, CancellationToken token)
	{
		using IDisposable gate = await PackDownloadGate.EnterAsync(packId, token);
		return LanguageChoice.IsInstalled(_packs, code);
	}

	private void Report(string code, CancellationTokenSource run, double value)
	{
		lock (_gate)
		{
			if (!IsCurrent(code, run))
			{
				return;
			}

			double clamped = Math.Clamp(value, 0, 1);
			if ((int)(clamped * PercentScale) == (int)(_progress.GetValueOrDefault(code) * PercentScale))
			{
				return; // a tick inside the same percent changes nothing on screen
			}

			_progress[code] = clamped;
		}

		RaiseChanged();
	}

	private void RaiseChanged() => MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke(this, EventArgs.Empty));

	private sealed class InlineProgress(Action<double> report) : IProgress<double>
	{
		public void Report(double value) => report(value);
	}
}
