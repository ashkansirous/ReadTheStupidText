using DinoReader.Core;
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

	public UsageCounter(AppConfigClient config)
	{
		_sender = new UsageQueueSender<UsageEvent>(new UsageQueueOptions<UsageEvent>(
			StoreClient.BaseAddress, EnabledKey, QueueKey, LogName, UsageBatch.MaxEvents,
			() => (config.Current.Telemetry.Enabled, config.Current.Telemetry.SampleRate),
			() => config.HasCopy,
			events => UsageBatch.ToJson(events, DeviceProbe.Current), UsageBatch.ParseQueue, UsageBatch.QueueToJson));
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
	public void Record(string name, Story story, string detail = "", string? language = null) =>
		_sender.Enqueue((sampleRate, now) =>
		{
			bool fromCatalogue = UsageEvent.ShelfId(story.SourceId) is { } id && StarterStories.IsCatalogueId(id);
			return UsageEvent.For(name, story, fromCatalogue, sampleRate, AppConfigClient.InstalledVersion, AppConfigClient.ThisPlatform, now, detail, language);
		});

	/// <summary>
	/// Queues the count <paramref name="name"/> for every book in <paramref name="counted"/> (its story and detail) in one queue write (Slice 191). Each is
	/// counted as the grown-up's own book (never a shelf book), so a restored book carries nothing of where it came from.
	/// </summary>
	public void RecordMany(string name, IReadOnlyList<(Story Story, string Detail)> counted) =>
		_sender.EnqueueMany((sampleRate, now) => counted
			.Select(c => UsageEvent.For(name, c.Story, false, sampleRate, AppConfigClient.InstalledVersion, AppConfigClient.ThisPlatform, now, c.Detail))
			.ToList());

	/// <summary>Queues a count about no book (a voice change, a launch, a language change); <paramref name="language"/> is the one the count is about.</summary>
	public void RecordGeneral(string name, string language, string detail = "") =>
		_sender.Enqueue((sampleRate, now) =>
			UsageEvent.ForNoBook(name, language, detail, sampleRate, AppConfigClient.InstalledVersion, AppConfigClient.ThisPlatform, now));

	/// <summary>Queues a count about a store story (by its id, never its title) with the store filters in use.</summary>
	public void RecordStoreDownload(StoreItem item, bool ageFilterSet, bool authorFilterSet) =>
		_sender.Enqueue((sampleRate, now) => UsageEvent.ForShelfBook(
			UsageEventName.StoreDownload, item.Id, item.Language, UsageDetail.StoreFilters(ageFilterSet, authorFilterSet),
			sampleRate, AppConfigClient.InstalledVersion, AppConfigClient.ThisPlatform, now));

	/// <summary>Sends what is queued, a batch at a time. Never throws; a batch that could not be delivered stays queued and is tried again after a growing pause. Calls overlap safely.</summary>
	public Task FlushAsync() => _sender.FlushAsync();
}
