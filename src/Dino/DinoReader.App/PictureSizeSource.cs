using System.Diagnostics;
using DinoReader.Core;
using Sirous.Reading.Application.Reading;

namespace DinoReader;

/// <summary>
/// Slice 375 C: reads this device and its connection and gives <see cref="PictureSizeChoice"/> its inputs. The device class is per device, never per window: Android by the display's shorter
/// side in dp (<see cref="LandscapeLayout.TabletShortSide"/>), iOS by the idiom, Windows is a desktop. The connection is unmetered only when <see cref="IPackUpdateNetwork"/> says so (Wi-Fi or
/// Ethernet; on Android also not a metered network, on Windows the connection cost is read, on iOS an expensive or Low Data Mode path counts as limited); mobile data, a metered link and roaming are limited; no speed is measured.
/// </summary>
public sealed class PictureSizeSource(IPackUpdateNetwork network)
{
	private PictureDeviceClass? _device;

	private int _watching;

	/// <summary>Slice 375 D: the connection changed. On Android and Windows this is MAUI's ConnectivityChanged; on iOS it is the path monitor's own snapshot handler, raised after the path it delivered is stored, so a handler that reads <see cref="Network"/> sees that same path.</summary>
	public event EventHandler? ConnectionChanged;

	/// <summary>Starts watching the connection (once; called at app start and by the upgrade runner): on iOS the path monitor then already has a path at the first read.</summary>
	public void StartWatching()
	{
		if (Interlocked.Exchange(ref _watching, 1) == 0)
		{
			StartPlatformWatch();
		}
	}

	private void RaiseConnectionChanged() => ConnectionChanged?.Invoke(this, EventArgs.Empty);

	/// <summary>The class of this device, read once.</summary>
	public PictureDeviceClass Device => _device ??= ReadDevice();

	/// <summary>The connection now.</summary>
	public PictureNetwork Network()
	{
#if IOS
		return PlatformNetwork(); // iOS reads only the path the latest NWPathMonitor snapshot delivered, never MAUI Connectivity
#else
		if (!IsOnline())
		{
			return PictureNetwork.None;
		}

		return IsUnmetered() ? PictureNetwork.Unmetered : PictureNetwork.Limited;
#endif
	}

	/// <summary>The pack this device takes for <paramref name="item"/> right now; the decision is logged (Debug) so a device check can read which path ran.</summary>
	public PictureSize ChoiceFor(StoreItem item) => Decide(item, Network(), "choice");

	private PictureSize Decide(StoreItem item, PictureNetwork connection, string what)
	{
		PictureSize size = PictureSizeChoice.Choose(Device, connection, item.HasHighRes);
		HighResLog.Write($"{what} {item.Id} v{item.Version} device={Device} network={connection} highres={item.HasHighRes} size={size}");
		return size;
	}

	private static PictureDeviceClass ReadDevice()
	{
		PictureDevicePlatform platform = DeviceInfo.Platform == DevicePlatform.WinUI ? PictureDevicePlatform.Windows
			: DeviceInfo.Platform == DevicePlatform.iOS ? PictureDevicePlatform.Ios
			: PictureDevicePlatform.Android;
		DisplayInfo display = DeviceDisplay.Current.MainDisplayInfo;
		double shortSideDp = display.Density > 0 ? Math.Min(display.Width, display.Height) / display.Density : 0;
		return PictureSizeChoice.DeviceClassOf(platform, shortSideDp, DeviceInfo.Idiom == DeviceIdiom.Tablet);
	}

#if !IOS
	private static bool IsOnline()
	{
		try
		{
			return Connectivity.Current.NetworkAccess != NetworkAccess.None;
		}
		catch (Exception ex)
		{
			// no-fallback: a platform that cannot say is treated as connected; a failed fetch reports itself
			Debug.WriteLine($"[HighRes] could not read the network access: {ex.Message}");
			return true;
		}
	}

	private bool IsUnmetered()
	{
		try
		{
			return network.AllowsAutomaticUpdate && !PlatformMetered();
		}
		catch (Exception ex)
		{
			// no-fallback: a connection that cannot be read counts as limited, so the standard pack comes first and the upgrade waits
			Debug.WriteLine($"[HighRes] could not read the connection cost: {ex.Message}");
			return false;
		}
	}

#endif

#if ANDROID
	private void StartPlatformWatch() => Connectivity.Current.ConnectivityChanged += (_, _) => RaiseConnectionChanged();

	private bool PlatformMetered() =>
		Android.App.Application.Context.GetSystemService(Android.Content.Context.ConnectivityService) is Android.Net.ConnectivityManager manager && manager.IsActiveNetworkMetered;
#elif WINDOWS
	private void StartPlatformWatch() => Connectivity.Current.ConnectivityChanged += (_, _) => RaiseConnectionChanged();

	private bool PlatformMetered() => !new Sirous.Reading.Infrastructure.Translation.WindowsPackUpdateNetwork().AllowsAutomaticUpdate;
#elif IOS
	// Slice 375 D (iOS connection reading): an expensive link (Personal Hotspot, cellular) or Low Data Mode is limited, like Android's metered check. The trigger is the NWPathMonitor snapshot handler itself
	// and the decision reads the path that snapshot delivered, so the event and the reading always agree. Before the first snapshot the connection counts as limited.
	private IosPathWatcher? _pathWatcher;

	private void StartPlatformWatch() => _pathWatcher = new IosPathWatcher(RaiseConnectionChanged);

	// Unsatisfied status is None; an expensive or constrained path or one over a cellular interface is Limited; otherwise Unmetered. No snapshot yet counts as limited.
	private PictureNetwork PlatformNetwork() => _pathWatcher?.Latest is not { } path ? PictureNetwork.Limited
		: path.Status != global::Network.NWPathStatus.Satisfied ? PictureNetwork.None
		: path.IsExpensive || path.IsConstrained || path.UsesInterfaceType(global::Network.NWInterfaceType.Cellular) ? PictureNetwork.Limited
		: PictureNetwork.Unmetered;

	private sealed class IosPathWatcher
	{
		private readonly Network.NWPathMonitor _monitor = new();
		private volatile Network.NWPath? _latest;

		public IosPathWatcher(Action changed)
		{
			_monitor.SnapshotHandler = path =>
			{
				_latest = path; // stored first: the handler below reads it back through Network()
				changed();
			};
			_monitor.SetQueue(CoreFoundation.DispatchQueue.DefaultGlobalQueue);
			_monitor.Start();
		}

		public Network.NWPath? Latest => _latest;
	}
#else
	private void StartPlatformWatch() => Connectivity.Current.ConnectivityChanged += (_, _) => RaiseConnectionChanged();

	private bool PlatformMetered() => false;
#endif
}
