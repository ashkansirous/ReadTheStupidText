namespace DinoReader;

/// <summary>The edit controls over one gallery cover; the gallery handles what each does.</summary>
public partial class TileOverlay : ContentView
{
	public TileOverlay()
	{
		InitializeComponent();
	}

	public event EventHandler? EditRequested;

	/// <summary>A tap on the bare cover: the book's "This book" sheet (Slice 174); editing is the Edit button.</summary>
	public event EventHandler? OpenRequested;

	public event EventHandler? CastRequested;

	public event EventHandler? DeleteRequested;

	public event EventHandler? RecordRequested;

	public event EventHandler? ExportRequested;

	private void OnEditClicked(object? sender, EventArgs e) => EditRequested?.Invoke(this, e);

	private void OnBackdropTapped(object? sender, TappedEventArgs e) => OpenRequested?.Invoke(this, e);

	private void OnCastClicked(object? sender, EventArgs e) => CastRequested?.Invoke(this, e);

	private void OnExportTapped(object? sender, TappedEventArgs e) => ExportRequested?.Invoke(this, e);

	private void OnDeleteTapped(object? sender, TappedEventArgs e) => DeleteRequested?.Invoke(this, e);

	private void OnRecordTapped(object? sender, TappedEventArgs e) => RecordRequested?.Invoke(this, e);
}
