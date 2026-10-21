using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>Edits one page's text (or writes a new page when the index is -1) inside the shared <see cref="StoryDraft"/>.</summary>
public sealed class PageTextPage : ContentPage, IGrownUpPage
{
	private const int NewPage = -1;

	private readonly StoryDraft _draft;
	private readonly BusyGuard _busy = new();
	private readonly int _index;
	private readonly Editor _editor;

	public PageTextPage(StoryDraft draft, int index)
	{
		_draft = draft;
		_index = index;
		NavigationPage.SetHasNavigationBar(this, false);
		BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		_editor = new Editor
		{
			Text = index == NewPage ? string.Empty : draft.Pages[index].Text,
			FontFamily = DinoFonts.Reading,
			FontSize = 18,
			AutoSize = EditorAutoSizeOption.Disabled,
			BackgroundColor = Colors.White,
		};
		Content = new Grid
		{
			RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
			Children = { Header(), new Border { Content = _editor, Padding = 12, Margin = new Thickness(14, 0, 14, 14), BackgroundColor = Colors.White }.Row(1) },
		};
	}

	private Grid Header()
	{
		var back = new Border
		{
			WidthRequest = 44, HeightRequest = 44, StrokeThickness = 0, BackgroundColor = Colors.Transparent,
			Content = new IconView { Kind = IconKind.Back, Color = DinoTokens.Get(DinoTokens.Ink), Size = 22, StrokeWidth = 2.6 },
		};
		SemanticProperties.SetDescription(back, AppResources.Common_Back);
		var backTap = new TapGestureRecognizer();
		backTap.Tapped += async (_, _) => await _busy.RunAsync(() => Navigation.PopAsync());
		back.GestureRecognizers.Add(backTap);
		var save = new Button
		{
			Text = AppResources.Common_Save, HeightRequest = 38, CornerRadius = 19, Padding = new Thickness(14, 0), FontSize = 13.5,
			BackgroundColor = DinoTokens.Get(DinoTokens.Brand), TextColor = Colors.White,
		};
		save.Clicked += async (_, _) => await _busy.RunAsync(SaveAsync);
		return new Grid
		{
			Padding = UiFlow.Mirror(new Thickness(8, 10, 16, 10)),
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
			Children = { back, save.Column(2) },
		};
	}

	private async Task SaveAsync()
	{
		string text = _editor.Text ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(text))
		{
			Store(text);
		}

		await Navigation.PopAsync();
	}

	private void Store(string text)
	{
		if (_index == NewPage)
		{
			_draft.Pages.Add(new StoryPage(text));
			return;
		}

		_draft.Pages[_index] = _draft.Pages[_index] with { Text = text };
	}
}
