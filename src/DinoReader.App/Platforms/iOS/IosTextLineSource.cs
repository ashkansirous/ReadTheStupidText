using CoreGraphics;
using DinoReader.Core;
using Foundation;
using Microsoft.Maui.Platform;
using UIKit;

namespace DinoReader;

/// <summary>
/// iOS's reader (Slice 171). UILabel exposes no layout manager or line fragments (only <c>TextRectForBounds</c>, one rectangle for the whole text), so its own layout cannot be read.
/// TextKit lays the same attributed string out instead, set up exactly as UILabel sets itself up: the label's font and paragraph attributes (line spacing, alignment), its line-break mode,
/// natural or right-to-left writing direction, no line-fragment padding, and a container as wide as the label's text area (its width less the insets). Points, which MAUI lays out in.
/// </summary>
public sealed class IosTextLineSource : ITextLineSource
{
	public TextLines? Read(Label label)
	{
		if (label.Handler?.PlatformView is not UILabel view || view.AttributedText is not { Length: > 0 } text || label.Width <= 0)
		{
			return null;
		}

		UIEdgeInsets insets = (view as MauiLabel)?.TextInsets ?? UIEdgeInsets.Zero;
		double width = label.Width - insets.Left - insets.Right;
		if (width <= 0)
		{
			return null;
		}

		using NSAttributedString styled = WithLabelParagraph(text, view);
		using var storage = new NSTextStorage();
		storage.SetString(styled);
		using var manager = new NSLayoutManager();
		using var container = new NSTextContainer(new CGSize(width, nfloat.MaxValue))
		{
			LineFragmentPadding = 0,
			LineBreakMode = ContainerBreakMode(view),
			MaximumNumberOfLines = (nuint)Math.Max(0, (int)view.Lines),
		};
		manager.AddTextContainer(container);
		storage.AddLayoutManager(manager);
		manager.EnsureLayoutForTextContainer(container);
		return Collect(manager, container, insets.Top);
	}

	// The label's paragraph style (line spacing, alignment) kept, with its line-break mode and writing direction as the label has them.
	private static NSAttributedString WithLabelParagraph(NSAttributedString text, UILabel view)
	{
		var mutable = new NSMutableAttributedString(text);
		NSParagraphStyle? existing = mutable.GetAttribute(UIStringAttributeKey.ParagraphStyle, 0, out _) as NSParagraphStyle;
		NSMutableParagraphStyle style = existing?.MutableCopy() as NSMutableParagraphStyle ?? new NSMutableParagraphStyle();
		style.LineBreakMode = ContainerBreakMode(view);
		style.BaseWritingDirection = view.SemanticContentAttribute == UISemanticContentAttribute.ForceRightToLeft
			? NSWritingDirection.RightToLeft
			: NSWritingDirection.Natural;
		mutable.AddAttribute(UIStringAttributeKey.ParagraphStyle, style, new NSRange(0, mutable.Length));
		return mutable;
	}

	// A label that may use any number of lines wraps by words (or characters); the truncating modes only apply with a line limit.
	private static UILineBreakMode ContainerBreakMode(UILabel view) =>
		view.Lines == 0 && view.LineBreakMode is UILineBreakMode.TailTruncation or UILineBreakMode.HeadTruncation or UILineBreakMode.MiddleTruncation
			? UILineBreakMode.WordWrap
			: view.LineBreakMode;

	private static TextLines? Collect(NSLayoutManager manager, NSTextContainer container, double top)
	{
		var tops = new List<double>();
		var bottoms = new List<double>();
		var starts = new List<int>();
		NSRange glyphs = manager.GetGlyphRange(container);
		manager.EnumerateLineFragments(glyphs, (CGRect rect, CGRect used, NSTextContainer box, NSRange range, out bool stop) =>
		{
			stop = false;
			tops.Add(top + rect.Y);
			bottoms.Add(top + rect.Y + rect.Height);
			starts.Add((int)manager.GetCharacterRange(range, out _).Location);
		});
		return tops.Count == 0 ? null : new TextLines(tops, bottoms, starts);
	}
}
