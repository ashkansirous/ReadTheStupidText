using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;

namespace DinoReader;

/// <summary>
/// What "update" means on each platform (Slice 132, Decision 139). The Android, iOS and Windows halves are in
/// <c>Platforms/&lt;OS&gt;/StoreUpdater.*.cs</c>. Both calls only start something outside the app and never throw.
/// </summary>
public static partial class StoreUpdater
{
	/// <summary>The Play listing id (<c>ApplicationId</c>).</summary>
	public const string AndroidPackageId = "uk.sirous.dinoreader";

	/// <summary>The Partner Center Store ID (<c>STORE.md</c>).</summary>
	public const string WindowsStoreId = "9N9VHRLCZXN5";

	/// <summary>The forced update: Android, Play's immediate in-app update; iOS, the App Store page; Windows, the Store's update request.</summary>
	public static partial Task StartUpdateAsync();

	/// <summary>Only opens the store page (the quiet "A new version is ready" row): no in-app update flow.</summary>
	public static partial Task OpenStoreAsync();
}

/// <summary>
/// The full-screen "Please update" page (Slice 132, Decision 139, mock frame 61 of "Dino Reader Batch 26-28"): shown instead of
/// the gallery while the installed version is below the config's <c>minVersion</c>, and nothing else is reachable. There is no
/// close or skip, and system Back does nothing. The button asks the grown-up gate first (Apple's Kids category needs a
/// parental gate before leaving the app), then starts <see cref="StoreUpdater.StartUpdateAsync"/>.
/// </summary>
public sealed class UpdatePage : ContentPage
{
	private const double Gutter = 22;
	private const double CircleSize = 150;
	private const double IconSize = 120;
	private const double IconRadius = 30;
	private const double BadgeSize = 44;
	private const double BadgeRing = 4;
	private const double BadgeInset = 2;
	private const double DownloadGlyph = 22;
	private const double TitleSize = 28;
	private const double BodySize = 14.5;
	private const double BodyMaxWidth = 280;

	private readonly GrownUpGate _gate;
	private readonly BusyGuard _busy = new();

	public UpdatePage(GrownUpGate gate)
	{
		_gate = gate;
		BackgroundColor = DinoTokens.Get(DinoTokens.Cream);
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		var button = new CoralButton { Text = AppResources.Update_Button };
		button.Clicked += (_, _) => _ = _busy.RunAsync(UpdateAsync);
		var grid = new Grid { RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)], Padding = new Thickness(Gutter, 0, Gutter, Gutter) };
		grid.Add(Message(), 0);
		grid.Add(button, 0, 1);
		Content = grid;
	}

	protected override bool OnBackButtonPressed() => true;

	private async Task UpdateAsync()
	{
		if (await _gate.RequestAsync(Navigation))
		{
			await StoreUpdater.StartUpdateAsync();
		}
	}

	private static VerticalStackLayout Message()
	{
		var title = new Label
		{
			Text = AppResources.Update_Title,
			FontSize = TitleSize,
			FontAttributes = FontAttributes.Bold,
			TextColor = DinoTokens.Get(DinoTokens.Ink),
			HorizontalTextAlignment = TextAlignment.Center,
			FlowDirection = UiFlow.ForText(AppResources.Update_Title),
		};
		SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
		var body = new Label
		{
			Text = AppResources.Update_Body,
			FontSize = BodySize,
			LineHeight = 1.4,
			TextColor = DinoTokens.Get(DinoTokens.Body),
			HorizontalTextAlignment = TextAlignment.Center,
			MaximumWidthRequest = BodyMaxWidth,
			FlowDirection = UiFlow.ForText(AppResources.Update_Body),
		};
		return new VerticalStackLayout { Spacing = 14, VerticalOptions = LayoutOptions.Center, Children = { Artwork(), title, body } };
	}

	// The app icon on a soft circle with a download badge at the bottom right.
	private static AbsoluteLayout Artwork()
	{
		var art = new AbsoluteLayout { WidthRequest = CircleSize, HeightRequest = CircleSize, HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 0, 0, 10) };
		var circle = new Border
		{
			StrokeThickness = 0,
			BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint),
			StrokeShape = new RoundRectangle { CornerRadius = CircleSize / 2 },
		};
		var icon = new Border
		{
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = IconRadius },
			Content = new Image { Source = "dino_icon", Aspect = Aspect.AspectFill },
		};
		var badge = new Border
		{
			StrokeThickness = BadgeRing,
			Stroke = DinoTokens.Get(DinoTokens.Cream),
			BackgroundColor = DinoTokens.Get(DinoTokens.Brand),
			StrokeShape = new RoundRectangle { CornerRadius = (BadgeSize + 2 * BadgeRing) / 2 },
			Content = new IconView { Kind = IconKind.Download, Color = Colors.White, Size = DownloadGlyph, StrokeWidth = 2.4, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};
		SemanticProperties.SetDescription(art, AppResources.Update_Title);
		AbsoluteLayout.SetLayoutBounds(circle, new Rect(0, 0, CircleSize, CircleSize));
		AbsoluteLayout.SetLayoutBounds(icon, new Rect((CircleSize - IconSize) / 2, (CircleSize - IconSize) / 2, IconSize, IconSize));
		double badgeBox = BadgeSize + 2 * BadgeRing;
		AbsoluteLayout.SetLayoutBounds(badge, new Rect(CircleSize - badgeBox - BadgeInset, CircleSize - badgeBox - BadgeInset, badgeBox, badgeBox));
		art.Children.Add(circle);
		art.Children.Add(icon);
		art.Children.Add(badge);
		return art;
	}
}

/// <summary>
/// The "Version" card of the grown-up settings (Slice 132, mock frame 62): a Version row showing this build, and, only while the
/// config names a newer release, a last row "A new version is ready" that opens the store (no popup, no badge, no extra gate:
/// grown-up mode is already gated). A grown-up page puts it in its column of cards.
/// </summary>
public static class VersionCard
{
	private const double CardRadius = 18;

	public static Border Build(AppConfigClient config, BusyGuard busy)
	{
		var rows = new VerticalStackLayout();
		rows.Children.Add(SettingsRows.Row(AppResources.Version_Title, SettingsRows.ValueLabel(AppConfigClient.InstalledVersion), null, busy));
		if (config.NewerVersionExists)
		{
			rows.Children.Add(SettingsRows.Divider());
			rows.Children.Add(NewVersionRow(busy));
		}

		return new Border { BackgroundColor = Colors.White, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = CardRadius }, Content = rows };
	}

	private static Grid NewVersionRow(BusyGuard busy)
	{
		Grid row = SettingsRows.Row(AppResources.Version_NewReady, new BoxView { WidthRequest = 0, Color = Colors.Transparent }, StoreUpdater.OpenStoreAsync, busy);
		if (row.Children.OfType<Label>().FirstOrDefault() is { } title)
		{
			title.FontAttributes = FontAttributes.Bold;
			title.TextColor = DinoTokens.Get(DinoTokens.Brand);
		}

		return row;
	}
}
