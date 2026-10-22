using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText_App;

/// <summary>
/// Text directions while the window layout stays left-to-right (Slice 88, the WinUI twin of the Mobile
/// helper from Slice 85). Localised UI strings take the UI language's direction; read, OCR or activity
/// text takes its own first strong letter.
/// </summary>
public static class TextFlow
{
    /// <summary>The direction of the UI language.</summary>
    public static FlowDirection UiDirection() => ToFlow(UiStrings.IsRightToLeft);

    /// <summary>The direction of content text; empty text follows the UI language.</summary>
    public static FlowDirection ContentDirection(string? text) =>
        ToFlow(TextDirection.Resolve(text, UiStrings.IsRightToLeft));

    /// <summary>Sets a localised UI string and the UI language's direction.</summary>
    public static void ForUi(TextBlock block, string? text)
    {
        block.Text = text ?? string.Empty;
        block.FlowDirection = UiDirection();
    }

    private static FlowDirection ToFlow(bool rightToLeft) =>
        rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}
