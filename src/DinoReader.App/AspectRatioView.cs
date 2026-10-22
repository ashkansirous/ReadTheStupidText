namespace DinoReader;

/// <summary>A container whose height is always its width times <see cref="HeightPerWidth"/>, decided in the
/// measure pass so the very first layout is already the right shape (no SizeChanged correction).</summary>
public class AspectRatioView : ContentView
{
	public static readonly BindableProperty HeightPerWidthProperty =
		BindableProperty.Create(nameof(HeightPerWidth), typeof(double), typeof(AspectRatioView), 4.0 / 3.0,
			propertyChanged: (b, _, _) => ((AspectRatioView)b).InvalidateMeasure());

	private double lastMeasuredWidth = -1;

	public double HeightPerWidth
	{
		get => (double)GetValue(HeightPerWidthProperty);
		set => SetValue(HeightPerWidthProperty, value);
	}

	protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
	{
		double width = double.IsFinite(widthConstraint) ? widthConstraint : UnconstrainedWidth();
		double height = HeightFor(width);
		lastMeasuredWidth = width;

		// The base measure runs the platform measure (padding and content included) at the corrected size.
		base.MeasureOverride(width, height);
		return new Size(width, height);
	}

	protected override Size ArrangeOverride(Rect bounds)
	{
		// Never clamp to the offered height (a recycled cell may carry a stale one): width x ratio always wins.
		double height = HeightFor(bounds.Width);
		if (Math.Abs(bounds.Width - lastMeasuredWidth) > 0.5)
		{
			InvalidateMeasure();
		}

		// The base arrange sets Frame and arranges the platform view, so Width/Height/SizeChanged stay correct.
		return base.ArrangeOverride(new Rect(bounds.X, bounds.Y, bounds.Width, height));
	}

	/// <summary>Height for a total width: the padded content box keeps the ratio.</summary>
	private double HeightFor(double width) =>
		Math.Max(0, width - Padding.HorizontalThickness) * HeightPerWidth + Padding.VerticalThickness;

	/// <summary>No width limit from the parent: fall back to the content's own desired width (plus padding).</summary>
	private double UnconstrainedWidth() =>
		Content is IView content
			? content.Measure(double.PositiveInfinity, double.PositiveInfinity).Width + Padding.HorizontalThickness
			: Padding.HorizontalThickness;
}
