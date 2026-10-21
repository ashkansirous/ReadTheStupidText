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
	Back,
	Check,
	File,
	Type,
	Camera,
	Photos,
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

	private static readonly PathGeometryConverter Converter = new();

	// Path data copied from the mock's inline SVGs (rects and circles rewritten as arcs).
	private static readonly Dictionary<IconKind, string> Paths = new()
	{
		[IconKind.Lock] = "M7.5 11h9A2.5 2.5 0 0 1 19 13.5v5A2.5 2.5 0 0 1 16.5 21h-9A2.5 2.5 0 0 1 5 18.5v-5A2.5 2.5 0 0 1 7.5 11z M8 11V8a4 4 0 0 1 8 0v3",
		[IconKind.Plus] = "M12 5v14M5 12h14",
		[IconKind.Trash] = "M4 7h16 M9 7V4.5h6V7 M6.5 7l1 13h9l1-13",
		[IconKind.Chevron] = "M4.5 2.5L8 6L4.5 9.5",
		[IconKind.Back] = "M15 5l-7 7 7 7",
		[IconKind.Check] = "M4 12.5l5 5L20 6.5",
		[IconKind.File] = "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8l-5-5z M14 3v5h5",
		[IconKind.Type] = "M4 7V5h16v2 M12 5v14 M9 19h6",
		[IconKind.Camera] = "M4 8a2 2 0 0 1 2-2h1.5l1.2-2h6.6l1.2 2H20a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8z M9.6 12.5a3.4 3.4 0 1 0 6.8 0a3.4 3.4 0 1 0-6.8 0",
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
		Content = new Path
		{
			Data = (Geometry)Converter.ConvertFromInvariantString(Paths[Kind])!,
			Stroke = new SolidColorBrush(Color),
			StrokeThickness = StrokeWidth,
			StrokeLineCap = PenLineCap.Round,
			StrokeLineJoin = PenLineJoin.Round,
			Aspect = Stretch.Uniform,
			WidthRequest = Size,
			HeightRequest = Size,
			HorizontalOptions = LayoutOptions.Center,
			VerticalOptions = LayoutOptions.Center,
		};
	}
}
