using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace DinoReader;

/// <summary>
/// A country flag in a circle (Slice 173, mock frames 79 and 88): drawn, not a picture, so nothing is bundled and it is sharp at any size. One flag per
/// language, named by <see cref="StoryLanguages.FlagOf"/>. The flags are simplified (stripes, crosses, discs); an unknown country draws a plain grey disc.
/// </summary>
public sealed class FlagView : GraphicsView
{
	private readonly FlagDrawable _drawable = new();

	public FlagView()
	{
		Drawable = _drawable;
		InputTransparent = true;
	}

	/// <summary>The flag's country code ("GB", "IR", ...).</summary>
	public string Country
	{
		get => _drawable.Country;
		set
		{
			_drawable.Country = value;
			Invalidate();
		}
	}
}

internal sealed class FlagDrawable : IDrawable
{
	public string Country { get; set; } = string.Empty;

	public void Draw(ICanvas canvas, RectF rect)
	{
		float size = Math.Min(rect.Width, rect.Height);
		var disc = new PathF();
		disc.AppendCircle(rect.Center.X, rect.Center.Y, size / 2);
		canvas.ClipPath(disc);
		var square = new RectF(rect.Center.X - size / 2, rect.Center.Y - size / 2, size, size);
		DrawCountry(canvas, square);
	}

	private void DrawCountry(ICanvas canvas, RectF r)
	{
		switch (Country)
		{
			case "GB": UnionJack(canvas, r); break;
			case "IR": Stripes(canvas, r, true, "#239F40", "#FFFFFF", "#DA0000"); break;
			case "DE": Stripes(canvas, r, true, "#000000", "#DD0000", "#FFCE00"); break;
			case "ES": Spain(canvas, r); break;
			case "FR": Stripes(canvas, r, false, "#0055A4", "#FFFFFF", "#EF4135"); break;
			case "PT": Portugal(canvas, r); break;
			case "CN": China(canvas, r); break;
			case "SA": Saudi(canvas, r); break;
			case "RU": Stripes(canvas, r, true, "#FFFFFF", "#0039A6", "#D52B1E"); break;
			case "JP": Japan(canvas, r); break;
			case "DK": NordicCross(canvas, r, "#C8102E", "#FFFFFF", null); break;
			case "FI": NordicCross(canvas, r, "#FFFFFF", "#003580", null); break;
			case "SE": NordicCross(canvas, r, "#006AA7", "#FECC00", null); break;
			case "NO": NordicCross(canvas, r, "#BA0C2F", "#FFFFFF", "#00205B"); break;
			case "NL": Stripes(canvas, r, true, "#AE1C28", "#FFFFFF", "#21468B"); break;
			default: Fill(canvas, r, "#C9C3D6"); break;
		}
	}

	private static void Fill(ICanvas canvas, RectF r, string color)
	{
		canvas.FillColor = Color.FromArgb(color);
		canvas.FillRectangle(r);
	}

	private static void Part(ICanvas canvas, RectF r, string color, float x, float y, float w, float h)
	{
		canvas.FillColor = Color.FromArgb(color);
		canvas.FillRectangle(r.X + x * r.Width, r.Y + y * r.Height, w * r.Width, h * r.Height);
	}

	private static void Disc(ICanvas canvas, RectF r, string color, float x, float y, float radius)
	{
		canvas.FillColor = Color.FromArgb(color);
		canvas.FillCircle(r.X + x * r.Width, r.Y + y * r.Height, radius * r.Width);
	}

	private static void Stripes(ICanvas canvas, RectF r, bool horizontal, string a, string b, string c)
	{
		string[] colors = [a, b, c];
		for (int i = 0; i < colors.Length; i++)
		{
			float at = i / 3f;
			Part(canvas, r, colors[i], horizontal ? 0 : at, horizontal ? at : 0, horizontal ? 1 : 1 / 3f, horizontal ? 1 / 3f : 1);
		}
	}

	private static void Spain(ICanvas canvas, RectF r)
	{
		Fill(canvas, r, "#AA151B");
		Part(canvas, r, "#F1BF00", 0, 0.25f, 1, 0.5f);
	}

	private static void Portugal(ICanvas canvas, RectF r)
	{
		Part(canvas, r, "#006600", 0, 0, 0.4f, 1);
		Part(canvas, r, "#FF0000", 0.4f, 0, 0.6f, 1);
		Disc(canvas, r, "#FFE000", 0.4f, 0.5f, 0.14f);
	}

	private static void Japan(ICanvas canvas, RectF r)
	{
		Fill(canvas, r, "#FFFFFF");
		Disc(canvas, r, "#BC002D", 0.5f, 0.5f, 0.3f);
	}

	private static void Saudi(ICanvas canvas, RectF r)
	{
		Fill(canvas, r, "#006C35");
		Part(canvas, r, "#FFFFFF", 0.2f, 0.62f, 0.6f, 0.07f);
		Part(canvas, r, "#FFFFFF", 0.3f, 0.38f, 0.4f, 0.1f);
	}

	private static void China(ICanvas canvas, RectF r)
	{
		Fill(canvas, r, "#DE2910");
		canvas.FillColor = Color.FromArgb("#FFDE00");
		var star = new PathF();
		const int Points = 5;
		float cx = r.X + 0.3f * r.Width;
		float cy = r.Y + 0.3f * r.Height;
		for (int i = 0; i < Points * 2; i++)
		{
			double angle = -Math.PI / 2 + i * Math.PI / Points;
			float radius = (i % 2 == 0 ? 0.2f : 0.08f) * r.Width;
			var point = new PointF(cx + (float)Math.Cos(angle) * radius, cy + (float)Math.Sin(angle) * radius);
			if (i == 0)
			{
				star.MoveTo(point);
			}
			else
			{
				star.LineTo(point);
			}
		}

		star.Close();
		canvas.FillPath(star);
	}

	private static void NordicCross(ICanvas canvas, RectF r, string ground, string cross, string? inner)
	{
		Fill(canvas, r, ground);
		Part(canvas, r, cross, 0.28f, 0, inner is null ? 0.16f : 0.24f, 1);
		Part(canvas, r, cross, 0, 0.42f, 1, inner is null ? 0.16f : 0.24f);
		if (inner is not null)
		{
			Part(canvas, r, inner, 0.325f, 0, 0.12f, 1);
			Part(canvas, r, inner, 0, 0.48f, 1, 0.12f);
		}
	}

	private static void UnionJack(ICanvas canvas, RectF r)
	{
		Fill(canvas, r, "#012169");
		canvas.StrokeLineCap = LineCap.Butt;
		canvas.StrokeColor = Colors.White;
		canvas.StrokeSize = r.Width * 0.2f;
		Diagonals(canvas, r);
		canvas.StrokeColor = Color.FromArgb("#C8102E");
		canvas.StrokeSize = r.Width * 0.07f;
		Diagonals(canvas, r);
		Part(canvas, r, "#FFFFFF", 0.38f, 0, 0.24f, 1);
		Part(canvas, r, "#FFFFFF", 0, 0.38f, 1, 0.24f);
		Part(canvas, r, "#C8102E", 0.44f, 0, 0.12f, 1);
		Part(canvas, r, "#C8102E", 0, 0.44f, 1, 0.12f);
	}

	private static void Diagonals(ICanvas canvas, RectF r)
	{
		canvas.DrawLine(r.Left, r.Top, r.Right, r.Bottom);
		canvas.DrawLine(r.Right, r.Top, r.Left, r.Bottom);
	}
}

/// <summary>
/// The kid's story-language button (Slice 173, mock frames 79 and 88): the current language's flag and a small chevron on a quiet fill, 46 to 48 dp high.
/// It shows only when there are two or three story languages. Screen readers say "Story language: English".
/// </summary>
public sealed class StoryLanguageButton : Border
{
	private const double FlagSize = 34;
	private const double ChevronSize = 12;
	private const double Corner = 16;
	private const float ChipAlpha = 0.92f;

	private readonly FlagView _flag = new() { WidthRequest = FlagSize, HeightRequest = FlagSize };
	private readonly IconView _chevron = new() { Kind = IconKind.ChevronDown, Size = ChevronSize, StrokeWidth = 1.8, VerticalOptions = LayoutOptions.Center };
	private string _language = UiLanguage.English;
	private Color _idle = Colors.Transparent;
	private Color _idleChevron = Colors.Gray;
	private bool _open;

	public StoryLanguageButton(double height, Func<Task> tapped)
	{
		HeightRequest = height;
		StrokeThickness = 0;
		Padding = new Thickness(6, 0, 10, 0);
		VerticalOptions = LayoutOptions.Center;
		StrokeShape = new RoundRectangle { CornerRadius = Corner };
		var ring = new Border
		{
			WidthRequest = FlagSize,
			HeightRequest = FlagSize,
			Stroke = Colors.White,
			StrokeThickness = 2,
			StrokeShape = new Ellipse(),
			Content = _flag,
			VerticalOptions = LayoutOptions.Center,
		};
		Content = new HorizontalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center, Children = { ring, _chevron } };
		CastViews.AddTap(this, tapped);
		Colour(DinoTokens.Get(DinoTokens.ControlFill), DinoTokens.Get(DinoTokens.Muted));
	}

	/// <summary>Shows <paramref name="language"/>'s flag and names it for screen readers.</summary>
	public void SetLanguage(string language)
	{
		_language = UiLanguage.Resolve(language);
		_flag.Country = StoryLanguages.FlagOf(_language);
		string label = AppResources.Format(AppResources.StoryLanguages_ButtonLabel, LanguageNames.NativeName(_language));
		SemanticProperties.SetDescription(this, label);
		ToolTipProperties.SetText(this, label);
	}

	/// <summary>The quiet fill and chevron colour; the brand colour shows while the list is open.</summary>
	public void Colour(Color fill, Color chevron)
	{
		_idle = fill;
		_idleChevron = chevron;
		SetOpen(_open);
	}

	/// <summary>Brand-coloured while the list is open (mock 88).</summary>
	public void SetOpen(bool open)
	{
		_open = open;
		BackgroundColor = open ? DinoTokens.Get(DinoTokens.Brand) : _idle;
		_chevron.Color = open ? Colors.White : _idleChevron;
	}

	/// <summary>On a picture (the sideways layouts) the button sits on the same white chip as the back button.</summary>
	public void SetOnPicture(bool onPicture, Color chip)
	{
		Shadow = onPicture ? new Shadow { Brush = Brush.Black, Opacity = 0.18f, Radius = 6, Offset = new Point(0, 2) } : null;
		if (onPicture)
		{
			_idle = chip.WithAlpha(ChipAlpha);
			SetOpen(_open);
		}
	}
}

/// <summary>
/// The list the language button opens (Slice 173, mock frame 88): one 56 px row per story language with its flag and its name in its own script, the current one
/// tinted. A tap outside, or back, closes it without a pick. It is an overlay on the host page's own root grid (not a modal), so its card shares the page's coordinates and sits right under the button on every platform.
/// </summary>
public sealed class StoryLanguageMenu : Grid
{
	private const int OverlayZ = 50;
	private const double CardWidth = 200;
	private const double RowHeight = 56;
	private const double RowFlag = 38;
	private const double CardCorner = 20;

	private readonly TaskCompletionSource<string?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private bool _closing;

	private StoryLanguageMenu(IReadOnlyList<string> languages, string current, Thickness anchor)
	{
		BackgroundColor = DinoTokens.Get(DinoTokens.Scrim);
		ZIndex = OverlayZ;
		FlowDirection = UiFlow.Screen;
		CastViews.AddTap(this, () => CloseAsync(null));
		Add(Card(languages, current, anchor));
	}

	private Border Card(IReadOnlyList<string> languages, string current, Thickness anchor)
	{
		var rows = new VerticalStackLayout { Spacing = 4 };
		foreach (string language in languages)
		{
			rows.Children.Add(Row(language, language == current));
		}

		var card = new Border
		{
			WidthRequest = CardWidth,
			BackgroundColor = Colors.White,
			StrokeThickness = 0,
			Padding = 8,
			Margin = anchor,
			HorizontalOptions = LayoutOptions.End,
			VerticalOptions = LayoutOptions.Start,
			StrokeShape = new RoundRectangle { CornerRadius = CardCorner },
			Shadow = new Shadow { Brush = Brush.Black, Opacity = 0.28f, Radius = 18, Offset = new Point(0, 8) },
			Content = rows,
		};
		CastViews.AddTap(card, () => Task.CompletedTask); // a tap on the card itself is not "outside"
		return card;
	}

	private View Row(string language, bool selected)
	{
		bool rtl = DinoFonts.IsRightToLeft(language);
		var flag = new Border
		{
			WidthRequest = RowFlag,
			HeightRequest = RowFlag,
			Stroke = Colors.White,
			StrokeThickness = 2,
			StrokeShape = new Ellipse(),
			VerticalOptions = LayoutOptions.Center,
			Content = new FlagView { Country = StoryLanguages.FlagOf(language), WidthRequest = RowFlag, HeightRequest = RowFlag },
		};
		var name = new Label
		{
			Text = LanguageNames.NativeName(language),
			FontSize = 16,
			FontAttributes = FontAttributes.Bold,
			FontFamily = rtl ? DinoFonts.Persian : DinoFonts.Ui,
			TextColor = DinoTokens.Get(DinoTokens.Ink),
			VerticalOptions = LayoutOptions.Center,
			FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
		};
		var row = new Border
		{
			HeightRequest = RowHeight,
			Padding = new Thickness(10, 0),
			StrokeThickness = 0,
			BackgroundColor = selected ? DinoTokens.Get(DinoTokens.SelectedRow) : Colors.Transparent,
			StrokeShape = new RoundRectangle { CornerRadius = 14 },
			Content = new HorizontalStackLayout { Spacing = 10, VerticalOptions = LayoutOptions.Center, Children = { flag, name } },
		};
		SemanticProperties.SetDescription(row, AppResources.Format(AppResources.StoryLanguages_ButtonLabel, LanguageNames.NativeName(language)));
		CastViews.AddTap(row, () => CloseAsync(language));
		return row;
	}

	private Task CloseAsync(string? language)
	{
		if (!_closing)
		{
			_closing = true;
			(Parent as Grid)?.Children.Remove(this);
			_result.TrySetResult(language);
		}

		return Task.CompletedTask;
	}

	/// <summary>Closes the list open over <paramref name="host"/> without a pick (hardware Back); false when none is open.</summary>
	public static bool DismissOpen(Grid host)
	{
		if (host.Children.OfType<StoryLanguageMenu>().FirstOrDefault() is not { } menu)
		{
			return false;
		}

		_ = menu.CloseAsync(null);
		return true;
	}

	/// <summary>The card's margin that puts its top right corner just under <paramref name="button"/>, <paramref name="gap"/> below it, in <paramref name="host"/>'s coordinates.</summary>
	public static Thickness BelowButton(Grid host, VisualElement button, double gap)
	{
		double x = 0;
		double y = 0;
		for (Element? element = button; element is VisualElement view && !ReferenceEquals(view, host); element = element.Parent)
		{
			x += view.X;
			y += view.Y;
		}

		double right = Math.Max(0, host.Width - (x + button.Width));
		return new Thickness(0, y + button.Height + gap, right, 0);
	}

	/// <summary>
	/// Shows the list over <paramref name="host"/> (the page's root grid) under <paramref name="button"/> and returns the language the child tapped, or null when it was
	/// closed without a pick.
	/// </summary>
	public static Task<string?> PickAsync(Grid host, VisualElement button, IReadOnlyList<string> languages, string current, double gap)
	{
		var menu = new StoryLanguageMenu(languages, current, BelowButton(host, button, gap));
		SetRowSpan(menu, Math.Max(1, host.RowDefinitions.Count));
		SetColumnSpan(menu, Math.Max(1, host.ColumnDefinitions.Count));
		host.Children.Add(menu);
		return menu._result.Task;
	}
}
