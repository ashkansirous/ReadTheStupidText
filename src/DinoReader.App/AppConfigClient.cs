using DinoReader.Core;
using ReadTheStupidText.Mobile.Shared.Usage;

namespace DinoReader;

/// <summary>
/// Dino's remote switchboard on the phone (Slice 132, Decisions 138-139). It fetches <c>GET /api/app-config</c> from
/// <see cref="StoreClient.BaseAddress"/>, keeps the last good copy in preferences and answers from that copy when the
/// phone is offline or the server fails; with no copy at all the answer is <see cref="AppConfig.Default"/> (no forced update,
/// telemetry off). The request is a plain GET: it sends nothing about the user and keeps no cookies. The fetch, cache and
/// back-off live in the shared <see cref="RemoteConfigClient{TConfig}"/> (Slice 137, also used by Read The Stupid Text); this class
/// adds Dino's version rules on top.
/// </summary>
public sealed class AppConfigClient
{
	private const string CacheKey = "dino.appconfig.json";
	private const string LogName = "Dino Reader";

	private readonly RemoteConfigClient<AppConfig> _client = new(new RemoteConfigOptions<AppConfig>(
		StoreClient.BaseAddress, CacheKey, LogName, AppConfig.Default, AppConfig.Parse, AppConfig.TryParseServed));

	/// <summary>
	/// Raised when a fetch brought a config that differs from the one held. Always raised on the main thread (by the client's own
	/// dispatch, never directly from the pool), so subscribers may touch UI; a caller awaiting <see cref="RefreshAsync"/> cannot assume handlers have run yet.
	/// </summary>
	public event EventHandler? Changed
	{
		add => _client.Changed += value;
		remove => _client.Changed -= value;
	}

	/// <summary>The config in force: the last good copy, else the safe default.</summary>
	public AppConfig Current => _client.Current;

	/// <summary>True once a real config is held (fetched, or a kept copy); false while <see cref="Current"/> is only the default.</summary>
	public bool HasCopy => _client.HasCopy;

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
	public Task RefreshAsync(CancellationToken cancel = default) => _client.RefreshAsync(cancel);
}
