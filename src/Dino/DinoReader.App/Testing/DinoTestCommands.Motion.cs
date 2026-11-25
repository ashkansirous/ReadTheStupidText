#if DEBUG
using Sirous.Reading.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Slice 433 item 6 (the App Store preview clips): Maestro's <c>swipe</c> moves nothing on iOS 27.2, so the recorded flows move the app through these two hooks, on Android and iOS alike.
/// <c>turn-page [dir=next|prev]</c> turns the open book one page with the surface's own swipe code (<c>ReadingSurface.TurnPage</c>): <c>HOOK turn-page ok page=&lt;n&gt;</c> (1-based, the page now shown)
/// or <c>failed &lt;reason&gt;</c> (bad dir, no book open, first or last page). <c>scroll-shelf by=&lt;fraction of the visible height&gt; [ms=&lt;duration&gt;]</c> scrolls the books list by that much, easing like a finger
/// drag, and returns at once: <c>HOOK scroll-shelf ok</c> when the move is done, or <c>failed &lt;reason&gt;</c>; a flow holds for the duration with an optional wait.
/// </summary>
internal static partial class DinoTestCommands
{
	private const string TurnPageName = "turn-page";
	private const string ScrollShelfName = "scroll-shelf";
	private const string DirKey = "dir";
	private const string ByKey = "by";
	private const string DurationKey = "ms";

	private static string MotionHook(TestHookCommand command)
	{
		_ = command.Name == TurnPageName
			? TurnPageAsync(command.Get(DirKey, string.Empty))
			: ScrollShelfAsync(command.Get(ByKey, string.Empty), command.Get(DurationKey, string.Empty));
		return StartedOutcome;
	}

	private static async Task TurnPageAsync(string dir)
	{
		try
		{
			bool? next = ShelfScrollPlan.ParseDirection(dir);
			ReadingPage? reading = next is null ? null : await MainThread.InvokeOnMainThreadAsync(() => GalleryRoot()?.Navigation.NavigationStack.LastOrDefault() as ReadingPage);
			if (next is null || reading is null)
			{
				Log(next is null ? $"{TurnPageName} failed bad dir {dir}" : $"{TurnPageName} failed no book open");
				return;
			}

			int page = await MainThread.InvokeOnMainThreadAsync(() => reading.DebugTurnPage(next.Value));
			Log(page > 0 ? $"{TurnPageName} ok page={page}" : $"{TurnPageName} failed no page {(next.Value ? "after" : "before")} this one");
		}
		catch (Exception ex)
		{
			// no-fallback: Debug test hook, logs the fault
			Log($"{TurnPageName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	private static async Task ScrollShelfAsync(string by, string ms)
	{
		try
		{
			if (!ShelfScrollPlan.TryParse(by, ms, out double fraction, out int milliseconds, out string error))
			{
				Log($"{ScrollShelfName} failed {error}");
				return;
			}

			GalleryPage? gallery = GalleryRoot()?.RootPage as GalleryPage;
			string why = gallery is null ? "no gallery" : await MainThread.InvokeOnMainThreadAsync(() => gallery.DebugScrollShelfAsync(fraction, milliseconds));
			Log(why.Length == 0 ? $"{ScrollShelfName} ok" : $"{ScrollShelfName} failed {why}");
		}
		catch (Exception ex)
		{
			// no-fallback: Debug test hook, logs the fault
			Log($"{ScrollShelfName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}
}
#endif
