using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The story's pages as a list (the "Pages" and "Fix the text" rows of the edit screen): tap a
/// page to fix its text, move it up or down, remove it, or add a blank page. Edits go into the
/// shared <see cref="StoryDraft"/>; the edit screen's Save writes them.
/// </summary>
public sealed class PagesPage : ContentPage, IGrownUpPage
{
	private const int PreviewLength = 90;

	private readonly StoryDraft _draft;
	private readonly BusyGuard _busy = new();
	private readonly VerticalStackLayout _list = new() { Spacing = 10, Padding = new Thickness(14, 4, 14, 14) };

	public PagesPage(StoryDraft draft)
	{
		_draft = draft;
		NavigationPage.SetHasNavigationBar(this, false);
		BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		Content = new Grid
		{
			RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
			Children = { Header(), new ScrollView { Content = _list }.Row(1) },
		};
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Rebuild();
	}

	private Grid Header()
	{
		var back = new Border
		{
			WidthRequest = 44, HeightRequest = 44, StrokeThickness = 0, BackgroundColor = Colors.Transparent,
			Content = new IconView { Kind = IconKind.Back, Color = DinoTokens.Get(DinoTokens.Ink), Size = 22, StrokeWidth = 2.6 },
		};
		SemanticProperties.SetDescription(back, AppResources.Common_Back);
		back.GestureRecognizers.Add(Tap(() => Navigation.PopAsync()));
		var add = new Border
		{
			WidthRequest = 44, HeightRequest = 44, StrokeThickness = 0, BackgroundColor = Colors.Transparent,
			Content = new IconView { Kind = IconKind.Plus, Color = DinoTokens.Get(DinoTokens.Brand), Size = 24, StrokeWidth = 2.6 },
		};
		SemanticProperties.SetDescription(add, AppResources.Pages_Add);
		add.GestureRecognizers.Add(Tap(() => OpenTextAsync(-1)));
		var title = new Label { Text = AppResources.Edit_Pages, FontSize = 20, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
		SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
		return new Grid
		{
			Padding = new Thickness(8, 10, 8, 10),
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
			Children = { back, title.Column(1), add.Column(2) },
		};
	}

	private void Rebuild()
	{
		_list.Children.Clear();
		for (int i = 0; i < _draft.Pages.Count; i++)
		{
			_list.Children.Add(PageRow(i));
		}
	}

	private Border PageRow(int index)
	{
		var text = new VerticalStackLayout
		{
			Spacing = 2,
			Children =
			{
				new Label { Text = $"{index + 1}", FontSize = 12, TextColor = DinoTokens.Get(DinoTokens.Muted) },
				new Label { Text = Preview(_draft.Pages[index].Text), FontFamily = DinoFonts.Reading, FontSize = 14, MaxLines = 2 },
			},
		};
		text.GestureRecognizers.Add(Tap(() => OpenTextAsync(index)));
		var grid = new Grid
		{
			ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
			ColumnSpacing = 8,
			Children = { text, Buttons(index).Column(1) },
		};
		return new Border
		{
			BackgroundColor = Colors.White, StrokeThickness = 0, Padding = 12,
			StrokeShape = new RoundRectangle { CornerRadius = 16 },
			Content = grid,
		};
	}

	private HorizontalStackLayout Buttons(int index) => new()
	{
		Spacing = 4,
		VerticalOptions = LayoutOptions.Center,
		Children =
		{
			SmallButton("↑", AppResources.Pages_MoveUp, () => Move(index, -1)),
			SmallButton("↓", AppResources.Pages_MoveDown, () => Move(index, 1)),
			SmallButton("✕", AppResources.Pages_Remove, () => Remove(index)),
		},
	};

	private static Button SmallButton(string glyph, string description, Action action)
	{
		var button = new Button
		{
			Text = glyph, WidthRequest = 40, HeightRequest = 40, Padding = 0, CornerRadius = 12, FontSize = 16,
			BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint), TextColor = DinoTokens.Get(DinoTokens.Brand),
		};
		SemanticProperties.SetDescription(button, description);
		button.Clicked += (_, _) => action();
		return button;
	}

	private void Move(int index, int direction)
	{
		_draft.Move(index, direction);
		Rebuild();
	}

	private void Remove(int index)
	{
		if (_draft.Pages.Count > 1)
		{
			_draft.Pages.RemoveAt(index);
			Rebuild();
		}
	}

	private async Task OpenTextAsync(int index) => await Navigation.PushAsync(new PageTextPage(_draft, index));

	private static string Preview(string text) =>
		text.Length <= PreviewLength ? text : text[..PreviewLength].TrimEnd() + "…";

	private TapGestureRecognizer Tap(Func<Task> action)
	{
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) => await _busy.RunAsync(action);
		return tap;
	}
}

/// <summary>Layout shorthands for the code-built pages.</summary>
internal static class GridPlacement
{
	public static T Row<T>(this T view, int row) where T : BindableObject
	{
		Grid.SetRow(view, row);
		return view;
	}

	public static T Column<T>(this T view, int column) where T : BindableObject
	{
		Grid.SetColumn(view, column);
		return view;
	}
}
