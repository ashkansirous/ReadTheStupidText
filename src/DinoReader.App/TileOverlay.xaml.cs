namespace DinoReader;

/// <summary>The edit controls over one gallery cover; the gallery handles what each does.</summary>
public partial class TileOverlay : ContentView
{
	public TileOverlay()
	{
		InitializeComponent();
	}

	public event EventHandler? EditRequested;

	public event EventHandler? CastRequested;

	public event EventHandler? DeleteRequested;

	private void OnEditClicked(object? sender, EventArgs e) => EditRequested?.Invoke(this, e);

	private void OnBackdropTapped(object? sender, TappedEventArgs e) => EditRequested?.Invoke(this, e);

	private void OnCastClicked(object? sender, EventArgs e) => CastRequested?.Invoke(this, e);

	private void OnDeleteTapped(object? sender, TappedEventArgs e) => DeleteRequested?.Invoke(this, e);
}
