using DinoReader.Core;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using UIKit;

namespace DinoReader;

/// <summary>
/// Measures text with the same typeface and size the reading labels draw with, so the page composer
/// wraps lines where the screen really does (the iOS twin of <c>AndroidTextMeasurer</c>). Widths are
/// in points, which MAUI also lays out in. Story text does not follow the system font scale (labels
/// set <c>FontAutoScalingEnabled</c> false). Pages are composed on a worker thread while the UI
/// thread measures for taps, so every measurement takes a lock.
/// </summary>
public sealed class IosTextMeasurer : ITextMeasurer, IDisposable
{
	private readonly object _gate = new();
	private readonly UIStringAttributes _attributes;
	private bool _disposed;

	public IosTextMeasurer(string fontFamily, double fontSize)
	{
		var fonts = IPlatformApplication.Current!.Services.GetRequiredService<IFontManager>();
		// Slice 244: a font the manager cannot supply becomes the system font, never a nil in the attributes.
		_attributes = IosAttributes.WithFont(fonts.GetFont(Microsoft.Maui.Font.OfSize(fontFamily, fontSize)), fontSize);
	}

	public double Width(string text)
	{
		lock (_gate)
		{
			// A layout still running when the owner lets go measures nothing; its result is discarded.
			if (_disposed || string.IsNullOrEmpty(text))
			{
				return 0;
			}

			using var content = new NSString(text);
			return content.GetSizeUsingAttributes(_attributes).Width;
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

			// Never dispose _attributes.Font: it comes from MAUI's IFontManager, a shared cached instance other users still hold.
			_disposed = true;
		}
	}
}
