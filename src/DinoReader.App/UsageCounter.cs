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

	/// <summary>The id of this launch's session (Slice 250), for crash and problem reports; kept in memory, and on disk only inside counts still waiting in the send queue.</summary>
	public string SessionId => _session.Id;

	public UsageCounter(AppConfigClient config)
	{
		_sender = new UsageQueueSender<UsageEvent>(new UsageQueueOptions<UsageEvent>(
			StoreClient.BaseAddress, EnabledKey, QueueKey, LogName, UsageBatch.MaxEvents,
			() => (config.Current.Telemetry.Enabled, config.Current.Telemetry.SampleRate),
			() => config.HasCopy,
			events => UsageBatch.ToJson(events, DeviceProbe.Current), UsageBatch.ParseQueue, UsageBatch.QueueToJson,
			SendsBeta: true, // Slice 251: TestFlight batches go out posted to the Worker's separate beta route (dino_usage_beta)
			Roll: _session.Roll)); // Slice 250: sampling is decided once per session
		WaitController.Finished -= RecordWait; // guarded: a second counter must not stay subscribed twice
		WaitController.Finished += RecordWait;
	}

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
#if DEBUG
		// Slice 250: a Debug-only line per count asked for (before the sampling and switch checks), so a device flow can read the order of a session's counts and its one session id.
		// Slice 273: with the counts a read_voice or fallback carries.
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Usage] {name} {detail} session={_session.Id}{(count is null ? "" : $" count={count}")}{(deviceCount is null ? "" : $" device={deviceCount}")}");
#endif
		RecordCore(name, story, detail, language, count, deviceCount);
	}

	/// <summary>
	/// Slice 273: counts that a feature fell back (<paramref name="area"/> and <paramref name="reason"/> are <see cref="FallbackArea"/> and <see cref="FallbackReason"/> values) while reading <paramref name="story"/> in <paramref name="language"/>.
	/// The first of each (area, reason, book, language) in this session is queued at once with count 1; if it happened again, <see cref="FlushAsync"/> queues one more row with the session total (<see cref="FallbackTally"/>).
	/// </summary>
	public void RecordFallback(Story story, string area, string reason, string? language = null)
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
		bool first = FallbackTally.IsFirst(times);
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Usage] fallback {detail} times={times} {(first ? "queued" : "counted, sent with the session flush")}");
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
		if (detail is null)
		{
			return;
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

	private void RecordCore(string name, Story story, string detail, string? language, int? count = null, int? deviceCount = null) =>
		_sender.Enqueue((sampleRate, now) =>
		{
			bool fromCatalogue = UsageEvent.ShelfId(story.SourceId) is { } id && StarterStories.IsCatalogueId(id);
			return Stamp(UsageEvent.For(name, story, fromCatalogue, sampleRate, AppConfigClient.InstalledVersion, AppConfigClient.ThisPlatform, now, detail, language) with { Count = count, DeviceCount = deviceCount });
		});

	// Slice 250: every count carries its session id and its place in the session, so it keeps them while it waits in the queue for a later launch.
	private UsageEvent Stamp(UsageEvent count) => count with { Session = _session.Id, Seq = _session.NextSeq() };

	/// <summary>
	/// Queues the count <paramref name="name"/> for every book in <paramref name="counted"/> (its story and detail) in one queue write (Slice 191). Each is
	/// counted as the grown-up's own book (never a shelf book), so a restored book carries nothing of where it came from.
	/// </summary>
	public void RecordMany(string name, IReadOnlyList<(Story Story, string Detail)> counted) =>
		_sender.EnqueueMany((sampleRate, now) => counted
			.Select(c => Stamp(UsageEvent.For(name, c.Story, false, sampleRate, AppConfigClient.InstalledVersion, AppConfigClient.ThisPlatform, now, c.Detail)))
			.ToList());

	/// <summary>Queues a count about no book (a voice change, a launch, a language change); <paramref name="language"/> is the one the count is about.</summary>
	public void RecordGeneral(string name, string language, string detail = "", int? count = null) =>
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
