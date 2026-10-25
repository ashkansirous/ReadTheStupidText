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

	private AppConfig? _current;
	private Task? _inFlight;
	private DateTimeOffset _notBefore = DateTimeOffset.MinValue;

	/// <summary>Raised on the calling thread of <see cref="RefreshAsync"/> when a fetch brought a config that differs from the one held.</summary>
	public event EventHandler? Changed;

	/// <summary>The config in force: the last good copy, else the safe default.</summary>
	public AppConfig Current => _current ??= AppConfig.Parse(ReadCache());

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
		if (_inFlight is { IsCompleted: false } running)
		{
			return running;
		}

		if (DateTimeOffset.UtcNow < _notBefore)
		{
			return Task.CompletedTask;
		}

		return _inFlight = FetchAndApplyAsync(cancel);
	}

	private async Task FetchAndApplyAsync(CancellationToken cancel)
	{
		try
		{
			string json = await FetchAsync(cancel);
			if (!AppConfig.TryParseServed(json, out AppConfig fetched))
			{
				Debug.WriteLine("Dino Reader ignored a remote config body that is not a config.");
				_notBefore = DateTimeOffset.UtcNow + RetryInterval;
				return;
			}

			// Compare before the cache is written: Current reads the cache when nothing is held yet.
			bool changed = fetched != Current;
			StoreCache(json);
			_notBefore = DateTimeOffset.UtcNow + MinInterval;
			if (changed)
			{
				_current = fetched;
				Changed?.Invoke(this, EventArgs.Empty);
			}
		}
		catch (Exception ex)
		{
			// Whatever went wrong (network, parsing, a Changed handler), the copy in force stays and the fire-and-forget task never faults.
			Debug.WriteLine($"Dino Reader could not load the remote config: {ex.Message}");
			_notBefore = DateTimeOffset.UtcNow + RetryInterval;
		}
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
