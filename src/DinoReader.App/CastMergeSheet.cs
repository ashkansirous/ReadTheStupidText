using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The merge sheet (mock screen 11): a white bottom sheet over a dimmed cast screen. A grown-up ticks
/// two characters, chooses which name to keep and confirms. It lives inside the cast page rather
/// than on the navigation stack, so leaving the app cannot strand it.
/// </summary>
internal sealed class CastMergeSheet : Grid
{
	private readonly VerticalStackLayout _list = new();
	private readonly Border _keepRow = new();
	private readonly Label _keepValue = new() { FontSize = 14.5, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
	private readonly Button _confirm = new();
	private MergeSelection _selection = new();
	private IReadOnlyList<CastRow> _rows = [];
	private string _language = string.Empty;

	public CastMergeSheet()
	{
		IsVisible = false;
		BackgroundColor = DinoTokens.Get(DinoTokens.SheetScrim);
		CastViews.AddTap(this, () => { Hide(); return Task.CompletedTask; });
		VerticalStackLayout content = Content();
		var sheet = new Border
		{
			VerticalOptions = LayoutOptions.End,
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			Padding = new Thickness(18, 22, 18, 26),
			StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(28, 28, 0, 0) },
			Content = content,
		};
		CastViews.AddTap(sheet, () => Task.CompletedTask);
		Children.Add(sheet);
	}

	/// <summary>Raised with the character that keeps its name and the one that joins it.</summary>
	public event Func<string, string, Task>? Confirmed;

	/// <summary>Asks which of the given labels the grown-up picks (the page shows an action sheet); the index, or -1 when cancelled.</summary>
	public Func<IReadOnlyList<string>, Task<int>>? ChooseKeep { get; set; }

	public void Show(IReadOnlyList<CastRow> rows, string language)
	{
		_rows = rows;
		_language = language;
		_selection = new MergeSelection();
		Render();
		IsVisible = true;
	}

	public void Hide() => IsVisible = false;

	private VerticalStackLayout Content()
	{
		_confirm.HeightRequest = 50;
		_confirm.CornerRadius = 25;
		_confirm.FontSize = 15.5;
		_confirm.BackgroundColor = DinoTokens.Get(DinoTokens.Brand);
		_confirm.TextColor = Colors.White;
		_confirm.Clicked += async (_, _) => await ConfirmAsync();
		var title = new Label { Text = CastText.MergeTitle, FontSize = 20, FontAttributes = FontAttributes.Bold };
		SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
		var body = new Label { Text = CastText.MergeBody, FontSize = 13.5, LineHeight = 1.45, TextColor = DinoTokens.Get(DinoTokens.Body) };
		return new VerticalStackLayout { Spacing = 12, Children = { title, body, ListBorder(), BuildKeepRow(), _confirm } };
	}

	private Border ListBorder() => new()
	{
		Stroke = DinoTokens.Get(DinoTokens.Hairline),
		StrokeThickness = 1,
		StrokeShape = new RoundRectangle { CornerRadius = 16 },
		Content = _list,
	};

	private Border BuildKeepRow()
	{
		var row = new Grid
		{
			ColumnSpacing = 10,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
		};
		row.Add(new Label { Text = CastText.KeepName, FontSize = 14, TextColor = DinoTokens.Get(DinoTokens.Body), VerticalOptions = LayoutOptions.Center }, 0);
		_keepValue.HorizontalOptions = LayoutOptions.End;
		row.Add(_keepValue, 1);
		row.Add(new IconView { Kind = IconKind.ChevronDown, Color = DinoTokens.Get(DinoTokens.Body), Size = 12, StrokeWidth = 1.6, VerticalOptions = LayoutOptions.Center }, 2);
		_keepRow.BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		_keepRow.StrokeThickness = 0;
		_keepRow.Padding = new Thickness(14, 10);
		_keepRow.StrokeShape = new RoundRectangle { CornerRadius = 14 };
		_keepRow.Content = row;
		CastViews.AddTap(_keepRow, ChooseKeepAsync);
		return _keepRow;
	}

	private void Render()
	{
		_list.Children.Clear();
		for (int i = 0; i < _rows.Count; i++)
		{
			if (i > 0)
			{
				_list.Children.Add(CastViews.Divider());
			}

			_list.Children.Add(PickRow(_rows[i], i));
		}

		RenderFooter();
	}

	private void RenderFooter()
	{
		CastRow? keep = _rows.FirstOrDefault(r => r.Member.CharacterId == _selection.KeepId);
		_keepRow.IsVisible = keep is not null;
		_keepValue.Text = keep is null ? string.Empty : CastText.NameAndVoice(keep.Member.Name, keep.Member.VoiceId, _language);
		_confirm.IsEnabled = keep is not null;
		_confirm.Opacity = keep is null ? 0.45 : 1;
		_confirm.Text = keep is null ? CastText.MergeButton : CastText.MergeInto(keep.Member.Name);
	}

	private Grid PickRow(CastRow row, int index)
	{
		bool picked = _selection.Contains(row.Member.CharacterId);
		var line = new Grid
		{
			Padding = new Thickness(14, 10),
			ColumnSpacing = 10,
			BackgroundColor = picked ? DinoTokens.Get(DinoTokens.SelectedRow) : Colors.Transparent,
			ColumnDefinitions =
			[
				new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto),
				new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto),
			],
		};
		line.Add(CheckBox(picked), 0);
		line.Add(CastViews.Avatar(AvatarPalette.InitialOf(row.Member.Name), Color.FromArgb(AvatarPalette.ColourAt(index)), Colors.White, 30, 13), 1);
		line.Add(new Label { Text = row.Member.Name, FontSize = 15, FontAttributes = picked ? FontAttributes.Bold : FontAttributes.None, VerticalOptions = LayoutOptions.Center }, 2);
		line.Add(new Label { Text = CastText.VoiceName(row.Member.VoiceId, _language), FontSize = 12.5, TextColor = DinoTokens.Get(DinoTokens.Muted), VerticalOptions = LayoutOptions.Center }, 3);
		SemanticProperties.SetDescription(line, row.Member.Name);
		CastViews.AddTap(line, () => ToggleAsync(row.Member.CharacterId));
		return line;
	}

	private static Border CheckBox(bool picked) => new()
	{
		WidthRequest = 24,
		HeightRequest = 24,
		StrokeShape = new RoundRectangle { CornerRadius = 7 },
		StrokeThickness = picked ? 0 : 2,
		Stroke = DinoTokens.Get(DinoTokens.CheckEmpty),
		BackgroundColor = picked ? DinoTokens.Get(DinoTokens.Brand) : Colors.Transparent,
		Content = picked
			? new IconView { Kind = IconKind.Check, Color = Colors.White, Size = 14, StrokeWidth = 3, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
			: null,
	};

	private Task ToggleAsync(string characterId)
	{
		_selection.Toggle(characterId);
		Render();
		return Task.CompletedTask;
	}

	private async Task ChooseKeepAsync()
	{
		if (!_selection.IsComplete || ChooseKeep is null)
		{
			return;
		}

		var picks = _selection.Picked.Select(id => _rows.First(r => r.Member.CharacterId == id)).ToList();
		var labels = picks.Select(r => CastText.NameAndVoice(r.Member.Name, r.Member.VoiceId, _language)).ToList();
		int at = await ChooseKeep(labels);
		if (at >= 0 && at < picks.Count)
		{
			_selection.Keep(picks[at].Member.CharacterId);
			RenderFooter();
		}
	}

	private async Task ConfirmAsync()
	{
		if (_selection is { KeepId: { } keep, DropId: { } drop } && Confirmed is { } handler)
		{
			Hide();
			await handler(keep, drop);
		}
	}
}
