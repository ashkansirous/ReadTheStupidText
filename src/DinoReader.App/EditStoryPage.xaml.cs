using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Edit screen (mock 8b): cover, title, and the pages. Changes live in a <see cref="StoryDraft"/>
/// until Save; a text change recasts the story but every character keeps the voice they had.
/// </summary>
public partial class EditStoryPage : ContentPage, IGrownUpPage
{
	private readonly IStoryLibrary _library;
	private readonly StoryVoicing _voicing;
	private readonly StoryActions _actions;
	private StoryDraft? _draft;
	private Label? _pagesValue;
	private readonly BusyGuard _busy = new();

	public EditStoryPage(IStoryLibrary library, StoryVoicing voicing, StoryActions actions)
	{
		InitializeComponent();
		_library = library;
		_voicing = voicing;
		_actions = actions;
	}

	/// <summary>Opens the page on <paramref name="story"/>. Called once, right after the page is created.</summary>
	public void Load(Story story)
	{
		_draft = new StoryDraft(story);
		TitleEntry.Text = story.Title;
		BuildRows(story);
		RenderCover();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (_pagesValue is not null && _draft is not null)
		{
			_pagesValue.Text = GalleryText.PagesSummary(_draft.Pages.Count);
		}
	}

	private void BuildRows(Story story)
	{
		RowsHost.Children.Clear();
		AddRow("Story language", "English", null);
		_pagesValue = AddRow("Pages", GalleryText.PagesSummary(story.Pages.Count), OpenPagesAsync);
		AddRow("Fix the text", "Edit OCR text", OpenPagesAsync);
		AddRow("Who’s speaking", GalleryText.Characters(story.Cast.Count), null);
	}

	private Label AddRow(string title, string value, Func<Task>? tapped)
	{
		var valueLabel = new Label
		{
			Text = value,
			FontSize = 14.5,
			FontAttributes = FontAttributes.Bold,
			TextColor = DinoTokens.Get(DinoTokens.Body),
			HorizontalTextAlignment = TextAlignment.End,
			VerticalOptions = LayoutOptions.Center,
		};
		var row = new Grid
		{
			MinimumHeightRequest = 50,
			Padding = new Thickness(14, 0),
			ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
			ColumnSpacing = 8,
		};
		row.Add(new Label { Text = title, FontSize = 14.5, VerticalOptions = LayoutOptions.Center }, 0);
		row.Add(valueLabel, 1);
		if (tapped is not null)
		{
			row.Add(Chevron(), 2);
			var tap = new TapGestureRecognizer();
			tap.Tapped += async (_, _) => await _busy.RunAsync(tapped);
			row.GestureRecognizers.Add(tap);
		}

		RowsHost.Children.Add(row);
		RowsHost.Children.Add(new BoxView { HeightRequest = 1, Color = DinoTokens.Get(DinoTokens.Hairline) });
		return valueLabel;
	}

	private static IconView Chevron() => new()
	{
		Kind = IconKind.Chevron,
		Color = DinoTokens.Get(DinoTokens.Muted),
		Size = 14,
		StrokeWidth = 1.6,
		VerticalOptions = LayoutOptions.Center,
	};

	private async Task OpenPagesAsync() => await Navigation.PushAsync(new PagesPage(_draft!));

	private void OnFirstPageChipTapped(object? sender, TappedEventArgs e) => SetCover(CoverStyle.FirstPage);

	private void OnColourChipTapped(object? sender, TappedEventArgs e) => SetCover(CoverStyle.Colour);

	private void SetCover(CoverStyle style)
	{
		if (style == CoverStyle.FirstPage && !_draft!.HasPageImages)
		{
			return;
		}

		_draft!.CoverStyle = style;
		RenderCover();
	}

	private void RenderCover()
	{
		StoryDraft draft = _draft!;
		bool firstPage = draft.CoverStyle == CoverStyle.FirstPage;
		StyleChip(FirstPageChip, FirstPageLabel, firstPage, draft.HasPageImages);
		StyleChip(ColourChip, ColourLabel, !firstPage, true);
		CoverPreview.BindingContext = StoryTile.From(
			draft.Original with { Title = TitleEntry.Text ?? draft.Title, CoverStyle = draft.CoverStyle, Pages = draft.Pages }, _library);
	}

	private static void StyleChip(Border chip, Label label, bool selected, bool enabled)
	{
		chip.BackgroundColor = selected ? DinoTokens.Get(DinoTokens.BrandTint) : Colors.White;
		label.TextColor = selected ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.Body);
		chip.Opacity = enabled ? 1 : 0.45;
	}

	private async void OnSaveClicked(object? sender, EventArgs e) => await _busy.RunAsync(SaveAsync);

	private async Task SaveAsync()
	{
		StoryDraft draft = _draft!;
		string title = string.IsNullOrWhiteSpace(TitleEntry.Text) ? draft.Original.Title : TitleEntry.Text.Trim();
		IReadOnlyList<CastMember> cast = draft.TextChanged
			? await _voicing.CastAsync(Story.JoinPages(draft.Pages), draft.Original.NarratorVoiceId, draft.Original.Cast)
			: draft.Original.Cast;
		await _library.UpdateAsync(draft.Original with
		{
			Title = title, CoverStyle = draft.CoverStyle, Pages = draft.Pages.ToList(), Cast = cast,
		});
		await Navigation.PopAsync();
	}

	private async void OnDeleteClicked(object? sender, EventArgs e) => await _busy.RunAsync(DeleteAsync);

	private async Task DeleteAsync()
	{
		if (await _actions.ConfirmDeleteAsync(this, _draft!.Original))
		{
			await Navigation.PopAsync();
		}
	}

	private async void OnBackTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(() => Navigation.PopAsync());
}
