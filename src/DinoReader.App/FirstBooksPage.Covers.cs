using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The covers row of the first-books getting page (Slice 261, mock Batch 47 frame 165; Slice 325 part D, onboarding mock frame O3): the picked books' covers, 58 wide at 3:4 with a 9 dp radius. A book that has
/// landed shows a 24 dp green tick badge (3 dp <c>Cream</c> border, offset -6 at the top right) at full strength; the others wait at 55% opacity.
/// </summary>
internal sealed class FirstBooksWaitCovers : HorizontalStackLayout
{
	private const double CoverWidth = 58;
	private const double CoverAspect = 4.0 / 3.0;
	private const double CoverRadius = 9;
	private const double BadgeSize = 24;
	private const double BadgeBorder = 3;
	private const double BadgeOffset = -6;
	private const double BadgeIconSize = 12;
	private const double BadgeIconStroke = 2.5;
	private const double CoverGap = 10;
	private const double WaitingOpacity = 0.55;
	private const string BadgeColour = "#4FC060";

	private readonly List<(View Cover, View Badge)> _parts = [];

	public FirstBooksWaitCovers(IEnumerable<string?> coverPaths)
	{
		Spacing = CoverGap;
		HorizontalOptions = LayoutOptions.Center;
		foreach (string? path in coverPaths)
		{
			Add(path);
		}

		SetLanded(0);
	}

	/// <summary>Marks the first <paramref name="landed"/> books as downloaded: badge on, full strength.</summary>
	public void SetLanded(int landed)
	{
		for (int i = 0; i < _parts.Count; i++)
		{
			bool done = i < landed;
			_parts[i].Badge.IsVisible = done;
			_parts[i].Cover.Opacity = done ? 1 : WaitingOpacity;
		}
	}

	private void Add(string? path)
	{
		var image = new Image { Aspect = Aspect.AspectFill, Source = path is null ? null : ImageSource.FromFile(path) };
		var cover = new Border
		{
			WidthRequest = CoverWidth, HeightRequest = CoverWidth * CoverAspect, StrokeThickness = 0, Content = image,
			StrokeShape = new RoundRectangle { CornerRadius = CoverRadius }, BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
		};
		View badge = BuildBadge();
		_parts.Add((cover, badge));
		Children.Add(new Grid { Children = { cover, badge } });
	}

	private static Border BuildBadge() => new()
	{
		WidthRequest = BadgeSize, HeightRequest = BadgeSize, StrokeShape = new Ellipse(), StrokeThickness = BadgeBorder,
		Stroke = DinoTokens.Get(DinoTokens.Cream), BackgroundColor = Color.FromArgb(BadgeColour),
		HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, BadgeOffset, BadgeOffset, 0), IsVisible = false,
		Content = new IconView { Kind = IconKind.Check, Color = Colors.White, Size = BadgeIconSize, StrokeWidth = BadgeIconStroke, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
	};
}

// The getting page (Slice 325 part D, onboarding mock frame O3): its own page, not a veil over the picker. The hopping Dino, the picked covers, a bar over all the books, "Book 2 of 2" and "72%", and Cancel.
public partial class FirstBooksPage
{
	private const double GettingIconSize = 84;
	private const double ShadowWidth = 64;
	private const double ShadowHeight = 10;
	private const double ShadowOpacity = 0.14;
	private const double HopHeight = 22;
	private const uint HopMilliseconds = 1100;
	private const string HopAnimation = "dinoHop";
	private const double GettingTitleSize = 19;
	private const double TrackHeight = 12;
	private const double TrackMaxWidth = 340;
	private const double DetailSize = 13.5;
	private const double CancelHeight = 44;
	private const double CancelBorder = 2;
	private const string CancelBorderColour = "#e3dccd";
	private const string ShadowColour = "#2b2540";

	private readonly Label _gettingTitle = new() { FontSize = GettingTitleSize, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, TextColor = DinoTokens.Get(DinoTokens.Ink), AutomationId = "WaitLine" };
	private readonly Label _bookOf = new() { FontSize = DetailSize, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Body), AutomationId = "WaitDetail" };
	private readonly Label _percent = new() { FontSize = DetailSize, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Body), HorizontalOptions = LayoutOptions.End };
	private readonly Border _fill = new() { StrokeThickness = 0, HorizontalOptions = LayoutOptions.Start, BackgroundColor = DinoTokens.Get(DinoTokens.Brand), StrokeShape = new RoundRectangle { CornerRadius = TrackHeight / 2 } };
	private readonly Border _track = new() { StrokeThickness = 0, HeightRequest = TrackHeight, BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint), StrokeShape = new RoundRectangle { CornerRadius = TrackHeight / 2 } };
	private readonly Image _hopper = new() { Source = "dino_icon", WidthRequest = GettingIconSize, HeightRequest = GettingIconSize, HorizontalOptions = LayoutOptions.Center };
	private readonly Border _hopShadow = new() { WidthRequest = ShadowWidth, HeightRequest = ShadowHeight, StrokeThickness = 0, StrokeShape = new Ellipse(), HorizontalOptions = LayoutOptions.Center, BackgroundColor = Color.FromArgb(ShadowColour).WithAlpha((float)ShadowOpacity) };
	private readonly VerticalStackLayout _coversHost = new() { HorizontalOptions = LayoutOptions.Center };
	private OnboardingShell _gettingShell = null!;

	private Grid BuildGettingLayer()
	{
		SemanticProperties.SetHeadingLevel(_gettingTitle, SemanticHeadingLevel.Level1);
		_track.Content = _fill;
		var details = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		details.Add(_bookOf, 0, 0);
		details.Add(_percent, 1, 0);
		var bar = new VerticalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center, Children = { _track, details } };
		var hop = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.Center, Children = { _hopper, _hopShadow } };
		var words = new VerticalStackLayout { Spacing = 18, HorizontalOptions = LayoutOptions.Fill, Children = { hop, _gettingTitle, _coversHost, bar } };
		words.SizeChanged += (_, _) => bar.WidthRequest = Math.Min(TrackMaxWidth, Math.Max(0, words.Width)); // a 340 column, narrower on a small window
		var cancel = BuildCancel();
		_gettingShell = new OnboardingShell(words, null, cancel, centred: true);
		_track.SizeChanged += (_, _) => ShowBar();
		return new Grid { AutomationId = "FirstBooksGetting", IsVisible = false, Children = { _gettingShell } };
	}

	// The outline "Cancel" (44 high, white, 2 px #e3dccd): it ends the download the same way Back does; the ticks and the books that landed stay.
	private View BuildCancel()
	{
		var label = new Label { Text = AppResources.Common_Cancel, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		var button = new Border
		{
			AutomationId = "WaitCancel", HeightRequest = CancelHeight, BackgroundColor = Colors.White, StrokeThickness = CancelBorder, Stroke = Color.FromArgb(CancelBorderColour),
			StrokeShape = new RoundRectangle { CornerRadius = CancelHeight / 2 }, HorizontalOptions = LayoutOptions.Center, Padding = new Thickness(36, 0), Content = label,
		};
		SemanticProperties.SetDescription(button, AppResources.Common_Cancel);
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => _downloadCancel?.Cancel();
		button.GestureRecognizers.Add(tap);
		return button;
	}

	// Shows the page over the picker with the picked covers and starts Dino hopping.
	private void ShowGetting(FirstBooksWaitCovers covers)
	{
		_coversHost.Children.Clear();
		_coversHost.Children.Add(covers);
		_progress = 0;
		SetGettingText(AppResources.FirstBooks_Getting, string.Empty, string.Empty, 0);
		ShowLayer(_gettingLayer);
		StartHop();
		LogState("getting");
	}

	private void SetGettingText(string line, string bookOf, string percent, double progress)
	{
		_gettingTitle.Text = line;
		_bookOf.Text = bookOf;
		_percent.Text = percent;
		_progress = progress;
		ShowBar();
	}

	private void ShowBar() => _fill.WidthRequest = Math.Max(0, _track.Width) * _progress;

	// The mock's dinoHop 1.1 s: Dino rises and falls while its shadow shrinks and grows. It runs only while the page shows.
	private void StartHop()
	{
		var hop = new Animation
		{
			{ 0, 0.5, new Animation(v => SetHop(v), 0, 1, Easing.CubicOut) },
			{ 0.5, 1, new Animation(v => SetHop(v), 1, 0, Easing.CubicIn) },
		};
		hop.Commit(this, HopAnimation, length: HopMilliseconds, repeat: () => true);
	}

	private void StopHop() => this.AbortAnimation(HopAnimation);

	private void SetHop(double lift)
	{
		_hopper.TranslationY = -HopHeight * lift;
		_hopShadow.Scale = 1 - (0.35 * lift);
		_hopShadow.Opacity = 1 - (0.5 * lift);
	}
}
