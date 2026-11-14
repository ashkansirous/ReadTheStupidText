using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>Small label helpers for text that can be in another script than the screen's (moved out of the cast screen's copy in Slice 278 part B).</summary>
public static class OwnScript
{
	/// <summary>Two labels joined the way this language writes a label and its value (a localized format, so RTL and full-width colons read right).</summary>
	public static string Pair(string label, string value) => AppResources.Format(AppResources.Cast_Pair, label, value);

	/// <summary>Sets a label that shows a name or words in the direction and font of its own script (a Farsi name reads right to left in Vazirmatn whatever the screen's language is).</summary>
	public static Label InOwnScript(Label label)
	{
		label.FlowDirection = UiFlow.ForText(label.Text);
		if (label.FlowDirection == FlowDirection.RightToLeft)
		{
			label.FontFamily = DinoFonts.Persian;
		}

		return label;
	}

	/// <summary>Like <see cref="InOwnScript"/>, and a right-to-left label also sits against the right edge of its column, whatever the screen's language is.</summary>
	public static Label InOwnScriptAligned(Label label)
	{
		InOwnScript(label);
		label.HorizontalOptions = label.FlowDirection == FlowDirection.RightToLeft ? LayoutOptions.End : LayoutOptions.Start;
		return label;
	}

	/// <summary>Puts <paramref name="follower"/> on the same edge as <paramref name="lead"/>: the right when the lead is right to left (the subtitle under a Farsi title).</summary>
	public static void AlignWith(Label follower, Label lead) =>
		follower.HorizontalOptions = lead.FlowDirection == FlowDirection.RightToLeft ? LayoutOptions.End : LayoutOptions.Start;
}
