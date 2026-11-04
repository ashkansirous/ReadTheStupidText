using DinoReader.Core;
using Microsoft.Maui.Networking;
using ReadTheStupidText.Mobile.Shared.Network;

namespace DinoReader;

/// <summary>
/// The one owner of "get a side language's pre-made audio for every store book on the shelf" (Slice 262, Decisions in its slice file). A singleton, so a run started in Settings or at the end of the
/// intro goes on when the page is left. It keeps no queue: each run asks <see cref="ShelfAudioPlan"/> what the shelf still lacks (store listing, shelf ids, installed packs), then installs the
/// packs one at a time through <see cref="AudioPackStore"/> (the only writer of the pack folders; an atomic install, so a kill leaves nothing half-written and the next launch asks again).
/// The reading voice of the language arrives first (<see cref="StoryLanguagePacks"/>); a run waits for it. Offline it waits for the connection, on mobile data it waits for Wi-Fi under the voice's rule.
/// Cancel stops the rest, keeps the language and the packs already in, and is remembered so launch does not start it again; removing the language clears that.
/// </summary>
public sealed class ShelfAudioPacks
{
	private const string CancelledKey = "dino.shelfaudio.cancelled";
	private const int MaxTriesPerBook = 3;
	private static readonly TimeSpan VoicePoll = TimeSpan.FromSeconds(1);
	private static readonly TimeSpan WifiPoll = TimeSpan.FromSeconds(5);
	private const int MaxListTriesOnline = 3;
	private static readonly TimeSpan OfflineRetry = TimeSpan.FromSeconds(15);
	private static readonly TimeSpan ResumeEvery = TimeSpan.FromMinutes(5);

	private readonly StoreClient _store;
	private readonly StoryLanguagePacks _voices;
	private readonly object _gate = new(); // guards _runs, _states, every Run's Ended flag and token, and the cancelled-language preference
	private readonly Dictionary<string, Run> _runs = [];
	private readonly Dictionary<string, ShelfAudioState> _states = [];
	private DateTimeOffset _lastResume = DateTimeOffset.MinValue;

	public ShelfAudioPacks(StoreClient store, StoryLanguagePacks voices)
	{
		_store = store;
		_voices = voices;
		StoryLanguages.SidesSaved += StopRemoved; // a language that left the set stops its run and forgets its cancel
	}

	/// <summary>Raised on the main thread whenever a state changes (a run starts, a book is done, a wait begins or ends).</summary>
	public event EventHandler? Changed;

	/// <summary>Where <paramref name="language"/>'s shelf audio stands. Cancelled when the grown-up stopped it and it has not been started again.</summary>
	public ShelfAudioState StateOf(string language)
	{
		string code = UiLanguage.Resolve(language);
		lock (_gate)
		{
			if (_states.TryGetValue(code, out ShelfAudioState? state))
			{
				return state;
			}
		}

		return IsMarkedCancelled(code) ? new ShelfAudioState(ShelfAudioPhase.Cancelled) : ShelfAudioState.Idle;
	}

	/// <summary>
	/// Starts the fetch for a language the grown-up just added (or re-added): clears its cancel mark and shows the card at once. Does nothing when the language is not in the set (the main
	/// language counts, Slice 267 rule) or a run is going. <paramref name="allowMobileData"/> is decided once here, from the grown-up's answer to the voice request, and kept on the run.
	/// </summary>
	public void Start(string language, bool allowMobileData = false) => Begin(UiLanguage.Resolve(language), quiet: false, allowMobileData);

	/// <summary>
	/// At launch and when the app returns: carries on the languages that still lack packs the store has, without showing a card until there is something to fetch.
	/// A cancelled language is left alone. Checked at most every few minutes unless <paramref name="force"/>.
	/// </summary>
	public void Resume(bool force = false)
	{
		lock (_gate)
		{
			if (!force && DateTimeOffset.UtcNow - _lastResume < ResumeEvery)
			{
				return;
			}

			_lastResume = DateTimeOffset.UtcNow;
		}

		foreach (string language in StoryLanguages.Current().All.Where(code => !IsMarkedCancelled(code))) // the main language counts like the story languages (Slice 267 rule)
		{
			Begin(language, quiet: true, allowMobileData: false); // a quiet run follows the ordinary Wi-Fi rule
		}
	}

	/// <summary>Cancel on the card: the rest is not fetched, the language and the packs already in stay, and launch does not start it again.</summary>
	public void Cancel(string language)
	{
		string code = UiLanguage.Resolve(language);
		SetMarked(code, true);
		Stop(code, new ShelfAudioState(ShelfAudioPhase.Cancelled));
		AudioPackLog.Write($"shelf audio {code}: cancelled by the grown-up");
	}

	private void Begin(string code, bool quiet, bool allowMobileData)
	{
		if (!StoryLanguages.Current().All.Contains(code))
		{
			return;
		}

		if (!quiet)
		{
			SetMarked(code, false); // an added (or re-added) language starts again, even after a Cancel
		}

		Run run;
		lock (_gate)
		{
			if (_runs.ContainsKey(code))
			{
				return;
			}

			run = _runs[code] = new Run(code, quiet, allowMobileData).Initialized();
			if (!quiet)
			{
				run.Published = true;
				run.State = _states[code] = new ShelfAudioState(ShelfAudioPhase.Preparing); // the card (with Cancel) shows at once, before the voice and the listing are done
			}
		}

		if (!quiet)
		{
			RaiseChanged();
		}

		AudioPackLog.Write($"shelf audio {code}: run starts ({(quiet ? "launch" : "added")})");
		_ = Task.Run(() => RunAsync(run));
	}

	private void StopRemoved(StoryLanguageSet set)
	{
		string[] known;
		lock (_gate)
		{
			known = [.. _states.Keys.Concat(_runs.Keys).Distinct()];
		}

		foreach (string code in known.Where(code => !set.All.Contains(code)))
		{
			SetMarked(code, false);
			Stop(code, null);
			AudioPackLog.Write($"shelf audio {code}: language removed, run stopped, packs kept");
		}
	}

	// A null state forgets the language's entry altogether. The run (if any) is retired and the state replaced in one step, so no later update of that run can overwrite it.
	private void Stop(string code, ShelfAudioState? state)
	{
		lock (_gate)
		{
			if (_runs.TryGetValue(code, out Run? run))
			{
				Retire(run);
			}

			if (state is null)
			{
				_states.Remove(code);
			}
			else
			{
				_states[code] = state;
			}
		}

		RaiseChanged();
	}

	// Call under _gate. The run leaves _runs, is cancelled and its token is disposed, all in one step: a Stop after an End (or the reverse) finds no run and does nothing.
	private void Retire(Run run)
	{
		if (run.Ended)
		{
			return;
		}

		run.Ended = true;
		if (_runs.TryGetValue(run.Code, out Run? current) && current == run)
		{
			_runs.Remove(run.Code);
		}

		run.Cts.Cancel();
		run.Cts.Dispose();
	}

	private async Task RunAsync(Run run)
	{
		string code = run.Code;
		try
		{
			CancellationToken cancel = run.Token; // taken when the run was made: a Cancel may already have disposed the source
			await WaitForVoiceAsync(run, cancel);
			ShelfAudioWork? work = await PlanAsync(run, cancel);
			if (work is not null)
			{
				await FetchAllAsync(run, work, cancel);
			}
		}
		catch (OperationCanceledException)
		{
			AudioPackLog.Write($"shelf audio {code}: run ended by cancel");
		}
		catch (Exception ex)
		{
			AudioPackLog.Write($"shelf audio {code}: run failed, will try at the next open ({ex.GetType().Name}: {ex.Message})");
		}
		finally
		{
			End(run);
		}
	}

	// Only if the run was not already retired by a Cancel or a removal (they replaced the state themselves).
	private void End(Run run)
	{
		lock (_gate)
		{
			if (run.Ended)
			{
				return;
			}

			ShelfAudioState state = run.State;
			Retire(run);
			if (!run.Published)
			{
				return; // a quiet run that never showed anything leaves the earlier visible state (its "no audio" and "failed" lines) alone
			}

			_states[run.Code] = state.IsActive ? ShelfAudioState.Idle with { NoAudioTitles = state.NoAudioTitles, Failed = state.Failed } : state;
		}

		RaiseChanged();
	}

	// Publishes a state of the run: dropped once the run was cancelled or ended, so a late update never brings back a card the grown-up cancelled.
	private bool Publish(Run run, ShelfAudioState state)
	{
		lock (_gate)
		{
			if (run.Ended)
			{
				return false;
			}

			run.State = state;
			run.Published = true;
			_states[run.Code] = state;
		}

		RaiseChanged();
		return true;
	}

	// The reading voice shows first in the same card (as today): a voice still on its way holds the audio back. The run's network permission was fixed when it started and is not read from here.
	private async Task WaitForVoiceAsync(Run run, CancellationToken cancel)
	{
		string code = run.Code;
		bool announced = false;
		while (_voices.StateOf(code) == LanguageVoiceState.Downloading || _voices.IsWaitingForInternet(code) || _voices.IsWaitingForWifi(code))
		{
			if (!announced)
			{
				AudioPackLog.Write($"shelf audio {code}: waits for the reading voice first");
				announced = true;
			}

			await Task.Delay(VoicePoll, cancel);
		}
	}

	// A failed listing while offline waits for the connection (a visible run says so, a launch check stays quiet); while online it is tried a few times, then given up until the next open.
	private async Task<ShelfAudioWork?> PlanAsync(Run run, CancellationToken cancel)
	{
		string code = run.Code;
		int onlineFailures = 0;
		for (int attempt = 0; ; attempt++)
		{
			StoreListing listing = await _store.ListAsync(cancel);
			if (listing.Status == StoreLoadStatus.Ok)
			{
				ISet<string> shelf = await _store.InstalledIdsAsync();
				return ShelfAudioPlan.For(listing.Items, shelf, code, _store.Packs.InstalledVersion, LanguageChoice.Current());
			}

			bool online = IsOnline();
			if (online && ++onlineFailures >= MaxListTriesOnline)
			{
				AudioPackLog.Write($"shelf audio {code}: the store listing failed {onlineFailures} times while online, gives up until the next open");
				return null;
			}

			if (!online && !run.Quiet)
			{
				Publish(run, new ShelfAudioState(ShelfAudioPhase.WaitingForInternet));
			}

			AudioPackLog.Write($"shelf audio {code}: the store cannot be reached ({(online ? "online" : "offline")}), tries again");
			await Task.Delay(ShelfAudioBackoff.Delay(attempt), cancel);
			await WaitForOnlineAsync(cancel);
		}
	}

	private async Task FetchAllAsync(Run run, ShelfAudioWork work, CancellationToken cancel)
	{
		string code = run.Code;
		int total = work.Needed.Count;
		var noAudio = work.NoAudioTitles.ToList();
		if (total == 0)
		{
			AudioPackLog.Write($"shelf audio {code}: nothing to fetch ({noAudio.Count} book(s) without a pack)");
			if (!run.Quiet)
			{
				Publish(run, ShelfAudioState.Idle with { NoAudioTitles = [.. noAudio] });
			}

			return;
		}

		AudioPackLog.Write($"shelf audio {code}: {total} book(s) need a pack, {noAudio.Count} without one");
		var state = new ShelfAudioState(ShelfAudioPhase.Running, 0, total, NoAudioTitles: [.. noAudio]);
		foreach (ShelfAudioPick pick in work.Needed)
		{
			cancel.ThrowIfCancellationRequested();
			state = state with { Phase = ShelfAudioPhase.Running, CurrentTitle = pick.Item.TitleIn(LanguageChoice.Current()) ?? pick.Item.Title };
			Publish(run, state);
			ShelfAudioInstall result = await InstallOneAsync(run, pick, noAudio, cancel);
			state = result == ShelfAudioInstall.Failed ? state.WithBookFailed() : state.WithBookDone();
			state = state with { NoAudioTitles = [.. noAudio] };
			AudioPackLog.Write($"shelf audio {code}: {state.Done} of {state.Total} done, {state.Failed} failed");
			Publish(run, state);
		}

		Publish(run, ShelfAudioState.Idle with { NoAudioTitles = state.NoAudioTitles, Failed = state.Failed });
		AudioPackLog.Write($"shelf audio {code}: finished, {total} book(s) handled");
	}

	// A pack the store turned out not to have (404) goes to the quiet line. A book that keeps failing while online is Failed (not done) for this run, with a backoff between tries; the next open tries it again.
	private async Task<ShelfAudioInstall> InstallOneAsync(Run run, ShelfAudioPick pick, List<string> noAudio, CancellationToken cancel)
	{
		string code = run.Code;
		string id = pick.Item.Id;
		for (int tries = 0; tries < MaxTriesPerBook;)
		{
			await WaitForWifiIfNeededAsync(run, cancel);
			bool ok = await _store.InstallPacksAsync(pick.Item, [(code, pick.Pack)], null, cancel);
			cancel.ThrowIfCancellationRequested();
			int? have = _store.Packs.InstalledVersion(id, code);
			if (have is int version && version >= pick.Pack.Version)
			{
				AudioPackLog.Write($"shelf audio {code}: {id} pack in");
				return ShelfAudioInstall.Installed;
			}

			if (ok && have is null)
			{
				noAudio.Add(pick.Item.TitleIn(LanguageChoice.Current()) ?? pick.Item.Title);
				AudioPackLog.Write($"shelf audio {code}: {id} has no pack in the store, reads with the reading voice");
				return ShelfAudioInstall.NoPack;
			}

			if (!IsOnline())
			{
				await WaitOfflineAsync(run, pick, cancel);
				continue; // being offline is not a try: the same book goes on when the connection is back
			}

			AudioPackLog.Write($"shelf audio {code}: {id} failed while online (try {tries + 1} of {MaxTriesPerBook})");
			if (++tries < MaxTriesPerBook)
			{
				await Task.Delay(ShelfAudioBackoff.Delay(tries - 1), cancel);
			}
		}

		return ShelfAudioInstall.Failed;
	}

	private async Task WaitOfflineAsync(Run run, ShelfAudioPick pick, CancellationToken cancel)
	{
		ShelfAudioState now = run.State;
		Publish(run, now with { Phase = ShelfAudioPhase.WaitingForInternet, CurrentTitle = now.CurrentTitle ?? pick.Item.Title });
		AudioPackLog.Write($"shelf audio {run.Code}: offline at {pick.Item.Id}, waits for the internet");
		await WaitForOnlineAsync(cancel);
		Publish(run, run.State with { Phase = ShelfAudioPhase.Running });
		AudioPackLog.Write($"shelf audio {run.Code}: back online, carries on");
	}

	// The voice's own rule: on mobile data only, and the run was not allowed mobile data when it started ("Download now" in this add), the audio waits for Wi-Fi.
	private async Task WaitForWifiIfNeededAsync(Run run, CancellationToken cancel)
	{
		if (!NeedsWifi(run))
		{
			return;
		}

		Publish(run, run.State with { Phase = ShelfAudioPhase.WaitingForWifi });
		AudioPackLog.Write($"shelf audio {run.Code}: on mobile data, waits for Wi-Fi");
		while (NeedsWifi(run))
		{
			await Task.Delay(WifiPoll, cancel);
		}

		Publish(run, run.State with { Phase = ShelfAudioPhase.Running });
		AudioPackLog.Write($"shelf audio {run.Code}: Wi-Fi is back, carries on");
	}

	private static bool NeedsWifi(Run run) => ShelfAudioNetwork.NeedsWifi(run.AllowMobileData, PackResumeWatcher.CellularOnly());

	private static bool IsOnline()
	{
		try
		{
			return Connectivity.Current.NetworkAccess != NetworkAccess.None;
		}
		catch (Exception)
		{
			return true; // a platform that cannot say is treated as connected; a failed fetch is then counted as a try
		}
	}

	private static async Task WaitForOnlineAsync(CancellationToken cancel)
	{
		TaskCompletionSource? waiting = null; // a fresh one for every wait, so an old "back online" never makes the next wait return at once
		void OnChanged(object? sender, ConnectivityChangedEventArgs e)
		{
			if (e.NetworkAccess != NetworkAccess.None)
			{
				Volatile.Read(ref waiting)?.TrySetResult();
			}
		}

		Connectivity.Current.ConnectivityChanged += OnChanged;
		try
		{
			while (!IsOnline())
			{
				var back = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
				Volatile.Write(ref waiting, back);
				using var pause = CancellationTokenSource.CreateLinkedTokenSource(cancel);
				await Task.WhenAny(back.Task, Task.Delay(OfflineRetry, pause.Token));
				pause.Cancel();
				cancel.ThrowIfCancellationRequested();
			}
		}
		finally
		{
			Connectivity.Current.ConnectivityChanged -= OnChanged;
		}
	}

	private void RaiseChanged() => MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke(this, EventArgs.Empty));

	private static bool IsMarkedCancelled(string code) => Marked().Contains(code);

	private static IReadOnlyList<string> Marked() => StoryLanguageSet.Split(Preferences.Default.Get(CancelledKey, (string?)null));

	// A read-modify-write of one preference: under the lock, so a Cancel and a removal at the same time cannot lose each other's mark.
	private void SetMarked(string code, bool cancelled)
	{
		lock (_gate)
		{
			List<string> marked = [.. Marked().Where(c => c != code)];
			if (cancelled)
			{
				marked.Add(code);
			}

			Preferences.Default.Set(CancelledKey, StoryLanguageSet.Join(marked));
		}
	}

	private enum ShelfAudioInstall
	{
		Installed,
		NoPack,
		Failed,
	}

	// One language run: it owns its token and its latest state. It leaves _runs (and its token is disposed) only through Retire, under _gate.
	private sealed class Run(string code, bool quiet, bool allowMobileData)
	{
		public string Code { get; } = code;

		public bool Quiet { get; } = quiet;

		public CancellationTokenSource Cts { get; } = new();

		public CancellationToken Token { get; private set; }

		public ShelfAudioState State { get; set; } = ShelfAudioState.Idle;

		public bool Ended { get; set; }

		public bool Published { get; set; } // set under _gate once the run has shown a state of its own

		public bool AllowMobileData { get; } = allowMobileData; // decided once when the run starts ("Download now" in this add); never re-read from the voice's live state

		public Run Initialized()
		{
			Token = Cts.Token;
			return this;
		}
	}
}
