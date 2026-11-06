using ReadTheStupidText.Application.Usage;

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
			// Counting is best effort: it never changes what the fallback does.
		}
	}
}
