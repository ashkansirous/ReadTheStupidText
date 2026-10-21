using System.Diagnostics;
using DinoReader.Core;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>
/// Adds a story from typed or pasted text, or from a .txt/.pdf/.docx file. The text is cast once
/// here (who speaks, which voice); the grown-up sees the result and nothing is stored until Save.
/// </summary>
public partial class AddStoryPage : ContentPage, IGrownUpPage
{
	private const string DefaultLanguage = "en";
	private const int DebounceMilliseconds = 500;

	private static readonly FilePickerFileType SupportedFileTypes = new(new Dictionary<DevicePlatform, IEnumerable<string>>
	{
		{ DevicePlatform.Android, ["text/plain", "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"] },
		{ DevicePlatform.iOS, ["public.plain-text", "com.adobe.pdf", "org.openxmlformats.wordprocessingml.document"] },
	});

	private readonly IStoryLibrary _library;
	private readonly StoryVoicing _voicing;
	private readonly IDocumentTextExtractor _documents;
	private readonly ExternalActivityGuard _external;
	private readonly BusyGuard _busy = new();
	private string _narratorVoiceId = SupertonicVoiceTable.Default.Id;
	private IReadOnlyList<CastMember> _cast = [];
	private CancellationTokenSource? _analysis;

	public AddStoryPage(IStoryLibrary library, StoryVoicing voicing, IDocumentTextExtractor documents, ExternalActivityGuard external)
	{
		InitializeComponent();
		_library = library;
		_voicing = voicing;
		_documents = documents;
		_external = external;
		AddSourceTile(IconKind.Camera, "Scan pages", "Keeps the page pictures", 0, 0, null);
		AddSourceTile(IconKind.Photos, "Photos", "Pick page photos", 1, 0, null);
		AddSourceTile(IconKind.File, "PDF or file", ".pdf · .docx · .txt", 0, 1, OnFileTapped);
		AddSourceTile(IconKind.Type, "Type or paste", "Text only", 1, 1, OnTypeTapped);
	}

	private void AddSourceTile(IconKind icon, string title, string subtitle, int column, int row, EventHandler<TappedEventArgs>? tapped)
	{
		var tile = new Border
		{
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			Padding = 14,
			MinimumHeightRequest = 104,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
			Content = SourceTileContent(icon, title, subtitle),
			Opacity = tapped is null ? 0.45 : 1,
		};
		SemanticProperties.SetDescription(tile, title);
		if (tapped is not null)
		{
			var tap = new TapGestureRecognizer();
			tap.Tapped += tapped;
			tile.GestureRecognizers.Add(tap);
		}

		SourceGrid.Add(tile, column, row);
	}

	private static VerticalStackLayout SourceTileContent(IconKind icon, string title, string subtitle)
	{
		var chip = new Border
		{
			WidthRequest = 42,
			HeightRequest = 42,
			BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
			StrokeThickness = 0,
			HorizontalOptions = LayoutOptions.Start,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
			Content = new IconView { Kind = icon, Color = DinoTokens.Get(DinoTokens.Brand), Size = 22 },
		};
		return new VerticalStackLayout
		{
			Spacing = 8,
			Children =
			{
				chip,
				new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold },
				new Label { Text = subtitle, FontSize = 11.5, TextColor = DinoTokens.Get(DinoTokens.Muted) },
			},
		};
	}

	private async void OnBackTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(() => Navigation.PopAsync());

	private void OnTypeTapped(object? sender, TappedEventArgs e)
	{
		TextCard.IsVisible = true;
		TextEditor.Focus();
	}

	private async void OnFileTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(ImportFileAsync);

	private async Task ImportFileAsync()
	{
		FileResult? file = await PickFileAsync();
		// Parent is null once the app reset to the gallery during a long pick: discard the pick.
		if (file is null || Parent is null)
		{
			return;
		}

		string? text = await ExtractAsync(file.FullPath);
		if (text is not null)
		{
			TitleEntry.Text = Path.GetFileNameWithoutExtension(file.FileName);
			TextEditor.Text = text;
			TextCard.IsVisible = true;
		}
	}

	private async Task<FileResult?> PickFileAsync()
	{
		try
		{
			// The picker stops the window; the guard keeps the grown-up pages open meanwhile.
			return await _external.RunAsync(() => FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = "Choose a .txt, .pdf or .docx file",
				FileTypes = SupportedFileTypes,
			}));
		}
		catch (PermissionException)
		{
			await DisplayAlertAsync("Permission needed", "Grant file access to read a document.", "OK");
			return null;
		}
	}

	private async Task<string?> ExtractAsync(string path)
	{
		try
		{
			return await _documents.ExtractTextAsync(path);
		}
		catch (DocumentTooLargeException ex)
		{
			await DisplayAlertAsync("File too large", $"That file has {ex.Actual} pages; the limit is {ex.Limit}.", "OK");
		}
		catch (Exception)
		{
			await DisplayAlertAsync("Couldn't read that file", "The file may be corrupted or in an unsupported format.", "OK");
		}

		return null;
	}

	// Casting waits for a pause in typing, so a long paste is analysed once, off the UI thread.
	private async void OnTextChanged(object? sender, TextChangedEventArgs e)
	{
		_analysis?.Cancel();
		var analysis = _analysis = new CancellationTokenSource();
		try
		{
			await Task.Delay(DebounceMilliseconds, analysis.Token);
			await AnalyseAsync(e.NewTextValue ?? string.Empty, analysis.Token);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			// The preview is a convenience: a failed analysis hides the card, it never crashes the app.
			Debug.WriteLine($"Dino Reader cast preview failed: {ex}");
			ResultCard.IsVisible = false;
		}
	}

	private async Task AnalyseAsync(string text, CancellationToken token)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			ResultCard.IsVisible = false;
			return;
		}

		IReadOnlyList<CastMember> cast = await _voicing.CastAsync(text, _narratorVoiceId, []);
		token.ThrowIfCancellationRequested();
		_cast = cast;
		ShowResult(cast);
	}

	private void ShowResult(IReadOnlyList<CastMember> cast)
	{
		SummaryLabel.Text = GalleryText.ImportSummary(1, cast.Count);
		NameChips.Children.Clear();
		foreach (CastMember member in cast)
		{
			NameChips.Children.Add(NameChip(member.Name));
		}

		ResultCard.IsVisible = true;
	}

	private static Border NameChip(string name) => new()
	{
		BackgroundColor = Color.FromArgb("#1FFFFFFF"),
		StrokeThickness = 0,
		Padding = new Thickness(10, 4),
		Margin = new Thickness(0, 0, 6, 6),
		StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
		Content = new Label { Text = name, FontSize = 12.5, TextColor = Colors.White },
	};

	private async void OnSaveClicked(object? sender, EventArgs e) => await _busy.RunAsync(SaveAsync);

	private async Task SaveAsync()
	{
		string text = TextEditor.Text ?? string.Empty;
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		string title = string.IsNullOrWhiteSpace(TitleEntry.Text) ? StoryTitles.FromText(text) : TitleEntry.Text.Trim();
		int existing = (await _library.ListAsync()).Count;
		var story = new Story(StoryIds.New(), title, DefaultLanguage, DateTimeOffset.UtcNow,
			[new StoryPage(text)], await CastNowAsync(text), CoverStyle.Colour, existing, _narratorVoiceId);
		await _library.AddAsync(story);
		await Navigation.PopAsync();
	}

	// The cast shown on the result card is for the text as it was when typing paused; recast if it moved on.
	private async Task<IReadOnlyList<CastMember>> CastNowAsync(string text) =>
		await _voicing.CastAsync(text, _narratorVoiceId, _cast);
}
