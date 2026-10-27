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
/// The first run (Slice 93, Decision 98, mock screens 16-18): the grown-up picks the language, the voice pack downloads with its real
/// progress while the starter stories land on the shelf, then "Set up the stories" opens grown-up mode. The language is stored at the
/// pick, so a kill after it goes straight to the gallery while the download carries on. No tutorial.
/// </summary>
public partial class FirstRunPage : ContentPage
{
	// One cover per starter, capped at four so the strip fits a 320dp-wide phone.
	private static readonly int ShelfCovers = Math.Min(StarterStories.Count, 4);
	private const double CoverWidth = 56;
	private const int PercentScale = 100;

	private readonly SpokenLanguage _spoken;
	private readonly VoicePackInstaller _installer;
	private readonly IVoicePackService _packs;
	private readonly ISpeechReader _reader;
	private readonly StarterStories _starters;
	private readonly IStoryLibrary _library;
	private readonly GrownUpMode _mode;
	private readonly UsageCounter _usage;
	private readonly IServiceProvider _services;
	private readonly BusyGuard _busy = new();
	private readonly Dictionary<string, Border> _buttons = [];
	private string _selected;
	private string _chosen = UiLanguage.English;
	private bool _shelfLoaded;

	public FirstRunPage(ISettingsStore settings, SpokenLanguage spoken, VoicePackInstaller installer, IVoicePackService packs, ISpeechReader reader,
		StarterStories starters, IStoryLibrary library, GrownUpMode mode, UsageCounter usage, IServiceProvider services)
	{
		InitializeComponent();
		_selected = LanguageChoice.Current(settings);
		_spoken = spoken;
		_installer = installer;
		_packs = packs;
		_reader = reader;
		_starters = starters;
		_library = library;
		_mode = mode;
		_usage = usage;
		_services = services;
		BuildLanguageButtons();
	}

	private void BuildLanguageButtons()
	{
		LanguageGrid.ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)];
		int index = 0;
		foreach ((string code, string name) in LanguageNames.All)
		{
			Border button = BuildLanguageButton(code, name);
			_buttons[code] = button;
			if (index % 2 == 0)
			{
				LanguageGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
			}

			LanguageGrid.Add(button, index % 2, index / 2);
			index++;
		}

		ShowSelection();
	}

	private Border BuildLanguageButton(string code, string name)
	{
		bool rtl = DinoFonts.IsRightToLeft(code);
		var button = new Border { HeightRequest = 52, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 16 }, Padding = new Thickness(14, 0) };
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

	private Task StartDownloadAsync()
	{
		_chosen = _selected;
		LanguageChoice.Apply(_chosen, _spoken);
		ApplyTexts();
		LanguagePanel.IsVisible = false;
		DownloadPanel.IsVisible = true;
		_installer.Changed -= OnInstallerChanged;
		_installer.Changed += OnInstallerChanged;
		Refresh();
		_ = LoadShelfAsync();
		return Task.CompletedTask;
	}

	// The texts of the download and ready screens are set here, after the pick, so they speak the language just chosen.
	private void ApplyTexts()
	{
		DownloadTitle.Text = AppResources.FirstRun_GettingReady;
		DownloadSub.Text = AppResources.Format(AppResources.FirstRun_Once, LanguageNames.NativeName(_chosen));
		DownloadState.Text = AppResources.FirstRun_Downloading;
		ShelfTitle.Text = AppResources.FirstRun_Waiting;
		PickAnother.Text = AppResources.FirstRun_PickAnother;
		PhoneVoice.Text = AppResources.FirstRun_PhoneVoice;
		ReadyTitle.Text = AppResources.FirstRun_Ready;
		ReadyBody.Text = StarterStories.Count == 1
			? AppResources.FirstRun_ReadyBodyOne
			: AppResources.Format(AppResources.FirstRun_ReadyBody, StarterStories.Count);
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
	}

	private void ShowReady()
	{
		_installer.Changed -= OnInstallerChanged;
		DownloadPanel.IsVisible = false;
		ReadyPanel.IsVisible = true;
	}

	private async Task LoadShelfAsync()
	{
		if (_shelfLoaded)
		{
			return;
		}

		_shelfLoaded = true;
		try
		{
			await _starters.InstallOnceAsync();
			foreach (Story story in (await _library.ListAsync()).Take(ShelfCovers))
			{
				Shelf.Children.Add(new StoryCover { WidthRequest = CoverWidth, BindingContext = StoryTile.From(story, _library, _mode) });
			}
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader could not show the shelf: {ex}");
		}
	}

	private void OnRetryTapped(object? sender, TappedEventArgs e) => _ = _installer.StartAsync();

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
