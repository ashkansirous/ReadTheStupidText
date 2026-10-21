using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Documents;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>
/// Adds a story from typed or pasted text, or from a .txt/.pdf/.docx file. The text is cast once
/// here (who speaks, which voice); the grown-up sees the result and nothing is stored until Save.
/// </summary>
public partial class AddStoryPage : ContentPage, IGrownUpPage
{
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
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly StoryTranslator _translator;
	private readonly LanguageCard _languageCard;
	private string _language = StoryTranslator.MyLanguage;
	private bool _languageDetected;
	private bool _languageChosen;
	private string _narratorVoiceId = SupertonicVoiceTable.Default.Id;
	private IReadOnlyList<CastMember> _cast = [];
	private IReadOnlyDictionary<string, string> _merges = Story.NoMerges;
	private CancellationTokenSource? _analysis;

	public AddStoryPage(IStoryLibrary library, StoryVoicing voicing, IDocumentTextExtractor documents, ExternalActivityGuard external, IServiceProvider services, StoryTranslator translator)
	{
		InitializeComponent();
		Header.Padding = UiFlow.Mirror(Header.Padding);
		_translator = translator;
		_library = library;
		_voicing = voicing;
		_documents = documents;
		_external = external;
		_services = services;
		AddSourceTile(IconKind.Camera, AppResources.Add_Scan, AppResources.Add_ScanSub, 0, 0, null);
		AddSourceTile(IconKind.Photos, AppResources.Add_Photos, AppResources.Add_PhotosSub, 1, 0, null);
		AddSourceTile(IconKind.File, AppResources.Add_File, AppResources.Add_FileSub, 0, 1, OnFileTapped);
		AddSourceTile(IconKind.Type, AppResources.Add_Type, AppResources.Add_TypeSub, 1, 1, OnTypeTapped);
		_languageCard = new LanguageCard(_translator.IsAvailable, () => _busy.RunAsync(ChooseLanguageAsync), _busy);
		_languageCard.Show(_language, detected: false);
		LanguageHost.Children.Add(_languageCard);
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
				PickerTitle = AppResources.Add_PickerTitle,
				FileTypes = SupportedFileTypes,
			}));
		}
		catch (PermissionException)
		{
			await DisplayAlertAsync(AppResources.Add_PermissionTitle, AppResources.Add_PermissionBody, AppResources.Common_Ok);
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
			await DisplayAlertAsync(AppResources.Add_FileTooLargeTitle, AppResources.Format(AppResources.Add_FileTooLargeBody, ex.Actual, ex.Limit), AppResources.Common_Ok);
		}
		catch (Exception)
		{
			await DisplayAlertAsync(AppResources.Add_ReadFailedTitle, AppResources.Add_ReadFailedBody, AppResources.Common_Ok);
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
		token.ThrowIfCancellationRequested();
		if (string.IsNullOrWhiteSpace(text))
		{
			ResultCard.IsVisible = false;
			return;
		}

		DetectLanguage(text);
		IReadOnlyList<CastMember> cast = await _voicing.CastAsync(_language, text, _narratorVoiceId, _cast, _merges);
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

	// The language is guessed from the text until the grown-up picks one; "· detected" shows while the guess stands.
	private void DetectLanguage(string text)
	{
		if (_languageChosen)
		{
			return;
		}

		if (ScriptLanguageGuesser.Guess(text) is not { } guess)
		{
			ClearDetected();
			return;
		}

		if (guess == _language && _languageDetected)
		{
			return;
		}

		_language = guess;
		_languageDetected = true;
		UseLanguage();
	}

	// No guess any more: the language stays as it was, but it no longer claims to be detected.
	private void ClearDetected()
	{
		if (!_languageDetected)
		{
			return;
		}

		_languageDetected = false;
		_languageCard.Show(_language, detected: false);
	}

	private async Task ChooseLanguageAsync()
	{
		if (await LanguageChooser.ChooseAsync(this) is not { } picked)
		{
			return;
		}

		_language = picked;
		_languageChosen = true;
		_languageDetected = false;
		_analysis?.Cancel();
		UseLanguage();
		string text = TextEditor.Text ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(text))
		{
			_cast = await _voicing.CastAsync(_language, text, _narratorVoiceId, _cast, _merges);
			ShowResult(_cast);
		}
	}

	// Shows the language, and moves the narrator to a voice of that language's pack when it has no such voice.
	private void UseLanguage()
	{
		_languageCard.Show(_language, _languageDetected);
		if (_voicing.VoicesFor(_language).All(v => v.Id != _narratorVoiceId))
		{
			_narratorVoiceId = _voicing.NarratorFor(_language);
		}
	}

	private async void OnSaveClicked(object? sender, EventArgs e) => await _busy.RunAsync(SaveAsync);

	private async Task SaveAsync()
	{
		string text = TextEditor.Text ?? string.Empty;
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		string title = TitleOrDefault(text);
		int existing = (await _library.ListAsync()).Count;
		var story = new Story(StoryIds.New(), title, _language, DateTimeOffset.UtcNow,
			[new StoryPage(text)], await CastNowAsync(text), CoverStyle.Colour, existing, _narratorVoiceId, null, _merges, _languageCard.Translate);
		await _library.AddAsync(story);
		await Navigation.PopAsync();
	}

	// The cast shown on the result card is for the text as it was when typing paused; recast if it moved on.
	private async Task<IReadOnlyList<CastMember>> CastNowAsync(string text) =>
		await _voicing.CastAsync(_language, text, _narratorVoiceId, _cast, _merges);

	private async void OnCheckCastClicked(object? sender, EventArgs e) => await _busy.RunAsync(OpenCastAsync);

	// The cast page works on the story as typed so far, in memory: nothing is saved until Save story.
	private async Task OpenCastAsync()
	{
		string text = TextEditor.Text ?? string.Empty;
		if (string.IsNullOrWhiteSpace(text))
		{
			return;
		}

		// A pending debounced analysis would land after the cast page's edits and overwrite them.
		_analysis?.Cancel();
		_cast = await CastNowAsync(text);
		// The id here is provisional (the cast page only edits it in memory); Save story mints a fresh one per attempt.
		var story = new Story(StoryIds.New(), TitleOrDefault(text), _language, DateTimeOffset.UtcNow,
			[new StoryPage(text)], _cast, CoverStyle.Colour, 0, _narratorVoiceId, null, _merges, _languageCard.Translate);
		var page = _services.GetRequiredService<CastPage>();
		page.Load(story, KeepCast);
		await Navigation.PushAsync(page);
	}

	private Task KeepCast(Story story)
	{
		_cast = story.Cast;
		_merges = story.Merges;
		_narratorVoiceId = story.NarratorVoiceId;
		ShowResult(_cast);
		return Task.CompletedTask;
	}

	private string TitleOrDefault(string text) =>
		string.IsNullOrWhiteSpace(TitleEntry.Text) ? StoryTitles.FromText(text) : TitleEntry.Text.Trim();
}
