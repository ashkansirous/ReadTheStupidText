using Microsoft.Extensions.DependencyInjection;
using Sirous.Reading.Application.Usage;

namespace DinoReader;

/// <summary>
/// Dino Reader's answer to the shared readers and translators (Slice 273 part B): a fallback they report goes to <see cref="UsageCounter"/> as a count about no book, once per
/// (area, reason, language) in the session with a count. Fallbacks that know their story call <see cref="UsageCounter.RecordFallback"/> directly. Never throws.
/// </summary>
public sealed class DinoFallbackSink(UsageCounter usage) : IFallbackSink
{
	public void Report(string area, string reason, string? language = null)
	{
		try
		{
			usage.RecordGeneralFallback(area, reason, language);
		}
		catch (Exception)
		{
			// no-fallback: counting is best effort and never reports itself
			// Counting is best effort: it never changes what the fallback does.
		}
	}
}

/// <summary>
/// Slice 273 part D: reports a fallback from static platform code (the store updater, the window frame, the font fallback) that has no sink of its own: it asks the app's services for the
/// <see cref="UsageCounter"/> when it is called. Never throws and never changes what the fallback does.
/// </summary>
public static class DinoFallbacks
{
	public static void Report(string area, string reason, string? language = null)
	{
		try
		{
			IPlatformApplication.Current?.Services.GetService<UsageCounter>()?.RecordGeneralFallback(area, reason, language);
		}
		catch (Exception)
		{
			// no-fallback: counting is best effort and never reports itself
		}
	}
}
