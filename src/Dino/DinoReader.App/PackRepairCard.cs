using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace DinoReader;

/// <summary>
/// The card under the reading page's words card when a book's sound cannot be got (Slice 360, Design SS2-SS4 of the Missing States mock): a sand card with a cloud-off glyph for "no internet", a warm
/// card with an alert glyph for "couldn't get it", and, when another family language's sound for the book is on the phone, the white "Read in {language}" button. Never a pop-up; the words keep showing
/// and highlighting above it. The layout never mirrors (Slice 96): only the label's own text runs right to left. SS1 (the download's percent) is <see cref="SoundStatusPill"/>'s.
/// </summary>
public sealed class PackRepairCard : Border
{
	/// <summary>The id the Maestro flows read the card's line by.</summary>
	public const string TextAutomationId = "ReadingSoundNote";

	/// <summary>The id of the "Read in {0}" button.</summary>
	public const string ReadInAutomationId = "ReadingSoundReadIn";

	private const double CornerRadius = 18;
	private const double GlyphSize = 20;
	private const double GlyphGrid = 24;
	private const double GlyphStroke = 2.2;
	private const double TextSize = 14;
	private const double LineHeight = 1.35;
	private const double ButtonHeight = 40;
	private const double ButtonRadius = 20;
	private const double ButtonTextSize = 14.5;
	private const double FlagSize = 28;
	private const double FlagRingWidth = 1.5;
	private const double Gap = 8;
	private const double ButtonGap = 7;
	private const string CloudOffPath = "M3 3l18 18 M8.5 6.3A6 6 0 0 1 17.6 10H18a4 4 0 0 1 2.3 7.3 M16.5 19H7a5 5 0 0 1-1.6-9.7";
	private const string AlertPath = "M3 12a9 9 0 1 0 18 0a9 9 0 1 0-18 0 M12 7.5v5.5 M12 16.5v.01";
	private static readonly Color SandInk = Color.FromArgb("#3f3a52"); // the mock's values for this card (no token carries them)
	private static readonly Color WarmFill = Color.FromArgb("#FFF0EA");
	private static readonly Color WarmInk = Color.FromArgb("#A4462A");
	private static readonly Color FlagRingColour = Color.FromRgba(43, 37, 64, 0.12);
	private static readonly PathGeometryConverter Converter = new();

	private readonly Path _glyph;
	private readonly Label _label;
	private readonly Border _button;
	private readonly Label _buttonLabel;
	private readonly FlagRing _flag;
	private readonly Func<string, Task> _readIn;
	private string? _switchTo;

	/// <param name="readIn">Called with the language code when the child taps "Read in {language}"; the page switches this reading to it.</param>
	public PackRepairCard(Func<string, Task> readIn, string flagLanguage)
	{
		_readIn = readIn;
		FlowDirection = FlowDirection.LeftToRight; // the mock's layout: glyph on the left, button centred; only the text takes its own direction
		StrokeThickness = 0;
		StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(CornerRadius) };
		Padding = new Thickness(14, 10);
		HorizontalOptions = LayoutOptions.Center;
		_glyph = new Path { StrokeThickness = GlyphStroke, StrokeLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round, WidthRequest = GlyphGrid, HeightRequest = GlyphGrid, Aspect = Stretch.None, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Scale = GlyphSize / GlyphGrid, AnchorX = 0, AnchorY = 0 };
		var glyphSlot = new Grid { WidthRequest = GlyphSize, HeightRequest = GlyphSize, VerticalOptions = LayoutOptions.Start, Children = { _glyph } };
		AutomationProperties.SetIsInAccessibleTree(glyphSlot, false); // the label carries the words
		_label = new Label { AutomationId = TextAutomationId, FontSize = TextSize, LineHeight = LineHeight, VerticalOptions = LayoutOptions.Center };
		(_button, _buttonLabel, _flag) = BuildButton(flagLanguage);
		Content = new VerticalStackLayout { Spacing = Gap, Children = { BuildLine(glyphSlot, _label), _button } };
		Taps.AddTap(_button, () => _switchTo is { } code ? _readIn(code) : Task.CompletedTask, UiControls.Language); // the same control as the language button: it switches the reading language
	}

	private static Grid BuildLine(View glyph, Label label)
	{
		var line = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)], ColumnSpacing = Gap, HorizontalOptions = LayoutOptions.Center };
		line.Add(glyph, 0);
		line.Add(label, 1);
		return line;
	}

	private static (Border Button, Label Label, FlagRing Flag) BuildButton(string flagLanguage)
	{
		var flag = new FlagRing(flagLanguage, FlagSize, FlagRingWidth, FlagRingColour) { HorizontalOptions = LayoutOptions.Start };
		var label = new Label { FontSize = ButtonTextSize, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center, TextColor = DinoTokens.Get(DinoTokens.Ink), FontFamily = DinoFonts.Ui };
		var row = new HorizontalStackLayout { Spacing = ButtonGap, Padding = new Thickness(6, 0, 14, 0), VerticalOptions = LayoutOptions.Center, Children = { flag, label } };
		var button = new Border
		{
			AutomationId = ReadInAutomationId,
			HeightRequest = ButtonHeight,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(ButtonRadius) },
			BackgroundColor = Colors.White,
			HorizontalOptions = LayoutOptions.Center,
			Content = row,
			Shadow = new Shadow { Brush = new SolidColorBrush(DinoTokens.Get(DinoTokens.Ink)), Offset = new Point(0, 2), Radius = 0, Opacity = 0.08f },
		};
		return (button, label, flag);
	}

	/// <summary>
	/// The line the card shows for <paramref name="view"/> (a <see cref="PackSoundKind.NoInternet"/> or <see cref="PackSoundKind.Failed"/> one), composed from the app's strings;
	/// <paramref name="nativeName"/> is the reading language's name in its own script.
	/// </summary>
	public static string TextFor(PackSoundView view, string nativeName) => view.Kind switch
	{
		PackSoundKind.NoInternet when view.HasSwitch => AppResources.Format(AppResources.Reading_NoInternetSound, nativeName),
		PackSoundKind.NoInternet => AppResources.Reading_NoInternetWordsShow,
		_ => AppResources.Format(AppResources.Reading_SoundFailed, nativeName),
	};

	/// <summary>Shows <paramref name="view"/>: the sand card for no internet, the warm card for a failure, with the button only when it has another language to switch to.</summary>
	public void Show(PackSoundView view, string nativeName, string? switchToName)
	{
		bool warm = view.Kind == PackSoundKind.Failed;
		Color ink = warm ? WarmInk : SandInk;
		BackgroundColor = warm ? WarmFill : DinoTokens.Get(DinoTokens.SoundPillSand);
		_glyph.Stroke = new SolidColorBrush(ink);
		_glyph.Data = (Geometry)Converter.ConvertFromInvariantString(warm ? AlertPath : CloudOffPath)!;
		string text = TextFor(view, nativeName);
		ShowText(text, nativeName, ink);
		_switchTo = view.SwitchTo;
		_button.IsVisible = view.HasSwitch;
		if (view.SwitchTo is { } code)
		{
			_flag.SetLanguage(code);
			_buttonLabel.Text = AppResources.Format(AppResources.Reading_ReadIn, switchToName ?? code);
			_buttonLabel.FontFamily = DinoFonts.UiMediumForText(_buttonLabel.Text) == DinoFonts.Persian ? DinoFonts.Persian : DinoFonts.Ui;
			SemanticProperties.SetDescription(_button, _buttonLabel.Text);
		}
	}

	private void ShowText(string text, string nativeName, Color ink)
	{
		string font = DinoFonts.UiMediumForText(text);
		_label.TextColor = ink;
		_label.FontFamily = font;
		_label.FlowDirection = UiFlow.ForText(text); // the card stays left to right; the words follow their own language when they wrap
		var formatted = new FormattedString();
		foreach (SoundPillRun run in SoundPillText.Split(text, nativeName))
		{
			formatted.Spans.Add(new Span { Text = run.Text, FontFamily = SoundPillText.FontFor(run, font, DinoFonts.Persian) });
		}

		_label.FormattedText = formatted;
		SemanticProperties.SetDescription(_label, text); // a FormattedText label can have an empty UIA name on Windows (Slice 205: TextPages.cs)
	}
}
