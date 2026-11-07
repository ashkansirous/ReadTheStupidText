using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The reading page's sound line as a pill (Slice 290, Batch 50 frames 197-199): a brand-tint pill with a progress ring while the book's sound is being
/// fetched, a quiet sand pill with a Wi-Fi or muted-speaker icon (or none) otherwise. Shape and spacing are the mock's; the words come from the caller.
/// </summary>
public sealed class SoundStatusPill : Border
{
	/// <summary>The id the Maestro flows read the pill's text by; it sits on the label, not the border.</summary>
	public const string TextAutomationId = "ReadingAudioStatus";

	private const double CornerRadius = 18;
	private const double MinHeight = 36;
	private const double Gap = 8;
	private const double TextSize = 14;
	private const double LineHeight = 1.3;
	private const double RingSize = 24;
	private const double IconSize = 20;
	private const double IconStroke = 2.2;
	private const double VerticalPad = 6;
	private const double RightPad = 14;
	private const double LeftPadRing = 8;
	private const double LeftPadIcon = 10;
	private readonly SoundStatusPillRing _ring = new();
	private readonly GraphicsView _ringView;
	private readonly IconView _icon;
	private readonly Label _label;
	private readonly Grid _row;
	private SoundStatusPillState? _shown;

	public SoundStatusPill()
	{
		// The page's 14 px side margin (ReadingPage.xaml) is the width limit: with Auto columns the pill hugs its text and wraps only past it.
		FlowDirection = FlowDirection.LeftToRight; // the mock's layout: the icon stays on the leading (left) side; the label's own text may still be RTL
		StrokeThickness = 0;
		StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(CornerRadius) };
		MinimumHeightRequest = MinHeight;
		HorizontalOptions = LayoutOptions.Center;
		_ringView = new GraphicsView { Drawable = _ring, WidthRequest = RingSize, HeightRequest = RingSize, VerticalOptions = LayoutOptions.Center, InputTransparent = true };
		_icon = new IconView { Size = IconSize, StrokeWidth = IconStroke, VerticalOptions = LayoutOptions.Center };
		AutomationProperties.SetIsInAccessibleTree(_ringView, false); // the label carries the words; the decoration is not read out
		AutomationProperties.SetIsInAccessibleTree(_icon, false);
		_label = new Label { AutomationId = TextAutomationId, FontSize = TextSize, FontAttributes = FontAttributes.None, LineHeight = LineHeight, VerticalOptions = LayoutOptions.Center };
		_row = BuildRow(_ringView, _icon, _label); // after every child exists, so BuildRow can never add a null one
		Content = _row;
	}

	/// <summary>Shows <paramref name="state"/> with <paramref name="text"/> (the caller composes it from the app's strings).</summary>
	public void Show(SoundStatusPillState state, string text)
	{
		Color ink = DinoTokens.Get(state.IsBrandPill ? DinoTokens.BrandEdge : DinoTokens.Body);
		BackgroundColor = DinoTokens.Get(state.IsBrandPill ? DinoTokens.BrandTint : DinoTokens.SoundPillSand);
		_label.TextColor = ink; // colours are re-read on every call, so a light/dark switch shows even when state and text are unchanged
		if (state == _shown && text == _label.Text)
		{
			_icon.Color = ink;
			return; // nothing else changed: skip the icon rebuild and the ring redraw
		}

		_shown = state;
		_label.Text = text;
		_label.FlowDirection = UiFlow.ForText(text); // the row stays left-to-right; the words follow their own language when they wrap
		ShowLeading(state, ink);
	}

	private static Grid BuildRow(GraphicsView ringView, IconView icon, Label label)
	{
		var row = new Grid
		{
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)], // Auto, so the pill hugs its text
			HorizontalOptions = LayoutOptions.Center,
		};
		row.Add(ringView, 0);
		row.Add(icon, 0);
		row.Add(label, 1);
		return row;
	}

	private void ShowLeading(SoundStatusPillState state, Color ink)
	{
		IconKind? kind = LeadingIcon(state.Kind);
		_ringView.IsVisible = state.Kind == SoundStatusKind.Getting;
		_icon.IsVisible = kind is not null;
		if (kind is { } shown)
		{
			_icon.Color = ink; // colour first: the kind change then renders once, in the right colour
			_icon.Kind = shown;
		}

		_ring.Progress = state.RingProgress;
		_ringView.Invalidate();
		bool hasLeading = _ringView.IsVisible || _icon.IsVisible;
		double left = _ringView.IsVisible ? LeftPadRing : LeftPadIcon;
		_row.Padding = new Thickness(left, VerticalPad, RightPad, VerticalPad); // the one place the padding is set
		_label.Margin = new Thickness(hasLeading ? Gap : 0, 0, 0, 0); // the icon-gap exists only when there is an icon
	}

	private static IconKind? LeadingIcon(SoundStatusKind kind) => kind switch
	{
		SoundStatusKind.WaitingForWifi or SoundStatusKind.WaitingForInternet => IconKind.Wifi,
		SoundStatusKind.NoSound => IconKind.SoundOff,
		_ => null, // Getting draws the ring; Preparing has no icon
	};
}
