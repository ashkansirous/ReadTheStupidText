using DinoReader.Core;

namespace DinoReader;

/// <summary>What a tap on a cast row does; the page supplies it (every one runs behind the page's <see cref="BusyGuard"/>).</summary>
internal sealed record CastRowActions(
	Func<CastRow, Task> Preview,
	Func<CastRow, Gender, Task> SetGender,
	Func<CastRow, Task> PickVoice,
	Func<CastRow, Task> PickFlavour,
	Func<CastRow, Task> PreviewFlavour);

/// <summary>
/// How a character row folds (mock frame 49, Slice 114): collapsed it is the name, "Boy · Puck · Small" and the play button; a tap on it opens
/// the full row, and a tap on the name of an open row closes it again. Null where rows never fold (no storyteller pack installed).
/// </summary>
internal sealed record CastRowFold(bool Collapsed, Func<Task> Toggle);

/// <summary>Builds the rows of the cast screen: the narrator, a character, the unknown speaker and the shared-voice note.</summary>
internal static class CastRows
{
	private const double AvatarSize = 34;
	private const double AvatarFont = 15;
	private const double TextIndent = 44;
	private const double LinkMinHeight = 44;

	/// <summary>The narrator row (mock 40): read-only, the voice named, with a link to the Narrator page where it is chosen for every book (Slice 109).</summary>
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
			Children = { new Label { Text = CastText.NameAndVoice(CastText.Narrator, voiceId, language), FontSize = 15, FontAttributes = FontAttributes.Bold }, link },
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

	public static View Character(CastRow row, int index, CastRowActions actions, string language, CastRowFold? fold = null)
	{
		if (fold is { Collapsed: true })
		{
			Grid collapsed = TopLine(row, index, actions, CastText.RowSummary(row.Member.Gender, row.Member.VoiceId, row.Flavour, language), fold.Toggle);
			collapsed.Padding = new Thickness(14, 12);
			return collapsed;
		}

		var body = new VerticalStackLayout { Padding = new Thickness(14, 12), Spacing = 9 };
		body.Add(TopLine(row, index, actions, CastText.Words(row.Words), fold?.Toggle));
		body.Add(SecondLine(row, actions, language));
		body.Add(SoundsLine(row, actions));
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

	// tapName: a tap on the avatar and name folds the row (null where rows do not fold); detail is the line under the name.
	private static Grid TopLine(CastRow row, int index, CastRowActions actions, string detail, Func<Task>? tapName)
	{
		Border preview = CastViews.PreviewButton(row.Member.Name);
		SemanticProperties.SetDescription(preview, CastText.HearName(row.Member.Name));
		CastViews.AddTap(preview, () => actions.Preview(row));
		var line = new Grid
		{
			ColumnSpacing = 10,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
		};
		VerticalStackLayout name = NameAndDetail(NameLine(row), detail);
		line.Add(AvatarFor(row, index), 0);
		line.Add(name, 1);
		line.Add(preview, 2);
		if (tapName is not null)
		{
			CastViews.AddTap(name, tapName);
		}

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

	// The "Sounds" row (Slice 113, mock frame 48): the flavour picker and a button that plays a sample of it.
	private static Grid SoundsLine(CastRow row, CastRowActions actions)
	{
		Border chip = CastViews.VoiceChip(CastText.FlavourName(row.Flavour));
		SemanticProperties.SetDescription(chip, CastText.Pair(CastText.Sounds, CastText.FlavourName(row.Flavour)));
		CastViews.AddTap(chip, () => actions.PickFlavour(row));
		Border sample = CastViews.PreviewButton(CastText.Pair(row.Member.Name, CastText.Sounds));
		SemanticProperties.SetDescription(sample, CastText.HearName(row.Member.Name));
		CastViews.AddTap(sample, () => actions.PreviewFlavour(row));
		var line = new Grid
		{
			Padding = new Thickness(TextIndent, 0, 0, 0),
			ColumnSpacing = 8,
			ColumnDefinitions =
			[
				new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star),
			],
		};
		line.Add(new Label { Text = CastText.Sounds, FontSize = 13, TextColor = DinoTokens.Get(DinoTokens.Body), VerticalOptions = LayoutOptions.Center }, 0);
		line.Add(chip, 1);
		sample.HorizontalOptions = LayoutOptions.End;
		line.Add(sample, 2);
		return line;
	}

	private static Grid SecondLine(CastRow row, CastRowActions actions, string language)
	{
		Border chip = CastViews.VoiceChip(CastText.VoiceName(row.Member.VoiceId, language));
		SemanticProperties.SetDescription(chip, row.Member.Name + ": " + CastText.VoiceName(row.Member.VoiceId, language));
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
