using System.Runtime.InteropServices;
using DinoReader.Core;
using Microsoft.Maui.Platform;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace DinoReader;

/// <summary>
/// The size, place and title bar of Dino Reader's Windows window (Slice 235 part A): opens at 1100 x 760 dp (or where it was
/// closed), cannot be made smaller than 800 x 560 dp, remembers its size, place and maximised state, and has a cream
/// title bar. The rectangle rules are pure (<see cref="WindowPlacement"/>); this class only reads and applies them.
/// Windows only: phones and tablets never see it. Each branch is traced to window-trace.log (<see cref="WindowTrace"/>), which works in Release.
/// </summary>
internal sealed class DinoWindowFrame
{
	/// <summary>The one Preferences key: <see cref="SavedWindow.ToText"/>.</summary>
	internal const string PlacementKey = "dino.window.placement";

	private const double BaseDpi = 96.0;
	private const int SaveDelayMs = 500;
	private const int EffectiveDpi = 0; // MDT_EFFECTIVE_DPI

	private readonly AppWindow _appWindow;
	private readonly DispatcherQueueTimer _saveTimer;
	private RectInt32 _restored;
	private bool _dirty;
	private bool _maximised;
	private double _minimumScale = 1.0;

	private DinoWindowFrame(AppWindow appWindow, DispatcherQueue queue)
	{
		_appWindow = appWindow;
		_saveTimer = queue.CreateTimer();
		_saveTimer.Interval = TimeSpan.FromMilliseconds(SaveDelayMs);
		_saveTimer.IsRepeating = false;
		_saveTimer.Tick += (_, _) => Save();
	}

	/// <summary>Applies the frame when the MAUI window gets its native window (before it is first shown).</summary>
	public static void Attach(Microsoft.Maui.Controls.Window window)
	{
		bool applied = false; // a second event must not snap the window back or stack a second frame
		void OnReady(object? sender, string hook)
		{
			bool native = sender is Microsoft.Maui.Controls.Window { Handler.PlatformView: MauiWinUIWindow };
			WindowTrace.Write($"attach hook {hook} fired, native window {(native ? "found" : "missing")}, already applied {applied}");
			if (!applied && sender is Microsoft.Maui.Controls.Window { Handler.PlatformView: MauiWinUIWindow nativeWindow })
			{
				applied = TryApply(nativeWindow); // stays false after a failure, so the other hook can retry
			}
		}

		// Window.Created is the documented "native window exists" event (context7 /dotnet/maui); HandlerChanged stays as a fallback.
		window.Created += (sender, _) => OnReady(sender, nameof(Microsoft.Maui.Controls.Window.Created));
		window.HandlerChanged += (sender, _) => OnReady(sender, nameof(Microsoft.Maui.Controls.Window.HandlerChanged));
	}

	private static bool TryApply(MauiWinUIWindow native)
	{
		try
		{
			AppWindow? appWindow = native.GetAppWindow();
			WindowTrace.Write($"AppWindow {(appWindow is null ? "not found" : "found")}, dispatcher {(native.DispatcherQueue is null ? "missing" : "present")}");
			if (appWindow is null || native.DispatcherQueue is null)
			{
				WindowTrace.Write("Dino window: no AppWindow, the default frame stays");
				return false;
			}

			new DinoWindowFrame(appWindow, native.DispatcherQueue).Apply(native);
			return true;
		}
		catch (Exception ex)
		{
			string site = ex.StackTrace?.Split('\n', 2)[0].Trim() ?? "no stack";
			WindowTrace.Write($"Dino window: could not apply the frame: {ex.GetType().Name}: {ex.Message} at {site}");
			return false;
		}
	}

	private void Apply(MauiWinUIWindow native)
	{
		SavedWindow? saved = SavedWindow.TryParse(Microsoft.Maui.Storage.Preferences.Default.Get(PlacementKey, string.Empty));
		List<DisplayArea> displays = DisplaysPrimaryFirst();
		double scale = ScaleOfDisplay(TargetDisplay(saved, displays), ScaleOf(native));
		ApplyMinimum(scale);
		ApplyTitleBar();
		WindowOpening opening = Opening(saved, displays, scale);
		_restored = ToRect(opening.Bounds);
		_maximised = opening.Maximised;
		_appWindow.MoveAndResize(_restored); // screen coordinates, the same as the saved and computed rectangles
		WindowTrace.Write($"opening rect {_restored.X},{_restored.Y} {_restored.Width} x {_restored.Height}; window now {_appWindow.Size.Width} x {_appWindow.Size.Height}");
		if (opening.Maximised && _appWindow.Presenter is OverlappedPresenter presenter)
		{
			presenter.Maximize();
			WindowTrace.Write("Dino window: opened maximised");
		}

		WindowTrace.Write($"Dino window: opened {opening.Bounds} scale {scale:0.00}");
		_appWindow.Changed += OnChanged;
		_appWindow.Closing += (_, _) => Save();
	}

	private void ApplyMinimum(double scale)
	{
		if (_appWindow.Presenter is OverlappedPresenter presenter)
		{
			// The docs do not state the unit of PreferredMinimum*; AppWindow sizes are physical pixels, so scale 800 x 560 dp
			// by the display's DPI like the placement does (Slice 235 review). Re-applied when the window changes display scale.
			int width = Scaled(WindowLayout.MinimumWindowWidth, scale);
			int height = Scaled(WindowLayout.MinimumWindowHeight, scale);
			presenter.PreferredMinimumWidth = width;
			presenter.PreferredMinimumHeight = height;
			_minimumScale = scale;
			WindowTrace.Write($"Dino window: minimum {width} x {height} px (scale {scale:0.00})");
		}
	}

	private void RefreshMinimumForDisplay()
	{
		// A minimised window sits near -32000,-32000, so its nearest display says nothing about where it will restore.
		if (_appWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized })
		{
			return;
		}

		DisplayArea display = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest);
		double scale = ScaleOfDisplay(display, _minimumScale);
		if (Math.Abs(scale - _minimumScale) > 0.001)
		{
			ApplyMinimum(scale);
		}
	}

	// The standard 32 px title bar with the app icon and name, in the kids' cream; per-page colours come in part B.
	private void ApplyTitleBar()
	{
		if (!AppWindowTitleBar.IsCustomizationSupported())
		{
			WindowTrace.Write("Dino window: title bar colours not supported, system colours stay");
			return;
		}

		global::Windows.UI.Color cream = DinoTokens.Get(DinoTokens.Cream).ToWindowsColor();
		global::Windows.UI.Color ink = DinoTokens.Get(DinoTokens.Ink).ToWindowsColor();
		AppWindowTitleBar bar = _appWindow.TitleBar;
		bar.BackgroundColor = cream;
		bar.InactiveBackgroundColor = cream;
		bar.ForegroundColor = ink;
		bar.ButtonBackgroundColor = cream;
		bar.ButtonInactiveBackgroundColor = cream;
		bar.ButtonForegroundColor = ink;
	}

	// DisplayArea.FindAll() throws InvalidCastException when enumerated (foreach or LINQ), microsoft/microsoft-ui-xaml issue #6454
	// (no fix is documented there), so read it by Count and indexer, and fall back to the window's own display plus the primary one.
	private List<DisplayArea> DisplaysPrimaryFirst()
	{
		var found = new List<DisplayArea>();
		try
		{
			var all = DisplayArea.FindAll();
			for (int i = 0; i < all.Count; i++)
			{
				found.Add(all[i]);
			}
		}
		catch (Exception ex)
		{
			WindowTrace.Write($"Dino window: DisplayArea.FindAll could not be read ({ex.GetType().Name}), using the primary and the window's display");
			found.Clear();
			found.Add(DisplayArea.Primary);
			found.Add(DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest));
		}

		return found.GroupBy(d => d.DisplayId.Value).Select(g => g.First()).OrderBy(d => d.IsPrimary ? 0 : 1).ToList();
	}

	// The display the window will open on: the saved one while the saved place is still reachable, else the main display.
	private static DisplayArea TargetDisplay(SavedWindow? saved, List<DisplayArea> displays)
	{
		var areas = displays.Select(d => FromRect(d.WorkArea)).ToList();
		bool reachable = saved is not null && WindowPlacement.IsOnAnyDisplay(saved.Bounds, areas);
		return reachable ? DisplayArea.GetFromRect(ToRect(saved!.Bounds), DisplayAreaFallback.Nearest) : displays.FirstOrDefault() ?? DisplayArea.Primary;
	}

	private static WindowOpening Opening(SavedWindow? saved, List<DisplayArea> displays, double scale)
	{
		var areas = displays.Select(d => FromRect(d.WorkArea)).ToList();
		var defaults = (Scaled(WindowLayout.DefaultWindowWidth, scale), Scaled(WindowLayout.DefaultWindowHeight, scale));
		var minimum = (Scaled(WindowLayout.MinimumWindowWidth, scale), Scaled(WindowLayout.MinimumWindowHeight, scale));
		WindowOpening opening = WindowPlacement.Resolve(saved, areas, defaults, minimum);
		WindowTrace.Write(saved is null ? "Dino window: nothing saved, default size centred" : $"Dino window: restoring {saved.ToText()}");
		return opening;
	}

	private void OnChanged(AppWindow sender, AppWindowChangedEventArgs args)
	{
		if (!args.DidSizeChange && !args.DidPositionChange && !args.DidPresenterChange)
		{
			return;
		}

		if (args.DidSizeChange && EnforceMinimum(sender))
		{
			return; // the resize back up raises Changed again, which tracks and saves the corrected size
		}

		TrackState(sender);
		if (args.DidPositionChange)
		{
			RefreshMinimumForDisplay();
		}

		_dirty = true;
		_saveTimer.Stop();
		_saveTimer.Start(); // debounced: a drag or resize saves once, 500 ms after it settles
	}

	// PreferredMinimum* only bounds a user drag; a programmatic resize (UI Automation) can go below it, so resize back up.
	private bool EnforceMinimum(AppWindow sender)
	{
		if (sender.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Restored })
		{
			return false; // minimised or maximised: the size says nothing about the restored window
		}

		var minimum = (Scaled(WindowLayout.MinimumWindowWidth, _minimumScale), Scaled(WindowLayout.MinimumWindowHeight, _minimumScale));
		(int Width, int Height)? target = WindowMinimum.Enforce((sender.Size.Width, sender.Size.Height), minimum);
		if (target is null)
		{
			return false;
		}

		sender.Resize(new SizeInt32(target.Value.Width, target.Value.Height));
		WindowTrace.Write($"Dino window: raised to the minimum {target.Value.Width} x {target.Value.Height} px");
		return true;
	}

	// Remembers the last Restored/Maximized state; a minimised window keeps the state it was minimised from.
	private void TrackState(AppWindow sender)
	{
		if (sender.Presenter is not OverlappedPresenter presenter)
		{
			return;
		}

		if (presenter.State == OverlappedPresenterState.Restored)
		{
			_restored = new RectInt32(sender.Position.X, sender.Position.Y, sender.Size.Width, sender.Size.Height);
			_maximised = false;
		}
		else if (presenter.State == OverlappedPresenterState.Maximized)
		{
			_maximised = true;
		}
	}

	private void Save()
	{
		_saveTimer.Stop();
		if (!_dirty)
		{
			return;
		}

		_dirty = false;
		var saved = new SavedWindow(FromRect(_restored), _maximised);
		Microsoft.Maui.Storage.Preferences.Default.Set(PlacementKey, saved.ToText());
		WindowTrace.Write($"Dino window: saved {saved.ToText()}");
	}

	private static double ScaleOf(MauiWinUIWindow native)
	{
		uint dpi = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(native));
		return dpi == 0 ? 1.0 : dpi / BaseDpi;
	}

	// The scale of one display (its effective DPI / 96), or the fallback when Windows will not say.
	private static double ScaleOfDisplay(DisplayArea display, double fallback)
	{
		IntPtr monitor = Microsoft.UI.Win32Interop.GetMonitorFromDisplayId(display.DisplayId);
		uint dpiX = 0;
		bool ok = monitor != IntPtr.Zero && GetDpiForMonitor(monitor, EffectiveDpi, out dpiX, out _) == 0 && dpiX != 0;
		return ok ? dpiX / BaseDpi : fallback;
	}

	private static int Scaled(int dp, double scale) => (int)Math.Round(dp * scale);

	private static PixelRect FromRect(RectInt32 r) => new(r.X, r.Y, r.Width, r.Height);

	private static RectInt32 ToRect(PixelRect r) => new(r.X, r.Y, r.Width, r.Height);

	[DllImport("user32.dll")]
	private static extern uint GetDpiForWindow(IntPtr hwnd);

	[DllImport("shcore.dll")]
	private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}
