using System.Diagnostics;
using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Slice 459, the one owner of the first book's prefetch: once the story language is saved in the intro, the pre-ticked book (The Three Little Pigs) and its main-language pack start downloading while the
/// grown-up is still on the intro steps, so Continue on the picker has little or nothing left to wait for. It owns its own token and one task for one (book, main language); the picker never passes its token in
/// and joins by awaiting <see cref="WaitAsync"/>, never by calling <see cref="StoreClient.DownloadAsync"/> for the same id. The rules (what an untick, a language save and a Continue do) are the pure ones in
/// <see cref="FirstBooks"/>. Offline or failed it says nothing: <see cref="PrefetchState.Failed"/> and the normal first-book path (today's error and Retry) takes over.
/// </summary>
public sealed class FirstBookPrefetch(StoreClient store)
{
	private readonly object _gate = new();
	private CancellationTokenSource? _cancel;
	private Task? _task;
	private PrefetchState _state = PrefetchState.Idle;
	private string? _language;
	private double _progress;
	private IReadOnlyList<StoreItem>? _listing;
	private bool _partsCleared;
	private bool _installedByRun; // this run put the book on the shelf (it was not there before): only such a book is removed by an untick

	/// <summary>The book prefetched: the pre-ticked first book.</summary>
	public static string BookId => OnboardingDemo.ThreeLittlePigsId;

	public PrefetchState State { get { lock (_gate) { return _state; } } }

	/// <summary>The main language the current run fetches the pack for, null when idle.</summary>
	public string? Language { get { lock (_gate) { return _language; } } }

	/// <summary>The book's package and pack as one 0..1 fraction (the book's own total; <see cref="SummedProgress"/>).</summary>
	public double Progress { get { lock (_gate) { return _progress; } } }

	/// <summary>The store list the prefetch read (every listed book), or null before it read one: the picker uses it instead of a second fetch.</summary>
	public IReadOnlyList<StoreItem>? Listing { get { lock (_gate) { return _listing; } } }

	/// <summary>Raised on the prefetch's own progress; subscribe while a page shows it and unsubscribe when it leaves.</summary>
	public event Action<double>? ProgressChanged;

	/// <summary>The language step saved its pick: starts the prefetch for <paramref name="mainLanguage"/>, restarts it when the main language changed, and does nothing when the same run is live or landed.</summary>
	public void Start(string mainLanguage)
	{
		CancellationTokenSource? replaced;
		lock (_gate)
		{
			// Slice 459 (review pass 2 #6): the decision and the action are one critical section, so two Start calls never both begin a run and lose the first one's token
			PrefetchStart what = FirstBooks.OnLanguageSaved(_state, _language, mainLanguage);
			Log($"start {what} language={mainLanguage}");
			if (what == PrefetchStart.Keep)
			{
				return;
			}

			replaced = DetachRun().Cancel;
			BeginRun(mainLanguage);
		}

		CancelQuietly(replaced); // outside the lock: a cancellation callback may read this object's state
	}

	/// <summary>The grown-up unticked <paramref name="bookId"/> on the picker (Decision 1): the prefetch is cancelled, its <c>.part</c> goes with it, and a book that already landed is removed from the shelf.</summary>
	public async Task OnUntickAsync(string bookId)
	{
		PrefetchAction action;
		lock (_gate)
		{
			action = FirstBooks.OnUntick(BookId, _state, bookId, _installedByRun);
		}

		Log($"untick {action}");
		if (action == PrefetchAction.Keep)
		{
			return;
		}

		Task? ending = StopRun();
		if (ending is not null)
		{
			await ending; // Slice 459 (review pass 2 #7): the run may be inside its import; whether it landed is known only once it has ended (RunAsync never throws)
		}

		bool landed;
		lock (_gate)
		{
			landed = _installedByRun;
		}

		if (landed)
		{
			await store.RemoveFromShelfAsync(BookId);
			lock (_gate)
			{
				_installedByRun = false;
			}
		}
	}

	/// <summary>
	/// Slice 459 (check pass #7): Continue has handed the first book over, so it is the parent's now. The run's state and its "this run installed it" mark are cleared; a later untick of that book in a re-run picker
	/// never deletes it or its reading progress. A run still going is left alone.
	/// </summary>
	public void HandOver()
	{
		lock (_gate)
		{
			if (_task is { IsCompleted: false })
			{
				return;
			}

			_installedByRun = false;
			_state = PrefetchState.Idle;
			_language = null;
			_progress = 0;
			_cancel = null;
			_task = null;
		}
	}

	/// <summary>Page Cancel or Back during the first-book wait: stops the run (landed books stay).</summary>
	public void CancelRun() => _ = StopRun(); // the run unwinds on its own: its finally deletes the .part

	// Detaches the run under the lock, cancels it outside the lock, and returns its task (null when none ran).
	private Task? StopRun()
	{
		(CancellationTokenSource? cancel, Task? task) = DetachRunLocked();
		CancelQuietly(cancel);
		return task;
	}

	private (CancellationTokenSource? Cancel, Task? Task) DetachRunLocked()
	{
		lock (_gate)
		{
			return DetachRun();
		}
	}

	// The caller holds _gate. Forgets the current run and says it is cancelled; the caller cancels the returned source after it left the lock.
	private (CancellationTokenSource? Cancel, Task? Task) DetachRun()
	{
		(CancellationTokenSource? Cancel, Task? Task) run = (_cancel, _task);
		if (_state is not (PrefetchState.Idle or PrefetchState.Failed))
		{
			_state = PrefetchState.Cancelled; // a landed book that stays on the shelf is no longer joinable either: Continue takes the on-shelf path
		}

		_cancel = null;
		_task = null;
		_language = null;
		return run;
	}

	// A run's source is disposed when the run ends; a cancel that comes after that has nothing left to stop.
	private static void CancelQuietly(CancellationTokenSource? source)
	{
		try
		{
			source?.Cancel();
		}
		catch (ObjectDisposedException)
		{
			// no-fallback: the run already ended and disposed its source
		}
	}

	/// <summary>The run's end, for a page that joins it: returns the state it ended in (a cancel of this wait does not cancel the run).</summary>
	public async Task<PrefetchState> WaitAsync(CancellationToken cancel)
	{
		Task? task;
		lock (_gate)
		{
			task = _task;
		}

		if (task is not null)
		{
			await task.WaitAsync(cancel);
		}

		return State;
	}

	// The caller holds _gate (Start's one critical section).
	private void BeginRun(string language)
	{
		CancellationTokenSource cancel = new();
		IProgress<double> progress = new Progress<double>(Report);
		_cancel = cancel;
		_language = language;
		_progress = 0;
		_state = PrefetchState.Listing;
		_task = Task.Run(() => RunAsync(language, progress, cancel));
	}

	private void Report(double fraction)
	{
		lock (_gate)
		{
			_progress = Math.Max(_progress, Math.Clamp(fraction, 0, 1));
		}

		ProgressChanged?.Invoke(Progress);
	}

	private async Task RunAsync(string language, IProgress<double> progress, CancellationTokenSource owner)
	{
		try
		{
			(PrefetchState state, bool installed) = await FetchAsync(language, progress, owner.Token);
			NoteInstalled(installed); // also for a run that was replaced or cancelled: a book it put on the shelf is still this session's (an untick removes it)
			SetState(owner, state);
		}
		catch (OperationCanceledException)
		{
			// no-fallback: the run was cancelled (untick, language change or the page's Cancel); CancelRun already set the state
			Log("cancelled");
		}
		catch (Exception ex)
		{
			// no-fallback: a prefetch that fails is silent by design; Continue runs the normal first-book path with its own error and Retry
			Debug.WriteLine($"Dino Reader first-book prefetch failed: {ex.Message}");
			SetState(owner, PrefetchState.Failed);
		}
		finally
		{
			owner.Dispose(); // the run is over: nothing cancels it from now on (CancelQuietly covers a cancel that arrives late)
		}
	}

	private void NoteInstalled(bool installed)
	{
		if (installed)
		{
			lock (_gate)
			{
				_installedByRun = true; // stays true across a restart of the run: the book this session landed is still this session's
			}
		}
	}

	private async Task<(PrefetchState State, bool Installed)> FetchAsync(string language, IProgress<double> progress, CancellationToken cancel)
	{
		bool firstRun;
		lock (_gate)
		{
			firstRun = !_partsCleared;
			_partsCleared = true;
		}

		if (firstRun)
		{
			store.DeleteLeftoverParts(BookId); // after a kill: a leftover .part of this book goes before the fresh start
		}

		StoreListing listing = await store.ListAsync(cancel);
		if (listing.Status != StoreLoadStatus.Ok)
		{
			return (PrefetchState.Failed, false);
		}

		lock (_gate)
		{
			_listing = listing.Items;
		}

		StoreItem? item = FirstBooks.Order(BookAccess.FirstRunOffer(store.Sales.EnabledForOffer, listing.Items)).FirstOrDefault(i => string.Equals(i.Id, BookId, StringComparison.OrdinalIgnoreCase));
		if (item is null)
		{
			return (PrefetchState.Failed, false);
		}

		MarkDownloading(cancel);
		return await GetBookAsync(item, language, progress, cancel);
	}

	private void MarkDownloading(CancellationToken cancel)
	{
		lock (_gate)
		{
			if (!cancel.IsCancellationRequested && _state == PrefetchState.Listing)
			{
				_state = PrefetchState.Downloading;
			}
		}

		Log("downloading");
	}

	// A book already on the shelf (an earlier prefetch of another language) only needs its pack; otherwise the package plus the main pack come in one download.
	private async Task<(PrefetchState State, bool Installed)> GetBookAsync(StoreItem item, string language, IProgress<double> progress, CancellationToken cancel)
	{
		if ((await store.InstalledIdsAsync()).Contains(item.Id))
		{
			await store.EnsurePackAsync(item, language, cancel);
			return (PrefetchState.Landed, false); // it was on the shelf before this run: an untick keeps it
		}

		DownloadOutcome outcome = await store.DownloadAsync(item, progress, cancel, packLanguages: [language]);
		if (outcome == DownloadOutcome.Done)
		{
			await store.EnsurePackAsync(item, language, cancel); // Slice 459 (check pass #1): a joined run or an earlier language may not have fetched this pack; a failed pack keeps the book
			return (PrefetchState.Landed, true);
		}

		return outcome == DownloadOutcome.Cancelled ? (PrefetchState.Cancelled, false) : (PrefetchState.Failed, false);
	}

	// A run that was replaced or cancelled never writes its state over the newer run's.
	private void SetState(CancellationTokenSource owner, PrefetchState state)
	{
		lock (_gate)
		{
			if (!ReferenceEquals(_cancel, owner))
			{
				return;
			}

			_state = state;
			if (state == PrefetchState.Landed)
			{
				_progress = 1;
			}
		}

		Log($"ended {state}");
		ProgressChanged?.Invoke(Progress);
	}

	private static void Log(string what)
	{
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Prefetch] {what}");
#endif
	}
}

/// <summary>
/// Slice 459: the background queue of the first run, owned by <see cref="StoreClient"/> so it outlives the picker: the other ticked books, then every other language's pack of each book, one job at a time.
/// It is one serial worker over one job list (<see cref="BackgroundQueueModel"/>, "Concurrency, one design"): a failed book or pack never stops the rest; Cancel stops it and what landed stays. Landed books reach the shelf through <see cref="StoreClient.LibraryChanged"/>.
/// </summary>
public sealed class BackgroundBooks(StoreClient store)
{
	private const string JobsKey = "dino.firstbooks.background"; // the remaining and failed jobs (BackgroundJobStore), so a kill does not lose the books the grown-up ticked

	private readonly object _gate = new();
	private readonly BackgroundQueueModel _queue = new(); // every job, one list, guarded by _gate
	private CancellationTokenSource? _jobCancel; // the running job's token, null between jobs
	private bool _running; // the one worker is alive (set and cleared under _gate)
	private string _mainLanguage = string.Empty;
	private bool _resumed;

	/// <summary>Raised when a book's package landed from the queue (the app counts it as a store download, as the picker's own books were counted).</summary>
	public event Action<StoreItem>? BookLanded;

	/// <summary>Jobs that failed in the last queue and are waiting for Retry (shown on the shelf banner); 0 otherwise.</summary>
	public int Failed { get { lock (_gate) { return _queue.Failed; } } }

	/// <summary>Jobs not yet finished; 0 when the queue is idle.</summary>
	public int Remaining { get { lock (_gate) { return _queue.Remaining; } } }

	/// <summary>Books whose package has not landed yet (the shelf's "N more books" count).</summary>
	public int BooksRemaining { get { lock (_gate) { return _queue.BooksRemaining; } } }

	/// <summary>Raised after every job ends (and when the queue is cancelled), so the shelf can redraw its progress.</summary>
	public event Action? Changed;

	/// <summary>
	/// Slice 459 ("Concurrency, one design"): adds <paramref name="jobs"/> to the one queue and wakes the worker, starting it when none runs. It never drops a job, also while a queue runs or unwinds after a Cancel;
	/// a job already waiting or running is not added twice.
	/// </summary>
	public void Start(IReadOnlyList<BackgroundJob> jobs, string mainLanguage)
	{
		bool startWorker;
		lock (_gate)
		{
			if (!string.IsNullOrEmpty(mainLanguage))
			{
				_mainLanguage = mainLanguage;
			}

			int added = _queue.Add(jobs);
			Save();
			if (added == 0)
			{
				return;
			}

			startWorker = WakeWorker();
		}

		Log($"queue add jobs={jobs.Count} worker={startWorker}");
		RaiseChanged();
		if (startWorker)
		{
			_ = Task.Run(RunWorkerAsync);
		}
	}

	/// <summary>
	/// Stops the rest of the queue (landed books and packs stay) and forgets what was left, or, with no queue running, dismisses the failed jobs. The running job is cancelled and, whatever it returns, forgotten; jobs
	/// added after the Cancel go on once it has ended.
	/// </summary>
	public void Cancel()
	{
		CancellationTokenSource? running;
		lock (_gate)
		{
			_queue.Cancel();
			running = _jobCancel;
			Save();
		}

		CancelQuietly(running); // outside the lock: a cancellation callback may read the queue's state
		RaiseChanged();
	}

	/// <summary>The banner's Retry: the failed jobs go back into the queue (also while it runs).</summary>
	public void Retry()
	{
		bool startWorker;
		lock (_gate)
		{
			if (_queue.Retry() == 0)
			{
				return;
			}

			Save();
			startWorker = WakeWorker();
		}

		RaiseChanged();
		if (startWorker)
		{
			_ = Task.Run(RunWorkerAsync);
		}
	}

	// The caller holds _gate. True when no worker was alive: the caller starts one (the flag is set here, so two callers never start two).
	private bool WakeWorker()
	{
		if (_running)
		{
			return false;
		}

		_running = true;
		return true;
	}

	private static void CancelQuietly(CancellationTokenSource? source)
	{
		try
		{
			source?.Cancel();
		}
		catch (ObjectDisposedException)
		{
			// no-fallback: the job already ended and disposed its source
		}
	}

	/// <summary>
	/// Kill recovery (once per launch, from the shelf's first show): picks the saved jobs up again, with the main pack of the first book queued when it is on the shelf without it. Offline, it tries again at the next show.
	/// </summary>
	public async Task ResumeAsync()
	{
		lock (_gate)
		{
			if (_resumed)
			{
				return;
			}

			_resumed = true;
		}

		try
		{
			IReadOnlyList<BackgroundJob> saved = BackgroundJobStore.Parse(Preferences.Default.Get(JobsKey, string.Empty), out bool damaged);
			if (damaged && saved.Count == 0)
			{
				Preferences.Default.Remove(JobsKey); // a damaged value would fail the same way at every launch
			}

			if (saved.Count > 0 && !await ResumeSavedAsync(saved))
			{
				lock (_gate)
				{
					_resumed = false; // a listing that could not be read leaves the jobs saved and asks again at the next show
				}
			}
		}
		catch (Exception ex)
		{
			// no-fallback: a failed resume leaves the saved jobs where they are; the next launch tries again
			Debug.WriteLine($"Dino Reader could not resume the first-run queue: {ex.Message}");
		}
	}

	private async Task<bool> ResumeSavedAsync(IReadOnlyList<BackgroundJob> saved)
	{
		StoreListing listing = await store.ListAsync(CancellationToken.None);
		if (listing.Status != StoreLoadStatus.Ok)
		{
			return false;
		}

		string main = StoryLanguages.Current().Main;
		HashSet<string> onShelf = await store.InstalledIdsAsync();
		IReadOnlyList<string> missing = await store.BooksMissingPackAsync([FirstBookPrefetch.BookId], main);
		IReadOnlyList<BackgroundJob> jobs = BackgroundJobStore.ResumeJobs(saved, onShelf, missing, main);
		Log($"resume saved={saved.Count} jobs={jobs.Count}");
		if (jobs.Count == 0)
		{
			lock (_gate)
			{
				Save(); // everything saved was dropped (a deleted book, an exhausted retry budget): it leaves the saved value now
			}

			return true;
		}

		Start(jobs, main); // saves the queue, so the dropped jobs leave the saved value with it
		return true;
	}

	// Slice 459 (check pass #6): a throwing subscriber never reaches the worker, so it cannot stop the queue or turn a landed book into a failed job. Called outside the lock.
	private void RaiseChanged()
	{
		try
		{
			Changed?.Invoke();
		}
		catch (Exception ex)
		{
			// no-fallback: the subscriber (the shelf's redraw) failed; the queue goes on and the next change redraws it
			Debug.WriteLine($"Dino Reader first-run queue: a Changed handler threw: {ex.Message}");
		}
	}

	private void RaiseBookLanded(StoreItem item)
	{
		try
		{
			BookLanded?.Invoke(item);
		}
		catch (Exception ex)
		{
			// no-fallback: the subscriber (the usage count) failed; the book landed and stays Done
			Debug.WriteLine($"Dino Reader first-run queue: a BookLanded handler threw: {ex.Message}");
		}
	}

	// Callers hold _gate. What is left (failed first, the running one, then not yet run, the original order) or nothing.
	private void Save()
	{
		string text = BackgroundJobStore.Serialize(_queue.Snapshot());
		if (text.Length == 0)
		{
			Preferences.Default.Remove(JobsKey);
		}
		else
		{
			Preferences.Default.Set(JobsKey, text);
		}
	}

	// The one serial worker: takes the next job, runs it, settles it, and ends only when it finds none (that check and clearing _running are one critical section, so a job added meanwhile always finds a worker).
	private async Task RunWorkerAsync()
	{
		try
		{
			while (await RunNextJobAsync())
			{
			}
		}
		catch (Exception ex)
		{
			// no-fallback: every job's own failure is caught in RunJobAsync; this only guards a throwing callback, so the worker is never left marked as running
			Debug.WriteLine($"Dino Reader first-run queue worker stopped: {ex.Message}");
			lock (_gate)
			{
				_queue.Settle(BackgroundResult.Failed);
				_jobCancel = null;
				_running = false;
				Save();
			}
		}
	}

	private async Task<bool> RunNextJobAsync()
	{
		BackgroundJob? job;
		string main;
		CancellationTokenSource? cancel = null;
		lock (_gate)
		{
			job = _queue.TakeNext();
			main = _mainLanguage;
			if (job is null)
			{
				_running = false;
				Save();
			}
			else
			{
				cancel = new CancellationTokenSource();
				_jobCancel = cancel;
			}
		}

		if (job is null)
		{
			RaiseChanged();
			return false;
		}

		BackgroundResult result = await RunJobAsync(job, main, cancel!.Token);
		BackgroundResult counted;
		lock (_gate)
		{
			counted = _queue.Settle(result);
			_jobCancel = null;
			Save();
			cancel.Dispose();
		}

		Log($"job {job.Kind} {result} counted={counted}");
		RaiseChanged();
		return true;
	}

	private async Task<BackgroundResult> RunJobAsync(BackgroundJob job, string mainLanguage, CancellationToken cancel)
	{
		try
		{
			StoreItem? item = store.ListedItem(job.BookId);
			bool onShelf = (await store.InstalledIdsAsync()).Contains(job.BookId);
			bool packageQueued;
			lock (_gate)
			{
				packageQueued = _queue.PackageQueued(job.BookId);
			}

			switch (BackgroundJobRules.Verdict(job, item is not null, store.LiveListingRead, onShelf, packageQueued))
			{
				case JobVerdict.Drop:
					return BackgroundResult.Done; // gone from the store, deleted from the shelf, or already there: nothing left to fetch, never retried
				case JobVerdict.Fail:
					return BackgroundResult.Failed;
			}

			return job.Kind == BackgroundJobKind.Package ? await RunPackageAsync(item!, mainLanguage, cancel) : await RunPackAsync(item!, job.Language!, cancel);
		}
		catch (OperationCanceledException)
		{
			// no-fallback: the job was cancelled (the grown-up's Cancel, or a joined download another owner cancelled; the queue's settle rule tells them apart)
			return BackgroundResult.Cancelled;
		}
		catch (Exception ex)
		{
			// no-fallback: one job's failure never stops the queue; the store client and the pack store report their own fallbacks
			Debug.WriteLine($"Dino Reader first-run background job failed: {ex.Message}");
			return BackgroundResult.Failed;
		}
	}

	private async Task<BackgroundResult> RunPackageAsync(StoreItem item, string mainLanguage, CancellationToken cancel)
	{
		store.DeleteLeftoverParts(item.Id); // Slice 459 (review pass 2 #10): a kill mid-book leaves its .part; every resumed package job starts from zero (skipped while that book's download runs)
		DownloadOutcome outcome = await store.DownloadAsync(item, new Progress<double>(), cancel, packLanguages: [mainLanguage]);
		if (outcome != DownloadOutcome.Done)
		{
			return outcome == DownloadOutcome.Cancelled ? BackgroundResult.Cancelled : BackgroundResult.Failed;
		}

		RaiseBookLanded(item); // counted as a store download, as the picker counted each book it fetched
		PackInstall pack = await store.EnsurePackAsync(item, mainLanguage, cancel); // Slice 459 (check pass #1): a run this job joined may not have fetched the main pack; a failed pack keeps the book, as today
		return pack == PackInstall.Cancelled ? BackgroundResult.Cancelled : BackgroundResult.Done;
	}

	// The verdict already checked that the book is on the shelf.
	private async Task<BackgroundResult> RunPackAsync(StoreItem item, string language, CancellationToken cancel)
	{
		PackInstall result = await store.EnsurePackAsync(item, language, cancel);
		return result switch
		{
			PackInstall.Cancelled => BackgroundResult.Cancelled,
			PackInstall.Failed => BackgroundResult.Failed,
			_ => BackgroundResult.Done,
		};
	}

	private static void Log(string what)
	{
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[FirstBackground] {what}");
#endif
	}
}
