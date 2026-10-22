using System.Globalization;
using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Text directions while the layout stays left-to-right (Slice 85). Localised UI strings take the UI
/// language's direction; user, OCR and translated text takes its own first strong letter.
/// </summary>
public static class TextFlow
{
    /// <summary>The direction of the current UI language.</summary>
    public static FlowDirection UiDirection() =>
        ToFlow(UiCulture.IsRightToLeft(CultureInfo.CurrentUICulture));

    /// <summary>The direction of content text; empty text follows the UI language.</summary>
    public static FlowDirection ContentDirection(string? text) =>
        ToFlow(TextDirection.Resolve(text, UiCulture.IsRightToLeft(CultureInfo.CurrentUICulture)));

    /// <summary>Sets a localised UI string and the UI language's direction.</summary>
    public static void ForUi(Label label, string? text)
    {
        label.Text = text;
        label.FlowDirection = UiDirection();
    }

    /// <summary>Gives a view (button, picker, label) the UI language's direction.</summary>
    public static void ForUi(VisualElement view) => view.FlowDirection = UiDirection();

    /// <summary>Sets user or translated text and the direction of its own content.</summary>
    public static void ForContent(Label label, string? text)
    {
        label.Text = text;
        label.FlowDirection = ContentDirection(text);
    }

    private static FlowDirection ToFlow(bool rightToLeft) =>
        rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}
