namespace DinoReader;

public partial class StoryCover : ContentView
{
	private const double DefaultShadowDepth = 4;

	/// <summary>How far down the cover's hard shadow falls, in dp: 4, and 5 on a tablet held upright (kids' shelf mock KT1). The gallery sets the page resource <c>TileShadowOffset</c> and the tile binds it here.</summary>
	public static readonly BindableProperty ShadowDepthProperty = BindableProperty.Create(
		nameof(ShadowDepth), typeof(double), typeof(StoryCover), DefaultShadowDepth, propertyChanged: (cover, _, depth) => ((StoryCover)cover).ApplyDepth((double)depth));

	public StoryCover()
	{
		InitializeComponent();
	}

	public double ShadowDepth
	{
		get => (double)GetValue(ShadowDepthProperty);
		set => SetValue(ShadowDepthProperty, value);
	}

	private void ApplyDepth(double depth) => CoverShadow.Offset = new Point(0, depth);
}
