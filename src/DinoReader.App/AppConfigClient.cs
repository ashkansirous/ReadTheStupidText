using System.Diagnostics;
using System.Text;
using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Dino's remote switchboard on the phone (Slice 132, Decisions 138-139). It fetches <c>GET /api/app-config</c> from
/// <see cref="StoreClient.BaseAddress"/>, keeps the last good copy in preferences and answers from that copy when the
/// phone is offline or the server fails; with no copy at all the answer is <see cref="AppConfig.Default"/> (no forced update,
/// telemetry off). The request is a plain GET: it sends nothing about the user and keeps no cookies.
/// </summary>
public sealed class AppConfigClient
{
	private const string ConfigPath = "/api/app-config";
	private const string CacheKey = "dino.appconfig.json";
	private const int MaxBytes = 64 * 1024;
	private const int BufferBytes = 4096;
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

	// A redirect is never followed, so the app only ever talks to the store's own host.
	private static readonly HttpClient Http = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };

	// Resume fires often (unlock, app switch, file picker), so a fetch is made at most once in this long and never twice at once.
	private static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(15);

	// A failed fetch (offline, 503, a body that is not a config) is tried again after this short back-off, not on every resume.
	private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(1);

	// A fetch that never reached the server (the network is not up yet at launch) is tried again sooner than a server-side failure:
	// after these waits in turn, then the normal one-minute rule. A success starts the list again.
	private static readonly TimeSpan[] ConnectBackoff = [TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)];

	// The fetch runs on the pool while the UI thread reads Current and calls RefreshAsync, so all three fields below are guarded by this lock.
	private readonly object _gate = new();
	private AppConfig? _current;
	private Task? _inFlight;
	private DateTimeOffset _notBefore = DateTimeOffset.MinValue;
	private int _connectFailures;

	/// <summary>
	/// Raised when a fetch brought a config that differs from the one held. Always raised on the main thread (by the client's own
	/// dispatch, never directly from the pool), so subscribers may touch UI; a caller awaiting <see cref="RefreshAsync"/> cannot assume handlers have run yet.
	/// </summary>
	public event EventHandler? Changed;

	/// <summary>The config in force: the last good copy, else the safe default.</summary>
	public AppConfig Current
	{
		get
		{
			return Held();
		}
	}

	// Preferences I/O stays outside the lock (the UI thread reads Current while the pool writes the cache); only the field swap is locked.
	private AppConfig Held()
	{
		lock (_gate)
		{
			if (_current is not null)
			{
				return _current;
			}
		}

		AppConfig fromCache = AppConfig.Parse(ReadCache());
		lock (_gate)
		{
			return _current ??= fromCache;
		}
	}

	/// <summary>The version this build reports (<c>ApplicationDisplayVersion</c>, set from the release version in CI).</summary>
	public static string InstalledVersion => AppInfo.Current.VersionString;

	public static AppPlatform ThisPlatform =>
		DeviceInfo.Platform == DevicePlatform.Android ? AppPlatform.Android
		: DeviceInfo.Platform == DevicePlatform.iOS ? AppPlatform.Ios
		: AppPlatform.Windows;

	/// <summary>True when the config in force says this build is too old to run.</summary>
	public bool UpdateRequired => Current.RequiresUpdate(ThisPlatform, InstalledVersion);

	/// <summary>True when a newer release exists; only ever a quiet line in the grown-up settings.</summary>
	public bool NewerVersionExists => Current.HasNewerVersion(ThisPlatform, InstalledVersion);

	/// <summary>
	/// Fetches the config and keeps it, at most once per 15 minutes (one minute after a failure) and never twice at once (a call while one runs waits for that one).
	/// Any failure, or a body that is not a served config, leaves the copy in force as it was; this never throws.
	/// </summary>
	public Task RefreshAsync(CancellationToken cancel = default)
	{
		lock (_gate)
		{
			if (_inFlight is { IsCompleted: false } running)
			{
				return running;
			}

			if (DateTimeOffset.UtcNow < _notBefore)
			{
				return Task.CompletedTask;
			}

			// The HTTP call must not run on the UI thread (Android throws NetworkOnMainThreadException), so the whole fetch is moved to the pool.
			return _inFlight = Task.Run(() => FetchAndApplyAsync(cancel), CancellationToken.None);
		}
	}

	private async Task FetchAndApplyAsync(CancellationToken cancel)
	{
		try
		{
			string json = await FetchAsync(cancel);
			if (!AppConfig.TryParseServed(json, out AppConfig fetched))
			{
				Debug.WriteLine("Dino Reader ignored a remote config body that is not a config.");
				SetNotBefore(RetryInterval);
				return;
			}

			ApplyFetched(json, fetched);
		}
		catch (OperationCanceledException) when (cancel.IsCancellationRequested)
		{
			// The caller gave up (shutdown): that is not a failed load, so no back-off and no retry.
			Debug.WriteLine("Dino Reader: the remote config fetch was cancelled by the caller.");
		}
		catch (Exception ex)
		{
			// Whatever went wrong (network, timeout, parsing), the copy in force stays and the fire-and-forget task never faults.
			Debug.WriteLine($"Dino Reader could not load the remote config: {ex.Message}");
			HandleFailure(ex);
		}
	}

	private void ApplyFetched(string json, AppConfig fetched)
	{
		// Load what is held before the cache is overwritten: Current reads the cache when nothing is held yet.
		Held();
		StoreCache(json);
		bool changed;
		lock (_gate)
		{
			changed = fetched != _current;
			_notBefore = DateTimeOffset.UtcNow + MinInterval;
			_connectFailures = 0;
			if (changed)
			{
				_current = fetched;
			}
		}

		if (changed)
		{
			MainThread.BeginInvokeOnMainThread(RaiseChanged);
		}
	}

	// A handler that throws must not escape into the main-thread dispatcher (that would crash the app).
	private void RaiseChanged()
	{
		try
		{
			Changed?.Invoke(this, EventArgs.Empty);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader: a remote config change handler failed: {ex.Message}");
		}
	}

	private void SetNotBefore(TimeSpan wait)
	{
		lock (_gate)
		{
			_notBefore = DateTimeOffset.UtcNow + wait;
		}
	}

	// "Never reached the server" (no connection, DNS, TLS, timeout) is retried sooner than a server-side failure.
	private static bool NeverReachedServer(Exception ex) =>
		ex is HttpRequestException { StatusCode: null } or OperationCanceledException;

	// The network is often not up yet at cold start and nothing else would call RefreshAsync again, so the client retries itself
	// after 10 s, 30 s and 60 s; after that the normal one-minute rule applies and the next resume asks again.
	private void HandleFailure(Exception ex)
	{
		TimeSpan wait = RetryInterval;
		bool retryItself = false;
		lock (_gate)
		{
			if (NeverReachedServer(ex))
			{
				retryItself = _connectFailures < ConnectBackoff.Length;
				wait = retryItself ? ConnectBackoff[_connectFailures] : RetryInterval;
				_connectFailures++;
			}

			_notBefore = DateTimeOffset.UtcNow + wait;
		}

		if (retryItself)
		{
			_ = Task.Run(() => RetryAfterDelayAsync(wait));
		}
	}

	private async Task RetryAfterDelayAsync(TimeSpan wait)
	{
		await Task.Delay(wait);
		await RefreshAsync();
	}

	private static async Task<string> FetchAsync(CancellationToken cancel)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
		timeout.CancelAfter(Timeout);
		using HttpResponseMessage response = await Http.GetAsync(new Uri(new Uri(StoreClient.BaseAddress), ConfigPath), HttpCompletionOption.ResponseHeadersRead, timeout.Token);
		response.EnsureSuccessStatusCode();
		await using Stream body = await response.Content.ReadAsStreamAsync(timeout.Token);
		using var copy = new MemoryStream();
		byte[] buffer = new byte[BufferBytes];
		int read;
		while ((read = await body.ReadAsync(buffer, timeout.Token)) > 0)
		{
			if (copy.Length + read > MaxBytes)
			{
				throw new IOException("The config is larger than allowed.");
			}

			copy.Write(buffer, 0, read);
		}

		return Encoding.UTF8.GetString(copy.GetBuffer(), 0, (int)copy.Length);
	}

	private static string? ReadCache()
	{
		try
		{
			return Preferences.Default.Get(CacheKey, (string?)null);
		}
		catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
		{
			return null;
		}
	}

	private static void StoreCache(string json)
	{
		try
		{
			Preferences.Default.Set(CacheKey, json);
		}
		catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
		{
			Debug.WriteLine($"Dino Reader could not keep the remote config: {ex.Message}");
		}
	}
}
