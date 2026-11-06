using Microsoft.UI.Xaml;

namespace DinoReader;

/// <summary>
/// Slice 279 (mock Batch 48 frame 184): when a piece opens on the Windows head the keyboard is on the big Record button and a focus ring shows around it. The button is a tapped MAUI Border, whose native
/// panel WinUI neither focuses by itself nor draws a focus visual on (<see cref="DinoFocusRing"/> styles only real controls), so it is made a tab stop and the ring is the Border's own 3 dp indigo stroke
/// while it holds the focus. Windows only.
/// </summary>
internal static class RecordFocus
{
	private const double RingThickness = 3;
	private const string IndigoHex = "#5B57E8";

	/// <summary>Puts the keyboard focus on <paramref name="button"/> once its native view exists; a button the page has since replaced or hidden is left alone.</summary>
	public static void Land(Border button)
	{
		button.Dispatcher.Dispatch(() =>
		{
			if (!button.IsVisible || button.Handler?.PlatformView is not UIElement element)
			{
				return;
			}

			element.IsTabStop = true;
			Brush? restStroke = button.Stroke;
			double restThickness = button.StrokeThickness;
			element.GotFocus += (_, _) =>
			{
				button.Stroke = new SolidColorBrush(Color.FromArgb(IndigoHex));
				button.StrokeThickness = RingThickness;
			};
			element.LostFocus += (_, _) =>
			{
				button.Stroke = restStroke;
				button.StrokeThickness = restThickness;
			};
			element.Focus(FocusState.Keyboard);
		});
	}
}
