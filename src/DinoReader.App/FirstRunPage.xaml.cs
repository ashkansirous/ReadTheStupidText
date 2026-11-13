using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Mobile.Shared;

namespace DinoReader;

/// <summary>What a new install still owes the grown-up (Slice 93, Decision 98).</summary>
public static class FirstRunState
{
	private const string HandoverPendingKey = "dino.handover.pending";

	/// <summary>A fresh install has no language yet: Dino no longer picks English by itself. Every install made
	/// before this slice stored one at launch, so none of them sees the first run.</summary>
	public static bool IsNeeded(ISettingsStore settings) => settings.Language is null;

	/// <summary>True from "See what Dino can do" until the grown-up first hands the phone to the child ("Ready for my child").</summary>
	public static bool HandoverPending
	{
		get => Preferences.Default.Get(HandoverPendingKey, false);
		set => Preferences.Default.Set(HandoverPendingKey, value);
	}
}

/// <summary>The coral call-to-action of the first-run screens (onboarding mock): 56 high by default (50 or 52 beside the panes, see <see cref="SetSize"/>), fully rounded, a 5 px darker edge underneath.</summary>
public sealed class CoralButton : Border
{
	private const double DefaultFaceHeight = 56;
	private const double EdgeDepth = 5;
	private const string DisabledFill = "#e6dfd0";
	private readonly Border _face;
	private readonly Label _label = new()
	{
		FontSize = 17,
		FontAttributes = FontAttributes.Bold,
		TextColor = Colors.White,
		HorizontalOptions = LayoutOptions.Center,
		VerticalOptions = LayoutOptions.Center,
	};

	private readonly HorizontalStackLayout _content = new() { Spacing = 10, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
	private bool _active = true;

	public CoralButton()
	{
		StrokeThickness = 0;
		Padding = new Thickness(0, 0, 0, EdgeDepth);
		_face = new Border
		{
			StrokeThickness = 0,
			Content = _content,
		};
		_content.Children.Add(_label);
		Content = _face;
		SetSize(double.NaN, DefaultFaceHeight);
		Look();
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => Clicked?.Invoke(this, EventArgs.Empty); // no-tap-count: the page that owns this button counts it with UiUsage.Press
		GestureRecognizers.Add(tap);
		SemanticProperties.SetHint(this, string.Empty);
	}

	public event EventHandler? Clicked;

	public string Text
	{
		get => _label.Text;
		set
		{
			_label.Text = value;
			SemanticProperties.SetDescription(this, value);
		}
	}

	/// <summary>Puts an icon before the text (the open book of "Ready for my child").</summary>
	public IconKind Icon
	{
		set
		{
			_content.Children.Clear();
			_content.Children.Add(new IconView { Kind = value, Color = Colors.White, Size = 22, StrokeWidth = 2.2, VerticalOptions = LayoutOptions.Center });
			_content.Children.Add(_label);
		}
	}

	/// <summary>False draws the button as the picker's dead Continue (pale fill, no edge, muted text); it still receives taps, the page decides what they do.</summary>
	public bool Active
	{
		get => _active;
		set
		{
			_active = value;
			Look();
		}
	}

	/// <summary>The face's height and the button's width (NaN fills the column): 56 high stacked, 50 (52 on Windows) beside the panes.</summary>
	public void SetSize(double width, double height)
	{
		StrokeShape = new RoundRectangle { CornerRadius = (height + EdgeDepth) / 2 };
		_face.StrokeShape = new RoundRectangle { CornerRadius = height / 2 };
		_face.HeightRequest = height;
		WidthRequest = double.IsNaN(width) ? -1 : width;
	}

	private void Look()
	{
		BackgroundColor = _active ? DinoTokens.Get(DinoTokens.CoralEdge) : Colors.Transparent;
		_face.BackgroundColor = _active ? DinoTokens.Get(DinoTokens.Coral) : Color.FromArgb(DisabledFill);
		_label.TextColor = _active ? Colors.White : DinoTokens.Get(DinoTokens.Muted);
	}
}

/// <summary>
/// The first run (Slice 93, Decision 98; onboarding mock 2026-11-13, Slice 362): step 1 of 6, the grown-up picks the language. Then come the story languages (<see cref="OnboardingLanguagesPage"/>), at least two of the
/// store's books (<see cref="FirstBooksPage"/>), the listen and voice steps and the grown-ups' corner tour (<see cref="OnboardingRoute"/>). Slice 278 part B: there is no voice to download, so the books' own audio packs
/// (fetched with the books) are all that is fetched. The language is stored at the pick; a kill before the books are chosen reopens on the book picker (<see cref="FirstBooksGate"/>), a kill after resumes at the saved
/// step (<see cref="OnboardingState"/>). The page sits on <see cref="OnboardingShell"/>.
/// </summary>
public partial class FirstRunPage : ContentPage
{
	private const string LanguageIdPrefix = "FirstRunLanguage-"; // every language button has its own fixed id, set once where it is made; the pick shows by colour and tick only
	private const double WelcomeGap = 20;
	private const double FlagRingThickness = 1.5;
	private const double ButtonRadius = 16;
	private const double ButtonGap = 10;
	private const double TickSize = 18;
	private static readonly Color FlagRingColour = Color.FromRgba(43 / 255.0, 37 / 255.0, 64 / 255.0, 0.12);

	private readonly ISettingsStore _settings;
	private readonly UsageCounter _usage;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly Dictionary<string, Border> _buttons = [];
	private readonly Grid _languageGrid = new() { ColumnSpacing = 8, RowSpacing = 8 };
	private readonly Grid _welcome = new() { RowSpacing = 12, ColumnSpacing = 14 };
	private readonly Border _welcomeIcon = new() { StrokeThickness = 0, HorizontalOptions = LayoutOptions.Center, Content = new Image { Source = "dino_icon", Aspect = Aspect.AspectFill } };
	private readonly Label _question = new() { Text = AppResources.FirstRun_Hello, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
	private readonly Label _hello = new() { Text = AppResources.FirstRun_HelloBody, TextColor = DinoTokens.Get(DinoTokens.Body), LineHeight = 1.4 };
	private readonly CoralButton _continue = new() { Text = AppResources.FirstRun_Continue };
	private OnboardingShell _languageShell = null!;
	private string _selected;
	private string _chosen = UiLanguage.English;
	private Size _windowSize;
	private OnboardingShape? _shape;

	public FirstRunPage(ISettingsStore settings, UsageCounter usage, IServiceProvider services)
	{
		InitializeComponent();
		_selected = LanguageChoice.Current(settings);
		_settings = settings;
		_usage = usage;
		_services = services;
		BuildLanguagePanel();
	}

	private void BuildLanguagePanel()
	{
		SemanticProperties.SetHeadingLevel(_question, SemanticHeadingLevel.Level1);
		SemanticProperties.SetDescription(_welcomeIcon, AppResources.App_Name);
		_welcome.Children.Add(_welcomeIcon);
		_welcome.Children.Add(_question);
		_welcome.Children.Add(_hello);
		foreach ((string code, string name) in LanguageNames.All)
		{
			Border button = BuildLanguageButton(code, name);
			_buttons[code] = button;
			_languageGrid.Children.Add(button);
		}

		UiUsage.Press(_continue, UiControls.Continue, () => OnContinueClicked(_continue, EventArgs.Empty));
		var scroll = new ScrollView { Content = _languageGrid };
		_languageShell = new OnboardingShell(_welcome, scroll, _continue, stepBar: new OnboardingStepBar(OnboardingStep.Language));
		LanguagePanel.Children.Add(_languageShell);
		ShowSelection();
	}

	// Onboarding mock 2026-11-13 frame N1 (Slice 362): one skeleton for every size, the step bar on top. Rotating only re-places views, so the pick and the page shown stay.
	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		(width, height) = WindowShapeSeam.Effective(DesktopTips.IsWindows, width, height);
		if (width <= 0 || height <= 0 || (width == _windowSize.Width && height == _windowSize.Height))
		{
			return;
		}

		_windowSize = new Size(width, height);
		OnboardingShape shape = OnboardingLayout.Shape(width, height, DesktopTips.IsWindows, intro: false);
		if (shape != _shape)
		{
			_shape = shape;
			PlacePanels(shape);
		}
	}

	private void PlacePanels(OnboardingShape shape)
	{
		_languageShell.Place(shape);
		PlaceWelcome(shape);
		PlaceLanguageButtons(shape);
		(double width, double height) = OnboardingLayout.ContinueSize(shape);
		_continue.SetSize(width, height);
		_continue.HorizontalOptions = shape.Split ? LayoutOptions.Start : LayoutOptions.Fill;
		LogLayout();
	}

	// Icon, question and the line: stacked and centred; beside each other under 700 dp tall (frame O1-8); left-aligned in a pane, sized per family (frames OL1, OTL1).
	private void PlaceWelcome(OnboardingShape shape)
	{
		bool beside = !shape.Split && shape.Compact;
		(double icon, double radius, double question, double line) = WelcomeSizes(shape, beside);
		_welcomeIcon.WidthRequest = icon;
		_welcomeIcon.HeightRequest = icon;
		_welcomeIcon.StrokeShape = new RoundRectangle { CornerRadius = radius };
		_question.FontSize = question;
		_hello.FontSize = line;
		_hello.MaximumWidthRequest = shape.Split ? double.PositiveInfinity : 300;
		TextAlignment align = shape.Split || beside ? TextAlignment.Start : TextAlignment.Center;
		_question.HorizontalTextAlignment = align;
		_hello.HorizontalTextAlignment = align;
		_hello.HorizontalOptions = shape.Split || beside ? LayoutOptions.Fill : LayoutOptions.Center;
		_welcome.RowSpacing = shape.Split ? 12 : WelcomeGap;
		_welcome.ColumnDefinitions = beside ? [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)] : [];
		_welcome.RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)];
		_welcomeIcon.HorizontalOptions = shape.Split ? LayoutOptions.Start : LayoutOptions.Center;
		SetCell(_welcomeIcon, 0, 0, 1);
		SetCell(_question, beside ? 0 : 1, beside ? 1 : 0, 1);
		SetCell(_hello, beside ? 1 : 2, 0, beside ? 2 : 1);
	}

	private static (double Icon, double Radius, double Question, double Line) WelcomeSizes(OnboardingShape shape, bool beside)
	{
		if (!shape.Split)
		{
			return beside ? (56, 16, 22, 14) : (88, 24, 24, 14);
		}

		return shape.Family switch
		{
			OnboardingFamily.PhoneLandscape => (60, 17, 21, 13),
			OnboardingFamily.Windows => (88, 24, 30, 15.5),
			_ => (120, 32, 36, 17),
		};
	}

	private static void SetCell(View view, int row, int column, int columnSpan)
	{
		Grid.SetRow(view, row);
		Grid.SetColumn(view, column);
		Grid.SetColumnSpan(view, columnSpan);
	}

	// The same buttons are re-placed in 2 or 3 columns and re-drawn with the family's flag and height (the pick, kept in _selected, is never lost).
	private void PlaceLanguageButtons(OnboardingShape shape)
	{
		int columns = OnboardingLayout.LanguageColumns(shape);
		_languageGrid.ColumnDefinitions = [.. Enumerable.Range(0, columns).Select(_ => new ColumnDefinition(GridLength.Star))];
		_languageGrid.RowDefinitions = [.. Enumerable.Range(0, OnboardingLayout.RowsFor(_buttons.Count, columns)).Select(_ => new RowDefinition(GridLength.Auto))];
		_languageGrid.ColumnSpacing = shape.Split && shape.Family != OnboardingFamily.PhoneLandscape ? 12 : 8;
		int index = 0;
		foreach ((string code, string name) in LanguageNames.All)
		{
			GridCell cell = OnboardingLayout.CellOf(index++, columns);
			Grid.SetRow(_buttons[code], cell.Row);
			Grid.SetColumn(_buttons[code], cell.Column);
			_buttons[code].HeightRequest = OnboardingLayout.LanguageButtonHeight(shape);
			_buttons[code].Content = ButtonContent(code, name, OnboardingLayout.FlagSize(shape));
		}

		ShowSelection();
	}

	// Debug-only marker the landscape flow reads: the layout, the page shown and the picked language (selected=<code>); [Redesign] names the page and shape for the onboarding flow.
	private void LogLayout()
	{
#if DEBUG
		if (_shape is { } shape)
		{
			string kind = shape.Split ? "split" : "stack";
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] firstrun split={shape.Split} columns={OnboardingLayout.LanguageColumns(shape)} page=language selected={_selected}");
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Redesign] onboarding page=language shape={kind}");
		}
#endif
	}

	private Border BuildLanguageButton(string code, string name)
	{
		var button = new Border { AutomationId = LanguageIdPrefix + code, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = ButtonRadius }, Padding = new Thickness(12, 0) };
		button.Content = ButtonContent(code, name, 28);
		SemanticProperties.SetDescription(button, name);
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) =>
		{
			UiUsage.Tap(UiControls.Language); // Slice 352: a language card (which one is not counted here)
			_selected = code;
			ShowSelection();
			LogLayout();
		};
		button.GestureRecognizers.Add(tap);
		return button;
	}

	// Flag in its ring, the language's own name, and a tick at the end that shows on the picked one (frame O1).
	private static Grid ButtonContent(string code, string name, double flagSize)
	{
		bool rtl = DinoFonts.IsRightToLeft(code);
		var label = new Label
		{
			Text = name,
			FontSize = 16,
			FontAttributes = FontAttributes.Bold,
			FontFamily = rtl ? DinoFonts.Persian : DinoFonts.Ui,
			FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
			VerticalOptions = LayoutOptions.Center,
			LineBreakMode = LineBreakMode.TailTruncation,
		};
		var tick = new IconView { Kind = IconKind.Check, Color = Colors.White, Size = TickSize, StrokeWidth = 3, VerticalOptions = LayoutOptions.Center, IsVisible = false };
		var row = new Grid { ColumnSpacing = ButtonGap, FlowDirection = FlowDirection.LeftToRight, VerticalOptions = LayoutOptions.Center, ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)] };
		row.Add(new FlagRing(code, flagSize, FlagRingThickness, FlagRingColour), 0, 0);
		row.Add(label, 1, 0);
		row.Add(tick, 2, 0);
		return row;
	}

	// The picked language is filled brand-blue with white text, a tick and a 3 px edge; the others are plain white.
	private void ShowSelection()
	{
		foreach ((string code, Border button) in _buttons)
		{
			bool selected = code == _selected;
			button.BackgroundColor = selected ? DinoTokens.Get(DinoTokens.Brand) : Colors.White;
			button.Shadow = selected ? new Shadow { Brush = DinoTokens.Get(DinoTokens.BrandEdge), Offset = new Point(0, 3), Radius = 0, Opacity = 1 } : null!;
			var row = (Grid)button.Content!;
			((Label)row.Children[1]).TextColor = selected ? Colors.White : DinoTokens.Get(DinoTokens.Ink);
			((IconView)row.Children[2]).IsVisible = selected;
		}
	}

	private async void OnContinueClicked(object? sender, EventArgs e) => await _busy.RunAsync(ContinueAsync);

	private async Task ContinueAsync()
	{
		_chosen = _selected;
		FirstBooksGate.StoryLanguagesPending = true; // Slice 362: the story languages step comes next; a kill on it reopens it, and its Continue clears this before the books
		FirstBooksGate.Pending = true; // Slice 261: language, then the story languages and the books (at least two); set before the language is stored so a kill in between reopens on the picker
		LanguageChoice.Apply(_chosen, _settings); // the grown-up's own choice
		if (!_startCounted)
		{
			_startCounted = true; // Back from step 2 or 3 returns here: the first run starts, and the language step is done, once
			OnboardingUsage.Started(_usage); // Slice 362
			OnboardingUsage.StepDone(_usage, OnboardingStep.Language);
		}

		await ShowStoryLanguagesAsync();
	}

	private bool _startCounted;

	// Slice 362 (mock N2, WN2): step 2 is pushed over the language page. Back returns to the language (the first choice is let go); the step's own Continue goes on to the book picker.
	// The step takes its words from the language just applied.
	private async Task ShowStoryLanguagesAsync()
	{
		var page = ActivatorUtilities.CreateInstance<OnboardingLanguagesPage>(_services);
		page.BackRequested = async () =>
		{
			_settings.Language = null;
			await Navigation.PopAsync();
		};
		await Navigation.PushAsync(page);
	}
}
