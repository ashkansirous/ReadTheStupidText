namespace DinoReader;

/// <summary>Where a text attribute's font comes from (Slice 244).</summary>
public enum FontSource
{
	/// <summary>The named font was found and is used.</summary>
	Named,

	/// <summary>The named font could not be found: the system font stands in, so no attribute value is ever null.</summary>
	System,
}

/// <summary>
/// The pure choices behind the iOS text attributes (Slice 244). Five TestFlight crashes were
/// <c>-[__NSDictionaryM __setObject:forKey:]: object cannot be nil</c> in a layout pass, so every value put into
/// an attributes dictionary comes through here: a font that is missing becomes the system font, a size that is
/// zero, negative or not a number becomes <see cref="DefaultSize"/>. No platform types, so the test project
/// compiles this very file.
/// </summary>
public static class AttributeFallback
{
	/// <summary>The size used when the requested one is not a usable number (UIKit's default label size).</summary>
	public const double DefaultSize = 17;

	public static FontSource SourceFor(bool namedFontFound) => namedFontFound ? FontSource.Named : FontSource.System;

	public static double SizeFor(double requested) => double.IsFinite(requested) && requested > 0 ? requested : DefaultSize;
}
