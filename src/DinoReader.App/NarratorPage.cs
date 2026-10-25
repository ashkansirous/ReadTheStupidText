using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Domain.Reading;

namespace DinoReader;

/// <summary>
/// The grown-up Narrator page (Slice 109, Decision 114, mock 39): one card per language with an installed voice pack (none for a language without one), my language first,
/// each with a narrator picker (an open list of the language's narrator characters, each with plain words and a sample, Slice 116, mock frame 55) and a sample. The pick is the narrator of every book read in that language (starter, store and own), so a change
/// re-casts, quietly, any character of any saved book who had that voice (<see cref="NarratorRecast"/>).
/// </summary>
public sealed class NarratorPage : ContentPage, IGrownUpPage
{
	private const double CardRadius = 18;
	private const double PickerHeight = 44;
	private const double SampleSize = 44;
	private const double ChevronSize = 12;
	private const double RowHeight = 52;
	private const double CheckSize = 18;

	private readonly StoryVoicing _voicing;
	private readonly IStoryLibrary _library;
	private readonly VoicePreview _preview;
	private readonly SpokenLanguage _spoken;
	private readonly ISettingsStore _settings;
	private readonly BusyGuard _busy = new();
	private readonly VerticalStackLayout _cards = new() { Spacing = 10 };
	private readonly HashSet<string> _open = [];

	public NarratorPage(StoryVoicing voicing, IStoryLibrary library, ISpeechReader reader, SpokenLanguage spoken, ISettingsStore settings)
	{
		_voicing = voicing;
		_library = library;
		_preview = new VoicePreview(reader);
		_spoken = spoken;
		_settings = settings;
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		Content = BuildLayout();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_preview.Attach();
		Rebuild();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_preview.Detach();
	}

	private Grid BuildLayout()
	{
		var footnote = new Label
		{
			Text = AppResources.Narrator_Footnote, FontSize = 13, LineHeight = 1.45, TextColor = DinoTokens.Get(DinoTokens.Body), Margin = new Thickness(4, 2),
		};
		var body = new VerticalStackLayout { Padding = new Thickness(14, 0, 14, 20), Spacing = 10, Children = { _cards, footnote } };
		var root = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
		root.Add(BuildHeader(), 0, 0);
		root.Add(new ScrollView { Content = body }, 0, 1);
		return root;
	}

	private Grid BuildHeader()
	{
		var back = new Border
		{
			WidthRequest = 44, HeightRequest = 44, StrokeThickness = 0, BackgroundColor = Colors.Transparent,
			Content = new IconView { Kind = IconKind.Back, Color = DinoTokens.Get(DinoTokens.Ink), Size = 22, StrokeWidth = 2.6 },
		};
		SemanticProperties.SetDescription(back, AppResources.Common_Back);
		CastViews.AddTap(back, () => _busy.RunAsync(() => Navigation.PopAsync()));
		Label title = CastText.InOwnScriptAligned(new Label { Text = AppResources.Narrator_Title, FontSize = 20, FontAttributes = FontAttributes.Bold });
		SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
		Label subtitle = CastText.InOwnScriptAligned(new Label { Text = AppResources.Narrator_Subtitle, FontSize = 12, TextColor = DinoTokens.Get(DinoTokens.Muted) });
		var header = new Grid
		{
			Padding = new Thickness(6, 8, 14, 10),
			ColumnSpacing = 6,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
		};
		header.Add(back, 0);
		header.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { title, subtitle } }, 1);
		return header;
	}

	private void Rebuild()
	{
		string mine = UiLanguage.Resolve(LanguageChoice.Current(_settings));
		_cards.Children.Clear();
		foreach (string language in _voicing.InstalledLanguages(mine))
		{
			_cards.Children.Add(BuildCard(language, language == mine));
		}
	}

	private Border BuildCard(string language, bool isMine)
	{
		string voice = _voicing.NarratorOf(language);
		var stack = new VerticalStackLayout { Spacing = 10, Children = { CardTitle(language, isMine), PickerRow(language, voice) } };
		if (_open.Contains(language))
		{
			stack.Children.Add(NarratorList(language, voice));
		}

		return new Border
		{
			BackgroundColor = Colors.White,
			Padding = new Thickness(14),
			StrokeThickness = isMine ? 2 : 0,
			Stroke = DinoTokens.Get(DinoTokens.MergeBorder),
			StrokeShape = new RoundRectangle { CornerRadius = CardRadius },
			Content = stack,
		};
	}

	private static View CardTitle(string language, bool isMine)
	{
		bool rtl = DinoFonts.IsRightToLeft(language);
		var name = new Label
		{
			Text = LanguageNames.NativeName(language),
			FontSize = 16,
			FontAttributes = FontAttributes.Bold,
			FontFamily = rtl ? DinoFonts.Persian : DinoFonts.Ui,
			FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
			VerticalOptions = LayoutOptions.Center,
		};
		var row = new HorizontalStackLayout { Spacing = 8, Children = { name } };
		if (isMine)
		{
			row.Children.Add(CastViews.Badge(AppResources.Narrator_MyLanguage, DinoTokens.Get(DinoTokens.Brand), DinoTokens.Get(DinoTokens.BrandTint)));
		}

		return row;
	}

	private View PickerRow(string language, string voice)
	{
		var row = new Grid
		{
			ColumnSpacing = 10,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
		};
		row.Add(VoicePicker(language, voice), 0);
		row.Add(SampleButton(language, voice), 1);
		return row;
	}

	private Border VoicePicker(string language, string voice)
	{
		Grid content = CastText.PickerLine(CastText.VoiceName(voice, language), 14.5, ChevronSize, LineBreakMode.WordWrap);
		var picker = new Border
		{
			HeightRequest = PickerHeight,
			Padding = new Thickness(12, 0),
			StrokeThickness = 0,
			BackgroundColor = DinoTokens.Get(DinoTokens.Sand),
			StrokeShape = new RoundRectangle { CornerRadius = 12 },
			Content = content,
		};
		SemanticProperties.SetDescription(picker, LanguageNames.NativeName(language) + ": " + CastText.VoiceName(voice, language));
		CastViews.AddTap(picker, () => ToggleList(language));
		return picker;
	}

	private Border SampleButton(string language, string voice)
	{
		var button = new Border
		{
			WidthRequest = SampleSize,
			HeightRequest = SampleSize,
			StrokeThickness = 0,
			BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
			StrokeShape = new RoundRectangle { CornerRadius = SampleSize / 2 },
			Content = new IconView { Kind = IconKind.Play, Color = DinoTokens.Get(DinoTokens.Brand), Filled = true, Size = 16 },
		};
		SemanticProperties.SetDescription(button, AppResources.Narrator_Sample + ": " + CastText.VoiceName(voice, language));
		CastViews.AddTap(button, () => _busy.RunAsync(() => SampleAsync(language, voice)));
		return button;
	}

	private Task ToggleList(string language)
	{
		if (!_open.Remove(language))
		{
			_open.Add(language);
		}

		Rebuild();
		return Task.CompletedTask;
	}

	// The open list (mock frame 55): a Sand box with a row per narrator character, the chosen one checked.
	private Border NarratorList(string language, string current)
	{
		var rows = new VerticalStackLayout();
		foreach (StorytellerCharacter narrator in _voicing.NarratorsFor(language))
		{
			rows.Children.Add(NarratorRow(language, narrator, narrator.VoiceId == current));
		}

		return new Border
		{
			BackgroundColor = DinoTokens.Get(DinoTokens.Sand),
			StrokeThickness = 0,
			Padding = new Thickness(4),
			StrokeShape = new RoundRectangle { CornerRadius = 14 },
			Content = rows,
		};
	}

	private Grid NarratorRow(string language, StorytellerCharacter narrator, bool selected)
	{
		var text = new VerticalStackLayout
		{
			VerticalOptions = LayoutOptions.Center,
			Children =
			{
				CastText.InOwnScript(new Label { Text = narrator.Name, FontSize = 14.5, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(selected ? DinoTokens.BrandEdge : DinoTokens.Ink) }),
				CastText.InOwnScript(new Label { Text = narrator.Hint, FontSize = 12, TextColor = DinoTokens.Get(DinoTokens.Muted) }),
			},
		};
		var row = new Grid
		{
			MinimumHeightRequest = RowHeight,
			BackgroundColor = DinoTokens.Get(DinoTokens.Sand),
			Padding = new Thickness(10, 4, 4, 4),
			ColumnSpacing = 8,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
		};
		row.Add(text, 0);
		if (selected)
		{
			row.Add(new IconView { Kind = IconKind.Check, Color = DinoTokens.Get(DinoTokens.BrandEdge), Size = CheckSize, StrokeWidth = 2.6, VerticalOptions = LayoutOptions.Center }, 1);
		}

		row.Add(SampleButton(language, narrator.VoiceId), 2);
		SemanticProperties.SetDescription(text, CastText.Pair(narrator.Name, narrator.Hint));
		CastViews.AddTap(row, () => _busy.RunAsync(() => ChooseAsync(language, narrator)));
		return row;
	}

	private async Task ChooseAsync(string language, StorytellerCharacter narrator)
	{
		_open.Remove(language);
		if (narrator.VoiceId != _voicing.NarratorOf(language))
		{
			_voicing.SetNarrator(language, narrator.VoiceId);
			await RecastLibraryAsync();
		}

		Rebuild();
	}

	// Every saved book follows the new narrator at once: a character who had its voice is given another (starter, store and own books alike).
	private async Task RecastLibraryAsync()
	{
		try
		{
			foreach (Story story in await _library.ListAsync())
			{
				Story? changed = await NarratorRecast.TryRecastAsync(_voicing, story);
				if (changed is not null)
				{
					await _library.UpdateAsync(changed);
				}
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader narrator recast failed: {ex}");
			await DisplayAlertAsync(CastText.SaveFailedTitle, CastText.SaveFailedBody, CastText.Ok);
		}
	}

	private async Task SampleAsync(string language, string voice)
	{
		string line = CastText.PreviewLineFor(_voicing.PreviewLanguage(language, _spoken.Current));
		try
		{
			await _preview.PlayAsync(line, voice, VoiceFlavour.Storyteller);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader narrator sample failed: {ex}");
			await DisplayAlertAsync(CastText.Title, CastText.PreviewFailedBody, CastText.Ok);
		}
	}
}
