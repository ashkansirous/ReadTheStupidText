using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace DinoReader;

/// <summary>
/// The grown-up gallery's storyteller status line (Slice 115, mock frame 50): a download glyph, "Getting the storyteller voices…", the percentage and a
/// progress bar; on mobile data a white card with a Wi-Fi glyph, the size and a "Download now" pill. It is empty while nothing is happening and in the child's gallery.
/// </summary>
internal sealed class StorytellerStatusLine : ContentView
{
	private const double BarHeight = 6;
	private const double CardCorner = 16;
	private const double PillHeight = 44;
	private const double WifiSize = 18;
	private const double MinStar = 0.0001;
	private const string WifiPath = "M2 8.5 A15 15 0 0 1 22 8.5 M5.5 12 A10 10 0 0 1 18.5 12 M9 15.5 A5 5 0 0 1 15 15.5 M11 19 A1 1 0 1 0 13 19 A1 1 0 1 0 11 19";
	private static readonly Color Track = Color.FromArgb("#ece3d0");
	private static readonly PathGeometryConverter Converter = new();

	private Label _percent = new() { FontSize = 13, TextColor = DinoTokens.Get(DinoTokens.Muted), VerticalOptions = LayoutOptions.Center };
	private ColumnDefinition _done = new(new GridLength(MinStar, GridUnitType.Star));
	private ColumnDefinition _left = new(new GridLength(1, GridUnitType.Star));
	private StorytellerPhase _built = StorytellerPhase.Idle;
	private int _builtMegabytes = -1;

	public StorytellerStatusLine()
	{
		Padding = new Thickness(18, 0, 18, 12);
		IsVisible = false;
	}

	/// <summary>What "Download now" does; set by the page.</summary>
	public Func<Task> DownloadNow { get; set; } = () => Task.CompletedTask;

	/// <summary>What "Try again" does; set by the page.</summary>
	public Func<Task> Retry { get; set; } = () => Task.CompletedTask;

	/// <summary>Forgets what was built, so the next <see cref="Apply"/> rebuilds the text (the app language changed).</summary>
	public void Invalidate()
	{
		_built = StorytellerPhase.Idle;
		_builtMegabytes = -1;
	}

	/// <summary>Draws <paramref name="status"/>; the line shows only in grown-up mode and while there is something to say. A download's progress only moves the bar and the percentage.</summary>
	public void Apply(StorytellerStatus status, bool grownUp)
	{
		IsVisible = grownUp && status.Phase != StorytellerPhase.Idle;
		if (status.Phase == StorytellerPhase.Downloading && _built == StorytellerPhase.Downloading)
		{
			ShowProgress(status.Progress);
			return;
		}

		if (status.Phase == _built && status.Megabytes == _builtMegabytes)
		{
			return;
		}

		_built = status.Phase;
		_builtMegabytes = status.Megabytes;
		Content = status.Phase switch
		{
			StorytellerPhase.Downloading => BuildDownloading(status.Progress),
			StorytellerPhase.WaitingForWifi => BuildWaiting(status.Megabytes),
			StorytellerPhase.Failed => BuildFailed(),
			_ => null,
		};
	}

	private void ShowProgress(double progress)
	{
		double done = Math.Clamp(progress, 0, 1);
		_percent.Text = done.ToString("P0", AppResources.Culture);
		_done.Width = new GridLength(Math.Max(done, MinStar), GridUnitType.Star);
		_left.Width = new GridLength(Math.Max(1 - done, MinStar), GridUnitType.Star);
		SemanticProperties.SetDescription(this, CastText.StorytellerGetting + " " + _percent.Text);
	}

	private VerticalStackLayout BuildDownloading(double progress)
	{
		// Fresh parts for every build: a replaced view may still own the old ones.
		_percent = new Label { FontSize = 13, TextColor = DinoTokens.Get(DinoTokens.Muted), VerticalOptions = LayoutOptions.Center };
		_done = new ColumnDefinition(new GridLength(MinStar, GridUnitType.Star));
		_left = new ColumnDefinition(new GridLength(1, GridUnitType.Star));
		var row = new Grid { ColumnSpacing = 8, ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		row.Add(new IconView { Kind = IconKind.Download, Color = DinoTokens.Get(DinoTokens.Brand), Size = 16, StrokeWidth = 2.4, VerticalOptions = LayoutOptions.Center }, 0);
		row.Add(new Label
		{
			Text = CastText.StorytellerGetting, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Body),
			VerticalOptions = LayoutOptions.Center,
		}, 1);
		row.Add(_percent, 2);
		var stack = new VerticalStackLayout { Spacing = 6, Children = { row, Bar() } };
		ShowProgress(progress);
		return stack;
	}

	// The fill is a star-sized column of the track, so it follows the track's width without measuring.
	private Border Bar()
	{
		var grid = new Grid { ColumnSpacing = 0, ColumnDefinitions = [_done, _left] };
		grid.Add(new BoxView { Color = DinoTokens.Get(DinoTokens.Brand), CornerRadius = BarHeight / 2 }, 0);
		return new Border
		{
			HeightRequest = BarHeight, BackgroundColor = Track, StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = BarHeight / 2 }, Content = grid,
		};
	}

	private Border BuildWaiting(int megabytes)
	{
		var texts = new VerticalStackLayout
		{
			VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new Label { Text = CastText.StorytellerWaitWifi, FontSize = 13, FontAttributes = FontAttributes.Bold },
				new Label { Text = CastText.StorytellerSize(megabytes), FontSize = 12, TextColor = DinoTokens.Get(DinoTokens.Muted) },
			},
		};
		return Card(WifiGlyph(), texts, CastText.StorytellerDownloadNow, () => DownloadNow());
	}

	private Border BuildFailed()
	{
		var text = new Label
		{
			Text = CastText.StorytellerFailed, FontSize = 13, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center,
		};
		return Card(WifiGlyph(), text, CastText.StorytellerTryAgain, () => Retry());
	}

	private static Border Card(View glyph, View texts, string pillText, Func<Task> tapped)
	{
		var card = new Grid
		{
			ColumnSpacing = 10,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
		};
		card.Add(glyph, 0);
		card.Add(texts, 1);
		card.Add(Pill(pillText, tapped), 2);
		return new Border
		{
			BackgroundColor = Colors.White, StrokeThickness = 0, Padding = new Thickness(14, 12),
			StrokeShape = new RoundRectangle { CornerRadius = CardCorner }, Content = card,
		};
	}

	private static Path WifiGlyph() => new()
	{
		Data = (Geometry)Converter.ConvertFromInvariantString(WifiPath)!,
		Stroke = new SolidColorBrush(DinoTokens.Get(DinoTokens.Muted)),
		StrokeThickness = 2.2,
		StrokeLineCap = PenLineCap.Round,
		StrokeLineJoin = PenLineJoin.Round,
		Aspect = Stretch.Uniform,
		WidthRequest = WifiSize,
		HeightRequest = WifiSize,
		VerticalOptions = LayoutOptions.Center,
	};

	private static Border Pill(string text, Func<Task> tapped)
	{
		var pill = new Border
		{
			HeightRequest = PillHeight, Padding = new Thickness(12, 0), BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint), StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = PillHeight / 2 },
			Content = new Label
			{
				Text = text, FontSize = 13.5, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Brand),
				VerticalOptions = LayoutOptions.Center,
			},
		};
		SemanticProperties.SetDescription(pill, text);
		CastViews.AddTap(pill, tapped);
		return pill;
	}
}

/// <summary>
/// The one-time "Storyteller voices are ready" sheet (Slice 115, mock frame 51): a green check, the title, the re-cast line, a narrator row whose
/// play button speaks the English narrator, and OK. It lives inside the gallery over the dimmed grid and opens only in grown-up mode.
/// </summary>
internal sealed class StorytellerReadySheet : Grid
{
	private const double SheetCorner = 28;
	private const double BadgeSize = 56;
	private const double PlaySize = 44;
	private const double OkHeight = 52;
	private const double OkEdge = 5;
	private static readonly Color BadgeFill = Color.FromArgb("#E3F4E1");

	private readonly Label _narrator = new() { FontSize = 15, FontAttributes = FontAttributes.Bold };
	private readonly Label _narratorRole = new() { FontSize = 12.5, TextColor = DinoTokens.Get(DinoTokens.Muted) };

	public StorytellerReadySheet()
	{
		IsVisible = false;
		BackgroundColor = DinoTokens.Get(DinoTokens.SheetScrim);
		CastViews.AddTap(this, () => Task.CompletedTask);
		Children.Add(new Border
		{
			VerticalOptions = LayoutOptions.End,
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			Padding = new Thickness(18, 24, 18, 26),
			StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(SheetCorner, SheetCorner, 0, 0) },
			Content = Body(),
		});
	}

	/// <summary>What the narrator's play button does; set by the page.</summary>
	public Func<Task> Play { get; set; } = () => Task.CompletedTask;

	/// <summary>Runs after OK closed the sheet; set by the page.</summary>
	public Action Closed { get; set; } = () => { };

	/// <summary>Opens the sheet naming <paramref name="narratorName"/> as the <paramref name="language"/> narrator.</summary>
	public void Show(string narratorName, string language)
	{
		_narrator.Text = narratorName;
		_narratorRole.Text = CastText.StorytellerYourNarrator(language);
		IsVisible = true;
	}

	public void Hide() => IsVisible = false;

	private VerticalStackLayout Body()
	{
		var title = new Label { Text = CastText.StorytellerReady, FontSize = 21, FontAttributes = FontAttributes.Bold };
		SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
		var text = new VerticalStackLayout
		{
			Spacing = 6,
			Children =
			{
				title,
				new Label { Text = CastText.StorytellerReadyBody, FontSize = 14.5, LineHeight = 1.4, TextColor = DinoTokens.Get(DinoTokens.Body) },
			},
		};
		return new VerticalStackLayout { Spacing = 14, Children = { Badge(), text, NarratorRow(), OkButton() } };
	}

	private static Border Badge() => new()
	{
		WidthRequest = BadgeSize, HeightRequest = BadgeSize, HorizontalOptions = LayoutOptions.Start, BackgroundColor = BadgeFill, StrokeThickness = 0,
		StrokeShape = new RoundRectangle { CornerRadius = BadgeSize / 2 },
		Content = new IconView
		{
			Kind = IconKind.Check, Color = DinoTokens.Get(DinoTokens.StorytellerOn), Size = 28, StrokeWidth = 2.8,
			HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
		},
	};

	private Border NarratorRow()
	{
		var play = new Border
		{
			WidthRequest = PlaySize, HeightRequest = PlaySize, BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint), StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = PlaySize / 2 },
			Content = new IconView
			{
				Kind = IconKind.Play, Color = DinoTokens.Get(DinoTokens.Brand), Filled = true, Size = 16,
				HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
			},
		};
		SemanticProperties.SetDescription(play, CastText.Narrator);
		CastViews.AddTap(play, () => Play());
		var row = new Grid
		{
			ColumnSpacing = 10,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
		};
		row.Add(play, 0);
		row.Add(new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Children = { _narrator, _narratorRole } }, 1);
		return new Border
		{
			BackgroundColor = DinoTokens.Get(DinoTokens.Sand), StrokeThickness = 0, Padding = new Thickness(12, 10),
			StrokeShape = new RoundRectangle { CornerRadius = 16 }, Content = row,
		};
	}

	// The coral face with its darker 5 px edge below, like the first-run button but at the mock's 52 px.
	private Border OkButton()
	{
		var face = new Border
		{
			HeightRequest = OkHeight, StrokeThickness = 0, BackgroundColor = DinoTokens.Get(DinoTokens.Coral),
			StrokeShape = new RoundRectangle { CornerRadius = OkHeight / 2 },
			Content = new Label
			{
				Text = CastText.Ok, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Colors.White,
				HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
			},
		};
		var edge = new Border
		{
			StrokeThickness = 0, Padding = new Thickness(0, 0, 0, OkEdge), BackgroundColor = DinoTokens.Get(DinoTokens.CoralEdge),
			StrokeShape = new RoundRectangle { CornerRadius = (OkHeight + OkEdge) / 2 }, Content = face,
		};
		SemanticProperties.SetDescription(edge, CastText.Ok);
		CastViews.AddTap(edge, () => { Hide(); Closed(); return Task.CompletedTask; });
		return edge;
	}
}
