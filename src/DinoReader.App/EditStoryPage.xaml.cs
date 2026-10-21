using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;

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
	private readonly StoryTranslator _translator;
	private readonly IServiceProvider _services;
	private StoryDraft? _draft;
	private Label? _languageValue;
	private Label? _pagesValue;
	private Label? _castValue;
	private readonly BusyGuard _busy = new();

	public EditStoryPage(IStoryLibrary library, StoryVoicing voicing, StoryActions actions, IServiceProvider services, StoryTranslator translator)
	{
		InitializeComponent();
		Header.Padding = UiFlow.Mirror(Header.Padding);
		_translator = translator;
		_library = library;
		_voicing = voicing;
		_actions = actions;
		_services = services;
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
		if (_draft is null)
		{
			return;
		}

		_pagesValue!.Text = GalleryText.PagesSummary(_draft.Pages.Count);
		_castValue!.Text = GalleryText.Characters(_draft.Cast.Count);
	}

	private void BuildRows(Story story)
	{
		RowsHost.Children.Clear();
		_languageValue = AddRow(AppResources.Story_Language, LanguageNames.NativeName(story.Language), ChooseLanguageAsync);
		AddTranslateRow(story);
		_pagesValue = AddRow(AppResources.Edit_Pages, GalleryText.PagesSummary(story.Pages.Count), OpenPagesAsync);
		AddRow(AppResources.Edit_FixText, AppResources.Edit_FixTextValue, OpenPagesAsync);
		_castValue = AddRow(CastText.Title, GalleryText.Characters(story.Cast.Count), OpenCastAsync);
	}

	// The switch only appears when this build has a translator (see StoryTranslator).
	private void AddTranslateRow(Story story)
	{
		if (!_translator.IsAvailable)
		{
			return;
		}

		var toggle = new ToggleSwitch { IsOn = story.TranslateToMyLanguage, VerticalOptions = LayoutOptions.Center };
		toggle.Toggled += (_, _) => _draft!.TranslateToMyLanguage = toggle.IsOn;
		RowsHost.Children.Add(SettingsRows.Row(AppResources.Story_Translate, toggle, null, _busy));
		RowsHost.Children.Add(SettingsRows.Divider());
	}

	// A new language recasts the story, because who speaks is worked out in the story's own language;
	// every character keeps the voice they had where the new language's pack has it, and the narrator moves
	// to that pack's voice when it does not.
	private async Task ChooseLanguageAsync()
	{
		StoryDraft draft = _draft!;
		if (await LanguageChooser.ChooseAsync(this) is not { } picked || picked == draft.Language)
		{
			return;
		}

		draft.Language = picked;
		_languageValue!.Text = LanguageNames.NativeName(picked);
		if (_voicing.VoicesFor(picked).All(v => v.Id != draft.NarratorVoiceId))
		{
			draft.NarratorVoiceId = _voicing.NarratorFor(picked);
		}

		draft.Cast = await _voicing.CastAsync(picked, Story.JoinPages(draft.Pages), draft.NarratorVoiceId, draft.Cast, draft.Merges);
		_castValue!.Text = GalleryText.Characters(draft.Cast.Count);
	}

	private Label AddRow(string title, string value, Func<Task>? tapped)
	{
		Label valueLabel = SettingsRows.ValueLabel(value);
		RowsHost.Children.Add(SettingsRows.Row(title, valueLabel, tapped, _busy));
		RowsHost.Children.Add(SettingsRows.Divider());
		return valueLabel;
	}

	// The cast page works on the draft as it stands (pages included) and writes its edits back into the
	// draft, so they are saved, or dropped, together with the rest of this screen's changes.
	private async Task OpenCastAsync()
	{
		StoryDraft draft = _draft!;
		draft.Title = string.IsNullOrWhiteSpace(TitleEntry.Text) ? draft.Original.Title : TitleEntry.Text.Trim();
		if (draft.TextChanged)
		{
			// The cast page must show the fixed text's characters; stored voices and merges stay as pins.
			draft.Cast = await _voicing.CastAsync(draft.Language, Story.JoinPages(draft.Pages), draft.NarratorVoiceId, draft.Cast, draft.Merges);
			_castValue!.Text = GalleryText.Characters(draft.Cast.Count);
		}

		var cast = _services.GetRequiredService<CastPage>();
		cast.Load(draft.Current, story => KeepCast(draft, story));
		await Navigation.PushAsync(cast);
	}

	private static Task KeepCast(StoryDraft draft, Story story)
	{
		draft.Cast = story.Cast;
		draft.Merges = story.Merges;
		draft.NarratorVoiceId = story.NarratorVoiceId;
		return Task.CompletedTask;
	}

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
			? await _voicing.CastAsync(draft.Language, Story.JoinPages(draft.Pages), draft.NarratorVoiceId, draft.Cast, draft.Merges)
			: draft.Cast;
		await _library.UpdateAsync(draft.Original with
		{
			Title = title, Language = draft.Language, TranslateToMyLanguage = draft.TranslateToMyLanguage,
			CoverStyle = draft.CoverStyle, Pages = draft.Pages.ToList(), Cast = cast,
			CharacterMerges = draft.Merges, NarratorVoiceId = draft.NarratorVoiceId,
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
