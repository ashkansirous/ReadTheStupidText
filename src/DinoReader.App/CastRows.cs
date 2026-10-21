using DinoReader.Core;

namespace DinoReader;

/// <summary>What a tap on a cast row does; the page supplies it (every one runs behind the page's <see cref="BusyGuard"/>).</summary>
internal sealed record CastRowActions(
	Func<CastRow, Task> Preview,
	Func<CastRow, Gender, Task> SetGender,
	Func<CastRow, Task> PickVoice);

/// <summary>Builds the rows of the cast screen: the narrator, a character, the unknown speaker and the shared-voice note.</summary>
internal static class CastRows
{
	private const double AvatarSize = 34;
	private const double AvatarFont = 15;
	private const double TextIndent = 44;

	public static View Narrator(string voiceId, Func<Task> pick)
	{
		var avatar = CastViews.Avatar(string.Empty, DinoTokens.Get(DinoTokens.Ink), Colors.White, AvatarSize, AvatarFont);
		avatar.Content = new IconView { Kind = IconKind.Book, Color = Colors.White, Size = 18, StrokeWidth = 2.2, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		Border chip = CastViews.VoiceChip(CastText.VoiceName(voiceId));
		SemanticProperties.SetDescription(chip, CastText.Narrator + ": " + CastText.VoiceName(voiceId));
		CastViews.AddTap(chip, pick);
		var row = new Grid
		{
			MinimumHeightRequest = 56,
			Padding = new Thickness(14, 0),
			ColumnSpacing = 10,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
		};
		row.Add(avatar, 0);
		row.Add(new Label { Text = CastText.Narrator, FontSize = 15, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }, 1);
		row.Add(chip, 2);
		return row;
	}

	public static View Character(CastRow row, int index, CastRowActions actions)
	{
		var body = new VerticalStackLayout { Padding = new Thickness(14, 12), Spacing = 9 };
		body.Add(TopLine(row, index, actions));
		body.Add(SecondLine(row, actions));
		return body;
	}

	public static View Unknown(int lines)
	{
		var row = new Grid
		{
			Padding = new Thickness(14, 12),
			ColumnSpacing = 10,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
		};
		row.Add(CastViews.Avatar("?", DinoTokens.Get(DinoTokens.AvatarGrey), DinoTokens.Get(DinoTokens.Body), AvatarSize, AvatarFont), 0);
		row.Add(NameAndDetail(new Label { Text = CastText.UnknownSpeaker, FontSize = 15, FontAttributes = FontAttributes.Bold }, CastText.UnknownLines(lines)), 1);
		return row;
	}

	public static View SharedNote(int voices) => new Border
	{
		BackgroundColor = DinoTokens.Get(DinoTokens.NoteFill),
		StrokeThickness = 0,
		Padding = new Thickness(13, 11),
		StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
		Content = new Label
		{
			Text = CastText.SharedNote(voices), FontSize = 13, LineHeight = 1.6, TextColor = DinoTokens.Get(DinoTokens.NoteInk),
		},
	};

	private static Grid TopLine(CastRow row, int index, CastRowActions actions)
	{
		Border preview = CastViews.PreviewButton(row.Member.Name);
		SemanticProperties.SetDescription(preview, CastText.HearName(row.Member.Name));
		CastViews.AddTap(preview, () => actions.Preview(row));
		var line = new Grid
		{
			ColumnSpacing = 10,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
		};
		line.Add(AvatarFor(row, index), 0);
		line.Add(NameAndDetail(NameLine(row), CastText.Words(row.Words)), 1);
		line.Add(preview, 2);
		return line;
	}

	private static Border AvatarFor(CastRow row, int index) => CastViews.Avatar(
		AvatarPalette.InitialOf(row.Member.Name), Color.FromArgb(AvatarPalette.ColourAt(index)), Colors.White, AvatarSize, AvatarFont);

	private static FlexLayout NameLine(CastRow row)
	{
		var line = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center };
		line.Add(new Label { Text = row.Member.Name, FontSize = 15, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 0, 6, 0) });
		AddBadge(line, row.IsMain, CastText.Main, DinoTokens.Brand, DinoTokens.BrandTint);
		AddBadge(line, row.Member.Edited, CastText.Edited, DinoTokens.EditedInk, DinoTokens.EditedFill);
		AddBadge(line, row.SharesVoice, CastText.SharedVoice, DinoTokens.SharedInk, DinoTokens.NoteFill);
		return line;
	}

	private static void AddBadge(FlexLayout line, bool show, string text, string inkKey, string fillKey)
	{
		if (!show)
		{
			return;
		}

		Border badge = CastViews.Badge(text, DinoTokens.Get(inkKey), DinoTokens.Get(fillKey));
		badge.Margin = new Thickness(0, 0, 6, 0);
		line.Add(badge);
	}

	private static VerticalStackLayout NameAndDetail(View name, string detail) => new()
	{
		VerticalOptions = LayoutOptions.Center,
		Children = { name, new Label { Text = detail, FontSize = 12, TextColor = DinoTokens.Get(DinoTokens.Muted) } },
	};

	private static Grid SecondLine(CastRow row, CastRowActions actions)
	{
		Border chip = CastViews.VoiceChip(CastText.VoiceName(row.Member.VoiceId));
		SemanticProperties.SetDescription(chip, row.Member.Name + ": " + CastText.VoiceName(row.Member.VoiceId));
		CastViews.AddTap(chip, () => actions.PickVoice(row));
		var line = new Grid
		{
			Padding = new Thickness(TextIndent, 0, 0, 0),
			ColumnSpacing = 8,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
		};
		line.Add(CastViews.GenderControl(row.Member.Gender, gender => actions.SetGender(row, gender)), 0);
		chip.HorizontalOptions = LayoutOptions.End;
		line.Add(chip, 2);
		return line;
	}
}
