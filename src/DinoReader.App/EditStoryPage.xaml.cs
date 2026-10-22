using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Images;

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
		RefreshCoverAfterPageEdits();
	}

	// Pages may have been moved or removed on the Pages screen: the preview and an open picker must follow.
	private void RefreshCoverAfterPageEdits()
	{
		RenderCover();
		if (CoverPickerHost.IsVisible)
		{
			ShowCoverPicker();
		}
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

	private void OnPhotoChipTapped(object? sender, TappedEventArgs e) => ShowCoverPicker();

	private void SetCover(CoverStyle style)
	{
		if (style == CoverStyle.FirstPage && !_draft!.HasPageImages)
		{
			return;
		}

		_draft!.ChooseCover(style);
		RenderCover();
		ShowCoverPicker();
	}

	// Tapping the cover opens or closes the picker (page pictures, a gallery photo, the colours).
	private void OnCoverTapped(object? sender, TappedEventArgs e)
	{
		if (CoverPickerHost.IsVisible)
		{
			CoverPickerHost.IsVisible = false;
			return;
		}

		ShowCoverPicker();
	}

	private void ShowCoverPicker()
	{
		if (CoverPickerHost.Content is CoverPicker picker)
		{
			picker.Refresh();
		}
		else
		{
			CoverPickerHost.Content = new CoverPicker(
				_draft!, _library, _services.GetRequiredService<ExternalActivityGuard>(), RenderCover);
		}

		CoverPickerHost.IsVisible = true;
	}

	private void RenderCover()
	{
		StoryDraft draft = _draft!;
		StyleChip(FirstPageChip, FirstPageLabel, draft.CoverStyle == CoverStyle.FirstPage, draft.HasPageImages);
		StyleChip(ColourChip, ColourLabel, draft.CoverStyle == CoverStyle.Colour, true);
		StyleChip(PhotoChip, PhotoLabel, draft.CoverStyle == CoverStyle.Picture && draft.CoverPageIndex is null, true);
		StoryTile tile = StoryTile.From(draft.Current with { Title = TitleEntry.Text ?? draft.Title }, _library);
		CoverPreview.BindingContext = draft.HasPendingCover ? tile with { CoverImagePath = draft.PendingCoverPhoto } : tile;
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
		string? coverFile = await StoreCoverFileAsync(draft);
		try
		{
			await _library.UpdateAsync(draft.Original with
			{
				Title = title, Language = draft.Language, TranslateToMyLanguage = draft.TranslateToMyLanguage,
				CoverStyle = draft.CoverStyle, CoverFile = coverFile, CoverPageIndex = draft.CoverPageIndex,
				CoverColourIndex = draft.CoverColourIndex, Pages = draft.Pages.ToList(), Cast = cast,
				CharacterMerges = draft.Merges, NarratorVoiceId = draft.NarratorVoiceId,
			});
		}
		catch
		{
			await DeleteCoverFileAsync(draft, coverFile, draft.Original.CoverFile);
			throw;
		}

		PickedPhotoCache.Delete(draft.PendingCoverPhoto);
		await DeleteCoverFileAsync(draft, draft.Original.CoverFile, coverFile);
		await Navigation.PopAsync();
	}

	// A photo picked as the cover is copied into the story only now, under a fresh name, so cancelling leaves
	// nothing behind and the image cache never shows the old photo. A cover that is not a photo keeps no file.
	private async Task<string?> StoreCoverFileAsync(StoryDraft draft)
	{
		if (draft.HasPendingCover)
		{
			var keeper = _services.GetRequiredService<IPageImageKeeper>();
			string stored = await _library.StoreFileAsync(draft.Original.Id, Story.CoverFilePrefix, Story.CoverFileExtension,
				target => keeper.KeepAsync(draft.PendingCoverPhoto!, target));
			return stored;
		}

		return draft.CoverStyle == CoverStyle.Picture && draft.CoverPageIndex is null ? draft.CoverFile : null;
	}

	// Removes a cover file that is no longer the story's (the old one after a save, the new one after a failed save).
	private async Task DeleteCoverFileAsync(StoryDraft draft, string? file, string? keep)
	{
		if (file is not null && file != keep)
		{
			await _library.DeleteFileAsync(draft.Original.Id, file);
		}
	}

	private async void OnDeleteClicked(object? sender, EventArgs e) => await _busy.RunAsync(DeleteAsync);

	private async Task DeleteAsync()
	{
		if (await _actions.ConfirmDeleteAsync(this, _draft!.Original))
		{
			await Navigation.PopAsync();
		}
	}

	private async void OnBackTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(CancelAsync);

	private async Task CancelAsync()
	{
		PickedPhotoCache.Delete(_draft?.PendingCoverPhoto);
		await Navigation.PopAsync();
	}
}
