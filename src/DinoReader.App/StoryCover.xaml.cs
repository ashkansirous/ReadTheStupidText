namespace DinoReader;

public partial class StoryCover : ContentView
{
	private const double CoverAspect = 4.0 / 3.0;

	public StoryCover()
	{
		InitializeComponent();
	}

	private void OnFrameSizeChanged(object? sender, EventArgs e)
	{
		double height = CoverFrame.Width * CoverAspect;
		if (CoverFrame.Width > 0 && Math.Abs(CoverFrame.HeightRequest - height) > 0.5)
		{
			CoverFrame.HeightRequest = height;
		}
	}
}
