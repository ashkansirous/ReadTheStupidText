using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace DinoReader;

/// <summary>The line icons of the Dino Reader v2 mock (24-unit grid, round caps and joins).</summary>
public enum IconKind
{
	Lock,
	Plus,
	Trash,
	Chevron,
	ChevronDown,
	Back,
	Check,
	File,
	Type,
	Camera,
	Photos,
	Book,
	Picture,
	SkipBack,
	SkipForward,
	Play,
	Pause,
	AddBook,
	Globe,
	Narrator,
}

/// <summary>Draws one <see cref="IconKind"/> as a stroked path, so colour and size follow the design tokens.</summary>
public sealed class IconView : ContentView
{
	private const double DefaultSize = 20;
	private const double DefaultStroke = 2;

	public static readonly BindableProperty KindProperty = BindableProperty.Create(
		nameof(Kind), typeof(IconKind), typeof(IconView), IconKind.Lock, propertyChanged: OnChanged);

	public static readonly BindableProperty ColorProperty = BindableProperty.Create(
		nameof(Color), typeof(Color), typeof(IconView), Colors.Black, propertyChanged: OnChanged);

	public static readonly BindableProperty SizeProperty = BindableProperty.Create(
		nameof(Size), typeof(double), typeof(IconView), DefaultSize, propertyChanged: OnChanged);

	public static readonly BindableProperty StrokeWidthProperty = BindableProperty.Create(
		nameof(StrokeWidth), typeof(double), typeof(IconView), DefaultStroke, propertyChanged: OnChanged);

	public static readonly BindableProperty FilledProperty = BindableProperty.Create(
		nameof(Filled), typeof(bool), typeof(IconView), false, propertyChanged: OnChanged);

	public static readonly BindableProperty BadgeColorProperty = BindableProperty.Create(
		nameof(BadgeColor), typeof(Color), typeof(IconView), null, propertyChanged: OnChanged);

	public static readonly BindableProperty PlusColorProperty = BindableProperty.Create(
		nameof(PlusColor), typeof(Color), typeof(IconView), null, propertyChanged: OnChanged);

	private static readonly PathGeometryConverter Converter = new();

	// The add-book icon (Slice 97) is a currentColor book plus a badge in the brand colour that never follows Color.
	// All three layers are drawn unstretched in the one 24-unit grid, then the whole grid is scaled uniformly to Size.
	private const double GridSize = 24;
	private const string AddBookBody = "M5 18V5.5A2.5 2.5 0 0 1 7.5 3H17v12.5 M5 18a2.5 2.5 0 0 1 2.5-2.5H12 M5 18a2.5 2.5 0 0 0 2.5 2.5H12";
	private const string AddBookBadge = "M13.4 18a4.6 4.6 0 1 0 9.2 0a4.6 4.6 0 1 0-9.2 0";
	private const string AddBookPlus = "M18 15.9v4.2M15.9 18h4.2";

	// Path data copied from the mock's inline SVGs (rects and circles rewritten as arcs).
	private static readonly Dictionary<IconKind, string> Paths = new()
	{
		[IconKind.Lock] = "M7.5 11h9A2.5 2.5 0 0 1 19 13.5v5A2.5 2.5 0 0 1 16.5 21h-9A2.5 2.5 0 0 1 5 18.5v-5A2.5 2.5 0 0 1 7.5 11z M8 11V8a4 4 0 0 1 8 0v3",
		[IconKind.Plus] = "M12 5v14M5 12h14",
		[IconKind.Trash] = "M4 7h16 M9 7V4.5h6V7 M6.5 7l1 13h9l1-13",
		[IconKind.Chevron] = "M4.5 2.5L8 6L4.5 9.5",
		[IconKind.ChevronDown] = "M2.5 4.5L6 8L9.5 4.5",
		[IconKind.Back] = "M15 5l-7 7 7 7",
		[IconKind.Check] = "M4 12.5l5 5L20 6.5",
		[IconKind.File] = "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8l-5-5z M14 3v5h5",
		[IconKind.Type] = "M4 7V5h16v2 M12 5v14 M9 19h6",
		[IconKind.Camera] = "M4 8a2 2 0 0 1 2-2h1.5l1.2-2h6.6l1.2 2H20a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8z M9.6 12.5a3.4 3.4 0 1 0 6.8 0a3.4 3.4 0 1 0-6.8 0",
		[IconKind.Globe] = "M3 12a9 9 0 1 0 18 0a9 9 0 1 0-18 0 M3 12h18 M12 3c2.5 2.7 3.8 5.7 3.8 9s-1.3 6.3-3.8 9c-2.5-2.7-3.8-5.7-3.8-9S9.5 5.7 12 3z",
		[IconKind.Narrator] = "M5.5 8a3.5 3.5 0 1 0 7 0a3.5 3.5 0 1 0-7 0 M3 20c.6-3.6 3-5.5 6-5.5s5.4 1.9 6 5.5 M17 8.5c1 .8 1.5 1.8 1.5 3s-.5 2.2-1.5 3 M19.5 6c1.6 1.4 2.5 3.4 2.5 5.5s-.9 4.1-2.5 5.5",
		[IconKind.Book] = "M12 6.5C10 5 7 4.5 3.5 5v13c3.5-.5 6.5 0 8.5 1.5 2-1.5 5-2 8.5-1.5V5C17 4.5 14 5 12 6.5z M12 6.5v13",
		[IconKind.Picture] = "M6 4.5h12A2.5 2.5 0 0 1 20.5 7v10a2.5 2.5 0 0 1-2.5 2.5H6A2.5 2.5 0 0 1 3.5 17V7A2.5 2.5 0 0 1 6 4.5z M7.2 10a1.8 1.8 0 1 0 3.6 0a1.8 1.8 0 1 0-3.6 0 M4.5 17.5l5-5 4 4 2.5-2.5 3.5 3.5",
		[IconKind.SkipBack] = "M11 5L4 12l7 7 M19 5l-7 7 7 7",
		[IconKind.SkipForward] = "M13 5l7 7-7 7 M5 5l7 7-7 7",
		[IconKind.Play] = "M5 3.4c0-.6.7-1 1.2-.6l5.4 4.1c.4.3.4 1 0 1.3l-5.4 4.1c-.5.4-1.2 0-1.2-.6z",
		[IconKind.Pause] = "M4.7 2.5h.6a1.4 1.4 0 0 1 1.4 1.4v8.2a1.4 1.4 0 0 1-1.4 1.4h-.6a1.4 1.4 0 0 1-1.4-1.4V3.9a1.4 1.4 0 0 1 1.4-1.4z M10.7 2.5h.6a1.4 1.4 0 0 1 1.4 1.4v8.2a1.4 1.4 0 0 1-1.4 1.4h-.6a1.4 1.4 0 0 1-1.4-1.4V3.9a1.4 1.4 0 0 1 1.4-1.4z",
		[IconKind.Photos] = "M6 4.5h12A2.5 2.5 0 0 1 20.5 7v10a2.5 2.5 0 0 1-2.5 2.5H6A2.5 2.5 0 0 1 3.5 17V7A2.5 2.5 0 0 1 6 4.5z M7.2 10a1.8 1.8 0 1 0 3.6 0a1.8 1.8 0 1 0-3.6 0 M4.5 17.5l5-5 4 4 2.5-2.5 3.5 3.5",
	};

	public IconView()
	{
		InputTransparent = true;
		Render();
	}

	public IconKind Kind
	{
		get => (IconKind)GetValue(KindProperty);
		set => SetValue(KindProperty, value);
	}

	public Color Color
	{
		get => (Color)GetValue(ColorProperty);
		set => SetValue(ColorProperty, value);
	}

	/// <summary>Fills the glyph with <see cref="Color"/> instead of stroking it (the play and pause glyphs).</summary>
	public bool Filled
	{
		get => (bool)GetValue(FilledProperty);
		set => SetValue(FilledProperty, value);
	}

	/// <summary>Badge of the add-book icon; null means the Brand token.</summary>
	public Color? BadgeColor
	{
		get => (Color?)GetValue(BadgeColorProperty);
		set => SetValue(BadgeColorProperty, value);
	}

	/// <summary>Plus glyph of the add-book icon; null means the OnBrand token.</summary>
	public Color? PlusColor
	{
		get => (Color?)GetValue(PlusColorProperty);
		set => SetValue(PlusColorProperty, value);
	}

	public double Size
	{
		get => (double)GetValue(SizeProperty);
		set => SetValue(SizeProperty, value);
	}

	public double StrokeWidth
	{
		get => (double)GetValue(StrokeWidthProperty);
		set => SetValue(StrokeWidthProperty, value);
	}

	private static void OnChanged(BindableObject bindable, object oldValue, object newValue) =>
		((IconView)bindable).Render();

	private void Render()
	{
		if (Kind == IconKind.AddBook)
		{
			Content = BuildAddBook();
			return;
		}

		var path = StrokedPath(
			ParseGeometry(Paths[Kind]),
			Color,
			Filled ? new SolidColorBrush(Color) : Brush.Transparent,
			Filled ? 0 : StrokeWidth,
			Stretch.Uniform);
		path.WidthRequest = Size;
		path.HeightRequest = Size;
		path.HorizontalOptions = LayoutOptions.Center;
		path.VerticalOptions = LayoutOptions.Center;
		Content = path;
	}

	private static Geometry ParseGeometry(string data) => (Geometry)Converter.ConvertFromInvariantString(data)!;

	private static Path StrokedPath(Geometry data, Color stroke, Brush fill, double thickness, Stretch aspect) => new()
	{
		Data = data,
		Stroke = new SolidColorBrush(stroke),
		Fill = fill,
		StrokeThickness = thickness,
		StrokeLineCap = PenLineCap.Round,
		StrokeLineJoin = PenLineJoin.Round,
		Aspect = aspect,
	};

	// AddBook is a layered kind with no entry in Paths and no Filled mode; Render() branches to it before the Paths lookup.
	private View BuildAddBook()
	{
		var brand = BadgeColor ?? DinoTokens.Get(DinoTokens.Brand);
		var plus = PlusColor ?? DinoTokens.Get(DinoTokens.OnBrand);
		var scale = Size / GridSize;
		var layout = new AbsoluteLayout
		{
			WidthRequest = Size,
			HeightRequest = Size,
			FlowDirection = FlowDirection.LeftToRight,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
		};
		layout.Add(GridLayer(AddBookBody, Color, Brush.Transparent, scale));
		layout.Add(GridLayer(AddBookBadge, brand, new SolidColorBrush(brand), scale));
		layout.Add(GridLayer(AddBookPlus, plus, Brush.Transparent, scale));
		return layout;
	}

	// Each layer is laid out on the 24-unit grid and scaled about the box origin into the Size x Size layout box (stroke too).
	private Path GridLayer(string data, Color stroke, Brush fill, double scale)
	{
		var path = StrokedPath(ParseGeometry(data), stroke, fill, StrokeWidth, Stretch.None);
		path.AnchorX = 0;
		path.AnchorY = 0;
		path.Scale = scale;
		AbsoluteLayout.SetLayoutBounds(path, new Rect(0, 0, GridSize, GridSize));
		return path;
	}
}
