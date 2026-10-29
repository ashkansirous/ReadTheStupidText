using System.Diagnostics;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Shared.Network;

namespace DinoReader;

/// <summary>What the grown-up gallery's status line shows (Slice 115, mock frame 50).</summary>
public enum StorytellerPhase
{
	/// <summary>Nothing to show: installed, not listed, or not started.</summary>
	Idle,

	/// <summary>The pack is downloading; <see cref="StorytellerStatus.Progress"/> is 0..1.</summary>
	Downloading,

	/// <summary>On mobile data: waiting for Wi-Fi, with "Download now" as the way out.</summary>
	WaitingForWifi,

	/// <summary>No network at all: the line says it waits for the internet and carries on by itself (Slice 182); the pill tries at once.</summary>
	WaitingForInternet,

	/// <summary>The download failed: the line says so and offers "Try again".</summary>
	Failed,
}

public sealed record StorytellerStatus(StorytellerPhase Phase, double Progress, int Megabytes)
{
	public static readonly StorytellerStatus None = new(StorytellerPhase.Idle, 0, 0);
}

/// <summary>
/// The storyteller pack is Dino's default voice (Slice 115, Decisions 120 and 121): on any launch where it is missing it downloads by itself over
/// Wi-Fi (mobile data waits for "Download now"), and when it lands every stored book is re-cast once, hand-picked voices included. One per app; the
/// pack service resumes a cut download, and the single-flight guard here keeps a second start from downloading twice.
/// </summary>
public sealed class StorytellerDefault
{
	private const string ResumeKey = "storyteller";
	private const string ReadyPendingKey = "dino.storyteller.ready.pending";
	private const double ProgressStep = 0.01;
	private static readonly string[] KokoroLanguages = [KokoroVoiceTable.EnglishCode, KokoroVoiceTable.SpanishCode, KokoroVoiceTable.PortugueseCode, KokoroVoiceTable.ChineseCode];
	private static readonly TimeSpan CatalogMaxAge = TimeSpan.FromMinutes(10);
	private static readonly IReadOnlyDictionary<string, IReadOnlyList<CastMember>> NoCasts = new Dictionary<string, IReadOnlyList<CastMember>>();
	private static readonly TimeSpan CatalogRetryBackoff = TimeSpan.FromSeconds(3);

	private readonly IVoicePackService _packs;
	private readonly IStoryLibrary _library;
	private readonly StoryVoicing _voicing;
	private readonly object _gate = new();
	private int _running;
	private int _mobileDataRequested;
	private int _watchingNetwork;
	private PackDownloadResult? _stoppedWith; // how the run that is ending stopped; reported only after the guard clears (Slice 182)
	private int _downloadNowChosen; // "Download now" was tapped: a stopped download may restart over mobile data (Slice 182)
	private readonly PackResumeWatcher _resume;
	private volatile bool _awaitingNetwork;
	private bool _downloadActive;
	private double _lastReported = -1;
	private StorytellerStatus _status = StorytellerStatus.None;

	public StorytellerDefault(IVoicePackService packs, IStoryLibrary library, StoryVoicing voicing, PackResumeWatcher resume)
	{
		_resume = resume;
		_packs = packs;
		_library = library;
		_voicing = voicing;
		_resume.NetworkChanged += (_, _) => RefreshWaitingPhase();
	}

	/// <summary>Raised (on any thread) when <see cref="Status"/> or <see cref="ReadyPending"/> changed.</summary>
	public event EventHandler? Changed;

	/// <summary>Raised (on any thread) after the library was re-cast and at least one book changed, so an open cast page loads the new cast.</summary>
	public event EventHandler? LibraryRecast;

	public StorytellerStatus Status
	{
		get
		{
			lock (_gate)
			{
				return _status;
			}
		}
	}

	/// <summary>True from the install finishing until the grown-up closes the "ready" sheet; kept across restarts.</summary>
	public bool ReadyPending => Preferences.Default.Get(ReadyPendingKey, false);

	public void AcknowledgeReady()
	{
		Preferences.Default.Set(ReadyPendingKey, false);
		Changed?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Starts the default download when the pack is missing (Wi-Fi only), or re-casts the library and updates the pack when it is there. Never throws.</summary>
	public Task StartAsync() => RunGuardedAsync(allowMobileData: false);

	/// <summary>"Download now": the same download over mobile data, for this download only.</summary>
	public Task DownloadNowAsync() => RunGuardedAsync(allowMobileData: true);

	/// <summary>"Try again" after a failed download: the same start as at launch (Wi-Fi only, mobile data waits for "Download now").</summary>
	public Task RetryAsync() => RunGuardedAsync(allowMobileData: false);

	private async Task RunGuardedAsync(bool allowMobileData)
	{
		if (!_packs.SupportsStorytellerPacks)
		{
			return;
		}

		if (Interlocked.Exchange(ref _running, 1) == 1)
		{
			// A tap that arrives while the start is still looking at the catalog is not lost: the run picks it up.
			if (allowMobileData)
			{
				Volatile.Write(ref _mobileDataRequested, 1);
			}

			return;
		}

		if (allowMobileData)
		{
			Volatile.Write(ref _downloadNowChosen, 1);
		}

		try
		{
			await RunAsync(allowMobileData);
		}
		catch (Exception ex)
		{
			// The books keep reading with the current voices; the grown-up can try again from the status line.
			Debug.WriteLine($"Dino Reader storyteller default failed: {ex}");
			SetStatus(FailureStatus(ex));
		}
		finally
		{
			Volatile.Write(ref _mobileDataRequested, 0);
			PackDownloadResult? stopped = _stoppedWith;
			_stoppedWith = null;
			Interlocked.Exchange(ref _running, 0);
			ReportEnd(stopped);
		}
	}

	// A network error while nothing was downloading is the quiet offline case (the first connection starts the download); only a download that broke says "Try again".
	private StorytellerStatus FailureStatus(Exception ex)
	{
		if (IsInstalled())
		{
			return StorytellerStatus.None;
		}

		if (Status.Phase != StorytellerPhase.Downloading && ex is HttpRequestException or IOException or TaskCanceledException)
		{
			WatchNetwork();
			return StorytellerStatus.None;
		}

		_stoppedWith = PackDownloadResult.Failed;
		return new StorytellerStatus(StorytellerPhase.Failed, 0, Status.Megabytes);
	}

	private async Task RunAsync(bool allowMobileData)
	{
		_awaitingNetwork = false;
		if (IsInstalled())
		{
			await RecastQuietlyAsync();
			await UpdateInstalledAsync();
			return;
		}

		PackCatalogResult catalog = await GetCatalogWithRetryAsync();
		if (catalog.Status != PackDownloadResult.Ok)
		{
			// Offline (or the catalog is down): nothing to say yet, and the first connection of this session starts the download.
			WatchNetwork();
			SetStatus(StorytellerStatus.None);
			return;
		}

		PackInfo? listed = StorytellerOffer.ListedFor(catalog.Packs, KokoroVoiceTable.EnglishCode);
		if (listed is null)
		{
			SetStatus(StorytellerStatus.None);
			return;
		}

		int megabytes = StorytellerOffer.Megabytes(listed.SizeBytes);
		if (!allowMobileData && Volatile.Read(ref _mobileDataRequested) == 0 && !WifiCapable())
		{
			WatchNetwork();
			SetStatus(new StorytellerStatus(StorytellerPhase.WaitingForWifi, 0, megabytes));
			return;
		}

		await DownloadAsync(megabytes);
	}

	// One more try after a short pause, so a blip while the network settles at launch does not leave the default undownloaded until the next network change.
	private async Task<PackCatalogResult> GetCatalogWithRetryAsync()
	{
		PackCatalogResult catalog = await _packs.GetCatalogCachedAsync(CatalogMaxAge, CancellationToken.None);
		if (catalog.Status == PackDownloadResult.Ok)
		{
			return catalog;
		}

		await Task.Delay(CatalogRetryBackoff);
		return await _packs.GetCatalogCachedAsync(CatalogMaxAge, CancellationToken.None);
	}

	private async Task DownloadAsync(int megabytes)
	{
		BeginDownload(megabytes);
		var progress = new Progress<double>(value => ReportProgress(value, megabytes));
		PackDownloadResult result = await _packs.DownloadAsync(PackConstants.StorytellerPackId, progress, CancellationToken.None);
		if (result != PackDownloadResult.Ok)
		{
			_stoppedWith = result;
			// Offline carries on by itself: the same waiting card as for Wi-Fi, not the "Try again" failure card.
			SetStatus(new StorytellerStatus(StoppedPhase(result), 0, megabytes));
			return;
		}

		Volatile.Write(ref _downloadNowChosen, 0);
		SetStatus(StorytellerStatus.None);
		Preferences.Default.Set(ReadyPendingKey, true);
		await RecastQuietlyAsync();
		Changed?.Invoke(this, EventArgs.Empty);
	}


	// Offline carries on by itself: "Waiting for the internet" with no connection, "Waiting for Wi-Fi" on mobile data only; Failed is the "Try again" card.
	private static StorytellerPhase StoppedPhase(PackDownloadResult result) => result switch
	{
		PackDownloadResult.Offline => PackResumeWatcher.CellularOnly() ? StorytellerPhase.WaitingForWifi : StorytellerPhase.WaitingForInternet,
		_ => StorytellerPhase.Failed,
	};

	// Slice 182: every run reports its end once (no stop means it finished); Offline or Failed starts again by itself when the network is back or the app returns (Wi-Fi/Ethernet unless "Download now").
	private void ReportEnd(PackDownloadResult? result)
	{
		bool now = Volatile.Read(ref _downloadNowChosen) == 1;
		PackRunEnd end = result is { } stopped ? IPackResume.EndOf(stopped) : PackRunEnd.Finished;
		_resume.Report(ResumeKey, end, () => ResumeAsync(now), PackConstants.StorytellerPackId, now);
	}

	// False while a run holds the guard; that run reports its own end.
	private async Task<bool> ResumeAsync(bool downloadNow)
	{
		if (Volatile.Read(ref _running) == 1)
		{
			return false;
		}

		await (downloadNow ? DownloadNowAsync() : RetryAsync());
		return true;
	}

	private void BeginDownload(int megabytes)
	{
		lock (_gate)
		{
			_lastReported = -1;
		}

		SetStatus(new StorytellerStatus(StorytellerPhase.Downloading, 0, megabytes));
	}

	// Progress posts to the thread pool, so a late report can arrive after the download ended: it is dropped, and so is every report inside the same whole percent.
	private void ReportProgress(double value, int megabytes)
	{
		lock (_gate)
		{
			if (!_downloadActive || (value - _lastReported < ProgressStep && value < 1))
			{
				return;
			}

			_lastReported = value;
			_status = new StorytellerStatus(StorytellerPhase.Downloading, value, megabytes);
		}

		Changed?.Invoke(this, EventArgs.Empty);
	}

	// A newer version of the installed pack comes in quietly, like the first download: Wi-Fi or Ethernet active, even with mobile data also listed.
	private async Task UpdateInstalledAsync()
	{
		InstalledVoicePack? installed = InstalledPacks().FirstOrDefault(p => p.Manifest.Id == PackConstants.StorytellerPackId);
		if (installed is null || !WifiCapable())
		{
			return;
		}

		PackCatalogResult catalog = await _packs.GetCatalogCachedAsync(CatalogMaxAge, CancellationToken.None);
		if (catalog.Status == PackDownloadResult.Ok && StorytellerOffer.UpdateAvailable(installed, catalog.Packs))
		{
			await DownloadUpdateAsync();
		}
	}

	// The new version can speak more languages (Slice 118: es, pt and zh): the library is re-cast from the voices it has now, and when the update added a language
	// the grown-up gets the "ready" note again.
	private async Task DownloadUpdateAsync()
	{
		IReadOnlyList<string> before = ServedLanguages();
		PackDownloadResult result = await _packs.DownloadAsync(PackConstants.StorytellerPackId, null, CancellationToken.None);
		if (result != PackDownloadResult.Ok)
		{
			return;
		}

		if (ServedLanguages().Except(before).Any())
		{
			Preferences.Default.Set(ReadyPendingKey, true);
		}

		await RecastQuietlyAsync();
		Changed?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Whether the installed storyteller pack speaks <paramref name="language"/> (a version-1 pack speaks English only).</summary>
	public bool Speaks(string language) => PackSelection.Serving(InstalledPacks(), PackKind.Storyteller, language).Any();

	private IReadOnlyList<string> ServedLanguages() => KokoroLanguages.Where(Speaks).ToList();

	// The library is re-cast whenever the pack is there (Decision 121): a book already on the pack's voices is left alone, so it is cheap and idempotent, and it covers
	// books added later and a pack removed and installed again. A failing book never stops the rest, and a failing handler never costs the "ready" note.
	private async Task RecastQuietlyAsync()
	{
		try
		{
			await RecastLibraryAsync();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader storyteller recast failed: {ex.Message}");
		}
	}

	// Every stored book is cast again from the voices it has now (Decision 121), one after the other; a book already on them is left alone.
	private async Task RecastLibraryAsync()
	{
		IReadOnlyList<Story> stories;
		try
		{
			stories = await _library.ListAsync();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader storyteller recast could not list the library: {ex.Message}");
			return;
		}

		bool changedAny = false;
		foreach (Story listed in stories)
		{
			RecastOutcome outcome = await RecastOneAsync(listed.Id);
			changedAny |= outcome == RecastOutcome.Changed;
		}

		if (changedAny)
		{
			LibraryRecast?.Invoke(this, EventArgs.Empty);
		}

	}

	private enum RecastOutcome
	{
		Unchanged,
		Changed,
		Failed,
	}

	// The book is read again just before it is cast, so an edit made since the listing is not overwritten.
	private async Task<RecastOutcome> RecastOneAsync(string storyId)
	{
		try
		{
			Story? current = await _library.GetAsync(storyId);
			Story? changed = current is null ? null : await NarratorRecast.TryRecastAsync(_voicing, current);
			if (changed is null)
			{
				return RecastOutcome.Unchanged;
			}

			// A cast edited in the app while this recast ran is newer than the snapshot: that book is left to the next pass.
			Story? latest = await _library.GetAsync(storyId);
			if (latest is null || !SameCasts(current!, latest))
			{
				return RecastOutcome.Unchanged;
			}

			await _library.UpdateAsync(changed);
			return RecastOutcome.Changed;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader storyteller recast of '{storyId}' failed: {ex.Message}");
			return RecastOutcome.Failed;
		}
	}

	private static bool SameCasts(Story before, Story after)
	{
		IReadOnlyDictionary<string, IReadOnlyList<CastMember>> was = before.LanguageCasts ?? NoCasts;
		IReadOnlyDictionary<string, IReadOnlyList<CastMember>> now = after.LanguageCasts ?? NoCasts;
		return before.Cast.SequenceEqual(after.Cast)
			&& was.Count == now.Count
			&& was.All(c => now.TryGetValue(c.Key, out IReadOnlyList<CastMember>? other) && other.SequenceEqual(c.Value));
	}

	private bool IsInstalled() => InstalledPacks().Any(p => p.Manifest.Id == PackConstants.StorytellerPackId);

	private IReadOnlyList<InstalledVoicePack> InstalledPacks()
	{
		try
		{
			return _packs.GetInstalledStorytellerPacks();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return [];
		}
	}

	// Wi-Fi or Ethernet among the active links is enough, even with mobile data also listed (Android reports both); only a cellular-only phone waits.
	internal static bool WifiCapable() => StorytellerNetworkPolicy.AllowsAutomaticDownload(Connectivity.Current.ConnectionProfiles.Select(ToProfile));

	private static NetworkProfile ToProfile(ConnectionProfile profile) => profile switch
	{
		ConnectionProfile.WiFi => NetworkProfile.WiFi,
		ConnectionProfile.Ethernet => NetworkProfile.Ethernet,
		ConnectionProfile.Cellular => NetworkProfile.Cellular,
		_ => NetworkProfile.Other,
	};

	// The phone came back on mobile data only: a card that says "Waiting for the internet" becomes "Waiting for Wi-Fi", with "Download now".
	private void RefreshWaitingPhase()
	{
		StorytellerStatus current = Status;
		if (current.Phase == StorytellerPhase.WaitingForInternet && PackResumeWatcher.CellularOnly())
		{
			SetStatus(current with { Phase = StorytellerPhase.WaitingForWifi });
		}
	}

	// A phone that joins Wi-Fi while the line waits (or that was offline at launch) starts the download without a tap.
	private void WatchNetwork()
	{
		_awaitingNetwork = true;
		if (Interlocked.Exchange(ref _watchingNetwork, 1) == 1)
		{
			return;
		}

		_resume.NetworkChanged += OnConnectivityChanged;
	}

	// One handler for the life of the singleton (registered once, atomically); it acts only while a start is waiting for the network.
	private void OnConnectivityChanged(object? sender, EventArgs e)
	{
		if (_awaitingNetwork && WifiCapable())
		{
			_ = StartAsync();
		}
	}

	// A state set here also ends the download, so a progress report still in flight cannot bring "Getting..." back.
	private void SetStatus(StorytellerStatus status)
	{
		lock (_gate)
		{
			_downloadActive = status.Phase == StorytellerPhase.Downloading;
			_status = status;
		}

		Changed?.Invoke(this, EventArgs.Empty);
	}
}
