using System.Diagnostics;
using DinoReader.Core;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>
/// "Who's speaking" (mock screens 10, 11, 15), behind the gate: the narrator's voice, each character's
/// gender and voice with a preview, and a merge for two names that are one character. Every edit is
/// pinned (EDITED) and saved at once through the callback the opener gave <see cref="Load"/>: the
/// library for a saved story, the edit draft or the add screen otherwise. Reading picks the cast up
/// at its next start; the gate always stops the story first, so no read is live while this is open.
/// </summary>
public sealed class CastPage : ContentPage, IGrownUpPage
{
	private readonly StoryVoicing _voicing;
	private readonly ISpeechReader _reader;
	private readonly SpokenLanguage _spoken;
	private readonly BusyGuard _busy = new();
	private readonly VerticalStackLayout _body = new() { Padding = new Thickness(14, 0, 14, 14), Spacing = 10 };
	private readonly Label _subtitle = new() { FontSize = 12, TextColor = DinoTokens.Get(DinoTokens.Muted) };
	private readonly Border _mergeButton = new();
	private readonly CastMergeSheet _sheet = new();
	private readonly CastRowActions _actions;
	private readonly Grid _root = new();
	private Story? _story;
	private Func<Story, Task>? _persist;
	private AttributionResult? _resolved;
	private bool _previewing;
	private bool _previewHeard;

	public CastPage(StoryVoicing voicing, ISpeechReader reader, SpokenLanguage spoken)
	{
		_voicing = voicing;
		_reader = reader;
		_spoken = spoken;
		_actions = new CastRowActions(
			row => _busy.RunAsync(() => PreviewAsync(row)),
			(row, gender) => _busy.RunAsync(() => SetGenderAsync(row, gender)),
			row => _busy.RunAsync(() => PickVoiceAsync(row)));
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
		_root.FlowDirection = UiFlow.ForCurrentCulture();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		_reader.StateChanged += OnReaderStateChanged;
		_reader.Completed += OnReaderCompleted;
		if (_story is not null && _resolved is null)
		{
			await RefreshOrLeaveAsync();
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
		StopPreview();
		_reader.StateChanged -= OnReaderStateChanged;
		_reader.Completed -= OnReaderCompleted;
	}

	protected override bool OnBackButtonPressed()
	{
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
		column.Add(BuildHeader(), 0, 0);
		column.Add(scroll, 0, 1);
		column.Add(BuildMergeButton(), 0, 2);
		_root.Add(column);
		_root.Add(_sheet);
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
		var title = new Label { Text = CastText.Title, FontSize = 20, FontAttributes = FontAttributes.Bold };
		SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
		var header = new Grid
		{
			Padding = UiFlow.Mirror(new Thickness(6, 8, 14, 8)),
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
		CastViews.AddTap(_mergeButton, () => { _sheet.Show(Sheet().Rows); return Task.CompletedTask; });
		return _mergeButton;
	}

	private async Task RefreshAsync()
	{
		_resolved = await _voicing.ResolveAsync(_story!);
		Render();
	}

	private CastSheet Sheet() => _resolved is null || _story is null
		? new CastSheet([], 0, false, 0)
		: CastSheet.Build(_resolved, _story.Cast, _voicing.CharacterVoiceCount(_story.Language, _story.NarratorVoiceId));

	private void Render()
	{
		CastSheet sheet = Sheet();
		_body.Children.Clear();
		if (sheet.VoicesAreShort)
		{
			_body.Children.Add(CastRows.SharedNote(sheet.VoiceCount));
		}

		_body.Children.Add(NarratorCard());
		_body.Children.Add(CharacterCard(sheet));
		_mergeButton.IsVisible = sheet.Rows.Count >= 2;
	}

	private View NarratorCard()
	{
		Border card = CastViews.Card();
		card.Content = CastRows.Narrator(_story!.NarratorVoiceId, () => _busy.RunAsync(ChangeNarratorAsync));
		return card;
	}

	private View CharacterCard(CastSheet sheet)
	{
		var rows = new VerticalStackLayout();
		for (int i = 0; i < sheet.Rows.Count; i++)
		{
			rows.Children.Add(CastRows.Character(sheet.Rows[i], i, _actions));
			rows.Children.Add(CastViews.Divider());
		}

		rows.Children.Add(CastRows.Unknown(sheet.UnknownLines));
		Border card = CastViews.Card();
		card.Content = rows;
		return card;
	}

	private CastEdit Edit() => new(_story!.Cast, _story.Merges);

	private async Task ApplyAsync(Func<CastEdit, CastEdit> change)
	{
		Story previous = _story!;
		CastEdit edit = change(Edit());
		_story = previous with { Cast = edit.Cast, CharacterMerges = edit.Merges };
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
			await RefreshAsync();
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

	private async Task SetGenderAsync(CastRow row, Gender gender)
	{
		if (row.Member.Gender == gender)
		{
			return;
		}

		AttributionResult resolved = _resolved!;
		string narrator = _story!.NarratorVoiceId;
		await ApplyAsync(edit => CastEditor.SetGender(edit, resolved.Characters, _voicing.VoicesFor(_story!.Language), narrator, row.Member.CharacterId, gender));
		if (!CastEditor.HasVoiceOfGender(_voicing.VoicesFor(_story!.Language), narrator, gender))
		{
			await DisplayAlertAsync(CastText.Title, CastText.NoVoiceOfGender(gender), CastText.Ok);
		}
	}

	private async Task PickVoiceAsync(CastRow row)
	{
		string narrator = _story!.NarratorVoiceId;
		VoiceOption? voice = await ChooseVoiceAsync(CastEditor.VoiceChoices(_voicing.VoicesFor(_story.Language), narrator, row.Member.Gender));
		if (voice is not null && voice.Id != row.Member.VoiceId)
		{
			await ApplyAsync(edit => CastEditor.SetVoice(edit, row.Member.CharacterId, voice.Id, narrator));
		}
	}

	private async Task ChangeNarratorAsync()
	{
		VoiceOption? voice = await ChooseVoiceAsync(_voicing.VoicesFor(_story!.Language));
		if (voice is null || voice.Id == _story!.NarratorVoiceId)
		{
			return;
		}

		Story previous = _story;
		IReadOnlyList<CastMember>? cast = await RecastForNarratorAsync(previous, voice.Id);
		if (cast is null)
		{
			Render();
			return;
		}

		// A character the grown-up pinned to this voice loses it to the narrator: say so, and unpin them.
		IReadOnlyList<string> moved = CastEditor.NamesLosingVoice(previous.Cast, cast, voice.Id);
		_story = previous with { NarratorVoiceId = voice.Id, Cast = CastEditor.UnpinOverridden(previous.Cast, cast, voice.Id) };
		await SaveOrRollBackAsync(previous);
		if (moved.Count > 0 && ReferenceEquals(_story, previous) is false)
		{
			await DisplayAlertAsync(CastText.Title, CastText.VoiceChanged(moved), CastText.Ok);
		}
	}

	// Recasting takes the new narrator's voice off any character who had it; everyone else keeps theirs.
	private async Task<IReadOnlyList<CastMember>?> RecastForNarratorAsync(Story story, string narratorVoiceId)
	{
		try
		{
			return await _voicing.CastAsync(story.Language, story.Text, narratorVoiceId, story.Cast, story.Merges, story.BreakMap());
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader narrator recast failed: {ex}");
			await DisplayAlertAsync(CastText.SaveFailedTitle, CastText.SaveFailedBody, CastText.Ok);
			return null;
		}
	}

	private async Task<VoiceOption?> ChooseVoiceAsync(IReadOnlyList<VoiceOption> choices)
	{
		int at = await ChooseIndexAsync(CastText.VoiceTitle, choices.Select(v => CastText.VoiceName(v.Id)).ToList());
		return at < 0 ? null : choices[at];
	}

	// The index of the picked label, or -1 when cancelled; two voices can share a display name, so never map by text.
	private async Task<int> ChooseIndexAsync(string title, IReadOnlyList<string> labels)
	{
		string picked = await DisplayActionSheetAsync(title, CastText.Cancel, null, labels.ToArray());
		return picked is null || picked == CastText.Cancel ? -1 : labels.ToList().IndexOf(picked);
	}

	private async Task PreviewAsync(CastRow row)
	{
		StopPreview();
		string line = CastText.PreviewLineFor(_voicing.PreviewLanguage(_story!.Language, _spoken.Current));
		var segment = new VoicedSegment(line, row.Member.VoiceId, 0, line.Length, 0);
		try
		{
			_previewing = true;
			_previewHeard = false;
			await (_reader.SupportsSegments ? _reader.SpeakSegmentsAsync([segment]) : _reader.SpeakAsync(line));
		}
		catch (Exception ex)
		{
			_previewing = false;
			Debug.WriteLine($"Dino Reader voice preview failed: {ex}");
			await DisplayAlertAsync(CastText.Title, CastText.PreviewFailedBody, CastText.Ok);
		}
	}

	private void StopPreview()
	{
		if (_previewing)
		{
			_previewing = false;
			_reader.Stop();
		}
	}

	// The preview is over once its read ends or stops (after it was heard playing), so leaving the page
	// afterwards does not stop whatever else the reader is doing.
	private void OnReaderStateChanged(object? sender, PlaybackState state)
	{
		if (state == PlaybackState.Playing)
		{
			_previewHeard = true;
		}
		else if (state == PlaybackState.Idle && _previewHeard)
		{
			_previewing = false;
		}
	}

	private void OnReaderCompleted(object? sender, EventArgs e) => _previewing = false;
}
