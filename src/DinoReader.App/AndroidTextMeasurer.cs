using AndroidPaint = Android.Graphics.Paint;
using AndroidPaintFlags = Android.Graphics.PaintFlags;
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DinoReader;

/// <summary>
/// Measures text with the same typeface and size the reading labels draw with, so the page composer
/// wraps lines where the screen really does. Widths are in dp. Story text does not follow the
/// system font scale (labels set <c>FontAutoScalingEnabled</c> false): the page geometry is fixed
/// big type, and scaling it would overflow the lines the composer laid out. Pages are composed on
/// a worker thread while the UI thread measures for taps, and an Android <c>Paint</c> is not safe to
/// share across threads, so every measurement takes a lock.
/// </summary>
public sealed class AndroidTextMeasurer : ITextMeasurer, IDisposable
{
	private readonly object _gate = new();
	private readonly AndroidPaint _paint = new(AndroidPaintFlags.AntiAlias);
	private readonly float _density;
	private bool _disposed;

	public AndroidTextMeasurer(string fontFamily, double fontSize)
	{
		var fonts = IPlatformApplication.Current!.Services.GetRequiredService<IFontManager>();
		_paint.SetTypeface(fonts.GetTypeface(Microsoft.Maui.Font.OfSize(fontFamily, fontSize)));
		_density = Platform.AppContext.Resources!.DisplayMetrics!.Density;
		_paint.TextSize = (float)(fontSize * _density);
	}

	public double Width(string text)
	{
		lock (_gate)
		{
			// A layout still running when the owner lets go measures nothing; its result is discarded.
			return _disposed ? 0 : _paint.MeasureText(text) / _density;
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
			_paint.Dispose();
		}
	}
}
