using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The character picker (Slice 116, mock frame 53): a bottom sheet over a dimmed cast screen listing a language's storyteller characters, each with its
/// plain words and a play button, under a Girls / Boys / Everyone filter that starts on the story character's gender. A tap on a row picks it and closes the sheet.
/// A character a book already uses shows that story character after its words, and may still be picked. Like the merge sheet it lives inside the cast page,
/// so leaving the app cannot strand it. (This file replaced the flavour menu of Slice 113: a flavour is now part of a character.)
/// </summary>
internal sealed class CastCharacterPicker : Grid
{
	private const double SheetMaxHeight = 640;
	private const double SheetRadius = 28;
	private const double FilterHeight = 38;
	private const double RowHeight = 60;
	private const double PlaySize = 44;
	private const double CheckSize = 20;
	private const double NameFont = 15;
	private const double HintFont = 12;

	private readonly VerticalStackLayout _rows = new();
	private readonly Label _title = new() { FontSize = 20, FontAttributes = FontAttributes.Bold };
	private readonly Label _subtitle = new() { FontSize = 13, TextColor = DinoTokens.Get(DinoTokens.Muted) };
	private readonly Grid _filter = new() { ColumnSpacing = 0 };
	private TaskCompletionSource<StorytellerCharacter?>? _pending;
	private IReadOnlyList<StorytellerCharacter> _choices = [];
	private StorytellerCharacter? _current;
	private Func<StorytellerCharacter, string?> _usedBy = _ => null;
	private Func<StorytellerCharacter, Task> _play = _ => Task.CompletedTask;
	private Gender _show = Gender.Unknown;

	public CastCharacterPicker()
	{
		IsVisible = false;
		BackgroundColor = DinoTokens.Get(DinoTokens.SheetScrim);
		CastViews.AddTap(this, () => { Close(null); return Task.CompletedTask; });
		Children.Add(BuildSheet());
	}

	/// <summary>Shows the sheet and returns the character picked, or null when it was dismissed. <paramref name="start"/> is the story character's gender
	/// (the filter's first position; unknown starts on Everyone), <paramref name="usedBy"/> names the story character already playing a character in this book.</summary>
	public Task<StorytellerCharacter?> PickAsync(
		string storyCharacter, string language, IReadOnlyList<StorytellerCharacter> choices, StorytellerCharacter? current, Gender start,
		Func<StorytellerCharacter, string?> usedBy, Func<StorytellerCharacter, Task> play)
	{
		Close(null);
		_title.Text = CastText.PickTitle(storyCharacter);
		_subtitle.Text = CastText.PickSubtitle(language);
		_choices = choices;
		_current = current;
		_usedBy = usedBy;
		_play = play;
		_show = start;
		Render();
		_pending = new TaskCompletionSource<StorytellerCharacter?>();
		IsVisible = true;
		return _pending.Task;
	}

	/// <summary>Closes the sheet without a pick (the system Back button).</summary>
	public void Dismiss() => Close(null);

	private void Close(StorytellerCharacter? picked)
	{
		IsVisible = false;
		_pending?.TrySetResult(picked);
		_pending = null;
	}

	private Border BuildSheet()
	{
		SemanticProperties.SetHeadingLevel(_title, SemanticHeadingLevel.Level1);
		var grid = new Grid
		{
			RowSpacing = 10,
			RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
		};
		grid.Add(_title, 0, 0);
		grid.Add(_subtitle, 0, 1);
		grid.Add(_filter, 0, 2);
		grid.Add(new ScrollView { Content = _rows }, 0, 3);
		var sheet = new Border
		{
			VerticalOptions = LayoutOptions.End,
			MaximumHeightRequest = SheetMaxHeight,
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			Padding = new Thickness(18, 22, 18, 16),
			StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(SheetRadius, SheetRadius, 0, 0) },
			Content = grid,
		};
		CastViews.AddTap(sheet, () => Task.CompletedTask);
		return sheet;
	}

	private void Render()
	{
		RenderFilter();
		_rows.Children.Clear();
		foreach (StorytellerCharacter character in _choices.Where(Shown))
		{
			_rows.Children.Add(Row(character));
		}
	}

	// Girls show the girls' and the voices that say nothing about it; Everyone shows all (a pack that does not tell its voices' gender has only those).
	private bool Shown(StorytellerCharacter character) =>
		_show == Gender.Unknown || character.Gender == _show || character.Gender == Gender.Unknown;

	private void RenderFilter()
	{
		_filter.Children.Clear();
		Gender[] order = [Gender.Female, Gender.Male, Gender.Unknown];
		string[] labels = [CastText.FilterGirls, CastText.FilterBoys, CastText.FilterEveryone];
		var cells = new Grid { ColumnSpacing = 2 };
		for (int i = 0; i < order.Length; i++)
		{
			cells.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			cells.Add(FilterCell(order[i], labels[i]), i, 0);
		}

		_filter.Add(new Border
		{
			BackgroundColor = DinoTokens.Get(DinoTokens.Sand),
			StrokeThickness = 0,
			Padding = 2,
			StrokeShape = new RoundRectangle { CornerRadius = 12 },
			Content = cells,
		});
	}

	private Border FilterCell(Gender gender, string label)
	{
		bool selected = gender == _show;
		var cell = new Border
		{
			HeightRequest = FilterHeight,
			StrokeThickness = 0,
			BackgroundColor = selected ? Colors.White : Colors.Transparent,
			StrokeShape = new RoundRectangle { CornerRadius = 10 },
			Content = new Label
			{
				Text = label, FontSize = 14, FontAttributes = FontAttributes.Bold,
				HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
				TextColor = DinoTokens.Get(selected ? DinoTokens.Ink : DinoTokens.Muted),
			},
		};
		if (selected)
		{
			cell.Shadow = new Shadow { Brush = new SolidColorBrush(DinoTokens.Get(DinoTokens.Ink)), Opacity = 0.15f, Offset = new Point(0, 1), Radius = 2 };
		}

		SemanticProperties.SetDescription(cell, label);
		CastViews.AddTap(cell, () => { _show = gender; Render(); return Task.CompletedTask; });
		return cell;
	}

	private Border Row(StorytellerCharacter character)
	{
		bool selected = character.Id == _current?.Id;
		var text = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
		text.Add(CastText.InOwnScript(new Label
		{
			Text = character.Name, FontSize = NameFont, FontAttributes = FontAttributes.Bold,
			TextColor = DinoTokens.Get(selected ? DinoTokens.BrandEdge : DinoTokens.Ink),
		}));
		text.Add(CastText.InOwnScript(new Label
		{
			Text = CastText.HintUsedBy(character.Hint, _usedBy(character)), FontSize = HintFont, TextColor = DinoTokens.Get(DinoTokens.Muted),
		}));
		var line = new Grid
		{
			ColumnSpacing = 10,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto)],
		};
		line.Add(text, 0);
		if (selected)
		{
			line.Add(new IconView { Kind = IconKind.Check, Color = DinoTokens.Get(DinoTokens.BrandEdge), Size = CheckSize, StrokeWidth = 3, VerticalOptions = LayoutOptions.Center }, 1);
		}

		line.Add(PlayButton(character), 2);
		var row = new Border
		{
			MinimumHeightRequest = RowHeight,
			Padding = new Thickness(12, 4, 8, 4),
			StrokeThickness = 0,
			BackgroundColor = selected ? DinoTokens.Get(DinoTokens.BrandTint) : Colors.Transparent,
			StrokeShape = new RoundRectangle { CornerRadius = 14 },
			Content = line,
		};
		SemanticProperties.SetDescription(row, CastText.Pair(character.Name, character.Hint));
		CastViews.AddTap(row, () => { Close(character); return Task.CompletedTask; });
		return row;
	}

	private Border PlayButton(StorytellerCharacter character)
	{
		var play = new Border
		{
			WidthRequest = PlaySize,
			HeightRequest = PlaySize,
			StrokeThickness = 0,
			BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
			StrokeShape = new RoundRectangle { CornerRadius = PlaySize / 2 },
			Content = new IconView { Kind = IconKind.Play, Color = DinoTokens.Get(DinoTokens.Brand), Filled = true, Size = 16 },
		};
		SemanticProperties.SetDescription(play, CastText.HearName(character.Name));
		CastViews.AddTap(play, () => _play(character));
		return play;
	}
}
