using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// "Close Dino Book Reader?" (Slice 174, Decision 169, mock frame 94): what Android's back button asks on the kids' shelf. Stay is the big
/// default; only Close exits. A tap outside the card, or a second back press, counts as Stay. No gate; the text is in the app language.
/// <see cref="Result"/> completes true for Close.
/// </summary>
public sealed class CloseQuestionPage : ContentPage
{
	private const double CardMargin = 24;
	private const double CardCorner = 28;
	private const double IconSize = 64;
	private const double IconCorner = 17;
	private const double StayHeight = 56;
	private const double StayEdge = 5;
	private const double CloseHeight = 48;
	private const double QuestionSize = 21;

	private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private bool _closing;

	public CloseQuestionPage()
	{
		BackgroundColor = Colors.Transparent;
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = SafeAreaEdges.None;
		FlowDirection = UiFlow.Screen;
		var scrim = new Grid { BackgroundColor = DinoTokens.Get(DinoTokens.Scrim), Padding = CardMargin };
		CastViews.AddTap(scrim, () => CloseAsync(false));
		scrim.Add(Card());
		Content = scrim;
	}

	public Task<bool> Result => _result.Task;

	protected override bool OnBackButtonPressed()
	{
		_ = CloseAsync(false);
		return true;
	}

	private Border Card()
	{
		var column = new VerticalStackLayout { Spacing = 14, HorizontalOptions = LayoutOptions.Fill };
		column.Children.Add(Icon());
		column.Children.Add(Question());
		column.Children.Add(StayButton());
		column.Children.Add(CloseButton());
		var card = new Border
		{
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			Padding = new Thickness(20, 26, 20, 20),
			VerticalOptions = LayoutOptions.Center,
			StrokeShape = new RoundRectangle { CornerRadius = CardCorner },
			Content = column,
		};
		CastViews.AddTap(card, () => Task.CompletedTask); // a tap on the card itself is not "outside"
		return card;
	}

	private static Image Icon() => new()
	{
		Source = "dino_icon",
		WidthRequest = IconSize,
		HeightRequest = IconSize,
		Aspect = Aspect.AspectFill,
		HorizontalOptions = LayoutOptions.Center,
		Clip = new RoundRectangleGeometry(IconCorner, new Rect(0, 0, IconSize, IconSize)),
	};

	private static Label Question()
	{
		var question = new Label
		{
			Text = AppResources.Close_Question,
			FontSize = QuestionSize,
			FontAttributes = FontAttributes.Bold,
			HorizontalTextAlignment = TextAlignment.Center,
			TextColor = DinoTokens.Get(DinoTokens.Ink),
		};
		SemanticProperties.SetHeadingLevel(question, SemanticHeadingLevel.Level1);
		return question;
	}

	private View StayButton()
	{
		var stay = new Border
		{
			HeightRequest = StayHeight,
			BackgroundColor = DinoTokens.Get(DinoTokens.Coral),
			StrokeThickness = 0,
			Margin = new Thickness(0, 4, 0, StayEdge),
			StrokeShape = new RoundRectangle { CornerRadius = StayHeight / 2 },
			Shadow = new Shadow { Brush = new SolidColorBrush(DinoTokens.Get(DinoTokens.CoralEdge)), Offset = new Point(0, StayEdge), Radius = 0, Opacity = 1 },
			Content = new Label
			{
				Text = AppResources.Common_Stay,
				TextColor = Colors.White,
				FontSize = 17,
				FontAttributes = FontAttributes.Bold,
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center,
			},
		};
		SemanticProperties.SetDescription(stay, AppResources.Common_Stay);
		CastViews.AddTap(stay, () => CloseAsync(false));
		return stay;
	}

	private View CloseButton()
	{
		var close = new Border
		{
			HeightRequest = CloseHeight,
			BackgroundColor = Colors.Transparent,
			StrokeThickness = 0,
			Content = new Label
			{
				Text = AppResources.Common_Close,
				TextColor = DinoTokens.Get(DinoTokens.Body),
				FontSize = 15.5,
				FontAttributes = FontAttributes.Bold,
				HorizontalOptions = LayoutOptions.Center,
				VerticalOptions = LayoutOptions.Center,
			},
		};
		SemanticProperties.SetDescription(close, AppResources.Common_Close);
		CastViews.AddTap(close, () => CloseAsync(true));
		return close;
	}

	// Pops first, then completes, so the caller (and a quick second back press) never runs under a still-open modal; the result is set even if the pop fails.
	private async Task CloseAsync(bool exit)
	{
		if (_closing)
		{
			return;
		}

		_closing = true;
		try
		{
			await Navigation.PopModalAsync(animated: false);
		}
		finally
		{
			_result.TrySetResult(exit);
		}
	}

	/// <summary>Shows the question over the current page; true when the child (or parent) chose Close.</summary>
	public static async Task<bool> AskAsync(INavigation navigation)
	{
		var page = new CloseQuestionPage();
		await navigation.PushModalAsync(page, animated: false);
		return await page.Result;
	}
}
