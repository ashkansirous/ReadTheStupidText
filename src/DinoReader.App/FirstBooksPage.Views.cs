using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

// The views of the first-run book picker (Slice 261, mock Batch 47 frames 163-166): the list with its pinned bottom bar, and the offline message. Built once; a size change only re-places the cards.
public partial class FirstBooksPage
{
	private const double BackSize = 44;
	private const double BackIconSize = 22;
	private const double HeadingSize = 24;
	private const double BodySize = 14;
	private const double CountSize = 13.5;
	private const double ContinueHeight = 56;
	private const double ContinueEdge = 5;
	private const double BarTop = 12;
	private const double BarBottom = 24;
	private const double OfflineIconSize = 150;
	private const double OfflineBadgeSize = 52;
	private const double OfflineBadgeBorder = 4;
	private const double OfflineBodyMaxWidth = 270;
	private const double OfflineHeadingSize = 23;
	private const double OfflineIconInner = 96;
	private const double NoWifiScale = 0.62;
	private const string DisabledFill = "#e6dfd0";

	private readonly List<Border> _backButtons = [];
	private readonly Grid _cardsGrid = new() { ColumnSpacing = ColumnGap, RowSpacing = RowGap };
	private readonly Label _count = new() { FontSize = CountSize, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center, AutomationId = "FirstBooksCount" };
	private readonly Border _continueEdge = new() { StrokeThickness = 0, Padding = new Thickness(0, 0, 0, ContinueEdge), AutomationId = "FirstBooksContinue", StrokeShape = new RoundRectangle { CornerRadius = (ContinueHeight + ContinueEdge) / 2 } };
	private readonly Border _continueFace = new() { HeightRequest = ContinueHeight, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = ContinueHeight / 2 } };
	private readonly Label _continueLabel = new() { FontSize = 17, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
	private Grid _listLayer = null!;
	private Grid _offlineLayer = null!;
	private VerticalStackLayout _bar = null!;

	private void BuildViews()
	{
		_listLayer = BuildListLayer();
		_offlineLayer = BuildOfflineLayer();
		_bar = BuildBar();
		var content = new Grid { Children = { _listLayer, _offlineLayer } };
		var root = new Grid { RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)] };
		root.Add(content, 0, 0);
		root.Add(_bar, 0, 1);
		Grid.SetRowSpan(_waitOverlay, 2);
		root.Add(_waitOverlay, 0, 0);
		Content = root;
		ShowLayer(_listLayer);
		SetContinueEnabled(false);
	}

	private Grid BuildListLayer()
	{
		var heading = new Label { Text = AppResources.FirstBooks_Title, FontSize = HeadingSize, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink) };
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
		var body = new Label { Text = AppResources.FirstBooks_Body, FontSize = BodySize, LineHeight = 1.4, TextColor = DinoTokens.Get(DinoTokens.Body) };
		var column = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(SideMargin, 0, SideMargin, BarBottom), Children = { BuildBack(), heading, body, _cardsGrid } };
		_cardsGrid.Margin = new Thickness(0, 12, 0, 0);
		return new Grid { AutomationId = "FirstBooksPicker", Children = { new ScrollView { Content = column } } };
	}

	private Grid BuildOfflineLayer()
	{
		var heading = new Label
		{
			Text = AppResources.FirstBooks_OfflineTitle, FontSize = OfflineHeadingSize, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center,
			TextColor = DinoTokens.Get(DinoTokens.Ink), MaximumWidthRequest = 300, AutomationId = "FirstBooksOfflineTitle",
		};
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
		var body = new Label
		{
			Text = AppResources.FirstBooks_OfflineBody, FontSize = BodySize, LineHeight = 1.4, HorizontalTextAlignment = TextAlignment.Center,
			TextColor = DinoTokens.Get(DinoTokens.Body), MaximumWidthRequest = OfflineBodyMaxWidth,
		};
		var middle = new VerticalStackLayout { Spacing = 16, VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.Center, Children = { BuildOfflineIcon(), heading, body } };
		var tryAgain = new CoralButton { Text = AppResources.Store_TryAgain, AutomationId = "FirstBooksTryAgain", Margin = new Thickness(SideMargin, 0, SideMargin, BarBottom) };
		tryAgain.Clicked += async (_, _) => await _busy.RunAsync(LoadAsync);
		var layer = new Grid { AutomationId = "FirstBooksOffline", RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)] };
		layer.Add(new VerticalStackLayout { Padding = new Thickness(SideMargin, 0), Children = { BuildBack() } }, 0, 0);
		layer.Add(middle, 0, 1);
		layer.Add(tryAgain, 0, 2);
		return layer;
	}

	// The dino in a 150 dp tinted circle with a small white no-connection badge at its bottom right (frame 166).
	private static Grid BuildOfflineIcon()
	{
		var circle = new Border
		{
			WidthRequest = OfflineIconSize, HeightRequest = OfflineIconSize, StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint), StrokeShape = new Ellipse(),
			Content = new Image { Source = "dino_icon", WidthRequest = OfflineIconInner, HeightRequest = OfflineIconInner, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		var badge = new Border
		{
			WidthRequest = OfflineBadgeSize, HeightRequest = OfflineBadgeSize, BackgroundColor = Colors.White, Stroke = DinoTokens.Get(DinoTokens.Cream), StrokeThickness = OfflineBadgeBorder,
			StrokeShape = new Ellipse(), HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.End,
			Content = new NoWifiIcon(DinoTokens.Get(DinoTokens.Body)) { Scale = NoWifiScale, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		return new Grid { HorizontalOptions = LayoutOptions.Center, Children = { circle, badge } };
	}

	private Border BuildBack()
	{
		var icon = new IconView { Kind = IconKind.Back, Color = DinoTokens.Get(DinoTokens.Ink), Size = BackIconSize, StrokeWidth = 2.2, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		var back = new Border
		{
			WidthRequest = BackSize, HeightRequest = BackSize, StrokeThickness = 0, BackgroundColor = Colors.Transparent, HorizontalOptions = LayoutOptions.Start, FlowDirection = FlowDirection.LeftToRight,
			IsVisible = BackRequested is not null, Content = icon,
		};
		SemanticProperties.SetDescription(back, AppResources.Common_Back);
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) => await _busy.RunAsync(GoBackAsync);
		back.GestureRecognizers.Add(tap);
		_backButtons.Add(back);
		return back;
	}

	// Each page owns the arrow it draws: both copies share one id per layer, set once here.
	private async Task GoBackAsync()
	{
		if (_backRequested is { } back)
		{
			await back();
		}
	}

	private void SetBackVisible(bool visible)
	{
		foreach (Border back in _backButtons)
		{
			back.IsVisible = visible;
		}
	}

	private VerticalStackLayout BuildBar()
	{
		_continueLabel.Text = AppResources.FirstRun_Continue;
		_continueFace.Content = _continueLabel;
		_continueEdge.Content = _continueFace;
		SemanticProperties.SetDescription(_continueEdge, AppResources.FirstRun_Continue);
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) => await _busy.RunAsync(ContinueAsync);
		_continueEdge.GestureRecognizers.Add(tap);
		return new VerticalStackLayout { Spacing = 8, Padding = new Thickness(SideMargin, BarTop, SideMargin, BarBottom), BackgroundColor = DinoTokens.Get(DinoTokens.Cream), Children = { _count, _continueEdge } };
	}

	private void SetContinueEnabled(bool enabled)
	{
		_continueEdge.BackgroundColor = enabled ? DinoTokens.Get(DinoTokens.CoralEdge) : Colors.Transparent;
		_continueFace.BackgroundColor = enabled ? DinoTokens.Get(DinoTokens.Coral) : Color.FromArgb(DisabledFill);
		_continueLabel.TextColor = enabled ? Colors.White : DinoTokens.Get(DinoTokens.Muted);
		SemanticProperties.SetHint(_continueEdge, enabled ? string.Empty : AppResources.FirstBooks_PickTwo);
	}

	// The count line and Continue follow the ticks: "Pick 2 books to start", "Pick 1 more", then "N picked · size" in the brand colour.
	private void RefreshBar()
	{
		int ticked = _ticked.Count;
		int need = FirstBooks.StillNeeded(ticked);
		_count.Text = need switch
		{
			2 => AppResources.FirstBooks_PickTwo,
			1 => AppResources.FirstBooks_PickOneMore,
			_ => PickedLine(ticked),
		};
		_count.TextColor = DinoTokens.Get(need == 0 ? DinoTokens.Brand : DinoTokens.Body);
		SetContinueEnabled(FirstBooks.CanContinue(ticked));
	}

	private string PickedLine(int ticked) =>
		FirstBooks.TotalBytes(_items, _ticked) is { } bytes
			? AppResources.Format(AppResources.FirstBooks_Picked, ticked, CastText.StorytellerSize(ReadTheStupidText.Application.Reading.StorytellerOffer.Megabytes(bytes)))
			: AppResources.Format(AppResources.FirstBooks_PickedNoSize, ticked);

	private void BuildCards()
	{
		_cards.Clear();
		_cardsGrid.Children.Clear();
		string language = LanguageChoice.Current();
		for (int i = 0; i < _items.Count; i++)
		{
			StoreItem item = _items[i];
			FirstBookCard card = null!;
			card = new FirstBookCard(item, i, ShippedTitles.ShownInStore(item, language), () => Toggle(card));
			card.SetPicked(_ticked.Contains(item.Id));
			_cards.Add(card);
			_cardsGrid.Children.Add(card);
		}

		PlaceCards();
	}

	private void PlaceCards()
	{
		if (_cards.Count == 0 || _width <= 0)
		{
			return;
		}

		int columns = Columns();
		double width = CardWidth(columns);
		_cardsGrid.ColumnDefinitions = [.. Enumerable.Range(0, columns).Select(_ => new ColumnDefinition(GridLength.Auto))];
		_cardsGrid.RowDefinitions = [.. Enumerable.Range(0, (_cards.Count + columns - 1) / columns).Select(_ => new RowDefinition(GridLength.Auto))];
		for (int i = 0; i < _cards.Count; i++)
		{
			_cards[i].SetWidth(width);
			Grid.SetRow(_cards[i], i / columns);
			Grid.SetColumn(_cards[i], i % columns);
		}
	}

	// One layer shows at a time; the bottom bar belongs to the list only.
	private void ShowLayer(Grid layer)
	{
		_listLayer.IsVisible = layer == _listLayer;
		_offlineLayer.IsVisible = layer == _offlineLayer;
		_bar.IsVisible = layer == _listLayer;
	}
}
