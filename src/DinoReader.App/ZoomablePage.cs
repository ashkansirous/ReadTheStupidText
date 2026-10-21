using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// One real page picture on the reading card (Slice 64): the picture fitted into the card's inset with its
/// aspect ratio kept, and the line boxes drawn in a layer on top of it. Picture and boxes sit in one container,
/// so pinch-zoom and pan (both applied to the container) keep every box on its text, and the boxes are
/// positioned from the picture's own 0..1 coordinates, never from the card's. The gestures sit on an outer view
/// that is never transformed (pan deltas are then in card space), and only an inner view, clipped to the outer
/// one, is scaled and moved. Pinch zooms about the centre of the picture; a pan recognizer is attached only
/// while zoomed in so an un-zoomed swipe still turns the page.
/// </summary>
public sealed class ZoomablePage
{
	private readonly Rect _fit;
	private const double ZoomSnapThreshold = 1.1;

	private readonly AbsoluteLayout _inner;
	private readonly List<View> _boxes = [];
	private double _scale = 1;
	private double _startScale = 1;
	private double _panX;
	private double _panY;
	private double _startPanX;
	private double _startPanY;
	private PanGestureRecognizer? _pan;

	private ZoomablePage(AbsoluteLayout container, AbsoluteLayout inner, Rect fit)
	{
		Container = container;
		_inner = inner;
		_fit = fit;
	}

	/// <summary>The untransformed view that takes the gestures and clips the zoomed picture; add it to the page layer.</summary>
	public AbsoluteLayout Container { get; }

	/// <summary>Whether the picture is zoomed in: a swipe then pans instead of turning the page.</summary>
	public bool IsZoomed => _scale > 1;

	/// <summary>Creates the page. The container sits in a hit-testable layer so pinch and pan reach it, so it
	/// forwards the card's taps and swipes (<paramref name="onTap"/>, <paramref name="onSwipe"/>) itself.</summary>
	public static ZoomablePage Create(string path, Size imageSize, Size card,
		Action<TappedEventArgs> onTap, Action<SwipedEventArgs> onSwipe)
	{
		Rect fit = FitInto(imageSize, card);
		var inner = new AbsoluteLayout { WidthRequest = fit.Width, HeightRequest = fit.Height };
		inner.Children.Add(Picture(path, fit));
		var container = new AbsoluteLayout
		{
			// The picture is the real page: its own left-to-right frame, whatever the app's direction.
			FlowDirection = FlowDirection.LeftToRight,
			WidthRequest = fit.Width,
			HeightRequest = fit.Height,
			IsClippedToBounds = true,
		};
		container.Children.Add(inner);
		AbsoluteLayout.SetLayoutBounds(inner, new Rect(0, 0, fit.Width, fit.Height));
		AbsoluteLayout.SetLayoutBounds(container, fit);
		var page = new ZoomablePage(container, inner, fit);
		page.AttachPinch();
		page.AttachTapAndSwipe(onTap, onSwipe);
		return page;
	}

	/// <summary>The aspect-fit rectangle of a picture inside the card's inset, centred.</summary>
	public static Rect FitInto(Size image, Size card)
	{
		double availableWidth = Math.Max(1, card.Width - 2 * ReadingMetrics.ScanPaddingX);
		double availableHeight = Math.Max(1, card.Height - ReadingMetrics.ScanPaddingTop - ReadingMetrics.ScanPaddingBottom);
		double scale = Math.Min(availableWidth / image.Width, availableHeight / image.Height);
		double width = image.Width * scale;
		double height = image.Height * scale;
		return new Rect(
			ReadingMetrics.ScanPaddingX + (availableWidth - width) / 2,
			ReadingMetrics.ScanPaddingTop + (availableHeight - height) / 2,
			width, height);
	}

	/// <summary>Replaces the boxes: one rounded box per line, each reaching a little past the line.</summary>
	public void ShowBoxes(IEnumerable<NormalizedRect> rects, ReadingTheme theme)
	{
		foreach (View old in _boxes)
		{
			_inner.Children.Remove(old);
		}

		_boxes.Clear();
		foreach (NormalizedRect rect in rects)
		{
			Border box = PageArt.Box(BoxBounds(rect), theme);
			_boxes.Add(box);
			_inner.Children.Add(box);
		}
	}

	/// <summary>A point of the card (dp) as a point of the picture in 0..1, or null when it is off the picture.</summary>
	public Point? ToPageUnits(Point card)
	{
		double x = _fit.Width / 2 + (card.X - (_fit.X + _fit.Width / 2) - _panX) / _scale;
		double y = _fit.Height / 2 + (card.Y - (_fit.Y + _fit.Height / 2) - _panY) / _scale;
		bool inside = x >= 0 && y >= 0 && x <= _fit.Width && y <= _fit.Height;
		return inside ? new Point(x / _fit.Width, y / _fit.Height) : null;
	}

	private Rect BoxBounds(NormalizedRect r) => new(
		r.X * _fit.Width - ReadingMetrics.ScanBoxOutsetX,
		r.Y * _fit.Height - ReadingMetrics.ScanBoxOutsetY,
		r.Width * _fit.Width + 2 * ReadingMetrics.ScanBoxOutsetX,
		r.Height * _fit.Height + 2 * ReadingMetrics.ScanBoxOutsetY);

	private static Image Picture(string path, Rect fit)
	{
		var image = new Image { Source = ImageSource.FromFile(path), Aspect = Aspect.Fill, InputTransparent = true };
		AbsoluteLayout.SetLayoutBounds(image, new Rect(0, 0, fit.Width, fit.Height));
		return image;
	}

	private void AttachPinch()
	{
		var pinch = new PinchGestureRecognizer();
		pinch.PinchUpdated += OnPinch;
		Container.GestureRecognizers.Add(pinch);
	}

	private void AttachTapAndSwipe(Action<TappedEventArgs> onTap, Action<SwipedEventArgs> onSwipe)
	{
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, e) => onTap(e);
		Container.GestureRecognizers.Add(tap);
		foreach (SwipeDirection direction in new[] { SwipeDirection.Left, SwipeDirection.Right })
		{
			var swipe = new SwipeGestureRecognizer { Direction = direction };
			swipe.Swiped += (_, e) => onSwipe(e);
			Container.GestureRecognizers.Add(swipe);
		}
	}

	private void OnPinch(object? sender, PinchGestureUpdatedEventArgs e)
	{
		switch (e.Status)
		{
			case GestureStatus.Started:
				_startScale = _scale;
				break;
			case GestureStatus.Running:
				SetScale(_scale + (e.Scale - 1) * _startScale);
				break;
			case GestureStatus.Completed:
				SetScale(_scale);
				break;
		}
	}

	// A finished pinch that ends near 1 snaps to exactly 1 and recentres, so the page is not left "zoomed" by a hair.
	private void SettlePinch()
	{
		if (_scale <= ZoomSnapThreshold)
		{
			_panX = 0;
			_panY = 0;
			SetScale(1);
			return;
		}

		SetScale(_scale);
	}

	private void SetScale(double scale)
	{
		_scale = Math.Clamp(scale, 1, ReadingMetrics.MaxZoom);
		if (_scale < ZoomSnapThreshold)
		{
			_scale = 1;
		}

		ClampPan();
		Apply();
		UpdatePanRecognizer();
	}

	private void OnPan(object? sender, PanUpdatedEventArgs e)
	{
		switch (e.StatusType)
		{
			case GestureStatus.Started:
				_startPanX = _panX;
				_startPanY = _panY;
				break;
			case GestureStatus.Running:
				_panX = _startPanX + e.TotalX;
				_panY = _startPanY + e.TotalY;
				ClampPan();
				Apply();
				break;
		}
	}

	// A zoomed picture may be dragged until its edge meets the edge of the unzoomed picture.
	private void ClampPan()
	{
		double maxX = (_scale - 1) * _fit.Width / 2;
		double maxY = (_scale - 1) * _fit.Height / 2;
		_panX = Math.Clamp(_panX, -maxX, maxX);
		_panY = Math.Clamp(_panY, -maxY, maxY);
	}

	private void Apply()
	{
		_inner.Scale = _scale;
		_inner.TranslationX = _panX;
		_inner.TranslationY = _panY;
	}

	private void UpdatePanRecognizer()
	{
		if (_scale > 1 && _pan is null)
		{
			_pan = new PanGestureRecognizer();
			_pan.PanUpdated += OnPan;
			Container.GestureRecognizers.Add(_pan);
		}
		else if (_scale <= 1 && _pan is not null)
		{
			Container.GestureRecognizers.Remove(_pan);
			_pan = null;
		}
	}
}
