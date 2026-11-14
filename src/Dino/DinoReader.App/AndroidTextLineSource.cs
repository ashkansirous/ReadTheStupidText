#if ANDROID
using Android.Widget;
using DinoReader.Core;

namespace DinoReader;

/// <summary>Android's reader (Slice 171): the <c>Layout</c> the label's own <c>TextView</c> drew its text with. Positions are in dp from the top of the text.</summary>
public sealed class AndroidTextLineSource : ITextLineSource
{
	public TextLines? Read(Label label)
	{
		if (label.Handler?.PlatformView is not TextView view || view.Layout is not { LineCount: > 0 } layout)
		{
			return null;
		}

		// The first measure can come from another width's pass; wait for the layout built for the width on screen.
		int contentWidth = view.Width - view.CompoundPaddingLeft - view.CompoundPaddingRight;
		if (Math.Abs(layout.Width - contentWidth) > 1)
		{
			return null;
		}

		float density = view.Resources?.DisplayMetrics?.Density ?? 1f;
		double inset = view.CompoundPaddingTop / density;
		int count = layout.LineCount;
		var tops = new double[count];
		var bottoms = new double[count];
		var starts = new int[count];
		for (int line = 0; line < count; line++)
		{
			tops[line] = inset + layout.GetLineTop(line) / density;
			bottoms[line] = inset + layout.GetLineBottom(line) / density;
			starts[line] = layout.GetLineStart(line);
		}

		return new TextLines(tops, bottoms, starts);
	}
}
#endif
