using UIKit;

namespace DinoReader;

/// <summary>
/// The one place iOS text attributes get their values (Slice 244): a font is never null (the system font stands
/// in for one that cannot be found, per <see cref="AttributeFallback"/>), so no attributes dictionary built by
/// Dino's own code can carry a nil value into UIKit's layout.
/// </summary>
internal static class IosAttributes
{
	private static int _fontFallbackReported;

	// Slice 273 part D: measured on every layout pass, so the report is bounded to once per process by a flag checked before any lookup or lock.
	private static void ReportFontFallbackOnce()
	{
		if (Interlocked.Exchange(ref _fontFallbackReported, 1) == 0)
		{
			DinoFallbacks.Report(ReadTheStupidText.Application.Usage.FallbackArea.Font, ReadTheStupidText.Application.Usage.FallbackReason.FontFallback);
		}
	}

	/// <summary>The named font when it exists, else the system font of the same (sanitised) size. Never null.</summary>
	public static UIFont FontOrSystem(UIFont? named, double size)
	{
		double usable = AttributeFallback.SizeFor(size);
		if (AttributeFallback.SourceFor(named is not null) == FontSource.System)
		{
			ReportFontFallbackOnce();
			return UIFont.SystemFontOfSize((nfloat)usable);
		}

		return named!;
	}

	/// <summary>Attributes holding a font and nothing else, with the font guaranteed.</summary>
	public static UIStringAttributes WithFont(UIFont? named, double size) =>
		new() { Font = FontOrSystem(named, size) };
}
