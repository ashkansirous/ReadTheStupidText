using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// A page picture with its text regions outlined (Slice 79): included regions in ink, left-out ones greyed with a dashed edge.
/// Tapping a box tells the owner to flip that region; the owner replaces the regions and the overlay redraws. The picture is
/// drawn AspectFit, and the regions' 0..1 boxes are placed in the picture's displayed rectangle (its offset and scale inside the
/// view), not in the view's bounds, so they sit on the glyphs even when the picture is letterboxed.
/// </summary>
public sealed class RegionOverlay : ContentView
{
	private const double FallbackAspect = 0.75;
	private const double ExcludedFillOpacity = 0.55;
	private const double BoxStrokeWidth = 2;
	private const double DashLength = 3;
	private const double DashGap = 2;
	private const double MaxHeightFactor = 1.6;

	private readonly AbsoluteLayout _layer = new();
	private readonly double _aspect;
	private readonly Action<int> _toggled;
	private readonly List<(Border Box, NormalizedRect Rect)> _boxes = [];

	public RegionOverlay(string imageFile, IReadOnlyList<PageRegion> regions, Action<int> toggled)
	{
		_toggled = toggled;
		Size? size = PageImageSize.Read(imageFile);
		_aspect = size is { Width: > 0, Height: > 0 } s ? s.Width / s.Height : FallbackAspect;
		var picture = new Image { Source = imageFile, Aspect = Aspect.AspectFit };
		var stack = new Grid { Children = { picture, _layer } };
		Content = stack;
		SizeChanged += (_, _) => FitHeight();
		_layer.SizeChanged += (_, _) => PlaceBoxes();
		Show(regions);
	}

	/// <summary>Draws <paramref name="regions"/>, replacing the boxes drawn before.</summary>
	public void Show(IReadOnlyList<PageRegion> regions)
	{
		_layer.Children.Clear();
		_boxes.Clear();

		// Biggest first, so a small box is added after (above) any box that encloses it and gets its own taps.
		foreach (int i in Enumerable.Range(0, regions.Count).OrderByDescending(i => regions[i].Box.Width * regions[i].Box.Height))
		{
			Border box = Box(regions[i], i);
			_boxes.Add((box, regions[i].Box));
			_layer.Children.Add(box);
		}

		PlaceBoxes();
	}

	// Each box goes at its 0..1 place inside the picture's displayed rectangle, in the layer's own units.
	private void PlaceBoxes()
	{
		(double x, double y, double w, double h) = AspectFit.ContentRect(_layer.Width, _layer.Height, _aspect, 1);
		foreach ((Border box, NormalizedRect r) in _boxes)
		{
			AbsoluteLayout.SetLayoutBounds(box, new Rect(x + r.X * w, y + r.Y * h, r.Width * w, r.Height * h));
		}
	}

	private void FitHeight()
	{
		if (Width > 0)
		{
			HeightRequest = Math.Min(Width / _aspect, Width * MaxHeightFactor);
		}
	}

	private Border Box(PageRegion region, int index)
	{
		Color ink = DinoTokens.Get(DinoTokens.Ink);
		Color grey = DinoTokens.Get(DinoTokens.Muted);
		bool included = region.IsIncluded;
		var box = new Border
		{
			Stroke = included ? ink : grey,
			StrokeThickness = BoxStrokeWidth,
			StrokeDashArray = included ? null : new DoubleCollection { DashLength, DashGap },
			// An explicit brush, even when see-through: a Border with no background does not take taps on Android.
			Background = new SolidColorBrush(included ? Colors.Transparent : Colors.White.WithAlpha((float)ExcludedFillOpacity)),
			StrokeShape = new RoundRectangle { CornerRadius = 4 },
		};
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => _toggled(index);
		box.GestureRecognizers.Add(tap);
		SemanticProperties.SetDescription(box, included ? AppResources.Regions_BoxRead : AppResources.Regions_BoxLeftOut);
		return box;
	}
}
