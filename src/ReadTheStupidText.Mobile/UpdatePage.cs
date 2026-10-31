using ReadTheStupidText.Application.AppConfig;
using ReadTheStupidText.Mobile.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// The full-screen "Update needed" page (Slice 217, mock frame R14 of "Batch 42 Updates and Paused Downloads"; port of Dino Reader's Slice 132):
/// shown instead of the whole app while the installed version is below the config's <c>minVersion</c>. Only the header bar, a message and one
/// button to the store; there is no close, skip or back inside the page, and Android's Back button takes its default way out of the app.
/// There is no grown-up gate (the mock leaves it out). Android and iOS share it; only the button text differs.
/// </summary>
public sealed class UpdatePage : ContentPage
{
    private const double Gutter = 24;
    private const double CircleSize = 132;
    private const double TileSize = 96;
    private const double TileRadius = 24;
    private const double TileGlyphInset = 14;
    private const double BadgeSize = 42;
    private const double BadgeRing = 4;
    private const double IconBox = 24;
    private const double IconStroke = 2.4;
    private const double TitleSize = 24;
    private const double BodySize = 15;
    private const double BodyLineHeight = 1.45;
    private const double BodyMaxWidth = 280;
    private const double ButtonHeight = 60;
    private const double ButtonRadius = 16;
    private const double ButtonTextSize = 17;
    private const double HeaderTitleSize = 17;
    private const double HeaderMinHeight = 64;
    private const string DownArrow = "M 12 5 v 14 M 6 13 l 6 6 l 6 -6";

    private static readonly Color BrandStart = Color.FromArgb("#5B57E8");
    private static readonly Color BrandEnd = Color.FromArgb("#3B82F6");
    private static readonly Color PageLight = Color.FromArgb("#F4F5F9");
    private static readonly Color PageDark = Color.FromArgb("#15161C");
    private static readonly Color CircleFill = Color.FromArgb("#1A5B57E8");
    private static readonly Color BodyLight = Color.FromArgb("#9E000000");
    private static readonly Color BodyDark = Color.FromArgb("#A3FFFFFF");
    private static readonly Color TitleLight = Color.FromArgb("#1f2027");
    private static readonly Color TitleDark = Color.FromArgb("#E6E6EA");

    private bool _opening;

    public UpdatePage()
    {
        Shell.SetNavBarIsVisible(this, false);
        NavigationPage.SetHasNavigationBar(this, false);
        SafeAreaEdges = SafeAreaEdges.None;
        this.SetAppThemeColor(BackgroundColorProperty, PageLight, PageDark);
        var grid = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)] };
        grid.Add(Header(), 0);
        grid.Add(Message(), 0, 1);
        grid.Add(StoreButton(), 0, 2);
        Content = grid;
    }

#if DEBUG
    protected override void OnAppearing()
    {
        base.OnAppearing();
        ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write("UPDATE_PAGE shown"); // the scripted check asserts on this line
    }
#endif

    private static Grid Header()
    {
        var title = new Label
        {
            TextColor = Colors.White,
            FontSize = HeaderTitleSize,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
        };
        TextFlow.ForUi(title, AppResources.App_Name);
        return new Grid
        {
            Padding = new Thickness(18, 8, 8, 8),
            MinimumHeightRequest = HeaderMinHeight,
            SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container),
            Background = Gradient(),
            Children = { title },
        };
    }

    private static LinearGradientBrush Gradient() => new(
        [new GradientStop(BrandStart, 0f), new GradientStop(BrandEnd, 1f)], new Point(0, 0), new Point(1, 1));

    private static VerticalStackLayout Message()
    {
        var title = new Label
        {
            AutomationId = "UpdateTitle",
            FontSize = TitleSize,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
        };
        title.SetAppThemeColor(Label.TextColorProperty, TitleLight, TitleDark);
        TextFlow.ForUi(title, AppResources.Update_Title);
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        var body = new Label
        {
            AutomationId = "UpdateBody",
            FontSize = BodySize,
            LineHeight = BodyLineHeight,
            HorizontalTextAlignment = TextAlignment.Center,
            MaximumWidthRequest = BodyMaxWidth,
        };
        body.SetAppThemeColor(Label.TextColorProperty, BodyLight, BodyDark);
        TextFlow.ForUi(body, AppResources.Update_Body);
        return new VerticalStackLayout { Spacing = 14, VerticalOptions = LayoutOptions.Center, Padding = new Thickness(Gutter, 0), Children = { Artwork(), title, body } };
    }

    // The app tile on a soft circle with a download badge at the bottom right.
    private static AbsoluteLayout Artwork()
    {
        var art = new AbsoluteLayout { WidthRequest = CircleSize, HeightRequest = CircleSize, HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 0, 0, 10) };
        var circle = new Border { StrokeThickness = 0, BackgroundColor = CircleFill, StrokeShape = new RoundRectangle { CornerRadius = CircleSize / 2 } };
        var tile = new Border
        {
            StrokeThickness = 0,
            Background = Gradient(),
            StrokeShape = new RoundRectangle { CornerRadius = TileRadius },
            Content = new Image { Source = "header_glyph.png", Aspect = Aspect.AspectFit, Margin = new Thickness(TileGlyphInset) },
        };
        double badgeBox = BadgeSize + 2 * BadgeRing;
        var badge = new Border
        {
            StrokeThickness = BadgeRing,
            BackgroundColor = BrandStart,
            StrokeShape = new RoundRectangle { CornerRadius = badgeBox / 2 },
            Content = DownArrowIcon(),
        };
        badge.SetAppThemeColor(Border.StrokeProperty, PageLight, PageDark);
        SemanticProperties.SetDescription(art, AppResources.Update_Title);
        AbsoluteLayout.SetLayoutBounds(circle, new Rect(0, 0, CircleSize, CircleSize));
        AbsoluteLayout.SetLayoutBounds(tile, new Rect((CircleSize - TileSize) / 2, (CircleSize - TileSize) / 2, TileSize, TileSize));
        AbsoluteLayout.SetLayoutBounds(badge, new Rect(CircleSize - badgeBox, CircleSize - badgeBox, badgeBox, badgeBox));
        art.Children.Add(circle);
        art.Children.Add(tile);
        art.Children.Add(badge);
        return art;
    }

    private static Microsoft.Maui.Controls.Shapes.Path DownArrowIcon() => new()
    {
        Data = (Geometry?)new PathGeometryConverter().ConvertFromInvariantString(DownArrow),
        Aspect = Stretch.None,
        WidthRequest = IconBox,
        HeightRequest = IconBox,
        Stroke = Colors.White,
        StrokeThickness = IconStroke,
        StrokeLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
    };

    private Border StoreButton()
    {
        string text = AppUpdate.ThisPlatform == AppPlatform.Ios ? AppResources.Update_ButtonIos : AppResources.Update_ButtonAndroid;
        var label = new Label
        {
            FontSize = ButtonTextSize,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };
        TextFlow.ForUi(label, text);
        var button = new Border
        {
            AutomationId = "UpdateButton",
            HeightRequest = ButtonHeight,
            Margin = new Thickness(Gutter, 0, Gutter, Gutter),
            Background = Gradient(),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = ButtonRadius },
            Shadow = new Shadow { Brush = BrandStart, Opacity = 0.32f, Radius = 14, Offset = new Point(0, 4) },
            Content = label,
        };
        SemanticProperties.SetDescription(button, text);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await OpenStoreOnceAsync();
        button.GestureRecognizers.Add(tap);
        return button;
    }

    // A second tap while the store is opening is ignored.
    private async Task OpenStoreOnceAsync()
    {
        if (_opening)
        {
            return;
        }

        _opening = true;
        try
        {
            await AppUpdate.OpenStoreAsync();
        }
        finally
        {
            _opening = false;
        }
    }
}
