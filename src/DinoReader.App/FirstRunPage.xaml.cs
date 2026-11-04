using System.Diagnostics;
using System.Globalization;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Mobile.Shared;

namespace DinoReader;

/// <summary>What a new install still owes the grown-up (Slice 93, Decision 98).</summary>
public static class FirstRunState
{
	private const string HandoverPendingKey = "dino.handover.pending";

	/// <summary>A fresh install has no language yet: Dino no longer picks English by itself (<see cref="SpeechStartup"/>). Every install made
	/// before this slice stored one at launch, so none of them sees the first run.</summary>
	public static bool IsNeeded(ISettingsStore settings) => settings.Language is null;

	/// <summary>True from "See what Dino can do" (or "Skip, start reading") until the grown-up first hands the phone to the child ("Ready for my child").</summary>
	public static bool HandoverPending
	{
		get => Preferences.Default.Get(HandoverPendingKey, false);
		set => Preferences.Default.Set(HandoverPendingKey, value);
	}
}

/// <summary>The coral call-to-action of the first-run screens (mock 16-19): 56 px, fully rounded, a 5 px darker edge underneath.</summary>
public sealed class CoralButton : Border
{
	private const double FaceHeight = 56;
	private const double EdgeDepth = 5;
	private readonly Label _label = new()
	{
		FontSize = 17,
		FontAttributes = FontAttributes.Bold,
		TextColor = Colors.White,
		HorizontalOptions = LayoutOptions.Center,
		VerticalOptions = LayoutOptions.Center,
	};

	private readonly HorizontalStackLayout _content = new() { Spacing = 10, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

	public CoralButton()
	{
		StrokeThickness = 0;
		StrokeShape = new RoundRectangle { CornerRadius = (FaceHeight + EdgeDepth) / 2 };
		BackgroundColor = DinoTokens.Get(DinoTokens.CoralEdge);
		Padding = new Thickness(0, 0, 0, EdgeDepth);
		var face = new Border
		{
			HeightRequest = FaceHeight,
			StrokeThickness = 0,
			StrokeShape = new RoundRectangle { CornerRadius = FaceHeight / 2 },
			BackgroundColor = DinoTokens.Get(DinoTokens.Coral),
			Content = _content,
		};
		_content.Children.Add(_label);
		Content = face;
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
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
}

/// <summary>
/// The first run (Slice 93, Decision 98, mock screens 16-18): the grown-up picks the language, then (Slice 261) at least two of the store's books
/// (<see cref="FirstBooksPage"/>, the voice pack downloads meanwhile), then the voice download shows its real progress, then "See what Dino can do"
/// or "Skip" hands over to grown-up mode. The language is stored at the pick; a kill before the books are chosen reopens on the book picker
/// (<see cref="FirstBooksGate"/>), a kill after goes straight to the gallery while the download carries on. No tutorial.
/// </summary>
public partial class FirstRunPage : ContentPage
{
	private const int PercentScale = 100;
	private const double UnsetWidth = -1; // VisualElement.WidthRequest's "no request"
	private const double CardPadding = 40;
	private const double CardCorner = 32;
	private const double CardStackTop = 0;
	private const double CardLanguageButtonHeight = 56;
	private const double PhoneLanguageButtonHeight = 52;

	private readonly SpokenLanguage _spoken;
	private readonly VoicePackInstaller _installer;
	private readonly IVoicePackService _packs;
	private readonly ISpeechReader _reader;
	private readonly GrownUpMode _mode;
	private readonly UsageCounter _usage;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly Dictionary<string, Border> _buttons = [];
	private string _selected;
	// Every language button has its own fixed id, set once where it is made; the pick shows by colour and check only.
	private const string LanguageIdPrefix = "FirstRunLanguage-";

	private string _chosen = UiLanguage.English;
	private Size _windowSize;
	private FirstRunShape? _shape;

	public FirstRunPage(ISettingsStore settings, SpokenLanguage spoken, VoicePackInstaller installer, IVoicePackService packs, ISpeechReader reader,
		GrownUpMode mode, UsageCounter usage, IServiceProvider services)
	{
		InitializeComponent();
		_selected = LanguageChoice.Current(settings);
		_spoken = spoken;
		_installer = installer;
		_packs = packs;
		_reader = reader;
		_mode = mode;
		_usage = usage;
		_services = services;
		BuildLanguageButtons();
	}

	private void BuildLanguageButtons()
	{
		foreach ((string code, string name) in LanguageNames.All)
		{
			Border button = BuildLanguageButton(code, name);
			_buttons[code] = button;
			LanguageGrid.Children.Add(button);
		}

		PlaceLanguageButtons(OnboardingLayout.UprightLanguageColumns);
		ShowSelection();
	}

	// The same buttons are re-placed in 2 or 3 columns (the pick, kept in _selected and the buttons' colours, is never rebuilt).
	private void PlaceLanguageButtons(int columns)
	{
		LanguageGrid.ColumnDefinitions = [.. Enumerable.Range(0, columns).Select(_ => new ColumnDefinition(GridLength.Star))];
		LanguageGrid.RowDefinitions = [.. Enumerable.Range(0, OnboardingLayout.RowsFor(_buttons.Count, columns)).Select(_ => new RowDefinition(GridLength.Auto))];
		int index = 0;
		foreach ((string code, _) in LanguageNames.All)
		{
			GridCell cell = OnboardingLayout.CellOf(index++, columns);
			Grid.SetRow(_buttons[code], cell.Row);
			Grid.SetColumn(_buttons[code], cell.Column);
		}
	}

	// Slice 232 part E (mock Batch 44 frames 123, 130): held sideways the language page splits (welcome left, a 3-column grid right with Continue under it) and the
	// download and ready pages lose their tall top padding and keep clear of the camera cut-out; rotating only re-places views, so the pick and the page shown stay.
	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		if (width <= 0 || height <= 0 || (width == _windowSize.Width && height == _windowSize.Height))
		{
			return;
		}

		_windowSize = new Size(width, height);
		FirstRunShape shape = OnboardingLayout.FirstRun(width, height);
		if (shape != _shape)
		{
			_shape = shape;
			PlacePanels(shape);
		}
	}

	private void PlacePanels(FirstRunShape shape)
	{
		Thickness padding = shape.Card ? new Thickness(CardPadding) : new Thickness(22, 0, 22, 22);
		LanguagePanel.Padding = padding;
		DownloadPanel.Padding = padding;
		ReadyPanel.Padding = padding;
		double topPadding = shape.Card ? CardStackTop : shape.Split ? 12 : 28;
		LanguageStack.Padding = new Thickness(0, topPadding, 0, 16);
		DownloadStack.Padding = new Thickness(0, shape.Card ? CardStackTop : shape.Split ? 12 : 56, 0, 0);
		ReadyStack.Padding = new Thickness(0, shape.Card ? CardStackTop : shape.Split ? 12 : 56, 0, 0);
		PlaceCardFrame(shape);
		PlaceLanguagePanel(shape);
		PlaceReadyPanel(shape);
		PlaceLanguageButtons(shape.LanguageColumns);
		LogLayout();
	}

	// A tablet held upright (Slice 234 part B, mock Batch 45 frame 138): the three pages share one centred white card, 620 dp wide at most, with taller language buttons.
	// Elsewhere the frame is transparent, fills the page and the buttons are the phone's 52 dp. Only sizes and options change; no page is rebuilt.
	private void PlaceCardFrame(FirstRunShape shape)
	{
		CardFrame.HorizontalOptions = shape.Card ? LayoutOptions.Center : LayoutOptions.Fill;
		CardFrame.VerticalOptions = shape.Card ? LayoutOptions.Center : LayoutOptions.Fill;
		CardFrame.WidthRequest = shape.Card ? shape.CardWidth : UnsetWidth;
		CardFrame.MaximumHeightRequest = shape.Card ? shape.CardMaxHeight : double.PositiveInfinity;
		CardFrame.BackgroundColor = shape.Card ? Colors.White : Colors.Transparent;
		CardFrame.StrokeShape = new RoundRectangle { CornerRadius = shape.Card ? CardCorner : 0 };
		foreach (Border button in _buttons.Values)
		{
			button.HeightRequest = LanguageButtonHeight(shape);
		}

		SizeCardToContent(shape);
	}

	private static double LanguageButtonHeight(FirstRunShape shape) => shape.Card ? CardLanguageButtonHeight : PhoneLanguageButtonHeight;

	// A star row would fill the whole card cap and leave a tall empty card around a short page. On the card the page's top row is Auto instead, so the card is as tall as its
	// content, and the page's scroll gets the cap less the card's padding and the action under it (it scrolls only when its content is taller than that).
	private void SizeCardToContent(FirstRunShape shape)
	{
		foreach ((Grid panel, ScrollView scroll, View below) in CardPages())
		{
			panel.RowDefinitions[0].Height = shape.Card ? GridLength.Auto : GridLength.Star;
			scroll.MaximumHeightRequest = shape.Card ? TabletCardLayout.ScrollAreaHeight(shape.CardMaxHeight, 2 * CardPadding, below.Height) : double.PositiveInfinity;
			below.SizeChanged -= OnCardActionSized;
			if (shape.Card)
			{
				below.SizeChanged += OnCardActionSized;
			}
		}
	}

	private (Grid Panel, ScrollView Scroll, View Below)[] CardPages() =>
		[(LanguagePanel, WelcomeScroll, ContinueButton), (DownloadPanel, DownloadScroll, PickAnother), (ReadyPanel, ReadyScroll, ReadyActions)];

	private void OnCardActionSized(object? sender, EventArgs e)
	{
		if (_shape is { Card: true } shape)
		{
			SizeCardToContent(shape);
		}
	}

	// Debug-only marker the landscape flow reads: the layout, the page shown and the picked language (selected=<code>).
	private void LogLayout()
	{
#if DEBUG
		if (_shape is { } shape)
		{
			ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] firstrun split={shape.Split} columns={shape.LanguageColumns} page={(LanguagePanel.IsVisible ? "language" : DownloadPanel.IsVisible ? "download" : "ready")} selected={_selected} card={shape.Card} cardWidth={shape.CardWidth}");
		}
#endif
	}

	private void PlaceLanguagePanel(FirstRunShape shape)
	{
		LanguagePanel.ColumnDefinitions = shape.Split ? [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)] : [];
		LanguagePanel.ColumnSpacing = 18;
		Grid.SetRowSpan(WelcomeScroll, shape.Split ? 2 : 1);
		Grid.SetColumn(ContinueButton, shape.Split ? 1 : 0);
		GridScroll.IsVisible = shape.Split;
		GridScroll.Content = null;
		LanguageStack.Children.Remove(LanguageGrid);
		if (shape.Split)
		{
			GridScroll.Content = LanguageGrid;
		}
		else
		{
			LanguageStack.Children.Add(LanguageGrid);
		}
	}

	private void PlaceReadyPanel(FirstRunShape shape)
	{
		ReadyPanel.ColumnDefinitions = shape.Split ? [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)] : [];
		ReadyPanel.ColumnSpacing = 18;
		Grid.SetColumn(ReadyActions, shape.Split ? 1 : 0);
		Grid.SetRow(ReadyActions, shape.Split ? 0 : 1);
		ReadyActions.VerticalOptions = shape.Split ? LayoutOptions.Center : LayoutOptions.End;
	}

	private Border BuildLanguageButton(string code, string name)
	{
		bool rtl = DinoFonts.IsRightToLeft(code);
		var button = new Border { AutomationId = LanguageIdPrefix + code, HeightRequest = _shape is { } shape ? LanguageButtonHeight(shape) : PhoneLanguageButtonHeight, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 16 }, Padding = new Thickness(14, 0) };
		var row = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
		row.Children.Add(new Label
		{
			Text = name,
			FontSize = 16,
			FontAttributes = FontAttributes.Bold,
			FontFamily = rtl ? DinoFonts.Persian : DinoFonts.Ui,
			FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
			VerticalOptions = LayoutOptions.Center,
		});
		button.Content = row;
		SemanticProperties.SetDescription(button, name);
		var tap = new TapGestureRecognizer();
		tap.Tapped += (_, _) =>
		{
			_selected = code;
			ShowSelection();
			LogLayout();
		};
		button.GestureRecognizers.Add(tap);
		return button;
	}

	// The picked language is filled brand-blue with a check and a 3 px edge; the others are plain white.
	private void ShowSelection()
	{
		foreach ((string code, Border button) in _buttons)
		{
			bool selected = code == _selected;
			button.BackgroundColor = selected ? DinoTokens.Get(DinoTokens.Brand) : Colors.White;
			button.Shadow = selected ? new Shadow { Brush = DinoTokens.Get(DinoTokens.BrandEdge), Offset = new Point(0, 3), Radius = 0, Opacity = 1 } : null!;
			var row = (HorizontalStackLayout)button.Content;
			((Label)row.Children[^1]).TextColor = selected ? Colors.White : DinoTokens.Get(DinoTokens.Ink);
			if (row.Children.FirstOrDefault() is IconView check)
			{
				row.Children.Remove(check);
			}

			if (selected)
			{
				row.Children.Insert(0, new IconView { Kind = IconKind.Check, Color = Colors.White, Size = 18, StrokeWidth = 3, VerticalOptions = LayoutOptions.Center });
			}
		}
	}

	private async void OnContinueClicked(object? sender, EventArgs e) => await _busy.RunAsync(StartDownloadAsync);

	private async Task StartDownloadAsync()
	{
		// Slice 183: asked once, just before the first big download (choosing the language starts it).
		await PackNotificationAsk.AskOnceAsync(() => DisplayAlertAsync(AppResources.Notify_Title, AppResources.Notify_Body, AppResources.Notify_Continue, AppResources.Notify_NotNow));
		_chosen = _selected;
		LanguageChoice.Apply(_chosen, _spoken, PackStartReason.User); // the grown-up's own choice; the voice pack downloads while the books are picked
		FirstBooksGate.Pending = true; // Slice 261: language, then the books (at least two), then the voice download; a kill in between reopens on the picker
		await ShowBooksAsync();
	}

	// Slice 261 (mock Batch 47 frames 163-166): the book picker is pushed over the language page. Back returns to the language (the first choice is let go, as Pick another does);
	// the page's own Completed carries on to the voice download. The picker takes its words from the language just applied.
	private async Task ShowBooksAsync()
	{
		FirstBooksPage page = _services.GetRequiredService<FirstBooksPage>();
		page.BackRequested = async () =>
		{
			_spoken.Clear();
			await Navigation.PopAsync();
		};
		page.Completed = async () =>
		{
			await Navigation.PopAsync();
			ResumeAfterBooks();
		};
		await Navigation.PushAsync(page);
	}

	/// <summary>The books are on the shelf (Slice 261): shows the voice download of the stored language, then the ready page. Also how a restart mid-picker carries on (the language was stored at the pick).</summary>
	public void ResumeAfterBooks()
	{
		_chosen = _selected;
		ApplyTexts();
		LanguagePanel.IsVisible = false;
		DownloadPanel.IsVisible = true;
		_installer.Changed -= OnInstallerChanged;
		_installer.Changed += OnInstallerChanged;
		Refresh();
	}

	// The texts of the download and ready screens are set here, after the pick, so they speak the language just chosen.
	private void ApplyTexts()
	{
		DownloadTitle.Text = AppResources.FirstRun_GettingReady;
		DownloadSub.Text = AppResources.Format(AppResources.FirstRun_Once, LanguageNames.NativeName(_chosen));
		DownloadState.Text = AppResources.FirstRun_Downloading;
		PickAnother.Text = AppResources.FirstRun_PickAnother;
		PhoneVoice.Text = AppResources.FirstRun_PhoneVoice;
		ReadyTitle.Text = AppResources.FirstRun_Ready;
		ReadyBody.Text = AppResources.FirstRun_ReadyBody; // Slice 261: true for two books or six, so it names no count
		HelloLabel.Text = AppResources.FirstRun_HearHello;
		SetUpButton.Text = AppResources.Intro_SeeWhat;
		SkipIntroLabel.Text = AppResources.Intro_SkipStart;
		ReadyUsage.Text = AppResources.FirstRun_Usage;
	}

	private void OnInstallerChanged(object? sender, EventArgs e) => Refresh();

	// Ready as soon as the chosen language's pack is installed (or there is no neural voice to wait for, so the phone voice reads).
	// An Unavailable state counts only once the worker has stored the chosen language: before that it is a stale reading.
	private void Refresh()
	{
		if (!DownloadPanel.IsVisible)
		{
			return;
		}

		PackInstallState state = _installer.State;
		if (LanguageChoice.IsInstalled(_packs, _chosen) || (state == PackInstallState.Unavailable && _spoken.HasStored(_chosen)))
		{
			ShowReady();
			return;
		}

		bool failed = _installer.CanRetry || state == PackInstallState.InvalidPack;
		double progress = state == PackInstallState.Downloading ? Math.Clamp(_installer.Progress, 0, 1) : 0;
		Bar.Progress = progress;
		DownloadPercent.Text = string.Create(AppResources.Culture, $"{(int)(progress * PercentScale)}%");
		DownloadProblem.IsVisible = failed;
		PhoneVoice.IsVisible = failed;
		DownloadProblem.Text = failed ? LocalizedPackStatus.Compose(state, progress) : null;
		DownloadProblem.TextColor = DinoTokens.Get(state == PackInstallState.Stopped ? DinoTokens.Body : DinoTokens.Danger); // Slice 204: a deliberate stop is not an error
	}

	private void ShowReady()
	{
		_installer.Changed -= OnInstallerChanged;
		DownloadPanel.IsVisible = false;
		ReadyPanel.IsVisible = true;
	}

	private void OnRetryTapped(object? sender, TappedEventArgs e) => _ = _installer.StartAsync(PackStartReason.User);

	// Offline or a failed download must not trap a fresh install: the phone voice reads until a pack is installed.
	private void OnPhoneVoiceTapped(object? sender, TappedEventArgs e) => ShowReady();

	// Going back to change the language lets go of the first choice through SpokenLanguage, so a pending switch cannot store it again.
	// A download already running may finish in the background; nothing waits for it.
	private void OnPickAnotherTapped(object? sender, TappedEventArgs e)
	{
		_installer.Changed -= OnInstallerChanged;
		_spoken.Clear();
		DownloadPanel.IsVisible = false;
		LanguagePanel.IsVisible = true;
	}

	private async void OnHelloTapped(object? sender, TappedEventArgs e)
	{
		try
		{
			string line = AppResources.For(nameof(AppResources.FirstRun_HelloLine), CultureInfo.GetCultureInfo(_chosen));
			await _reader.SpeakAsync(line);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			Debug.WriteLine($"Dino Reader could not say hello: {ex}");
		}
	}

	// "See what Dino can do" (Slice 155, mock 73) starts the intro: a fresh root (in the chosen language and flow direction) replaces this page, and the
	// gallery opens the saved intro step on top of itself (GalleryPage.OnAppearing), the same way it resumes after a restart.
	private async void OnSeeWhatClicked(object? sender, EventArgs e) => await _busy.RunAsync(StartIntroAsync);

	private Task StartIntroAsync()
	{
		_usage.RecordGeneral(UsageEventName.IntroStarted, _chosen, UsageDetail.IntroFirstRun);
		IntroState.Start();
		return HandOverAsync();
	}

	// "Skip, start reading" works like Skip all: straight to "Ready for my child", which is grown-up mode (mock 19), not the kids' shelf.
	private async void OnSkipIntroTapped(object? sender, EventArgs e) => await _busy.RunAsync(SkipIntroAsync);

	private Task SkipIntroAsync()
	{
		_usage.RecordGeneral(UsageEventName.IntroEnded, _chosen, UsageDetail.IntroSkippedAll);
		IntroState.Clear();
		return HandOverAsync();
	}

	private async Task HandOverAsync()
	{
		FirstRunState.HandoverPending = true;
		_mode.Enter();
		((DinoApp)Microsoft.Maui.Controls.Application.Current!).RebuildRoot();
		await Task.CompletedTask;
	}
}
