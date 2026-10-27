using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Reads where a label's text really wrapped, from the platform's own layout of that label (Slice 171): Android's <c>TextView.Layout</c>, iOS TextKit over the label's attributed string and width.
/// Null while the label has no native layout yet (or on a platform with no reader), and the caller tries again later.
/// </summary>
public interface ITextLineSource
{
	TextLines? Read(Label label);
}

/// <summary>The reader for this platform; Windows has none yet, so its card keeps the capped height and does not follow the reading.</summary>
public static class TextLineSources
{
	public static ITextLineSource Platform { get; } =
#if ANDROID
		new AndroidTextLineSource();
#elif IOS
		new IosTextLineSource();
#else
		new NoTextLineSource();
#endif
}

#if !ANDROID && !IOS
internal sealed class NoTextLineSource : ITextLineSource
{
	public TextLines? Read(Label label) => null;
}
#endif
