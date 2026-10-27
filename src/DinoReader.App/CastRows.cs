using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>What a tap on a cast row does; the page supplies it (every one runs behind the page's <see cref="BusyGuard"/>).</summary>
internal sealed record CastRowActions(Func<CastRow, Task> Preview, Func<CastRow, Task> PickCharacter);

/// <summary>Builds the rows of the cast screen: the narrator, a character, the unknown speaker and the shared-voice note.</summary>
internal static class CastRows
{
	private const double AvatarSize = 34;
	private const double AvatarFont = 15;
	private const double TextIndent = 44;
	private const double LinkMinHeight = 44;
	private const double PickerHeight = 40;
	private const double PickerRing = 2;
	private const double PlaySize = 40;

	/// <summary>The narrator row (mock frames 40 and 52): read-only, the narrator character named, with a link to the Narrator page where it is chosen for every book (Slice 109).</summary>
	public static View Narrator(string voiceId, string language, Func<Task> changeForAll)
	{
		var avatar = CastViews.Avatar(string.Empty, DinoTokens.Get(DinoTokens.Ink), Colors.White, AvatarSize, AvatarFont);
		avatar.Content = new IconView { Kind = IconKind.Book, Color = Colors.White, Size = 18, StrokeWidth = 2.2, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		var link = new Label
		{
			Text = CastText.ChangeForAll, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Brand),
			MinimumHeightRequest = LinkMinHeight, VerticalTextAlignment = TextAlignment.Center,
		};
		SemanticProperties.SetDescription(link, CastText.ChangeForAll);
		CastViews.AddTap(link, changeForAll);
		var text = new VerticalStackLayout
		{
			VerticalOptions = LayoutOptions.Center,
			Children = { CastText.InOwnScript(new Label { Text = CastText.NarratorLine(voiceId, language), FontSize = 15, FontAttributes = FontAttributes.Bold }), link },
		};
		var row = new Grid
		{
			MinimumHeightRequest = 60,
			Padding = new Thickness(14, 8),
			ColumnSpacing = 10,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
		};
		row.Add(avatar, 0);
		row.Add(text, 1);
		return row;
	}

	/// <summary>A story character (mock frame 52, Slice 116): its initial, name and word count, and under them the storyteller character playing it (a picker) with a play button; nothing else (Slice 136: the character names the animal).</summary>
	public static View Character(CastRow row, int index, CastRowActions actions, string language)
	{
		var body = new VerticalStackLayout { Padding = new Thickness(14, 12), Spacing = 9 };
		body.Add(TopLine(row, index));
		body.Add(PickerLine(row, actions, language));
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

	private static Grid TopLine(CastRow row, int index)
	{
		var line = new Grid
		{
			ColumnSpacing = 10,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
		};
		line.Add(AvatarFor(row, index), 0);
		line.Add(NameAndDetail(NameLine(row), CastText.Words(row.Words)), 1);
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

	// The picker names the storyteller character; an edited row has a white fill and a ring (mock frame 52). A tap opens the picker sheet.
	private static Grid PickerLine(CastRow row, CastRowActions actions, string language)
	{
		string character = row.Character?.Name ?? CastText.CharacterName(row.Member.VoiceId, row.Flavour, language);
		Grid label = CastText.PickerLine(character, 14, 12);
		bool edited = row.Member.Edited;
		var picker = new Border
		{
			HeightRequest = PickerHeight,
			Padding = new Thickness(12, 0),
			BackgroundColor = edited ? Colors.White : DinoTokens.Get(DinoTokens.Sand),
			Stroke = DinoTokens.Get(DinoTokens.Brand),
			StrokeThickness = edited ? PickerRing : 0,
			StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
			Content = label,
		};
		SemanticProperties.SetDescription(picker, CastText.Pair(row.Member.Name, character));
		CastViews.AddTap(picker, () => actions.PickCharacter(row));
		Border play = Play(row, actions);
		var line = new Grid
		{
			Padding = new Thickness(TextIndent, 0, 0, 0),
			ColumnSpacing = 8,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
		};
		line.Add(picker, 0);
		line.Add(play, 1);
		return line;
	}

	private static Border Play(CastRow row, CastRowActions actions)
	{
		Border play = CastViews.PreviewButton(row.Member.Name);
		play.WidthRequest = PlaySize;
		play.HeightRequest = PlaySize;
		play.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = PlaySize / 2 };
		SemanticProperties.SetDescription(play, CastText.HearName(row.Member.Name));
		CastViews.AddTap(play, () => actions.Preview(row));
		return play;
	}
}
