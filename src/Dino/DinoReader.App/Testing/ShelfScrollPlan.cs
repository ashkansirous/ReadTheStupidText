#if DEBUG || DINO_TEST_BOOKS // Debug app builds and the DinoReader.Tests link (it defines DINO_TEST_BOOKS); never in a Release app
namespace DinoReader.Testing;

/// <summary>
/// Pure part of the Debug <c>scroll-shelf by=&lt;fraction&gt; ms=&lt;duration&gt;</c> hook (Slice 433 item 6): reads its two arguments and cuts the move into per-frame pixel steps that ease in and out
/// like a finger drag. Maestro's <c>swipe</c> moves nothing on iOS 27.2, so the recorded shelf clip scrolls through this instead.
/// </summary>
internal static class ShelfScrollPlan
{
	internal const int DefaultMilliseconds = 900;
	internal const int FrameMilliseconds = 16;
	internal const int MinMilliseconds = 100;
	internal const int MaxMilliseconds = 10000;
	internal const double MaxFraction = 5;

	/// <summary>False (with the reason) when <paramref name="by"/> is not a number between -5 and 5 other than 0, or <paramref name="ms"/> is given and not 100-10000; an empty <paramref name="ms"/> means the default.</summary>
	internal static bool TryParse(string by, string ms, out double fraction, out int milliseconds, out string error)
	{
		milliseconds = DefaultMilliseconds;
		error = string.Empty;
		if (!double.TryParse(by, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out fraction) || !double.IsFinite(fraction) || fraction == 0 || Math.Abs(fraction) > MaxFraction)
		{
			error = $"bad by {by}";
			return false;
		}

		if (ms.Length > 0 && (!int.TryParse(ms, out milliseconds) || milliseconds < MinMilliseconds || milliseconds > MaxMilliseconds))
		{
			error = $"bad ms {ms}";
			return false;
		}

		return true;
	}

	/// <summary>The pixels to move in each frame, easing in and out (smoothstep) and adding up to exactly <paramref name="totalPixels"/>.</summary>
	internal static IReadOnlyList<int> FrameDeltas(int totalPixels, int milliseconds)
	{
		int frames = Math.Max(1, milliseconds / FrameMilliseconds);
		var deltas = new List<int>(frames);
		int applied = 0;
		for (int frame = 1; frame <= frames; frame++)
		{
			double t = (double)frame / frames;
			int reached = (int)Math.Round(totalPixels * t * t * (3 - 2 * t));
			deltas.Add(reached - applied);
			applied = reached;
		}

		return deltas;
	}

	/// <summary>True for <c>next</c> (or no value), false for <c>prev</c>; null for anything else, so the turn-page hook fails loudly on a typo.</summary>
	internal static bool? ParseDirection(string dir) => dir switch
	{
		"" or "next" => true,
		"prev" => false,
		_ => null,
	};
}
#endif
