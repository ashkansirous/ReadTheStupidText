using System.Diagnostics;
using System.Net;
using System.Text;
using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Dino's anonymous usage counts (Slice 133, Decisions 140-142). A count is a name plus context (<see cref="UsageEvent"/>): no id of the
/// install or the device, no typed text, no own-book title. Counts are queued in preferences and sent in batches of at most
/// <see cref="UsageBatch.MaxEvents"/> to <c>POST /api/events</c> when a reading session ends or the app next starts. Nothing is queued
/// or sent when the grown-up's switch is off, when the remote config has telemetry off (<see cref="AppConfigClient"/>), or when this launch
/// was not picked by the config's <c>sampleRate</c>; the Worker drops everything again when telemetry is off.
/// </summary>
public sealed class UsageCounter(AppConfigClient config)
{
	/// <summary>The grown-up's switch (settings, behind the gate). On by default (Decision 141).</summary>
	public const string EnabledKey = "dino.usage.enabled";

	private const string QueueKey = "dino.usage.queue";
	private const string EventsPath = "/api/events";
	private const int MaxQueued = 200;
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

	// Like AppConfigClient: only the app's own host, no redirects, no cookies.
	private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

	private static readonly TimeSpan FirstBackoff = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

	// Every read or change of the queue happens under this lock, on the in-memory copy, so a send that is in flight can neither
	// drop events recorded meanwhile nor bring back events a grown-up switched off.
	private readonly object _queueLock = new();
	private readonly SemaphoreSlim _sending = new(1, 1);
	private readonly double _roll = Random.Shared.NextDouble();
	private List<UsageEvent>? _queue;
	private int _failures;
	private DateTimeOffset _nextAttempt = DateTimeOffset.MinValue;

	private enum SendResult { Sent, Rejected, Retry }

	/// <summary>Whether counts may be sent at all; turning it off also forgets what was waiting.</summary>
	public bool Enabled
	{
		get => Preferences.Default.Get(EnabledKey, true);
		set
		{
			Preferences.Default.Set(EnabledKey, value);
			if (!value)
			{
				Clear();
			}
		}
	}

	/// <summary>Queues the count <paramref name="name"/> (a <see cref="UsageEventName"/>) about <paramref name="story"/>, unless counting is off for this launch.</summary>
	public void Record(string name, Story story)
	{
		TelemetryConfig telemetry = config.Current.Telemetry;
		if (!Enabled || !UsageBatch.Samples(telemetry, _roll))
		{
			return;
		}

		bool fromCatalogue = UsageEvent.ShelfId(story.SourceId) is { } id && StarterStories.IsCatalogueId(id);
		var count = UsageEvent.For(name, story, fromCatalogue, telemetry.SampleRate, AppConfigClient.InstalledVersion, AppConfigClient.ThisPlatform, DateTimeOffset.UtcNow);
		lock (_queueLock)
		{
			List<UsageEvent> queue = Queue();
			queue.Add(count);
			if (queue.Count > MaxQueued)
			{
				queue.RemoveRange(0, queue.Count - MaxQueued);
			}

			Save(queue);
		}
	}

	/// <summary>Sends what is queued, a batch at a time. Never throws; a batch that could not be delivered stays queued and is tried again after a growing pause. Calls overlap safely.</summary>
	public async Task FlushAsync()
	{
		if (!await _sending.WaitAsync(0))
		{
			return;
		}

		try
		{
			if (!Enabled || !config.Current.Telemetry.Enabled)
			{
				// Off means off (kids' app): when the switch or the remote config says so, whatever is queued is discarded, not kept for later.
				Clear();
				return;
			}

			if (DateTimeOffset.UtcNow >= _nextAttempt)
			{
				await SendQueuedAsync();
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not send its counts: {ex.Message}");
			BackOff();
		}
		finally
		{
			_sending.Release();
		}
	}

	private async Task SendQueuedAsync()
	{
		while (true)
		{
			List<UsageEvent> batch = Peek();
			if (batch.Count == 0)
			{
				return;
			}

			SendResult result = await PostAsync(batch);
			if (result == SendResult.Retry)
			{
				BackOff();
				return;
			}

			// A batch the Worker refused for good (old Worker, bad body) is dropped: sending it again could never succeed and would block the rest.
			_failures = 0;
			Remove(batch);
		}
	}

	private void BackOff()
	{
		_failures++;
		TimeSpan pause = FirstBackoff * Math.Pow(2, Math.Min(_failures - 1, 6));
		_nextAttempt = DateTimeOffset.UtcNow + (pause > MaxBackoff ? MaxBackoff : pause);
	}

	private static async Task<SendResult> PostAsync(IReadOnlyList<UsageEvent> batch)
	{
		using var timeout = new CancellationTokenSource(Timeout);
		using var body = new StringContent(UsageBatch.ToJson(batch), Encoding.UTF8, "application/json");
		using HttpResponseMessage response = await Http.PostAsync(new Uri(new Uri(StoreClient.BaseAddress), EventsPath), body, timeout.Token);
		return Classify(response.StatusCode);
	}

	// 2xx is delivered; 408 and 429 and every 5xx (the Worker's 503 when counting is not set up yet) are worth trying again; any other status is final.
	private static SendResult Classify(HttpStatusCode status)
	{
		int code = (int)status;
		if (code is >= 200 and < 300)
		{
			return SendResult.Sent;
		}

		return code is 408 or 429 or >= 500 ? SendResult.Retry : SendResult.Rejected;
	}

	private List<UsageEvent> Peek()
	{
		lock (_queueLock)
		{
			return Queue().Take(UsageBatch.MaxEvents).ToList();
		}
	}

	private void Remove(IReadOnlyList<UsageEvent> sent)
	{
		lock (_queueLock)
		{
			List<UsageEvent> queue = Queue();
			// By reference, not by position or value: Record may have trimmed the head meanwhile, and two counts can be equal.
			var delivered = new HashSet<UsageEvent>(sent, ReferenceEqualityComparer.Instance);
			if (queue.RemoveAll(delivered.Contains) > 0)
			{
				Save(queue);
			}
		}
	}

	private void Clear()
	{
		lock (_queueLock)
		{
			_queue = [];
			Save(_queue);
		}
	}

	// Call with _queueLock held.
	private List<UsageEvent> Queue() => _queue ??= UsageBatch.ParseQueue(Preferences.Default.Get(QueueKey, (string?)null));

	// Call with _queueLock held.
	private static void Save(IReadOnlyList<UsageEvent> queue)
	{
		if (queue.Count == 0)
		{
			Preferences.Default.Remove(QueueKey);
			return;
		}

		Preferences.Default.Set(QueueKey, UsageBatch.QueueToJson(queue));
	}
}
