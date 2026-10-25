using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>
/// "Who's speaking" (mock screens 10, 11, 15 and, since Slice 116, frames 52-53), behind the gate: the narrator, each story character's
/// storyteller character with a preview and a picker sheet, and a merge for two names that are one character. Every edit is
/// pinned (EDITED) and saved at once through the callback the opener gave <see cref="Load"/>: the
/// library for a saved story, the edit draft or the add screen otherwise. Reading picks the cast up
/// at its next start; the gate always stops the story first, so no read is live while this is open.
/// </summary>
public sealed class CastPage : ContentPage, IGrownUpPage
{
	private readonly StoryVoicing _voicing;
	private readonly ISpeechReader _reader;
	private readonly SpokenLanguage _spoken;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly VerticalStackLayout _body = new() { Padding = new Thickness(14, 0, 14, 14), Spacing = 10 };
	private readonly Label _subtitle = new() { FontSize = 12, TextColor = DinoTokens.Get(DinoTokens.Muted) };
	private readonly Border _mergeButton = new();
	private readonly ContentView _languageChip = new() { Margin = new Thickness(14, 0, 14, 10) };
	private readonly CastMergeSheet _sheet = new();
	private readonly CastCharacterPicker _picker = new();
	private readonly CastAnimalPicker _animalPicker = new();
	private readonly CastRowActions _actions;
	private readonly StorytellerDefault _storyteller;
	private readonly Grid _root = new();
	private Story? _story;
	private Func<Story, Task>? _persist;
	private AttributionResult? _resolved;
	private string _castLanguage = string.Empty;
	private readonly VoicePreview _preview;
	private bool _returningFromNarrator;

	public CastPage(StoryVoicing voicing, ISpeechReader reader, SpokenLanguage spoken, IServiceProvider services)
	{
		_services = services;
		_storyteller = services.GetRequiredService<StorytellerDefault>();
		_voicing = voicing;
		_reader = reader;
		_preview = new VoicePreview(reader);
		_spoken = spoken;
		_actions = new CastRowActions(
			row => _busy.RunAsync(() => PreviewAsync(row.Member.VoiceId, row.Flavour)),
			row => _busy.RunAsync(() => PickCharacterAsync(row)),
			row => _busy.RunAsync(() => PickAnimalAsync(row)));
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		_sheet.ChooseKeep = labels => ChooseIndexAsync(CastText.KeepName, labels);
		_sheet.Confirmed += (keep, drop) => _busy.RunAsync(() => ApplyAsync(edit => CastEditor.Merge(edit, keep, drop)));
		Content = BuildLayout();
	}

	/// <summary>Opens the page on <paramref name="story"/>; <paramref name="persist"/> is called after every edit.</summary>
	public void Load(Story story, Func<Story, Task> persist)
	{
		_story = story;
		_persist = persist;
		_subtitle.Text = story.Title;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		_preview.Attach();
		_storyteller.LibraryRecast -= OnLibraryRecast;
		_storyteller.LibraryRecast += OnLibraryRecast;
		if (_story is not null && (_resolved is null || _returningFromNarrator))
		{
			_returningFromNarrator = false;
			await _busy.RunAsync(RefreshOrLeaveAsync);
		}
	}

	private async Task RefreshOrLeaveAsync()
	{
		try
		{
			await RefreshAsync();
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader cast load failed: {ex}");
			await DisplayAlertAsync(CastText.LoadFailedTitle, CastText.LoadFailedBody, CastText.Ok);
			await Navigation.PopAsync();
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_preview.Detach();
		_storyteller.LibraryRecast -= OnLibraryRecast;
	}

	// The storyteller pack finished installing in the background and the library was re-cast (Slice 115): this book is read again so the next edit does not save the old cast over it.
	private void OnLibraryRecast(object? sender, EventArgs e) =>
		MainThread.BeginInvokeOnMainThread(() => _ = _busy.RunWhenFreeAsync(ReloadAfterRecastAsync));

	private async Task ReloadAfterRecastAsync()
	{
		if (_story is null)
		{
			return;
		}

		Story? fresh = await _services.GetRequiredService<IStoryLibrary>().GetAsync(_story.Id);
		if (fresh is null)
		{
			return;
		}

		_story = fresh;
		await RefreshOrLeaveAsync();
	}

	protected override bool OnBackButtonPressed()
	{
		if (_picker.IsVisible)
		{
			_picker.Dismiss();
			return true;
		}

		if (_animalPicker.IsVisible)
		{
			_animalPicker.Dismiss();
			return true;
		}

		if (!_sheet.IsVisible)
		{
			return base.OnBackButtonPressed();
		}

		_sheet.Hide();
		return true;
	}

	private Grid BuildLayout()
	{
		var scroll = new ScrollView { Content = _body };
		var column = new Grid
		{
			RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)],
		};
		column.Add(new VerticalStackLayout { Children = { BuildHeader(), _languageChip } }, 0, 0);
		column.Add(scroll, 0, 1);
		column.Add(new VerticalStackLayout { Children = { BuildMergeButton() } }, 0, 2);
		_root.Add(column);
		_root.Add(_sheet);
		_root.Add(_picker);
		_root.Add(_animalPicker);
		return _root;
	}

	private Grid BuildHeader()
	{
		var back = new Border
		{
			WidthRequest = 44, HeightRequest = 44, StrokeThickness = 0, BackgroundColor = Colors.Transparent,
			Content = new IconView { Kind = IconKind.Back, Color = DinoTokens.Get(DinoTokens.Ink), Size = 22, StrokeWidth = 2.6 },
		};
		SemanticProperties.SetDescription(back, CastText.Back);
		CastViews.AddTap(back, () => _busy.RunAsync(() => Navigation.PopAsync()));
		Label title = CastText.InOwnScriptAligned(new Label { Text = CastText.Title, FontSize = 20, FontAttributes = FontAttributes.Bold });
		CastText.AlignWith(_subtitle, title);
		SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
		var header = new Grid
		{
			Padding = new Thickness(6, 8, 14, 8),
			ColumnSpacing = 6,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
		};
		header.Add(back, 0);
		header.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { title, _subtitle } }, 1);
		return header;
	}

	private Border BuildMergeButton()
	{
		_mergeButton.IsVisible = false;
		_mergeButton.HeightRequest = 48;
		_mergeButton.Margin = new Thickness(14, 0, 14, 14);
		_mergeButton.BackgroundColor = Colors.Transparent;
		_mergeButton.Stroke = DinoTokens.Get(DinoTokens.MergeBorder);
		_mergeButton.StrokeThickness = 2;
		_mergeButton.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 24 };
		_mergeButton.Content = new Label
		{
			Text = CastText.MergeButton, FontSize = 15, FontAttributes = FontAttributes.Bold,
			TextColor = DinoTokens.Get(DinoTokens.Brand), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
		};
		SemanticProperties.SetDescription(_mergeButton, CastText.MergeButton);
		CastViews.AddTap(_mergeButton, () => { _sheet.Show(Sheet().Rows, _castLanguage); return Task.CompletedTask; });
		return _mergeButton;
	}

	// The housekeeping (a missing cast, a clash with the narrator) is only needed when the page opens or the narrator may have changed, not after each edit.
	private async Task RefreshAsync(bool housekeeping = true)
	{
		_castLanguage = _voicing.ReadingLanguage(_story!);
		if (housekeeping)
		{
			await EnsureCastAsync();
			await FreeNarratorVoiceAsync();
		}

		_resolved = await _voicing.ResolveAsync(_story!, _castLanguage);
		Render();
	}

	// The language the book will be read in may have no cast yet (a first look at a book read in another language): make and keep it (Slice 108).
	private async Task EnsureCastAsync()
	{
		StoryVoicing.NewCast? made = await _voicing.NewCastForReadingAsync(_story!);
		if (made is null)
		{
			return;
		}

		_story = _story!.WithCastIn(made.Language, made.Members);
		// A failed save (already reported) keeps the cast on screen: the next edit saves it with the story.
		await SaveAsync();
	}

	private CastSheet Sheet() => _resolved is null || _story is null
		? new CastSheet([], 0, false, 0)
		: CastSheet.Build(_resolved, _story.CastIn(_castLanguage), _voicing.CharacterVoiceCount(_castLanguage), LanguageRules.For(_story.Language), roster: _voicing.RosterFor(_castLanguage));

	private void Render()
	{
		CastSheet sheet = Sheet();
		_body.Children.Clear();
		_languageChip.Content = CastViews.LanguageChip(CastText.VoicesFor(_castLanguage));
		if (sheet.VoicesAreShort)
		{
			_body.Children.Add(CastRows.SharedNote(sheet.VoiceCount));
		}

		_body.Children.Add(NarratorCard());
		_body.Children.Add(CharacterCard(sheet));
		_body.Children.Add(AnimalSoundsCard());
		if (PictureTextBook.IsPictureTextBook(_story!.Pages))
		{
			_body.Children.Add(WordsOverPicturesCard());
		}

		_mergeButton.IsVisible = sheet.Rows.Count >= 2;
	}

	private View NarratorCard()
	{
		Border card = CastViews.Card();
		card.Content = CastRows.Narrator(_voicing.NarratorOf(_castLanguage), _castLanguage, () => _busy.RunAsync(OpenNarratorPageAsync));
		return card;
	}

	private View CharacterCard(CastSheet sheet)
	{
		var rows = new VerticalStackLayout();
		IReadOnlyDictionary<string, AnimalKind> animals = Animals();
		for (int i = 0; i < sheet.Rows.Count; i++)
		{
			AnimalKind? animal = animals.TryGetValue(sheet.Rows[i].Member.CharacterId, out AnimalKind kind) ? kind : null;
			rows.Children.Add(CastRows.Character(sheet.Rows[i], i, _actions, _castLanguage, animal));
			rows.Children.Add(CastViews.Divider());
		}

		rows.Children.Add(CastRows.Unknown(sheet.UnknownLines));
		Border card = CastViews.Card();
		card.Content = rows;
		return card;
	}

	// What each character is (Slice 125): the stored animal, then its name, then a guess from the story text.
	private IReadOnlyDictionary<string, AnimalKind> Animals() =>
		_resolved is null ? new Dictionary<string, AnimalKind>() : AnimalSounds.Resolve(_resolved.Characters, _story!.Language, _story.CastIn(_castLanguage), _story.Text);

	// The per-story switch is saved with the story (Slice 117); a story with no sounding animal shows it dimmed and keeps the value.
	private View AnimalSoundsCard()
	{
		bool hasAnimals = _resolved is not null && AnimalSounds.AnimalsIn(_resolved.Characters, _story!.Language, _story.CastIn(_castLanguage), _story.Text).Count > 0;
		return CastRows.AnimalSoundsRow(hasAnimals, _story!.AnimalSoundsOn, on => _ = _busy.RunAsync(() => SetAnimalSoundsAsync(on)));
	}

	// Saved with the story (Slice 122); the reading screen follows the next time the book is opened.
	private View WordsOverPicturesCard() =>
		CastRows.WordsOverPicturesRow(PictureTextBook.ShowsWordsOverPictures(_story!), on => _ = _busy.RunAsync(() => SetWordsOverPicturesAsync(on)));

	private async Task SetWordsOverPicturesAsync(bool on)
	{
		Story previous = _story!;
		_story = previous with { WordsOverPictures = on };
		if (!await SaveAsync())
		{
			_story = previous;
			Render();
		}
	}

	private async Task SetAnimalSoundsAsync(bool on)
	{
		Story previous = _story!;
		_story = previous with { AnimalSoundsOn = on };
		if (!await SaveAsync())
		{
			_story = previous;
			Render();
		}
	}

	// Edits go into the reading language's cast; merges are the story's, shared by every language.
	private async Task ApplyAsync(Func<CastEdit, CastEdit> change)
	{
		Story previous = _story!;
		_story = CastEditor.Close(previous, _castLanguage, change(CastEditor.Open(previous, _castLanguage)));
		await SaveOrRollBackAsync(previous);
	}

	// A failed save puts the story back as it was, so the screen never shows an edit that was not kept.
	private async Task SaveOrRollBackAsync(Story previous)
	{
		if (await SaveAsync())
		{
			await RefreshAfterSaveAsync();
			return;
		}

		_story = previous;
		Render();
	}

	// The edit is saved by now: a failed refresh shows the saved story again instead of leaving the old rows.
	private async Task RefreshAfterSaveAsync()
	{
		try
		{
			await RefreshAsync(housekeeping: false);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader cast refresh failed: {ex}");
			Render();
			await DisplayAlertAsync(CastText.LoadFailedTitle, CastText.LoadFailedBody, CastText.Ok);
		}
	}

	private async Task<bool> SaveAsync()
	{
		try
		{
			await _persist!(_story!);
			return true;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader cast save failed: {ex}");
			await DisplayAlertAsync(CastText.SaveFailedTitle, CastText.SaveFailedBody, CastText.Ok);
			return false;
		}
	}

	// The picker sheet (Slice 116, mock frame 53): the pick is pinned (voice and flavour together) in this language's cast; a character already used in this book is labelled, not blocked.
	private async Task PickCharacterAsync(CastRow row)
	{
		IReadOnlyList<StorytellerCharacter> choices = _voicing.ChoicesFor(_castLanguage);
		IReadOnlyList<CastRow> rows = Sheet().Rows;
		StorytellerCharacter? picked = await _picker.PickAsync(
			row.Member.Name, _castLanguage, choices, row.Character, row.Member.Gender, character => UsedBy(rows, character, row), PreviewCharacterAsync);
		// Keeping the character a row already shows still pins it when the row is not yet edited, so a later narrator or pack change cannot re-cast it.
		bool changed = picked is not null && (picked.VoiceId != row.Member.VoiceId || picked.Flavour != row.Flavour || !row.Member.Edited);
		if (picked is not null && changed)
		{
			await ApplyAsync(edit => CastEditor.SetCharacter(edit, row.Member.CharacterId, picked));
		}
	}

	// The animal sheet (Slice 125): the animal is the character's in every language's cast; its voice is picked again unless the grown-up chose that by hand.
	private async Task PickAnimalAsync(CastRow row)
	{
		AnimalKind? current = Animals().TryGetValue(row.Member.CharacterId, out AnimalKind kind) ? kind : null;
		AnimalPick? picked = await _animalPicker.PickAsync(row.Member.Name, current);
		if (picked is not null && picked.Kind != current)
		{
			await ApplyAnimalAsync(row.Member.CharacterId, picked.Kind);
		}
	}

	private async Task ApplyAnimalAsync(string characterId, AnimalKind? animal)
	{
		Story previous = _story!;
		CastEdit edit = CastEditor.SetAnimal(CastEditor.Open(previous, _castLanguage), characterId, animal);
		IReadOnlyList<CastMember> cast = await _voicing.RecastCharacterAsync(previous, _castLanguage, edit.Cast, characterId);
		_story = CastEditor.WithAnimal(CastEditor.Close(previous, _castLanguage, edit with { Cast = cast }), characterId, animal);
		await SaveOrRollBackAsync(previous);
	}

	// The name of another story character of this book who already has the character (the same voice and flavour), for the picker's hint.
	private static string? UsedBy(IReadOnlyList<CastRow> rows, StorytellerCharacter character, CastRow asking) =>
		rows.FirstOrDefault(r => r.Member.CharacterId != asking.Member.CharacterId && r.Character?.Id == character.Id)?.Member.Name;

	private Task PreviewCharacterAsync(StorytellerCharacter character) => PreviewAsync(character.VoiceId, character.Flavour);

	// The Narrator page chooses the voice for every book; the grown-up comes back here and the cast is looked at again (Slice 109).
	private async Task OpenNarratorPageAsync()
	{
		var page = _services.GetRequiredService<NarratorPage>();
		_returningFromNarrator = true;
		await Navigation.PushAsync(page);
	}

	// A character who has the narrator's voice (the narrator changed since this cast was made) gets another one, quietly (Slice 109).
	private async Task FreeNarratorVoiceAsync()
	{
		try
		{
			Story? freed = await NarratorRecast.TryRecastAsync(_voicing, _story!);
			if (freed is not null)
			{
				_story = freed;
				await SaveAsync();
			}
		}
		catch (Exception ex)
		{
			// Housekeeping only: the cast screen still opens, with the stored voices as they are.
			Debug.WriteLine($"Dino Reader narrator recast failed: {ex}");
		}
	}

	// The index of the picked label, or -1 when cancelled; two voices can share a display name, so never map by text.
	private async Task<int> ChooseIndexAsync(string title, IReadOnlyList<string> labels)
	{
		string picked = await DisplayActionSheetAsync(title, CastText.Cancel, null, labels.ToArray());
		return picked is null || picked == CastText.Cancel ? -1 : labels.ToList().IndexOf(picked);
	}

	private async Task PreviewAsync(string voiceId, VoiceFlavour flavour)
	{
		string line = CastText.PreviewLineFor(_voicing.PreviewLanguage(_castLanguage, _spoken.Current));
		try
		{
			await _preview.PlayAsync(line, voiceId, flavour);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader voice preview failed: {ex}");
			await DisplayAlertAsync(CastText.Title, CastText.PreviewFailedBody, CastText.Ok);
		}
	}
}
