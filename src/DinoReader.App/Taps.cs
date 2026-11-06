using FallbackArea = ReadTheStupidText.Application.Usage.FallbackArea;
using FallbackReason = ReadTheStupidText.Application.Usage.FallbackReason;

namespace DinoReader;

/// <summary>The one way a view in this app gets a tap handler (moved out of the cast screen's helpers in Slice 278 part B).</summary>
internal static class Taps
{
	public static void AddTap(View view, Func<Task> tapped)
	{
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) => await RunAsync(tapped);
		view.GestureRecognizers.Add(tap);
	}

	// An async-void handler must never throw: a failed tap is logged and the app carries on.
	private static async Task RunAsync(Func<Task> tapped)
	{
		try
		{
			await tapped();
		}
		catch (OperationCanceledException)
		{
			// no-fallback: a cancelled tap is not a failure and is not counted
		}
		catch (Exception ex)
		{
			// no-fallback: Report below counts it as Ui/TapFailed (inside a guard, so the report cannot throw out of the handler)
			Report(ex);
		}
	}

	// Counted, so a tap that does nothing is not invisible in Release; the report itself can never throw out of the async-void handler.
	private static void Report(Exception ex)
	{
		try
		{
			DinoFallbacks.Report(FallbackArea.Ui, FallbackReason.TapFailed);
			System.Diagnostics.Debug.WriteLine($"Dino Reader tap failed: {ex}");
		}
		catch (Exception reportEx)
		{
			// no-fallback: reporting failed; the tap already failed and the app carries on
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not report a failed tap: {reportEx.Message}");
		}
	}
}
