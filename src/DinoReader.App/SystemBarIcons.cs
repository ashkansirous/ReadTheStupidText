using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Picks the status and navigation bar icon colour from the page background actually drawn under
/// the bars (Dino's cream and sand pages versus the bedtime palette), not from the system night
/// mode: only the reading screen follows the phone's dark mode. Android's MainActivity supplies
/// <see cref="Applier"/>; other platforms leave it null.
/// </summary>
internal static partial class SystemBarIcons
{
	// Relative luminance at which black and white icons have equal contrast (WCAG): about 0.179.
	private const double LightBackgroundLuminance = 0.179;
	private const double SrgbLinearCutoff = 0.04045;
	private const double SrgbLinearDivisor = 12.92;
	private const double SrgbOffset = 0.055;
	private const double SrgbScale = 1.055;
	private const double SrgbGamma = 2.4;
	private const double RedWeight = 0.2126;
	private const double GreenWeight = 0.7152;
	private const double BlueWeight = 0.0722;

	private static bool? _darkIcons;

	/// <summary>Called with true when the bar icons should be dark (the page behind them is light).</summary>
	public static Action<bool>? Applier { get; set; }

	/// <summary>Follows <paramref name="page"/>'s background; the gate (a scrim over the previous page) keeps the current icons.</summary>
	public static void ForPage(Page page)
	{
		if (page is GatePage || BackgroundOf(page) is not { } background)
		{
			return;
		}

		double luminance = (RedWeight * Linear(background.Red)) + (GreenWeight * Linear(background.Green)) + (BlueWeight * Linear(background.Blue));
		bool ink = page is IInkUnderStatusBar { InkUnderStatusBar: true };
		_darkIcons = StatusBarIcons.UseDarkIcons(ink, luminance > LightBackgroundLuminance);
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[StatusBar] page={page.GetType().Name} bg={HexOf(background)} flag={ink} darkIcons={_darkIcons}"); // one line per apply: Slice 349's flow asserts the whole line (RTST-TEST tag)
#endif
		Reapply();
	}

	private static string HexOf(Color color) =>
		$"#{(int)Math.Round(color.Red * 255):X2}{(int)Math.Round(color.Green * 255):X2}{(int)Math.Round(color.Blue * 255):X2}";

	private static Color? BackgroundOf(Page page) =>
		page.BackgroundColor ?? (page.Background as SolidColorBrush)?.Color;

	private static double Linear(float channel) =>
		channel <= SrgbLinearCutoff ? channel / SrgbLinearDivisor : Math.Pow((channel + SrgbOffset) / SrgbScale, SrgbGamma);

	/// <summary>Applies the last choice again, for when the system reset the bars (a UI mode change).</summary>
	public static void Reapply()
	{
		if (_darkIcons is { } dark)
		{
			Applier?.Invoke(dark);
			ApplyPlatform(dark);
		}
	}

	/// <summary>The iOS twin of <see cref="Applier"/> (Platforms/iOS); other platforms do nothing.</summary>
	static partial void ApplyPlatform(bool darkIcons);
}

/// <summary>A page whose header is ink under the status bar (the grown-ups' corner on a phone held upright): its icons are light whatever its own background is.</summary>
internal interface IInkUnderStatusBar
{
	bool InkUnderStatusBar { get; }
}
