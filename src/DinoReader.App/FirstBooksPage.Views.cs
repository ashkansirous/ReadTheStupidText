using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

// The views of the first-run book picker (Slice 261, mock Batch 47 frames 163-166; Slice 325 part D, onboarding mock frames O2, O2-8, OL2, OT2, OW2, O4): the list with its pinned bottom bar and the offline
// message, both on OnboardingShell. Built once; a size change only re-places the views and the cards.
public partial class FirstBooksPage
{
	private const double BackSize = 44;
	private const double BackIconSize = 22;
	private const double CountSize = 13.5;
	private const double ColumnGap = 14;
	private const double RowGap = 16;
	private const double BarFade = 16;
	private const double OfflineIconSize = 150;
	private const double OfflineBadgeSize = 44;
	private const double OfflineBadgeBorder = 4;
	private const double OfflineBodyMaxWidth = 270;
	private const double OfflineHeadingSize = 23;
	private const double OfflineIconInner = 110;
	private const double NoWifiScale = 0.5;
	private const double HeaderIconSize = 64;
	private const double HeaderIconRadius = 18;

	private readonly List<Border> _backButtons = [];
	private readonly Grid _cardsGrid = new() { ColumnSpacing = ColumnGap, RowSpacing = RowGap };
	private readonly Label _heading = new() { FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Ink), VerticalOptions = LayoutOptions.Center };
	private readonly Label _body = new() { LineHeight = 1.4, TextColor = DinoTokens.Get(DinoTokens.Body) };
	private readonly Label _count = new() { FontSize = CountSize, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, AutomationId = "FirstBooksCount" };
	private readonly CoralButton _continue = new() { AutomationId = "FirstBooksContinue", Text = AppResources.FirstBooks_GetThese }; // Slice 362 (mock N3): "Get these books"
	private readonly Border _headerIcon = new() { WidthRequest = HeaderIconSize, HeightRequest = HeaderIconSize, StrokeThickness = 0, HorizontalOptions = LayoutOptions.Start, IsVisible = false, StrokeShape = new RoundRectangle { CornerRadius = HeaderIconRadius }, Content = new Image { Source = "dino_icon", Aspect = Aspect.AspectFill } };
	private readonly VerticalStackLayout _titleBlock = new() { Spacing = 4, VerticalOptions = LayoutOptions.Center };
	private Grid _header = null!;
	private Border _headerBack = null!;
	private Grid _listLayer = null!;
	private Grid _offlineLayer = null!;
	private Grid _gettingLayer = null!;
	private Grid _bar = null!;
	private OnboardingShell _listShell = null!;
	private OnboardingShell _offlineShell = null!;

	private void BuildViews()
	{
		_listLayer = BuildListLayer();
		_offlineLayer = BuildOfflineLayer();
		_gettingLayer = BuildGettingLayer();
		var root = new Grid { Children = { _listLayer, _offlineLayer, _gettingLayer, _waitOverlay } };
		Content = root;
		ShowLayer(_listLayer);
		SetContinueEnabled(false);
	}

	private Grid BuildListLayer()
	{
		SemanticProperties.SetHeadingLevel(_heading, SemanticHeadingLevel.Level1);
		_heading.Text = AppResources.FirstBooks_Title;
		_body.Text = AppResources.FirstBooks_Body;
		_titleBlock.Children.Add(_heading);
		_titleBlock.Children.Add(_body);
		_headerBack = BuildBack();
		_header = new Grid { RowSpacing = 8, ColumnSpacing = 4, Children = { _headerBack, _headerIcon, _titleBlock } };
		_bar = BuildBar();
		_cardsGrid.Margin = new Thickness(0, 3, 0, 8);
		_listShell = new OnboardingShell(_header, new ScrollView { Content = _cardsGrid }, _bar, stepBar: new OnboardingStepBar(OnboardingStep.Books)); // Slice 362: step 3 of 6
		return new Grid { AutomationId = "FirstBooksPicker", Children = { _listShell } };
	}

	private Grid BuildOfflineLayer()
	{
		var heading = new Label
		{
			Text = AppResources.FirstBooks_OfflineTitle, FontSize = OfflineHeadingSize, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center,
			TextColor = DinoTokens.Get(DinoTokens.Ink), AutomationId = "FirstBooksOfflineTitle",
		};
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
		var body = new Label
		{
			Text = AppResources.FirstBooks_OfflineBody, FontSize = 14, LineHeight = 1.4, HorizontalTextAlignment = TextAlignment.Center,
			TextColor = DinoTokens.Get(DinoTokens.Body), MaximumWidthRequest = OfflineBodyMaxWidth, HorizontalOptions = LayoutOptions.Center,
		};
		var words = new VerticalStackLayout { Spacing = 16, HorizontalOptions = LayoutOptions.Fill, Children = { BuildOfflineIcon(), heading, body } };
		var tryAgain = new CoralButton { Text = AppResources.Store_TryAgain, AutomationId = "FirstBooksTryAgain" };
		UiUsage.Press(tryAgain, UiControls.Retry, async () => await _busy.RunAsync(LoadAsync));
		_offlineShell = new OnboardingShell(words, null, tryAgain, centred: true);
		var layer = new Grid { AutomationId = "FirstBooksOffline", RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
		layer.Add(new VerticalStackLayout { Padding = new Thickness(OnboardingBackInset, 0), Children = { BuildBack() } }, 0, 0);
		layer.Add(_offlineShell, 0, 1);
		return layer;
	}

	private const double OnboardingBackInset = 14;

	// The dino in a 150 dp tinted circle with a white 44 dp no-connection badge at its bottom right (frame O4).
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
		tap.Tapped += async (_, _) =>
		{
			UiUsage.Tap(UiControls.Back);
			await _busy.RunAsync(GoBackAsync);
		};
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

	// The pinned bottom bar (frame O2): cream with a soft fade above it, the count and Continue. Which of them goes where is PlaceBar's call.
	private Grid BuildBar()
	{
		UiUsage.Press(_continue, UiControls.Continue, async () => await _busy.RunAsync(ContinueAsync));
		var bar = new Grid { BackgroundColor = DinoTokens.Get(DinoTokens.Cream), Shadow = new Shadow { Brush = new Color(1f, 246 / 255f, 230 / 255f, 0.95f), Offset = new Point(0, -8), Radius = (float)BarFade, Opacity = 0.95f } };
		bar.Add(_count);
		bar.Add(_continue);
		return bar;
	}

	// Frames O2 / O2-8 / OL2 / OW2: the count sits above Continue on a normal phone and beside it (count left, Continue right) on the iPhone 8 and in every two-pane shape.
	private void PlaceBar(OnboardingShape shape)
	{
		bool beside = OnboardingLayout.CountBesideContinue(shape);
		(double width, double height) = OnboardingLayout.ContinueSize(shape);
		_continue.SetSize(beside && double.IsNaN(width) ? 200 : width, height);
		_bar.RowSpacing = 8;
		_bar.ColumnSpacing = 12;
		_bar.ColumnDefinitions = beside ? [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] : [];
		_bar.RowDefinitions = beside ? [new RowDefinition(GridLength.Auto)] : [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];
		Grid.SetRow(_count, 0);
		Grid.SetColumn(_count, 0);
		Grid.SetRow(_continue, beside ? 0 : 1);
		Grid.SetColumn(_continue, beside ? 1 : 0);
		_count.HorizontalTextAlignment = beside ? TextAlignment.Start : TextAlignment.Center;
		_count.HorizontalOptions = beside ? LayoutOptions.Start : LayoutOptions.Fill;
		_continue.HorizontalOptions = beside ? LayoutOptions.End : LayoutOptions.Fill;
	}

	// Back arrow, title and line (frames O2, O2-8, OL2, OT2, OW2): stacked normally; on the iPhone 8 the title and the shorter line join the back row; on Windows the 64 dp icon sits above the title.
	private void PlaceHeader(OnboardingShape shape)
	{
		bool joined = shape.Compact;
		(double heading, double body) = HeaderSizes(shape);
		_heading.FontSize = heading;
		_body.FontSize = body;
		_body.Text = OnboardingLayout.ShortBooksLine(shape) ? AppResources.FirstBooks_BodyShort : AppResources.FirstBooks_Body;
		_headerIcon.IsVisible = shape.Split && shape.Family == OnboardingFamily.Windows;
		_header.ColumnDefinitions = joined ? [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] : [];
		_header.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];
		Grid.SetRow(_headerBack, 0);
		Grid.SetColumn(_headerBack, 0);
		Grid.SetRow(_headerIcon, 1);
		Grid.SetColumn(_headerIcon, 0);
		Grid.SetRow(_titleBlock, joined ? 0 : 2);
		Grid.SetColumn(_titleBlock, joined ? 1 : 0);
		Grid.SetColumnSpan(_headerIcon, joined ? 2 : 1);
		Grid.SetColumnSpan(_titleBlock, 1);
	}

	private static (double Heading, double Body) HeaderSizes(OnboardingShape shape)
	{
		if (!shape.Split)
		{
			return (shape.Compact ? 22 : 24, shape.Compact ? 13 : 14);
		}

		return shape.Family switch
		{
			OnboardingFamily.PhoneLandscape => (21, 13),
			OnboardingFamily.Windows => (30, 15.5),
			_ => (30, 15),
		};
	}

	private void SetContinueEnabled(bool enabled)
	{
		_continue.Active = enabled;
		SemanticProperties.SetHint(_continue, enabled ? string.Empty : AppResources.FirstBooks_PickTwo);
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
			? AppResources.Format(AppResources.FirstBooks_Picked, ticked, AppResources.Format(AppResources.Storyteller_Size, ReadTheStupidText.Application.Reading.StorytellerOffer.Megabytes(bytes)))
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

	// A size change: every shell and the header and bar are re-placed, then the covers.
	private void ApplyShape(OnboardingShape shape)
	{
		_listShell.Place(shape);
		_offlineShell.Place(shape);
		_gettingShell.Place(shape);
		PlaceHeader(shape);
		PlaceBar(shape);
		PlaceCards();
		LogShape();
	}

	private void PlaceCards()
	{
		if (_shape is not { } shape || _cards.Count == 0 || _windowSize.Width <= 0)
		{
			return;
		}

		int columns = OnboardingLayout.BookColumns(shape);
		double available = OnboardingShell.RightWidth(_windowSize.Width, shape);
		double width = Math.Max(0, (available - (ColumnGap * (columns - 1))) / columns);
		bool tags = OnboardingLayout.TagsShown(shape);
		_cardsGrid.ColumnDefinitions = [.. Enumerable.Range(0, columns).Select(_ => new ColumnDefinition(GridLength.Auto))];
		_cardsGrid.RowDefinitions = [.. Enumerable.Range(0, OnboardingLayout.RowsFor(_cards.Count, columns)).Select(_ => new RowDefinition(GridLength.Auto))];
		for (int i = 0; i < _cards.Count; i++)
		{
			GridCell cell = OnboardingLayout.CellOf(i, columns);
			_cards[i].SetWidth(width);
			_cards[i].SetTagsVisible(tags);
			Grid.SetRow(_cards[i], cell.Row);
			Grid.SetColumn(_cards[i], cell.Column);
		}
	}

	// One layer shows at a time; the bottom bar is the list shell's foot. The hop runs only while the getting page shows.
	private void ShowLayer(Grid layer)
	{
		_listLayer.IsVisible = layer == _listLayer;
		_offlineLayer.IsVisible = layer == _offlineLayer;
		_gettingLayer.IsVisible = layer == _gettingLayer;
		if (layer != _gettingLayer)
		{
			StopHop();
		}

		LogShape();
	}

	// Debug-only marker the onboarding flow reads: the page shown and how the shell is arranged (the waits, offline and ready pages are always one centred column).
	private void LogShape()
	{
#if DEBUG
		if (_shape is { } shape)
		{
			string page = _gettingLayer.IsVisible ? "getting" : _offlineLayer.IsVisible ? "offline" : "books";
			string kind = page == "books" ? (shape.Split ? "split" : "stack") : "centred";
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Redesign] onboarding page={page} shape={kind}");
		}
#endif
	}
}
