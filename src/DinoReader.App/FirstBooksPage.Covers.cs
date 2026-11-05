using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// The covers row of the first-books download wait (Slice 261, mock Batch 47 frame 165): the picked books' covers, 54 wide at 3:4 with a 9 dp radius. A book that has landed shows a 24 dp
/// green tick badge (3 dp <c>Cream</c> border, offset -6 at the top right) at full strength; the others wait at 55% opacity. It sits in the shared wait overlay's content slot.
/// </summary>
internal sealed class FirstBooksWaitCovers : HorizontalStackLayout
{
	private const double CoverWidth = 54;
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
