using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The in-row card on Story languages (Slice 262, mock Dino Reader Batch 47 Onboarding, frame 169) that shows <see cref="ShelfAudioPacks"/>' state under a side language's name: "Getting Farsi
/// audio for your books", "2 of 4", a bar, the book in progress and Cancel, then once the quiet line for books the store has no audio for. It only draws the state it is given.
/// </summary>
internal static class ShelfAudioPacksCard
{
	private const double Corner = 14;
	private const double CardPadding = 12;
	private const double CardSpacing = 8;
	private const double TitleSize = 13.5;
	private const double NoteSize = 12.5;
	private const double BarHeight = 8;
	private const double CancelHeight = 36;
	private const string CardIdPrefix = "ShelfAudioCard-";
	private const string CancelIdPrefix = "ShelfAudioCancel-";
	private const string NoneIdPrefix = "ShelfAudioNone-";
	private const string FailedIdPrefix = "ShelfAudioFailed-";

	/// <summary>The card for <paramref name="state"/>, or null when there is nothing to show (idle with no quiet line, or cancelled).</summary>
	public static View? For(string language, ShelfAudioState state, Func<Task> cancel)
	{
		if (state.Phase == ShelfAudioPhase.Cancelled || (!state.IsActive && state.NoAudio.Count == 0 && state.Failed == 0))
		{
			return null;
		}

		var stack = new VerticalStackLayout { Spacing = CardSpacing };
		if (state.IsActive)
		{
			stack.Children.Add(Heading(language, state));
			stack.Children.Add(Bar(state.Fraction));
			stack.Children.Add(Footer(language, state, cancel));
		}

		if (state.NoAudio.Count > 0)
		{
			stack.Children.Add(NoAudioLine(language, state.NoAudio));
		}

		if (state.Failed > 0)
		{
			stack.Children.Add(FailedLine(language));
		}

		return new Border
		{
			AutomationId = CardIdPrefix + language,
			BackgroundColor = DinoTokens.Get(DinoTokens.Sand),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = Corner },
			Padding = CardPadding,
			Content = stack,
		};
	}

	private static Grid Heading(string language, ShelfAudioState state)
	{
		var grid = new Grid { ColumnSpacing = 8, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		grid.Add(new Label
		{
			Text = AppResources.Format(AppResources.ShelfAudio_Getting, LanguageNames.NativeName(language)),
			FontSize = TitleSize,
			FontAttributes = FontAttributes.Bold,
			TextColor = DinoTokens.Get(DinoTokens.Ink),
		}, 0);
		grid.Add(new Label
		{
			Text = state.Total > 0 ? AppResources.Format(AppResources.Intro_Counter, state.Position, state.Total) : string.Empty, // nothing to count while preparing
			FontSize = TitleSize,
			FontAttributes = FontAttributes.Bold,
			TextColor = DinoTokens.Get(DinoTokens.Brand),
			VerticalOptions = LayoutOptions.Center,
		}, 1);
		return grid;
	}

	// A MAUI ProgressBar cannot colour its track, so the bar is two rounded boxes whose widths are the done and the left share.
	private static Border Bar(double fraction)
	{
		double done = Math.Clamp(fraction, 0, 1);
		var cells = new Grid
		{
			ColumnSpacing = 0,
			ColumnDefinitions = [new ColumnDefinition(new GridLength(done, GridUnitType.Star)), new ColumnDefinition(new GridLength(1 - done, GridUnitType.Star))],
		};
		cells.Add(new BoxView { Color = DinoTokens.Get(DinoTokens.Brand), CornerRadius = BarHeight / 2 }, 0);
		return new Border
		{
			HeightRequest = BarHeight,
			BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = BarHeight / 2 },
			Content = cells,
		};
	}

	private static Grid Footer(string language, ShelfAudioState state, Func<Task> cancel)
	{
		var grid = new Grid { ColumnSpacing = 8, ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		grid.Add(new Label
		{
			Text = LineOf(state),
			FontSize = NoteSize,
			TextColor = DinoTokens.Get(state.Phase == ShelfAudioPhase.Running ? DinoTokens.Body : DinoTokens.Muted),
			VerticalOptions = LayoutOptions.Center,
		}, 0);
		grid.Add(CancelPill(language, cancel), 1);
		return grid;
	}

	// The book in progress while running; what it waits for otherwise.
	private static string LineOf(ShelfAudioState state) => state.Phase switch
	{
		ShelfAudioPhase.Preparing => AppResources.ShelfAudio_Preparing,
		ShelfAudioPhase.WaitingForInternet => AppResources.ShelfAudio_WaitingInternet,
		_ => state.CurrentTitle ?? string.Empty,
	};

	private static Border CancelPill(string language, Func<Task> cancel)
	{
		var pill = new Border
		{
			AutomationId = CancelIdPrefix + language,
			HeightRequest = CancelHeight,
			Padding = new Thickness(14, 0),
			StrokeShape = new RoundRectangle { CornerRadius = CancelHeight / 2 },
			BackgroundColor = Colors.White,
			Stroke = DinoTokens.Get(DinoTokens.Hairline),
			StrokeThickness = 2,
			VerticalOptions = LayoutOptions.Center,
			Content = new Label { Text = AppResources.Common_Cancel, FontSize = TitleSize, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Body), VerticalOptions = LayoutOptions.Center },
		};
		SemanticProperties.SetDescription(pill, AppResources.Common_Cancel);
		Taps.AddTap(pill, cancel, UiControls.Cancel);
		return pill;
	}

	// One quiet line when some packs gave up after their tries; the next open tries them again.
	private static Label FailedLine(string language) => new()
	{
		AutomationId = FailedIdPrefix + language,
		Text = AppResources.Format(AppResources.ShelfAudio_Failed, LanguageNames.NativeName(language)),
		FontSize = NoteSize,
		TextColor = DinoTokens.Get(DinoTokens.Muted),
	};

	// One line per run: the first book's name, and how many more when there are several.
	private static Label NoAudioLine(string language, IReadOnlyList<string> titles) => new()
	{
		AutomationId = NoneIdPrefix + language,
		Text = titles.Count == 1
			? AppResources.Format(AppResources.ShelfAudio_NoneOne, titles[0], LanguageNames.NativeName(language))
			: AppResources.Format(AppResources.ShelfAudio_NoneMany, titles[0], LanguageNames.NativeName(language), titles.Count - 1),
		FontSize = NoteSize,
		TextColor = DinoTokens.Get(DinoTokens.Body),
	};
}
