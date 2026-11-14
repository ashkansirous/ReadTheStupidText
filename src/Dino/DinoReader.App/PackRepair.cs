using DinoReader.Core;
using Microsoft.Maui.Networking;
using Sirous.Reading.Application.Usage;

namespace DinoReader;

/// <summary>What one request for a repair run asks for (Slice 360): the network just came back (an offline try is due again) and/or books that were just opened (due at once, ignoring the hour).</summary>
public sealed record RepairScope(bool NetworkBack = false, IReadOnlyCollection<string>? ForceBooks = null, bool ForceAll = false)
{
	public static readonly RepairScope Ordinary = new();

	/// <summary>A grown-up's own action (a language added or ticked again): every missing pack is due at once, ignoring the hour.</summary>
	public static readonly RepairScope Everything = new(ForceAll: true);

	/// <summary>This scope widened by <paramref name="other"/> (a null one adds nothing).</summary>
	public RepairScope Merge(RepairScope? other) =>
		other is null ? this : new RepairScope(NetworkBack || other.NetworkBack, [.. (ForceBooks ?? []).Union(other.ForceBooks ?? [], StringComparer.OrdinalIgnoreCase)], ForceAll || other.ForceAll);

	public bool IsForced(string bookId) => ForceAll || ForceBooks?.Contains(bookId, StringComparer.OrdinalIgnoreCase) == true;
}

/// <summary>
/// The one owner of the pack repair state (Slice 360, Design): which book's pack in which language was tried when and how it ended, kept in memory only, and the triggers that ask for a retry: an hourly tick
/// while the app is in the foreground, the network coming back, and opening a book whose sound is missing (the launch and the app return are made by <see cref="ShelfAudioPacks.Resume"/>). The runs
/// themselves are <see cref="ShelfAudioPacks"/>' (it is the only fetcher of book audio); it asks <see cref="IsDue"/> for each pack and reports every outcome here. One attempt per pack per trigger, never a loop.
/// </summary>
public sealed class PackRepair(Action<RepairScope> requestRun, Func<string, string, bool> stillNeeded, IFallbackSink fallbacks)
{
	private static readonly TimeSpan Tick = TimeSpan.FromMinutes(5); // short, so a pack that failed at the end of a try is found once its hour is up; PackRepairLog.AnyDue decides whether a tick does any work
	private static readonly TimeSpan NetworkDebounce = TimeSpan.FromSeconds(20); // a Wi-Fi/cellular handover raises several events in a row
	private static readonly TimeSpan OpenThrottle = TimeSpan.FromSeconds(30); // the page appears again after every sheet that covered it; one open is one trigger
	private const string Tag = "pack repair";
	private readonly PackRepairLog _log = new();
	private readonly object _gate = new();
	private readonly Dictionary<string, DateTimeOffset> _lastOpened = [];
	private Timer? _timer;
	private int _lastPercentRaised = -1;
	private string? _lastPercentKey;
	private DateTimeOffset _lastNetworkRun = DateTimeOffset.MinValue;

#if DEBUG
	private static long _clockOffsetMinutes;

	/// <summary>Debug only (the <c>pack-repair</c> test hook): moves this process's clock forward so a flow can see the hourly retry without waiting an hour.</summary>
	public static void AdvanceClock(int minutes) => Interlocked.Add(ref _clockOffsetMinutes, minutes);

	private static DateTimeOffset Now => DateTimeOffset.UtcNow.AddMinutes(Interlocked.Read(ref _clockOffsetMinutes));

	/// <summary>Debug only (the <c>pack-repair</c> test hook): the page's "no internet" state is shown as if the phone were offline (the downloads themselves are unaffected), so a flow can check SS2 and SS4 without airplane mode.</summary>
	public static bool ForcedOffline { get; set; }

	/// <summary>Debug only: the same pass the hourly tick makes, now.</summary>
	public void TickNow() => OnTick();
#else
	private static DateTimeOffset Now => DateTimeOffset.UtcNow;
#endif

	/// <summary>Raised (on any thread) whenever a pack's state changed: a try started, moved a whole percent, ended, or the connection changed.</summary>
	public event Action? Changed;

	/// <summary>Whether the pack should be tried now. <paramref name="forced"/> (the book was just opened) and a first try in this launch are due at once; otherwise <see cref="PackRepairDue"/> decides.</summary>
	public bool IsDue(string bookId, string language, int version, bool forced, bool networkBack)
	{
		string key = BundledTranslations.Key(language);
		bool due = forced || _log.IsDue(bookId, key, Now, version, networkBack);
		if (!due)
		{
			PackRepairEntry? entry = _log.EntryOf(bookId, key);
			AudioPackLog.Write($"{Tag} {bookId}/{key} v{version}: not due yet, last try {entry?.LastAttemptUtc:O} ({entry?.LastOutcome})");
		}

		return due;
	}

	/// <summary>A try of this pack begins: the page shows "Getting ... 0%".</summary>
	public void Begin(string bookId, string language, int version)
	{
		string key = BundledTranslations.Key(language);
		_log.SetPercent(bookId, key, 0);
		AudioPackLog.Write($"{Tag} {bookId}/{key} v{version}: try starts");
		Changed?.Invoke();
	}

	/// <summary>The byte progress of a try, as a whole percent of the listed size; <see cref="Changed"/> fires only when the percent moves.</summary>
	public IProgress<long> ProgressOf(string bookId, string language, long? listedBytes)
	{
		string key = BundledTranslations.Key(language);
		return new SyncProgress<long>(bytes => OnBytes(bookId, key, bytes, listedBytes));
	}

	private void OnBytes(string bookId, string key, long bytes, long? listedBytes)
	{
		int percent = PackRepairLog.PercentOfBytes(bytes, listedBytes);
		_log.SetPercent(bookId, key, percent);
		string id = PackRepairLog.Key(bookId, key);
		bool moved;
		lock (_gate)
		{
			moved = percent != _lastPercentRaised || id != _lastPercentKey;
			_lastPercentRaised = percent;
			_lastPercentKey = id;
		}

		if (moved)
		{
			Changed?.Invoke();
		}
	}

	/// <summary>A try of this pack ended with <paramref name="outcome"/> for the listed <paramref name="version"/>.</summary>
	public void End(string bookId, string language, PackOutcome outcome, int version)
	{
		string key = BundledTranslations.Key(language);
		_log.Record(bookId, key, outcome, Now, version);
		AudioPackLog.Write($"{Tag} {bookId}/{key} v{version}: {outcome}");
		Changed?.Invoke();
	}

	/// <summary>A run could not read the store listing while online, so it never got to try this pack: recorded Failed (keeping the version last asked for), so the page says "will try again soon" and the hourly tick retries.</summary>
	public void NoteListingFailed(string bookId, string language)
	{
		string key = BundledTranslations.Key(language);
		_log.Record(bookId, key, PackOutcome.Failed, Now, _log.EntryOf(bookId, key)?.ListedVersion);
		AudioPackLog.Write($"{Tag} {bookId}/{key}: the store listing could not be read, Failed");
		Changed?.Invoke();
	}

	/// <summary>A try ended with no outcome (the run was cancelled): the page stops showing its percent.</summary>
	public void Abandon(string bookId, string language)
	{
		_log.ClearPercent(bookId, BundledTranslations.Key(language));
		Changed?.Invoke();
	}

	/// <summary>What this log holds for the pack now (null when it was never tried), so a cancelled offline wait can put it back.</summary>
	public PackRepairEntry? EntryOf(string bookId, string language) => _log.EntryOf(bookId, BundledTranslations.Key(language));

	/// <summary>A run cancelled while waiting offline records no Offline outcome: the entry from before the try (null: none) is put back.</summary>
	public void AbandonOffline(string bookId, string language, PackRepairEntry? earlier)
	{
		_log.Restore(bookId, BundledTranslations.Key(language), earlier);
		Changed?.Invoke();
	}

	/// <summary>What the page shows for a book's sound in <paramref name="language"/> now.</summary>
	public PackSoundView ViewOf(string bookId, string language, string? switchTo, PackExpectationKind expectation, bool installed)
	{
		string key = BundledTranslations.Key(language);
		return PackSoundView.ForPack(expectation, installed, _log.PercentOf(bookId, key), IsOnline(), _log.EntryOf(bookId, key)?.LastOutcome, switchTo);
	}

	/// <summary>
	/// Opening a book whose pack is expected and missing in <paramref name="languages"/>: while offline each is recorded Offline (keeping the version last asked for), so the network coming back retries it even
	/// when no listing was read; then its packs are tried at once, ignoring the hour (Decision 2). The same book is not asked for again within a few seconds.
	/// </summary>
	public void OnBookOpened(string bookId, IEnumerable<string> languages)
	{
		if (!IsOnline())
		{
			foreach (string language in languages)
			{
				string key = BundledTranslations.Key(language);
				_log.Record(bookId, key, PackOutcome.Offline, Now, _log.EntryOf(bookId, key)?.ListedVersion);
			}

			Changed?.Invoke();
		}

		DateTimeOffset now = DateTimeOffset.UtcNow;
		lock (_gate)
		{
			if (_lastOpened.TryGetValue(bookId, out DateTimeOffset last) && now - last < OpenThrottle)
			{
				return;
			}

			_lastOpened[bookId] = now;
		}

		AudioPackLog.Write($"{Tag} {bookId}: opened, missing packs are tried now");
		requestRun(new RepairScope(ForceBooks: [bookId]));
	}

	/// <summary>Starts the hourly tick and the network-back watch while the app is in the foreground. Safe to call on every resume.</summary>
	public void StartTicking()
	{
		lock (_gate)
		{
			if (_timer is not null)
			{
				return;
			}

			_timer = new Timer(_ => OnTick(), null, Tick, Tick);
			Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
		}
	}

	/// <summary>Stops the tick and the watch when the window stops; the next launch or return starts them again.</summary>
	public void StopTicking()
	{
		lock (_gate)
		{
			_timer?.Dispose();
			_timer = null;
			Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
		}
	}

	// A timer or platform thread: an exception must never leave it (it would end the process), so a failed request is reported and the next tick asks again.
	private void OnTick()
	{
		try
		{
			_log.Prune(stillNeeded); // an installed pack, a book with no listing or no pack by design, a removed language: nothing to retry
			AudioPackLog.Write($"{Tag}: hourly tick");
			if (!_log.AnyDue(Now))
			{
				AudioPackLog.Write($"{Tag}: tick, nothing is due yet");
				return; // nothing that was tried and failed is an hour old yet: no listing fetch, no scan
			}

			AudioPackLog.Write($"{Tag}: tick, a pack is due again");
			requestRun(RepairScope.Ordinary);
		}
		catch (Exception ex)
		{
			AudioPackLog.Write($"{Tag}: a retry request failed: {ex.Message}");
			fallbacks.Report(FallbackArea.Pack, FallbackReason.PackSyncFailed); // tried again by the next tick or return; counting never throws
		}
	}

	private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
	{
		try
		{
			Changed?.Invoke(); // the page's "no internet" card follows the connection
			if (e.NetworkAccess != NetworkAccess.Internet)
			{
				return; // Local and ConstrainedInternet are not "back online"; the handover's real Internet event still comes
			}

			_log.Prune(stillNeeded);
			if (_log.AnyOffline() && NetworkRunAllowed())
			{
				AudioPackLog.Write($"{Tag}: network is back, offline tries are due");
				requestRun(new RepairScope(NetworkBack: true)); // the Offline entries are due at once, the hour ignored
			}
		}
		catch (Exception ex)
		{
			AudioPackLog.Write($"{Tag}: a retry request failed: {ex.Message}");
			fallbacks.Report(FallbackArea.Pack, FallbackReason.PackSyncFailed); // tried again by the next tick or return; counting never throws
		}
	}

	// Stamped only here, once every other condition holds: the run is requested right after.
	private bool NetworkRunAllowed()
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		lock (_gate)
		{
			if (now - _lastNetworkRun < NetworkDebounce)
			{
				return false;
			}

			_lastNetworkRun = now;
			return true;
		}
	}

	private static bool IsOnline()
	{
#if DEBUG
		if (ForcedOffline)
		{
			return false;
		}
#endif
		try
		{
			return Connectivity.Current.NetworkAccess != NetworkAccess.None;
		}
		catch (Exception)
		{
			// no-fallback: a platform that cannot say is treated as connected; a failed fetch reports itself
			return true;
		}
	}
}
