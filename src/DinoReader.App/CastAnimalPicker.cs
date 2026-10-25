using System.Globalization;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>What the grown-up chose on the animal sheet: an animal, or <c>null</c> for "Not an animal".</summary>
internal sealed record AnimalPick(AnimalKind? Kind);

/// <summary>
/// The animal sheet (Slice 125): a bottom sheet over the dimmed cast screen in the style of the character picker (<see cref="CastCharacterPicker"/>), with "Not an animal"
/// first and then the animals by their word in the screen's language. A tap on a row picks it and closes the sheet. There is no mock for it; it reuses the picker's
/// look. Like the other sheets it lives inside the cast page, so leaving the app cannot strand it.
/// </summary>
internal sealed class CastAnimalPicker : Grid
{
	private const double SheetMaxHeight = 640;
	private const double SheetRadius = 28;
	private const double RowHeight = 52;
	private const double CheckSize = 20;
	private const double NameFont = 15;

	private readonly VerticalStackLayout _rows = new();
	private readonly Label _title = new() { FontSize = 20, FontAttributes = FontAttributes.Bold };
	private readonly Label _subtitle = new() { FontSize = 13, TextColor = DinoTokens.Get(DinoTokens.Muted) };
	private TaskCompletionSource<AnimalPick?>? _pending;
	private AnimalKind? _current;

	public CastAnimalPicker()
	{
		IsVisible = false;
		BackgroundColor = DinoTokens.Get(DinoTokens.SheetScrim);
		CastViews.AddTap(this, () => { Close(null); return Task.CompletedTask; });
		Children.Add(BuildSheet());
	}

	/// <summary>Shows the sheet for <paramref name="storyCharacter"/> and returns what was picked, or null when it was dismissed. <paramref name="current"/> is the animal now (null: none).</summary>
	public Task<AnimalPick?> PickAsync(string storyCharacter, AnimalKind? current)
	{
		Close(null);
		_title.Text = AppResources.Format(AppResources.Cast_Animal_Title, storyCharacter);
		_subtitle.Text = AppResources.Cast_Animal_Hint;
		_current = current;
		Render();
		_pending = new TaskCompletionSource<AnimalPick?>();
		IsVisible = true;
		return _pending.Task;
	}

	/// <summary>Closes the sheet without a pick (the system Back button).</summary>
	public void Dismiss() => Close(null);

	private void Close(AnimalPick? picked)
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
			RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
		};
		grid.Add(_title, 0, 0);
		grid.Add(_subtitle, 0, 1);
		grid.Add(new ScrollView { Content = _rows }, 0, 2);
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
		_rows.Children.Clear();
		_rows.Children.Add(Row(AppResources.Cast_Animal_NotAnimal, null));
		string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
		foreach (var (kind, word) in Enum.GetValues<AnimalKind>().Select(k => (k, AnimalSounds.NameOf(k, language))).OrderBy(x => x.Item2, StringComparer.CurrentCulture))
		{
			_rows.Children.Add(Row(word, kind));
		}
	}

	private Border Row(string word, AnimalKind? kind)
	{
		bool selected = kind == _current;
		var line = new Grid
		{
			ColumnSpacing = 10,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
		};
		line.Add(CastText.InOwnScript(new Label
		{
			Text = word, FontSize = NameFont, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center,
			TextColor = DinoTokens.Get(selected ? DinoTokens.BrandEdge : DinoTokens.Ink),
		}), 0);
		if (selected)
		{
			line.Add(new IconView { Kind = IconKind.Check, Color = DinoTokens.Get(DinoTokens.BrandEdge), Size = CheckSize, StrokeWidth = 3, VerticalOptions = LayoutOptions.Center }, 1);
		}

		var row = new Border
		{
			MinimumHeightRequest = RowHeight,
			Padding = new Thickness(12, 4, 8, 4),
			StrokeThickness = 0,
			BackgroundColor = selected ? DinoTokens.Get(DinoTokens.BrandTint) : Colors.Transparent,
			StrokeShape = new RoundRectangle { CornerRadius = 14 },
			Content = line,
		};
		SemanticProperties.SetDescription(row, word);
		CastViews.AddTap(row, () => { Close(new AnimalPick(kind)); return Task.CompletedTask; });
		return row;
	}
}
