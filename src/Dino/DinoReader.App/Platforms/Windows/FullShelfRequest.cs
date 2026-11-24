#if DINO_TEST_BOOKS
using DinoReader.Testing;
using Microsoft.Maui.Platform;
using Microsoft.UI.Windowing;

namespace DinoReader;

/// <summary>
/// Slice 434: the Windows VM's way to the <c>full-shelf</c> hook. A Release test package (<c>-p:DinoTestBooks=true</c>) has no hook dispatcher, so the store-shot check drops a one-word
/// <see cref="FileName"/> into the app data folder and this watcher, started by the head's <c>App</c>, deletes it and runs the hook; its <c>HOOK full-shelf ...</c> outcome lines go to
/// <c>window-trace.log</c> (Debug builds keep the dispatcher and ignore the file). Compiled out of store builds.
/// </summary>
internal static class FullShelfRequest
{
	/// <summary>The request file the check writes into the app data folder.</summary>
	internal const string FileName = "full-shelf.request";

	/// <summary>The request file of the story-language hook: its text is the language code (<c>en</c>, <c>es</c>, <c>ja</c>).</summary>
	internal const string LanguageFileName = "story-language.request";

	/// <summary>The request file of the full-screen hook (Slice 434): any text; the window then fills the whole display with no frame or title bar.</summary>
	internal const string FullScreenFileName = "fullscreen.request";

	/// <summary>The request file of the open-book hook (Slice 434): its text is a story id; the book's reading page opens as after a tile tap.</summary>
	internal const string OpenBookFileName = "open-book.request";

	private const string FullScreenHookName = "fullscreen";

	private const int PollMilliseconds = 500;

	internal static void Start()
	{
#if !DEBUG
		DinoTestCommands.ReleaseSink = WindowTrace.Write;
#endif
		_ = Task.Run(WatchAsync);
	}

	private static async Task WatchAsync()
	{
		while (true)
		{
			await Task.Delay(PollMilliseconds);
			if (IPlatformApplication.Current?.Services is null)
			{
				continue; // the app is still starting
			}

			await PollOnceAsync();
		}
	}

	// One pass over both request files; a fault is written to the trace and never ends the watcher, so a later request is still served.
	private static async Task PollOnceAsync()
	{
		try
		{
			if (TakeRequest(FileName) is not null)
			{
				await RunAsync();
			}

			if (TakeRequest(LanguageFileName) is { } code)
			{
				string outcome = await MainThread.InvokeOnMainThreadAsync(() => StoryLanguageHook.Apply(code.Trim())); // SaveSides raises an event the UI listens to
				WindowTrace.Write($"HOOK {StoryLanguageHook.Name} {outcome}");
			}

			if (TakeRequest(FullScreenFileName) is not null)
			{
				string outcome = await MainThread.InvokeOnMainThreadAsync(EnterFullScreen); // the presenter belongs to the UI thread
				WindowTrace.Write($"HOOK {FullScreenHookName} {outcome}");
			}

			if (TakeRequest(OpenBookFileName) is { } bookId)
			{
				string outcome = await MainThread.InvokeOnMainThreadAsync(() => OpenBookRequestHook.OpenAsync(bookId.Trim())); // the navigation stack belongs to the UI thread
				WindowTrace.Write($"HOOK {OpenBookRequestHook.Name} {outcome}");
			}
		}
		catch (Exception ex)
		{
			// no-fallback: test-only watcher; the fault is written to the trace and the next poll goes on
			WindowTrace.Write($"HOOK request fault {ex.GetType().Name}: {ex.Message.ReplaceLineEndings(" ")}");
		}
	}

	// Slice 434: puts the first MAUI window in the FullScreen presenter, so its client area is the whole display (1920 x 1080 for the store shots) with no frame or title bar. Returns "ok <w>x<h>" or "failed <why>".
	private static string EnterFullScreen()
	{
		if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView is not MauiWinUIWindow native)
		{
			return "failed no native window";
		}

		AppWindow? appWindow = native.GetAppWindow();
		if (appWindow is null)
		{
			return "failed no AppWindow";
		}

		appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
		string zoom = ApplyZoom(native, appWindow.Size.Width, appWindow.Size.Height);
		return $"ok {appWindow.Size.Width}x{appWindow.Size.Height} {zoom}";
	}

	// Slice 434: the VM cannot set a 150% display scale, so the root XAML element is laid out at 1/1.5 of the display (1280 x 720) and drawn 1.5x from the top-left: the 1920 x 1080 capture then shows the app as a
	// laptop at 150% would (text and shapes are re-rasterized at the larger scale by XAML, so they stay sharp; bitmaps are scaled from the size they were decoded at). Returns "zoom <factor>" or "zoom failed <why>".
	private static string ApplyZoom(MauiWinUIWindow native, int displayWidth, int displayHeight)
	{
		if (native.Content is not Microsoft.UI.Xaml.FrameworkElement root)
		{
			return "zoom failed no root element";
		}

		root.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left;
		root.VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top;
		root.Width = displayWidth / StoreZoom;
		root.Height = displayHeight / StoreZoom;
		root.RenderTransform = new Microsoft.UI.Xaml.Media.ScaleTransform { ScaleX = StoreZoom, ScaleY = StoreZoom };
		return $"zoom {StoreZoom.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture)}";
	}

	private const double StoreZoom = 1.5;

	// The request file's text, or null when there is none or the writer has not put its text in yet (read again on the next poll); the file is deleted once its text was read.
	private static string? TakeRequest(string fileName)
	{
		try
		{
			string path = Path.Combine(FileSystem.AppDataDirectory, fileName);
			if (!File.Exists(path))
			{
				return null;
			}

			string text = File.ReadAllText(path);
			if (string.IsNullOrWhiteSpace(text))
			{
				return null; // created but not yet written (Set-Content creates, then writes)
			}

			File.Delete(path);
			return text;
		}
		catch (IOException ex)
		{
			// no-fallback: test-only watcher; a locked request file is tried again on the next poll and noted in the trace
			WindowTrace.Write($"HOOK request {fileName} {ex.GetType().Name}: {ex.Message}");
			return null;
		}
	}

	private static async Task RunAsync()
	{
		WindowTrace.Write("HOOK full-shelf requested");
#if !DEBUG
		await MainThread.InvokeOnMainThreadAsync(DinoTestCommands.RunFullShelfAsync); // the shelf and store state it touches belong to the UI thread; outcome lines reach window-trace.log through ReleaseSink
#else
		await Task.CompletedTask; // Debug builds run the hook through the dispatcher, not this file
#endif
	}
}
#endif
