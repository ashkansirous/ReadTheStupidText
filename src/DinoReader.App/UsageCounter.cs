using DinoReader.Core;
using FallbackArea = ReadTheStupidText.Application.Usage.FallbackArea;
using FallbackReason = ReadTheStupidText.Application.Usage.FallbackReason;
using FallbackTally = ReadTheStupidText.Application.Usage.FallbackTally;
using FallbackTotal = ReadTheStupidText.Application.Usage.FallbackTotal;
using UsageBuckets = ReadTheStupidText.Application.Usage.UsageBuckets;
using UsageWaitStage = ReadTheStupidText.Application.Usage.UsageWaitStage;
using ReadTheStupidText.Mobile.Shared.Waiting;
using ReadTheStupidText.Mobile.Shared.Usage;

namespace DinoReader;

/// <summary>
/// Dino's anonymous usage counts (Slice 133, Decisions 140-142). A count is a name plus context (<see cref="UsageEvent"/>): no id of the
/// install or the device, no typed text, no own-book title. Counts are queued in preferences and sent in batches of at most
/// <see cref="UsageBatch.MaxEvents"/> to <c>POST /api/events</c> when a reading session ends or the app next starts. Nothing is queued
/// or sent when the grown-up's switch is off, when the remote config has telemetry off (<see cref="AppConfigClient"/>), or when this launch
/// was not picked by the config's <c>sampleRate</c>; the Worker drops everything again when telemetry is off. The queue, batch and send
/// logic is the shared <see cref="UsageQueueSender{TEvent}"/> (Slice 137, also used by Read The Stupid Text); this class adds Dino's counts.
/// </summary>
public sealed class UsageCounter
{
	/// <summary>The grown-up's switch (settings, behind the gate). On by default (Decision 141).</summary>
	public const string EnabledKey = "dino.usage.enabled";

	private const string QueueKey = "dino.usage.queue";
	private const string LogName = "Dino Reader";

	private readonly UsageQueueSender<UsageEvent> _sender;
	private readonly FallbackTally _fallbacks = new(); // Slice 273: in memory, so a new launch starts at zero
	private readonly Dictionary<(string Book, string Language), (Story Story, string? Language)> _fallbackStories = []; // the book and language behind each tally entry, for the flush's repeat rows
	private readonly UsageSession _session = new(); // Slice 250: one per process start, in memory only; a cold start is a new session
	private readonly UsageLaunch _launch = new(new PreferencesLaunchStore(), DateOnly.FromDateTime(DateTime.UtcNow), () => Environment.TickCount64); // Slice 351: counts this launch; buckets only, the stored day and count are never sent
	private const int ListenTimerSlackMilliseconds = 50;
	private readonly object _listen = new(); // Slice 288: guards the tally, the story it is for and its timer
	private readonly ListenTally _tally = new(() => Environment.TickCount64);
	private readonly System.Threading.Timer _listenTimer; // fires when the current reading's played time reaches 30 s
	private (Story Story, string? Language)? _listenStory;

	// Slice 288: a book_listened decided under _listen, queued after the lock is released (so no lock is held across the send queue).
	private readonly record struct PendingListen(Story Story, string? Language);

	/// <summary>The id of this launch's session (Slice 250), for crash and problem reports; kept in memory, and on disk only inside counts still waiting in the send queue.</summary>
	public string SessionId => _session.Id;

	private readonly ConfigFallbackHold _configHold; // Slice 273 part C: the default config has telemetry off, so config and first-launch ui_language fallbacks wait for the first real one

	public UsageCounter(AppConfigClient config)
	{
		_listenTimer = new System.Threading.Timer(_ => OnListenTimer(), null, Timeout.Infinite, Timeout.Infinite);
		_configHold = new ConfigFallbackHold(() => config.HasCopy, (area, reason, language) => RecordGeneralFallback(area, reason, language));
		config.Fetched += (_, _) => _configHold.Release(); // every successful fetch, not only a changed config
		_sender = new UsageQueueSender<UsageEvent>(new UsageQueueOptions<UsageEvent>(
			StoreClient.BaseAddress, EnabledKey, QueueKey, LogName, UsageBatch.FlushAt, // Slice 352: batches of 40 (never past the Worker's 50), and a full batch is sent at once
			() => (config.Current.Telemetry.Enabled, config.Current.Telemetry.SampleRate),
			() => config.HasCopy,
			events => UsageBatch.ToJson(events, DeviceProbe.Current), UsageBatch.ParseQueue, UsageBatch.QueueToJson,
			SendsBeta: true, // Slice 251: TestFlight batches go out posted to the Worker's separate beta route (dino_usage_beta)
			Roll: _session.Roll, // Slice 250: sampling is decided once per session
			IsNoise: IsUiRow)); // Slice 352: a full queue drops taps, screens and setting changes before any other row
		if (_launch.StoreFailed)
		{
			RecordGeneralFallback(ReadTheStupidText.Application.Usage.FallbackArea.Config, ReadTheStupidText.Application.Usage.FallbackReason.PreferenceUnsaved); // Slice 351: the launch history could not be read or saved; the counts carry "none" buckets
		}

		WaitController.Finished -= RecordWait; // guarded: a second counter must not stay subscribed twice
		WaitController.Finished += RecordWait;
	}

	private static bool IsUiRow(UsageEvent row) => row.Name is UsageEventName.Tap or UsageEventName.Screen or UsageEventName.SettingChanged;

	// Slice 148: every wait overlay that showed, and every Cancel on one, with its stage and a coarse duration. About no book.
	private void RecordWait(WaitFinish finish)
	{
		string detail = UsageDetail.Wait(UsageWaitStage.Of(finish.Stage), UsageBuckets.Duration(finish.Shown));
		RecordGeneral(UsageEventName.WaitShown, UsageEvent.NoLanguage, detail);
		if (finish.Cancelled)
		{
			RecordGeneral(UsageEventName.WaitCancelled, UsageEvent.NoLanguage, detail);
		}
	}

	/// <summary>Whether counts may be sent at all; turning it off also forgets what was waiting.</summary>
	public bool Enabled
	{
		get => _sender.Enabled;
		set => _sender.Enabled = value;
	}

	/// <summary>
	/// Queues the count <paramref name="name"/> (a <see cref="UsageEventName"/>) about <paramref name="story"/>, unless counting is off for this launch.
	/// <paramref name="language"/> is the language it was read in (Slice 173); null counts the book's own language.
	/// </summary>
	public void Record(string name, Story story, string detail = "", string? language = null, int? count = null, int? deviceCount = null)
	{
		(PendingListen? listen, int? seconds) = TallyPlayback(name, story, detail, language);
		if (listen is { } reached)
		{
			RecordListen(reached); // before the pause or stop row it belongs ahead of, on this same thread
		}

		RecordLogged(name, story, detail, language, count, deviceCount, seconds);
	}

	// The one path every count takes to the queue.
	private void RecordLogged(string name, Story story, string detail, string? language, int? count, int? deviceCount, int? seconds)
	{
#if DEBUG
		// Slice 250: a Debug-only line per count asked for (before the sampling and switch checks), so a device flow can read the order of a session's counts and its one session id.
		// Slice 273: with the counts a read_voice or fallback carries. Slice 288: with the played seconds a read_paused or read_stopped carries (book_listened carries none).
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Usage] {name}{(detail.Length == 0 ? "" : $" {detail}")} session={_session.Id}{(count is null ? "" : $" count={count}")}{(deviceCount is null ? "" : $" device={deviceCount}")}{(seconds is null ? "" : $" seconds={seconds}")}");
#endif
		RecordCore(name, story, detail, language, count, deviceCount, seconds);
	}

	private void RecordListen(PendingListen listen) =>
		RecordLogged(UsageEventName.BookListened, listen.Story, string.Empty, listen.Language, null, null, null);

	/// <summary>
	/// Slice 288: the reading session says whether audio is really being heard (a clip or recording playing, or a silent piece shown): false while a piece loads, a menu holds the read, a person
	/// paused, or a silent piece is covered. This is the only thing that runs or stops the listen tally; the playback counts only read it. Safe to call from any thread.
	/// </summary>
	public void SetAudible(bool audible)
	{
		PendingListen? listen;
		lock (_listen)
		{
			_tally.SetAudible(audible); // the tally remembers it, so the next reading starts in step; a usage verb never sets it
			if (audible)
			{
				ScheduleListen();
				return;
			}

			_listenTimer.Change(Timeout.Infinite, Timeout.Infinite);
			listen = TakeListenIfReached(); // a listen that crossed 30 s just before the pause still counts, once
		}

		if (listen is { } reached)
		{
			RecordListen(reached);
		}
	}

	/// <summary>
	/// Slice 288: the page was left. A reading still open here (its read_stopped never came, e.g. after a language switch with no restart) gets its read_stopped (left_book) carrying the seconds heard,
	/// preceded by its listen if due; then audio is reported as not heard. Does nothing when no reading is open. Safe to call from any thread.
	/// </summary>
	public void EndReading()
	{
		PendingListen? listen = null;
		int? seconds = null;
		(Story Story, string? Language)? book;
		lock (_listen)
		{
			book = _listenStory;
			if (_tally.Active)
			{
				(listen, seconds) = ReportHeardSeconds(ended: true);
			}
		}

		if (listen is { } reached)
		{
			RecordListen(reached);
		}

		if (book is { } open && seconds is not null)
		{
			RecordLogged(UsageEventName.ReadStopped, open.Story, ReadPlaybackNames.LeftBook, open.Language, null, null, seconds);
		}

		SetAudible(false);
	}

	// Slice 288: a start opens a reading's tally (it never resets one for the same story; a different story's start ends the old one first); a pause or stop gives back the whole seconds heard since the last of them, 0 when none (the row always carries them).
	// Any listen that just reached 30 s is decided here too, and queued by the caller after the lock is released, ahead of the row.
	private (PendingListen? Listen, int? Seconds) TallyPlayback(string name, Story story, string detail, string? language)
	{
		lock (_listen)
		{
			switch (name)
			{
				case UsageEventName.ReadStarted:
					StartListening(story, language);
					return (null, null);
				case UsageEventName.ReadPaused:
					return ReportHeardSeconds(ended: false);
				case UsageEventName.ReadStopped:
					return ReportHeardSeconds(ended: detail != ReadPlaybackNames.LanguageChanged); // a language switch keeps the sitting: its start follows for the same story
				default:
					return (null, null);
			}
		}
	}

	private void StartListening(Story story, string? language)
	{
		// A different story than the open one ended that reading first (its unsent seconds are dropped). A second start of the open story (a language switch) keeps the tally and takes the current language.
		_tally.Start(story.Id);
		_listenStory = (story, language);

		ScheduleListen();
	}

	// A pause or stop: the listen that just reached 30 s comes first (the caller queues it before this row), then the seconds are read. A stop closes the reading; whether audio is heard is the session's word alone.
	private (PendingListen? Listen, int? Seconds) ReportHeardSeconds(bool ended)
	{
		PendingListen? listen = TakeListenIfReached();
		int seconds = _tally.TakeSeconds() ?? 0;
		if (ended)
		{
			_tally.Stop();
			_listenTimer.Change(Timeout.Infinite, Timeout.Infinite);
		}

		return (listen, seconds);
	}

	// Arms the timer for the moment played time reaches 30 s, or leaves it off when the reading is not playing or already counted.
	private void ScheduleListen()
	{
		long? left = _tally.MillisecondsUntilListen();
		_listenTimer.Change(left is { } due ? due + ListenTimerSlackMilliseconds : Timeout.Infinite, Timeout.Infinite);
	}

	// The timer fired: if the reading really has 30 s of played time, the one book_listened is queued for it (an app killed later still counts the listen).
	private void OnListenTimer()
	{
		PendingListen? listen;
		lock (_listen)
		{
			listen = TakeListenIfReached();
			if (listen is null)
			{
				ScheduleListen(); // woke a hair early: wait out the rest
			}
		}

		if (listen is { } reached)
		{
			RecordListen(reached);
		}
	}

	// Called with _listen held: decides whether the one book_listened is due (it carries no seconds: they are on the pause and stop rows only). The caller queues it after the lock is released.
	private PendingListen? TakeListenIfReached()
	{
		if (!_tally.TryMarkListened() || _listenStory is not { } book)
		{
			return null;
		}

		return new PendingListen(book.Story, book.Language);
	}

	/// <summary>
	/// Slice 273: counts that a feature fell back (<paramref name="area"/> and <paramref name="reason"/> are <see cref="FallbackArea"/> and <see cref="FallbackReason"/> values) while reading <paramref name="story"/> in <paramref name="language"/>.
	/// The first of each (area, reason, book, language) in this session is queued at once with count 1; if it happened again, <see cref="FlushAsync"/> queues one more row with the session total (<see cref="FallbackTally"/>).
	/// </summary>
	public void RecordFallback(Story story, string area, string reason, string? language = null, Action? counted = null)
	{
		string? detail = FallbackUsage.Detail(area, reason);
		if (detail is null)
		{
			return;
		}

		string readIn = NormalisedLanguage(language ?? story.Language);
		lock (_fallbackStories)
		{
			_fallbackStories[(story.Id, readIn)] = (story, language);
		}

		int times = _fallbacks.Note(area, reason, story.Id, readIn);
		counted?.Invoke(); // Slice 351 review: the caller learns the tally has it, so a later failure here is not reported to the sink a second time
		bool first = FallbackTally.IsFirst(times);
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Usage] fallback {detail} times={times} {(first ? "queued" : "counted, sent with the session flush")} book={story.Id} lang={readIn}");
#endif
		if (first)
		{
			Record(UsageEventName.Fallback, story, detail, language, times);
		}
	}

	/// <summary>
	/// Slice 273 part B: counts a fallback that is about no book (a speech or translation fallback with no story at hand), in <paramref name="language"/> when known. Same once-per-session rule as
	/// <see cref="RecordFallback"/>, with the book left empty in the tally.
	/// </summary>
	public void RecordGeneralFallback(string area, string reason, string? language = null)
	{
		string? detail = FallbackUsage.Detail(area, reason);
		if (detail is null || _configHold.TryHold(area, reason, language))
		{
			return; // Slice 273 part C: a config or first-launch ui_language fallback waits for a real config; the hold sends it back through here
		}

		string readIn = NormalisedLanguage(language);
		int times = _fallbacks.Note(area, reason, string.Empty, readIn);
		bool first = FallbackTally.IsFirst(times);
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Usage] fallback {detail} times={times} {(first ? "queued" : "counted, sent with the session flush")}");
#endif
		if (first)
		{
			RecordGeneral(UsageEventName.Fallback, readIn.Length == 0 ? UsageEvent.NoLanguage : readIn, detail, times);
		}
	}

	// The tally key and the event use one language code ("en-US" and "en" are one kind): the primary subtag, else the text as given.
	private static string NormalisedLanguage(string? language) =>
		ReadTheStupidText.Application.Usage.UsageEvents.Language(language) ?? language ?? string.Empty;

	// Slice 273: a fallback that happened again gets one more row with the session total, queued just before the flush.
	private void QueueFallbackRepeats()
	{
		foreach (FallbackTotal total in _fallbacks.TakeRepeats())
		{
			if (total.Book.Length == 0)
			{
				if (FallbackUsage.Detail(total.Area, total.Reason) is { } general)
				{
					RecordGeneral(UsageEventName.Fallback, total.Language.Length == 0 ? UsageEvent.NoLanguage : total.Language, general, total.Count);
				}

				continue;
			}

			(Story story, string? language) known;
			lock (_fallbackStories)
			{
				if (!_fallbackStories.TryGetValue((total.Book, total.Language), out known))
				{
					continue;
				}
			}

			if (FallbackUsage.Detail(total.Area, total.Reason) is { } detail)
			{
				Record(UsageEventName.Fallback, known.story, detail, known.language, total.Count);
			}
		}
	}

	private void RecordCore(string name, Story story, string detail, string? language, int? count = null, int? deviceCount = null, int? seconds = null) =>
		_sender.Enqueue((sampleRate, now) =>
		{
			bool fromCatalogue = UsageEvent.ShelfId(story.SourceId) is { } id && StarterStories.IsCatalogueId(id);
			return Stamp(UsageEvent.For(name, story, fromCatalogue, sampleRate, AppConfigClient.InstalledVersion, AppConfigClient.ThisPlatform, now, detail, language) with { Count = count, DeviceCount = deviceCount, Seconds = seconds });
		});

	// Slice 351: the screen language, or "none" when the preference cannot be read; that failure is reported (the tally lets only the first one queue a row, so the report cannot loop back through Stamp).
	private readonly ReentryGuard _uiLangReport = new();

	private string UiLanguageOrNone()
	{
		try
		{
			return LanguageChoice.Current();
		}
		catch (Exception)
		{
			using IDisposable? scope = _uiLangReport.TryEnter(); // a report that stamps a count and fails here again is skipped, never nested (Slice 351 review)
			if (scope is not null)
			{
				RecordGeneralFallback(ReadTheStupidText.Application.Usage.FallbackArea.Config, ReadTheStupidText.Application.Usage.FallbackReason.PreferenceUnsaved);
			}

			return UsageSource.None;
		}
	}

	// Slice 250: every count carries its session id and its place in the session, so it keeps them while it waits in the queue for a later launch.
	private UsageEvent Stamp(UsageEvent count)
	{
		// Slice 351: and who and when, as closed values: the screen language, install-age and launch-count buckets (from a first-launch day kept on the device, never sent) and the seconds since this launch started.
		UsageEvent stamped = count with
		{
			Session = _session.Id,
			Seq = _session.NextSeq(),
			UiLang = UiLanguageOrNone(),
			InstallAge = _launch.InstallAge,
			LaunchNo = _launch.LaunchNo,
			SinceLaunch = _launch.SinceLaunch(),
			Screen = UiScreen.Current, // Slice 352: where in the app this happened (blob20)
		};
#if DEBUG
		// Slice 320 item 7: a Debug-only line per count that reached the queue, with the seq it was given (the line in RecordLogged is written before sampling, so it has none), so a flow reads the sequence on a device. Nothing is sent from here.
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Usage] seq={stamped.Seq} {stamped.Name} session={stamped.Session} uiLang={stamped.UiLang} installAge={stamped.InstallAge} launchNo={stamped.LaunchNo} sinceLaunch={stamped.SinceLaunch} screen={stamped.Screen} detail={stamped.Detail}");
#endif
		return stamped;
	}

	/// <summary>
	/// Queues the count <paramref name="name"/> for every book in <paramref name="counted"/> (its story and detail) in one queue write (Slice 191). Each is
	/// counted as the grown-up's own book (never a shelf book), so a restored book carries nothing of where it came from.
	/// </summary>
	public void RecordMany(string name, IReadOnlyList<(Story Story, string Detail)> counted) =>
		_sender.EnqueueMany((sampleRate, now) => counted
			.Select(c => Stamp(UsageEvent.For(name, c.Story, false, sampleRate, AppConfigClient.InstalledVersion, AppConfigClient.ThisPlatform, now, c.Detail)))
			.ToList());

	/// <summary>Queues a count about no book (a voice change, a launch, a language change); <paramref name="language"/> is the one the count is about.</summary>
	public bool RecordGeneral(string name, string language, string detail = "", int? count = null) =>
		_sender.Enqueue((sampleRate, now) =>
			Stamp(UsageEvent.ForNoBook(name, language, detail, sampleRate, AppConfigClient.InstalledVersion, AppConfigClient.ThisPlatform, now) with { Count = count }));

	/// <summary>Slice 267: queues a count about one store book by its package id (never its title), in <paramref name="language"/>, with a closed <paramref name="detail"/> (an audio pack download's outcome).</summary>
	public void RecordShelfBook(string name, string bookId, string language, string detail) =>
		_sender.Enqueue((sampleRate, now) => UsageEvent.ForShelfBook(
			name, bookId, language, detail, sampleRate, AppConfigClient.InstalledVersion, AppConfigClient.ThisPlatform, now) is { } count ? Stamp(count) : null);

	/// <summary>Queues a count about a store story (by its id, never its title) with the store filters in use.</summary>
	public void RecordStoreDownload(StoreItem item, bool ageFilterSet, bool authorFilterSet) =>
		_sender.Enqueue((sampleRate, now) => UsageEvent.ForShelfBook(
			UsageEventName.StoreDownload, item.Id, item.Language, UsageDetail.StoreFilters(ageFilterSet, authorFilterSet),
			sampleRate, AppConfigClient.InstalledVersion, AppConfigClient.ThisPlatform, now) is { } count ? Stamp(count) : null);

	/// <summary>Sends what is queued, a batch at a time. Never throws; a batch that could not be delivered stays queued and is tried again after a growing pause. Calls overlap safely.</summary>
	public Task FlushAsync()
	{
		QueueFallbackRepeats();
		return _sender.FlushAsync();
	}
}
