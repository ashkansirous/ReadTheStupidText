using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>
/// "Who's speaking" (mock screens 10, 11, 15 and, since Slice 116, frames 52-53), behind the gate: the narrator, each story character's
/// storyteller character with a preview and a picker sheet. Every edit is
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
	private readonly ContentView _languageChip = new() { Margin = new Thickness(14, 0, 14, 10) };
	private readonly CastCharacterPicker _picker = new();
	private readonly CastRowActions _actions;
	private readonly StorytellerDefault _storyteller;
	private readonly Grid _root = new();
	private Story? _story;
	private Func<Story, Task>? _persist;
	private AttributionResult? _resolved;
	private string _castLanguage = string.Empty;
	private readonly VoicePreview _preview;
	private bool _returningFromNarrator;
	private readonly WaitOverlay _waitOverlay = DinoWait.OverPage();
	private readonly WaitController _wait;
	private IReadOnlyList<CastRow> _shownRows = [];
	private CancellationTokenSource _pageCancel = new(); // exists before OnAppearing so an early tap is still cancelled on leaving
	private CancellationTokenSource? _previewCancel;

	public CastPage(StoryVoicing voicing, ISpeechReader reader, SpokenLanguage spoken, IServiceProvider services)
	{
		_services = services;
		_storyteller = services.GetRequiredService<StorytellerDefault>();
		_voicing = voicing;
		_reader = reader;
		_preview = new VoicePreview(reader);
		_spoken = spoken;
		_actions = new CastRowActions(
			PreviewRowAsync,
			row => _busy.RunAsync(() => PickCharacterAsync(row)),
			row => _busy.RunAsync(() => BackToBookVoiceAsync(row)));
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		_wait = new WaitController(_waitOverlay);
		_wait.CancelRequested += OnWaitCancelled;
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
		_pageCancel.Cancel();
		_pageCancel.Dispose();
		_pageCancel = new CancellationTokenSource();
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
		_pageCancel.Cancel(); // a queued or running sample never plays on the screen the grown-up went back to
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

		return base.OnBackButtonPressed();
	}

	private Grid BuildLayout()
	{
		var scroll = new ScrollView { Content = _body };
		var column = new Grid
		{
			RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
		};
		column.Add(new VerticalStackLayout { Children = { BuildHeader(), _languageChip } }, 0, 0);
		column.Add(scroll, 0, 1);
		_root.Add(column);
		_root.Add(_picker);
		_root.Add(_waitOverlay); // last, so it covers the picker sheet too
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

	// The housekeeping (a missing cast, a clash with the narrator) is only needed when the page opens or the narrator may have changed, not after each edit.
	private async Task RefreshAsync(bool housekeeping = true)
	{
		_castLanguage = _voicing.ReadingLanguage(_story!, StoryTranslator.MyLanguage); // a grown-up page: the app language, never the child's pick
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
		StoryVoicing.NewCast? made = await _voicing.NewCastForReadingAsync(_story!, StoryTranslator.MyLanguage);
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
		_shownRows = sheet.Rows;
		_body.Children.Clear();
		_languageChip.Content = CastViews.LanguageChip(CastText.VoicesFor(_castLanguage));
		if (sheet.VoicesAreShort)
		{
			_body.Children.Add(CastRows.SharedNote(sheet.VoiceCount));
		}

		_body.Children.Add(NarratorCard());
		_body.Children.Add(CharacterCard(sheet));
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
		IReadOnlyList<StorytellerCharacter> speakers = CastCharacters.BookableSpeakers(_voicing.RosterFor(_castLanguage), _voicing.NarratorOf(_castLanguage));
		for (int i = 0; i < sheet.Rows.Count; i++)
		{
			CastRow row = sheet.Rows[i];
			rows.Children.Add(CastRows.Character(row, i, _actions, _castLanguage, CastEditor.OffersBookVoice(row.Member, row.Flavour, speakers)));
			rows.Children.Add(CastViews.Divider());
		}

		rows.Children.Add(CastRows.Unknown(sheet.UnknownLines));
		Border card = CastViews.Card();
		card.Content = rows;
		return card;
	}

	// Edits go into the reading language's cast.
	private async Task<bool> ApplyAsync(Func<CastEdit, CastEdit> change)
	{
		Story previous = _story!;
		_story = CastEditor.Close(previous, _castLanguage, change(CastEditor.Open(previous, _castLanguage)));
		return await SaveOrRollBackAsync(previous);
	}

	// A failed save puts the story back as it was, so the screen never shows an edit that was not kept.
	private async Task<bool> SaveOrRollBackAsync(Story previous)
	{
		if (await SaveAsync())
		{
			await RefreshAfterSaveAsync();
			return true;
		}

		_story = previous;
		Render();
		return false;
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
			// Worked out before the apply, from the row as the grown-up saw it; a voice and a flavour changed together count as both.
			string[] kinds = ChangeKinds(row, picked);
			bool kept = await ApplyAsync(edit => CastEditor.SetCharacter(edit, row.Member.CharacterId, picked));
			// Counted only when the save kept the change; a keep that only pins the same voice and flavour changed nothing.
			if (kept)
			{
				foreach (string kind in kinds)
				{
					CountVoiceChange(kind);
				}
			}
		}
	}

	// "Back to the book's voice" (Slice 156): the grown-up's pick is dropped and the character is cast again, which gives it the book's suggestion (or its closest traits).
	private async Task BackToBookVoiceAsync(CastRow row)
	{
		Story previous = _story!;
		CastEdit released = CastEditor.UseBookVoice(CastEditor.Open(previous, _castLanguage), row.Member.CharacterId);
		IReadOnlyList<CastMember> recast = await Task.Run(() => _voicing.RecastCharacter(previous, _castLanguage, released.Cast, row.Member.CharacterId));
		_story = CastEditor.Close(previous, _castLanguage, new CastEdit(recast));
		await SaveOrRollBackAsync(previous);
	}

	private void CountVoiceChange(string kind)
	{
		try
		{
			_services.GetRequiredService<UsageCounter>().RecordGeneral(UsageEventName.VoiceChanged, _castLanguage, kind);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not count a voice change: {ex.Message}");
		}
	}

	// A new voice is a character voice change, another flavour is a flavour change; a pick can be both (Slice 134).
	private static string[] ChangeKinds(CastRow row, StorytellerCharacter picked)
	{
		var kinds = new List<string>();
		if (picked.VoiceId != row.Member.VoiceId)
		{
			kinds.Add(UsageDetail.ChangeCharacterVoice);
		}

		if (picked.Flavour != row.Flavour)
		{
			kinds.Add(UsageDetail.ChangeFlavour);
		}

		return [.. kinds];
	}

	// The name of another story character of this book who already has the character (the same voice and flavour), for the picker's hint.
	private static string? UsedBy(IReadOnlyList<CastRow> rows, StorytellerCharacter character, CastRow asking) =>
		rows.FirstOrDefault(r => r.Member.CharacterId != asking.Member.CharacterId && r.Character?.Id == character.Id)?.Member.Name;

	private Task PreviewCharacterAsync(StorytellerCharacter character) => PreviewAsync(() => new PreviewTarget(character.VoiceId, character.Flavour), waitForEdit: false);

	// The Narrator page chooses the voice for every book; the grown-up comes back here and the cast is looked at again (Slice 109).
	private async Task OpenNarratorPageAsync()
	{
		var page = _services.GetRequiredService<NarratorPage>();
		page.Language = _castLanguage;
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

	// A row's play button (Slice 113 follow-up). The sample takes seconds to synthesize and play, so it never holds the busy guard (a second tap
	// used to be dropped silently); it does wait for an edit in flight (the flavour just picked) and then plays the row as it is now, not as it was drawn.
	private Task PreviewRowAsync(CastRow tapped) =>
		PreviewAsync(() => PreviewTarget.Of(CastRowLookup.Current(_shownRows, tapped)), waitForEdit: true);

	private void OnWaitCancelled()
	{
		_previewCancel?.Cancel();
		_preview.Stop();
	}

	// One sample at a time: a new tap cancels the one before it, and leaving the page cancels both. The overlay (after 500 ms, Cancel) covers the
	// wait for an edit in flight and the first, slow synthesis of a flavour.
	private async Task PreviewAsync(Func<PreviewTarget> target, bool waitForEdit)
	{
		_previewCancel?.Cancel();
		using CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(_pageCancel.Token);
		_previewCancel = cancel;
		_wait.Begin(WaitStage.PreparingVoice, AppResources.Wait_GettingVoices, canCancel: true);
		bool failed = false;
		try
		{
			if (waitForEdit)
			{
				await _busy.WhenFreeAsync(cancel.Token);
			}

			PreviewTarget now = target();
			string line = CastText.PreviewLineFor(_voicing.PreviewLanguage(_castLanguage, _spoken.Current));
			await _preview.PlayAsync(line, now.VoiceId, now.Flavour).WaitAsync(cancel.Token);
		}
		catch (OperationCanceledException)
		{
			Debug.WriteLine("Dino Reader voice preview cancelled.");
			StopIfCurrent(cancel);
		}
		catch (Exception ex) when (!cancel.IsCancellationRequested)
		{
			Debug.WriteLine($"Dino Reader voice preview failed: {ex}");
			failed = true;
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader voice preview ended after cancel: {ex.Message}");
		}
		finally
		{
			EndPreview(cancel);
		}

		if (failed)
		{
			await DisplayAlertAsync(CastText.Title, CastText.PreviewFailedBody, CastText.Ok);
		}
	}

	// Cancel (or leaving) stops a sample that already started; a sample a newer tap replaced is left to that tap.
	private void StopIfCurrent(CancellationTokenSource mine)
	{
		if (ReferenceEquals(_previewCancel, mine))
		{
			_preview.Stop();
		}
	}

	// A cancelled older sample must not end the wait a newer tap has begun.
	private void EndPreview(CancellationTokenSource mine)
	{
		if (ReferenceEquals(_previewCancel, mine))
		{
			_previewCancel = null;
			_wait.End();
		}
	}

	private readonly record struct PreviewTarget(string VoiceId, VoiceFlavour Flavour)
	{
		public static PreviewTarget Of(CastRow row) => new(row.Member.VoiceId, row.Flavour);
	}
}
