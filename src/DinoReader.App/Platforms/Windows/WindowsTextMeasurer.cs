using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;

namespace DinoReader;

/// <summary>
/// Measures text with the same typeface and size the reading labels draw with, so the page composer
/// wraps lines where the screen really does (the Windows twin of <c>AndroidTextMeasurer</c> and
/// <c>IosTextMeasurer</c>, Slice 67). Widths are in device-independent pixels, which MAUI lays out in.
/// Story text does not follow the system text scale (labels set <c>FontAutoScalingEnabled</c> false).
/// Pages are composed on a worker thread while the UI thread measures for taps, so every
/// measurement takes a lock and uses Win2D's shared device, which needs no UI thread.
/// </summary>
public sealed class WindowsTextMeasurer : ITextMeasurer, IDisposable
{
	// Wide enough that the text never wraps: only the width of one line is wanted.
	private const float UnwrappedWidth = 100_000f;

	private readonly object _gate = new();
	private readonly CanvasTextFormat _format;
	private bool _disposed;

	public WindowsTextMeasurer(string fontFamily, double fontSize)
	{
		var fonts = IPlatformApplication.Current!.Services.GetRequiredService<IFontManager>();
		_format = new CanvasTextFormat
		{
			FontFamily = fonts.GetFontFamily(Microsoft.Maui.Font.OfSize(fontFamily, fontSize)).Source,
			FontSize = (float)fontSize,
			WordWrapping = CanvasWordWrapping.NoWrap,
		};
	}

	public double Width(string text)
	{
		lock (_gate)
		{
			// A layout still running when the owner lets go measures nothing; its result is discarded.
			if (_disposed || text.Length == 0)
			{
				return 0;
			}

			using var layout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), text, _format, UnwrappedWidth, 0);
			// Including trailing whitespace, so Width(" ") is a real space width, not 0.
			return layout.LayoutBoundsIncludingTrailingWhitespace.Width;
		}
	}

	public void Dispose()
	{
		lock (_gate)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_format.Dispose();
		}
	}
}
