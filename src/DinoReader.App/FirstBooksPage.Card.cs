using DinoReader.Core;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// One book in the first-run picker (Slice 261, mock Batch 47 frames 163-164): a 3:4 cover with a tick circle at its top right, the title under it and the store tags under that
/// (Slice 260). A tap on the cover or the circle toggles it. Picked: the circle is filled <c>Brand</c> with a white tick and the cover gets a 3 dp <c>Brand</c> ring set off by a 3 dp
/// <c>Cream</c> gap. The card is built once for one store item (its AutomationId can be set only once); a reload builds new cards.
/// </summary>
internal sealed class FirstBookCard : VerticalStackLayout
{
	private const double CoverRadius = 16;
	private const double RingWidth = 3;
	private const double RingGap = 3;
	private const double TickSize = 34;
	private const double TickInset = 8;
	private const double TickBorder = 2;
	private const double TickIconSize = 18;
	private const double TickIconStroke = 3;
	private const double TitleSize = 14;
	private const double CardSpacing = 6;
	private const double CoverAspect = 4.0 / 3.0;
	private const string AutomationPrefix = "FirstBookCard-";

	private readonly Border _ring = new() { Padding = RingGap, StrokeThickness = RingWidth, Stroke = Colors.Transparent, StrokeShape = new RoundRectangle { CornerRadius = CoverRadius + RingGap } };
	private readonly Border _cover = new() { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = CoverRadius }, BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint) };
	private readonly Image _image = new() { Aspect = Aspect.AspectFill };
	private readonly Border _tick = new() { WidthRequest = TickSize, HeightRequest = TickSize, StrokeShape = new Ellipse(), HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, Margin = new Thickness(0, TickInset, TickInset, 0) };
	private readonly IconView _tickIcon = new() { Kind = IconKind.Check, Color = Colors.White, Size = TickIconSize, StrokeWidth = TickIconStroke, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

	public FirstBookCard(StoreItem item, int index, string title, Action toggled)
	{
		Item = item;
		Spacing = CardSpacing;
		_cover.Content = _image;
		_ring.Content = _cover;
		_tick.Content = _tickIcon;
		var host = new Grid { AutomationId = AutomationPrefix + index, Children = { _ring, _tick } };
		SemanticProperties.SetDescription(host, title);
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => toggled();
		host.GestureRecognizers.Add(tap);
		Children.Add(host);
		Children.Add(new Label { Text = title, FontSize = TitleSize, FontAttributes = FontAttributes.Bold, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation, TextColor = DinoTokens.Get(DinoTokens.Ink) });
		Children.Add(new StoreTagRow { Tags = item.Badges });
		SetPicked(false);
	}

	public StoreItem Item { get; }

	/// <summary>Shows or clears the tick and the ring.</summary>
	public void SetPicked(bool picked)
	{
		_ring.Stroke = picked ? DinoTokens.Get(DinoTokens.Brand) : Colors.Transparent;
		_tick.BackgroundColor = picked ? DinoTokens.Get(DinoTokens.Brand) : Colors.White;
		_tick.Stroke = picked ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.DotIdle);
		_tick.StrokeThickness = TickBorder;
		_tickIcon.IsVisible = picked;
	}

	/// <summary>Sets the cover picture; a book with none keeps the tinted block.</summary>
	public void SetCover(string? path)
	{
		CoverPath = path;
		_image.Source = path is null ? null : ImageSource.FromFile(path);
	}

	/// <summary>The cover file shown, or null while it loads (the download wait draws the same cover).</summary>
	public string? CoverPath { get; private set; }

	/// <summary>Sizes the card to <paramref name="width"/>: the cover is 3:4 inside the ring and its gap.</summary>
	public void SetWidth(double width)
	{
		WidthRequest = width;
		_cover.HeightRequest = (width - 2 * (RingWidth + RingGap)) * CoverAspect;
	}
}
