using DinoReader.Core;
using Microsoft.Maui.Networking;
using ReadTheStupidText.Application.Usage;
using ReadTheStupidText.Mobile.Shared.Network;

namespace DinoReader;

/// <summary>
/// The one owner of "get a side language's pre-made audio for every store book on the shelf" (Slice 262, Decisions in its slice file). A singleton, so a run started in Settings or at the end of the
/// intro goes on when the page is left. It keeps no queue: each run asks <see cref="ShelfAudioPlan"/> what the shelf still lacks (store listing, shelf ids, installed packs), then installs the
/// packs one at a time through <see cref="AudioPackStore"/> (the only writer of the pack folders; an atomic install, so a kill leaves nothing half-written and the next launch asks again).
/// Offline it waits for the connection, on mobile data it waits for Wi-Fi unless the grown-up chose "Download now" when adding the language (<see cref="ShelfAudioAsk"/>); a quiet launch run is never asked.
/// Cancel stops the rest, keeps the language and the packs already in, and is remembered so launch does not start it again; removing the language clears that.
/// </summary>
public sealed class ShelfAudioPacks
{
	private const string CancelledKey = "dino.shelfaudio.cancelled";
	private const int MaxTriesPerBook = 3;
	private static readonly TimeSpan WifiPoll = TimeSpan.FromSeconds(5);
	private const int MaxListTriesOnline = 3;
	private static readonly TimeSpan OfflineRetry = TimeSpan.FromSeconds(15);
	private static readonly TimeSpan ResumeEvery = TimeSpan.FromMinutes(5);

	private readonly StoreClient _store;
	private static readonly object _gate = new(); // static so WithoutCancelled can read under it (the class is a singleton); guards _runs, _unwinding, _states, every Run's mutable fields, and the cancelled-language preference
	private readonly Dictionary<string, Run> _runs = []; // at most one live run per language
	private readonly Dictionary<string, Task> _unwinding = []; // the Completion of the language's last retired run: a new run for it waits for that
	private readonly Dictionary<string, ShelfAudioState> _states = [];
	private DateTimeOffset _lastResume = DateTimeOffset.MinValue;

	private const string InstalledKey = "dino.shelfaudio.installed";
	private InstalledShelfAudio _installed; // under _gate: the last scan of which (book, language) packs are on the phone; the one source both pages read (Slice 278 part B, reviews/s278b-design.md)
	private readonly ScanRequests _scanRequests = new();
	private readonly object _scanLock = new();
	private TaskCompletionSource _scanDone = new();

	private readonly StagedBookUpdates _staged;
	private readonly IBookTextSync _textSync; // Slice 313: brings a behind book's text up to date before its pack is planned (injected, so no start-up path can forget it)

	public ShelfAudioPacks(StoreClient store, IBookTextSync textSync, StagedBookUpdates staged)
	{
		_store = store;
		_textSync = textSync;
		_staged = staged;
		staged.Swapped += (_, _) => TextChanged(); // a book's text was just swapped in: its pack is planned again, whichever path swapped it
		store.Packs.PacksChanged += () => _ = RescanAsync(); // a book download, a book update, a removal or the sweep changed the pack folders: the snapshot follows
		_installed = InstalledShelfAudio.Parse(Preferences.Default.Get(InstalledKey, (string?)null)); // the last-known list, read at once, so a cold start shows it before the scan corrects it
		StoryLanguages.SidesSaved += StopRemoved; // a language that left the set stops its run and forgets its cancel
	}

	/// <summary>
	/// Slice 313: a shelf book's newer text was just swapped in, so the pack made for it must be fetched now. A language with a live run is marked and planned again when that run ends (joining
	/// it would keep the old plan, which left this book out); any other language starts a quiet run at once, past the <see cref="ResumeEvery"/> throttle. A cancelled language stays cancelled.
	/// </summary>
	public void TextChanged()
	{
		StoryLanguageSet set = StoryLanguages.Current();
		IReadOnlyList<string> languages;
		lock (_gate)
		{
			// Only the main language and the ones with audio installed on the shelf (a language with none has no pack to make for this text), plus any with a live run.
			languages = set.All.Where(l => _runs.ContainsKey(l) || _installed.ForShelf(set.All, set.Main).Contains(l)).ToList();
		}

		foreach (string language in languages)
		{
			PlanAgain(language);
		}
	}

	private void PlanAgain(string code)
	{
		lock (_gate)
		{
			if (_runs.TryGetValue(code, out Run? live))
			{
				live.PlanAgain = true; // read by RunAsync after End, which retires the run under this same lock
				return;
			}
		}

		Begin(code, visible: false, VoiceRequestOutcome.NothingAsked, cancelled: false);
	}

	/// <summary>The languages the child's list offers on the shelf: the main one, and each with audio installed for at least one shelf book. Reads the snapshot only, never the disk.</summary>
	public IReadOnlyList<string> ShelfLanguages(StoryLanguageSet set)
	{
		lock (_gate)
		{
			return _installed.ForShelf(set.All, set.Main);
		}
	}

	/// <summary>Whether a book can be read in a language: the main one always, otherwise when that book's audio is installed. Reads the snapshot only, never the disk.</summary>
	public bool OffersBook(string language, string main, string? bookId)
	{
		lock (_gate)
		{
			return _installed.OffersBook(language, main, bookId);
		}
	}

	/// <summary>
	/// Scans the pack folders for the shelf books off the UI thread and replaces the snapshot (saving it); <see cref="Changed"/> fires on the main thread when it differs. Called at start, after
	/// every pack installed here, when the language set changes and when the shelf list changes. The newest scan wins.
	/// </summary>
	public Task RescanAsync()
	{
		TaskCompletionSource done;
		bool startLoop;
		lock (_scanLock)
		{
			startLoop = _scanRequests.Request();
			if (startLoop)
			{
				_scanDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			}

			done = _scanDone;
		}

		if (startLoop)
		{
			_ = Task.Run(() => ScanLoopAsync(done));
		}

		return done.Task; // completes when the scan that includes this request has finished
	}

	// Single-flight: one scan at a time; a request during a scan reruns once after it (ScanRequests).
	private async Task ScanLoopAsync(TaskCompletionSource done)
	{
		try
		{
			while (_scanRequests.TakeNext())
			{
				await ScanGuardedAsync();
			}
		}
		finally
		{
			done.TrySetResult();
		}
	}

	// Any throw in one scan (disk, Preferences, Serialize, a listener) must not leave the loop, or ScanRequests stays running and no scan ever starts again.
	private async Task ScanGuardedAsync()
	{
		try
		{
			await ScanOnceAsync();
		}
		catch (Exception ex)
		{
			// the last good snapshot stays; the next request scans again
			AudioPackLog.Write($"installed audio scan failed ({ex.GetType().Name})");
			_store.Fallbacks.Report(FallbackArea.Pack, FallbackReason.PackSyncFailed);
		}
	}

	private async Task ScanOnceAsync()
	{
		ISet<string> ids = await _store.InstalledIdsAsync();
		IReadOnlyList<string> languages = StoryLanguages.Current().All;
		AudioPackStore packs = _store.Packs;
		InstalledShelfAudio found = await Task.Run(() => ScanInstalled(packs, ids, languages));

		bool changed;
		lock (_gate)
		{
			changed = !_installed.SameAs(found);
			_installed = found;
			Preferences.Default.Set(InstalledKey, found.Serialize());
		}

		if (changed)
		{
			RaiseChanged();
		}
	}

	private static InstalledShelfAudio ScanInstalled(AudioPackStore packs, IEnumerable<string> bookIds, IReadOnlyList<string> languages) =>
		new(bookIds.SelectMany(id => languages.Where(language => packs.InstalledVersion(id, BundledTranslations.Key(language)) is not null).Select(language => (language, id))));

	/// <summary>Raised on the main thread whenever a state changes (a run starts, a book is done, a wait begins or ends).</summary>
	public event EventHandler? Changed;

	/// <summary>Whether the live run for <paramref name="language"/> still has to fetch this book's pack. Only then does a book's status line show the run's progress.</summary>
	public bool RunIncludes(string language, string? bookId)
	{
		string code = UiLanguage.Resolve(language);
		lock (_gate)
		{
			return bookId is not null && _runs.TryGetValue(code, out Run? run) && !run.Ended && run.Pending.Contains(bookId);
		}
	}

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
	/// language counts, Slice 267 rule) or the voice request was cancelled. A run already going for the language is never replaced: it becomes visible, and mobile data is allowed on it when
	/// <paramref name="outcome"/> (the grown-up's answer to the voice request) says so.
	/// </summary>
	public void Start(string language, VoiceRequestOutcome outcome = VoiceRequestOutcome.NothingAsked) =>
		Begin(UiLanguage.Resolve(language), visible: true, outcome, cancelled: false);

	/// <summary>
	/// At launch and when the app returns: carries on the languages that still lack packs the store has, without showing a card until there is something to fetch.
	/// A cancelled language is left alone. Checked at most every few minutes unless <paramref name="force"/>.
	/// </summary>
	public void Resume(bool force = false)
	{
		_ = RescanAsync(); // start and every return to the app: a pack changed meanwhile shows in the child's list
		lock (_gate)
		{
			if (!force && DateTimeOffset.UtcNow - _lastResume < ResumeEvery)
			{
				return;
			}

			_lastResume = DateTimeOffset.UtcNow;
		}

		IReadOnlyList<string> cancelled = Marked(); // read once for the whole pass
		foreach (string language in StoryLanguages.Current().All) // the main language counts like the story languages (Slice 267 rule)
		{
			Begin(language, visible: false, VoiceRequestOutcome.NothingAsked, cancelled.Contains(language)); // a quiet run follows the ordinary Wi-Fi rule
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

	// The one entry for every start. One run per language: a language with a live run joins it (Join), it never starts a second or drops the first.
	private void Begin(string code, bool visible, VoiceRequestOutcome outcome, bool cancelled)
	{
		if (visible)
		{
			SetMarked(code, false); // an added (or re-added) language starts again, even after a Cancel
		}

		Run? run = null;
		bool joined;
		lock (_gate)
		{
			// The start decision and the registering of the run are one step: a Cancel or a removal landing meanwhile either comes before it (and is seen here) or retires the run made here.
			bool markedCancelled = !visible && (cancelled || Marked().Contains(code));
			if (!StoryLanguages.Current().All.Contains(code) || ShelfAudioStart.Decide(markedCancelled, visible, outcome) == ShelfAudioStartDecision.Skip)
			{
				return;
			}

			joined = _runs.TryGetValue(code, out Run? live);
			if (joined)
			{
				Join(live!, visible, outcome);
			}
			else
			{
				run = _runs[code] = new Run(code, visible, ShelfAudioNetwork.AllowsMobileData(outcome), _unwinding.GetValueOrDefault(code) ?? Task.CompletedTask);
			}
		}

		if (run is null)
		{
			PublishVisible(code);
			return;
		}

		AudioPackLog.Write($"shelf audio {code}: run starts ({(visible ? "added" : "launch")})");
		_ = Task.Run(() => RunAsync(run));
		PublishVisible(code);
	}

	// Call under _gate. A quiet run turns visible, never the reverse, and mobile data, once allowed, stays allowed.
	private static void Join(Run run, bool visible, VoiceRequestOutcome outcome)
	{
		run.Visible |= visible;
		run.AllowMobileData |= ShelfAudioNetwork.AllowsMobileData(outcome);
	}

	// A visible run shows its current state at once (Preparing before anything else), so the card with Cancel is there before the voice and the listing are done.
	private void PublishVisible(string code)
	{
		lock (_gate)
		{
			if (!_runs.TryGetValue(code, out Run? run) || !run.Visible || run.Ended)
			{
				return;
			}

			run.Published = true;
			_states[code] = run.State; // read and written in one step: a state the run published meanwhile is never overwritten by a stale one
		}

		RaiseChanged();
	}

	private void StopRemoved(StoryLanguageSet set)
	{
		_ = RescanAsync(); // the language set changed: the child's list follows it
		string[] known;
		lock (_gate)
		{
			known = [.. _states.Keys.Concat(_runs.Keys).Concat(Marked()).Distinct()]; // a persisted Cancel mark counts too: it can outlive any run or state (a restart)
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
		Run? retired = null;
		lock (_gate)
		{
			if (_runs.TryGetValue(code, out Run? run))
			{
				retired = Retire(run) ? run : null;
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

		retired?.Cts.Cancel(); // outside the lock: continuations of the awaited wait may run inline on this thread and take _gate
		RaiseChanged();
	}

	// Call under _gate. The run leaves _runs and is marked ended in one step (a Stop after an End, or the reverse, finds no run and does nothing); returns true for the one caller that retired it, who
	// then cancels its token after leaving the lock. The token source is never disposed (it has no timer, so nothing leaks), and a new run for the language waits for that Completion (_unwinding) before it touches the pack folders.
	private bool Retire(Run run)
	{
		if (run.Ended)
		{
			return false;
		}
		run.Ended = true;
		if (_runs.TryGetValue(run.Code, out Run? current) && current == run)
		{
			_runs.Remove(run.Code);
		}

		_unwinding[run.Code] = run.Completion.Task;
		return true;
	}

	private async Task RunAsync(Run run)
	{
		string code = run.Code;
		try
		{
			CancellationToken cancel = run.Token;
			await run.Previous.WaitAsync(cancel); // a re-add waits for the run it replaced to finish unwinding
			ShelfAudioWork? work = await PlanAsync(run, cancel);
			if (work is not null)
			{
				await FetchAllAsync(run, work, cancel);
			}
		}
		catch (OperationCanceledException)
		{
			// no-fallback: cancelled by the app or the language change; the next launch or return to the app plans the run again
			AudioPackLog.Write($"shelf audio {code}: run ended by cancel");
		}
		catch (Exception ex)
		{
			AudioPackLog.Write($"shelf audio {code}: run failed, will try at the next launch or return to the app ({ex.GetType().Name})");
			_store.Fallbacks.Report(FallbackArea.Pack, FallbackReason.PackSyncFailed);
		}
		finally
		{
			End(run);
			// The run's Cts is never disposed: it has no timer or wait handle to release, so the GC frees it, and a Cancel that
			// arrives after the run ended then stays harmless (no ObjectDisposedException).
			run.Completion.TrySetResult();
		}

		StartAgainIfAsked(run);
	}

	// Slice 313: a text swapped in while this run was live asked for a new plan. Read after End, so a request made after the run left _runs started its own run instead; one request, one extra run.
	private void StartAgainIfAsked(Run run)
	{
		bool again;
		lock (_gate)
		{
			again = run.PlanAgain;
		}

		if (again)
		{
			Begin(run.Code, visible: false, VoiceRequestOutcome.NothingAsked, cancelled: false);
		}
	}

	// Only if the run was not already retired by a Cancel or a removal (they replaced the state themselves).
	private void End(Run run)
	{
		bool published;
		lock (_gate)
		{
			if (!Retire(run))
			{
				return;
			}

			ShelfAudioState state = run.State;
			published = run.Published;
			if (published)
			{
				_states[run.Code] = state.IsActive ? ShelfAudioState.Idle with { NoAudioTitles = state.NoAudioTitles, Failed = state.Failed } : state;
			}
		}

		run.Cts.Cancel(); // outside the lock; safe on a finished run (the source is never disposed)
		if (published) // a quiet run that never showed anything leaves the earlier visible state (its "no audio" and "failed" lines) alone
		{
			RaiseChanged();
		}
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

	// A failed listing while offline waits for the connection (a visible run says so, a launch check stays quiet); while online it is tried a few times, then given up until the next launch or return to the app.
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
				IReadOnlyList<StoreItem> fetchable = _store.Fetchable(listing.Items); // Slice 346: a sold book the store account does not own gets no audio request (it would be refused); the shelf copy is never touched
				ISet<string> textBehind = await TextBehindAsync(run, fetchable, cancel);
				return ShelfAudioPlan.For(fetchable, shelf, code, _store.Packs.InstalledVersion, LanguageChoice.Current(), textBehind);
			}

			bool online = IsOnline();
			if (online && ++onlineFailures >= MaxListTriesOnline)
			{
				AudioPackLog.Write($"shelf audio {code}: the store listing failed {onlineFailures} times while online, gives up until the next launch or return to the app");
				return null;
			}

			if (!online && IsVisible(run))
			{
				Publish(run, new ShelfAudioState(ShelfAudioPhase.WaitingForInternet));
			}

			AudioPackLog.Write($"shelf audio {code}: the store cannot be reached ({(online ? "online" : "offline")}), tries again");
			await Task.Delay(ShelfAudioBackoff.Delay(attempt), cancel);
			await WaitForOnlineAsync(cancel);
		}
	}

	// Slice 313: the text comes first. A text sync that fails for any reason other than the cancel leaves the plan as it was before (no book held back): audio is optional and never blocks the run.
	// The text's downloads follow the same network rule as the pack downloads: on mobile data the run waits for Wi-Fi first, unless the grown-up allowed mobile data for it.
	private async Task<ISet<string>> TextBehindAsync(Run run, IReadOnlyList<StoreItem> items, CancellationToken cancel)
	{
		try
		{
			ISet<string> behind = await _textSync.SyncTextAsync(items, token => WaitForWifiIfNeededAsync(run, token), cancel);
			if (behind.Count > 0)
			{
				AudioPackLog.Write($"shelf audio: {behind.Count} book(s) have older text than the store, their packs wait");
			}

			return behind;
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !cancel.IsCancellationRequested)
		{
			AudioPackLog.Write($"shelf audio: the book text check failed, packs planned as before: {ex.Message}");
			_store.Fallbacks.Report(ReadTheStupidText.Application.Usage.FallbackArea.Store, ReadTheStupidText.Application.Usage.FallbackReason.UpdateCheckFailed);
			return new HashSet<string>();
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
			Publish(run, ShelfAudioState.Idle with { NoAudioTitles = [.. noAudio] }); // this run's own result, quiet or not: nothing of an earlier run's lines stays
			return;
		}

		AudioPackLog.Write($"shelf audio {code}: {total} book(s) need a pack, {noAudio.Count} without one");
		lock (_gate)
		{
			run.Pending.UnionWith(work.Needed.Select(pick => pick.Item.Id));
		}

		var state = new ShelfAudioState(ShelfAudioPhase.Running, 0, total, NoAudioTitles: [.. noAudio]);
		foreach (ShelfAudioPick pick in work.Needed)
		{
			cancel.ThrowIfCancellationRequested();
			state = state with { Phase = ShelfAudioPhase.Running, CurrentTitle = pick.Item.TitleIn(LanguageChoice.Current()) ?? pick.Item.Title };
			Publish(run, state);
			ShelfAudioInstall result = await InstallOneAsync(run, pick, noAudio, cancel);
			lock (_gate)
			{
				run.Pending.Remove(pick.Item.Id); // handled (in, no pack, or given up): the book no longer waits on this run
			}

			state = result == ShelfAudioInstall.Failed ? state.WithBookFailed() : state.WithBookDone();
			state = state with { NoAudioTitles = [.. noAudio] };
			AudioPackLog.Write($"shelf audio {code}: {state.Done} of {state.Total} done, {state.Failed} failed");
			Publish(run, state);
		}

		Publish(run, ShelfAudioState.Idle with { NoAudioTitles = state.NoAudioTitles, Failed = state.Failed });
		AudioPackLog.Write($"shelf audio {code}: finished, {total} book(s) handled");
	}

	// A pack the store turned out not to have (404) goes to the quiet line. A book that keeps failing while online is Failed (not done) for this run, with a backoff between tries; the next launch or return to the app tries it again.
	private async Task<ShelfAudioInstall> InstallOneAsync(Run run, ShelfAudioPick pick, List<string> noAudio, CancellationToken cancel)
	{
		string code = run.Code;
		string id = pick.Item.Id;
		for (int tries = 0; tries < MaxTriesPerBook;)
		{
			await WaitForWifiIfNeededAsync(run, cancel);
			PackInstall result = await _store.InstallPackAsync(pick.Item, code, pick.Pack, cancel);
			cancel.ThrowIfCancellationRequested();
			if (result is PackInstall.Installed or PackInstall.AlreadyInstalled)
			{
				AudioPackLog.Write($"shelf audio {code}: {id} pack in");
				await RescanAsync(); // the pages read the snapshot, so it is refreshed before the run publishes this book as done
				return ShelfAudioInstall.Installed;
			}

			if (result == PackInstall.NotFound)
			{
				noAudio.Add(pick.Item.TitleIn(LanguageChoice.Current()) ?? pick.Item.Title);
				AudioPackLog.Write($"shelf audio {code}: {id} has no pack in the store yet");
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

	// On mobile data only, and the run was not allowed mobile data when it started, the audio waits for Wi-Fi.
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

	// The permission can widen while the run goes (a quiet run the grown-up then added with "Download now"), so it is read under the gate each time.
	private bool NeedsWifi(Run run)
	{
		bool allowed;
		lock (_gate)
		{
			allowed = run.AllowMobileData;
		}

		return ShelfAudioNetwork.NeedsWifi(allowed, PackResumeWatcher.CellularOnly());
	}

	private bool IsVisible(Run run)
	{
		lock (_gate)
		{
			return run.Visible;
		}
	}

	private static bool IsOnline()
	{
		try
		{
			return Connectivity.Current.NetworkAccess != NetworkAccess.None;
		}
		catch (Exception)
		{
			// no-fallback: a platform that cannot say is treated as connected; a failed fetch reports itself
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

	/// <summary>
	/// <paramref name="languages"/> with the cancelled ones left out (Slice 269: the weekly book check must not fetch packs for them). Read under the same lock the Cancel writes under, so a Cancel
	/// pressed during the pass is seen by the next read.
	/// </summary>
	public static IReadOnlyList<string> WithoutCancelled(IEnumerable<string> languages)
	{
		lock (_gate)
		{
			return CancelledAudioLanguages.Without(languages, Preferences.Default.Get(CancelledKey, (string?)null));
		}
	}

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

	// One language run: it owns its token and its own state, built from this run only (it starts Preparing). It leaves _runs only through Retire, under _gate; its token source is never disposed.
	private sealed class Run
	{
		public Run(string code, bool visible, bool allowMobileData, Task previous)
		{
			Code = code;
			Visible = visible;
			AllowMobileData = allowMobileData;
			Previous = previous;
			Token = Cts.Token;
		}

		public string Code { get; }

		public Task Previous { get; } // the Completion of the run this one replaced

		public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public CancellationTokenSource Cts { get; } = new();

		public CancellationToken Token { get; } // set in the constructor

		public ShelfAudioState State { get; set; } = new(ShelfAudioPhase.Preparing);

		public bool Visible { get; set; } // under _gate: a quiet run becomes visible, never the reverse

		public bool Ended { get; set; }

		public bool PlanAgain { get; set; } // under _gate: a book's text was swapped in while the run was live, so the language is planned again when it ends

		public bool Published { get; set; } // set under _gate once the run has shown a state of its own

		public HashSet<string> Pending { get; } = []; // under _gate: ids of the books this run still has to fetch (set when it has planned; a book leaves it once handled)

		public bool AllowMobileData { get; set; } // under _gate: decided by ShelfAudioNetwork when the run starts, widened only by a later Start with "Download now"
	}
}
