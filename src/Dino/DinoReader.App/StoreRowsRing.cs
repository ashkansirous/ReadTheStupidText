namespace DinoReader;

/// <summary>The 24 px download ring of a Store row (Slice 364, mock S1c): the getting-sound pill's ring, bound to a row's progress.</summary>
public sealed class StoreProgressRing : GraphicsView
{
	public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
		nameof(Progress), typeof(double), typeof(StoreProgressRing), 0.0, propertyChanged: (bindable, _, _) => ((StoreProgressRing)bindable).Redraw());

	private readonly SoundStatusPillRing _ring = new();

	public StoreProgressRing()
	{
		WidthRequest = SoundStatusPillRing.BoxSize;
		HeightRequest = SoundStatusPillRing.BoxSize;
		InputTransparent = true;
		Drawable = _ring;
	}

	/// <summary>0 to 1: how much of the book has arrived.</summary>
	public double Progress
	{
		get => (double)GetValue(ProgressProperty);
		set => SetValue(ProgressProperty, value);
	}

	private void Redraw()
	{
		_ring.Progress = Progress;
		Invalidate();
	}
}
