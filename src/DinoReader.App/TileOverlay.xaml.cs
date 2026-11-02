namespace DinoReader;

/// <summary>The grown-up "⋯" over one gallery cover (Slice 189); the gallery opens the book's options sheet.</summary>
public partial class TileOverlay : ContentView
{
	public TileOverlay()
	{
		InitializeComponent();
		BindingContextChanged += (_, _) =>
		{
			if (BindingContext is StoryTile tile)
			{
				DesktopTips.Set(MoreButton, tile.MoreHint); // Slice 235 part B: Windows only
			}
		};
	}

	/// <summary>A tap on the dot or on the bare cover: the book's options sheet.</summary>
	public event EventHandler? OpenRequested;

	private void OnBackdropTapped(object? sender, TappedEventArgs e) => OpenRequested?.Invoke(this, e);

	private void OnMoreTapped(object? sender, TappedEventArgs e) => OpenRequested?.Invoke(this, e);
}
