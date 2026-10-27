using System.Diagnostics;
using System.Globalization;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Application.Settings;
using ReadTheStupidText.Mobile.Shared;

namespace DinoReader;

/// <summary>Where a language stands on this phone: its voice is installed, is on its way, or has not been fetched.</summary>
public enum LanguageVoiceState
{
	NotInstalled,
	Downloading,
	Ready,
}

/// <summary>
/// The grown-up's language choice (Slice 93, Decision 98), shared by the first run and the Language page: one pick sets the voice
/// language (and starts its pack, <see cref="SpokenLanguage"/>), the words on the screens and the language stories translate into
/// (both follow the UI culture, <see cref="StoryTranslator.MyLanguage"/>). The words apply to screens opened from now on.
/// </summary>
public static class LanguageChoice
{
	private const string UiLanguageKey = "dino.ui.language";

	/// <summary>The culture the screens start in: the grown-up's pick when there is one, else the device's.</summary>
	public static CultureInfo Preferred()
	{
		try
		{
			return Preferences.Default.Get(UiLanguageKey, (string?)null) is { } code ? CultureInfo.GetCultureInfo(UiLanguage.Resolve(code)) : CultureInfo.CurrentUICulture;
		}
		catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
		{
			return CultureInfo.CurrentUICulture;
		}
	}

	/// <summary>The language the grown-up picked, else the one the screens speak.</summary>
	public static string Current() => UiLanguage.Resolve(Preferences.Default.Get(UiLanguageKey, (string?)null) ?? CultureInfo.CurrentUICulture.Name);

	/// <summary>The language the voice speaks: the stored one, else <see cref="Current()"/> (a fresh install that has not picked yet).</summary>
	public static string Current(ISettingsStore settings) => settings.Language is { } stored ? UiLanguage.Resolve(stored) : Current();

	/// <summary>Switches the voice, the screens' words and the translation language to <paramref name="code"/>.</summary>
	public static void Apply(string code, SpokenLanguage spoken)
	{
		string language = UiLanguage.Resolve(code);
		Preferences.Default.Set(UiLanguageKey, language);
		UiCulture.Apply(CultureInfo.GetCultureInfo(language));
		spoken.Use(language);
	}

	/// <summary>Whether a voice pack for <paramref name="code"/> is installed.</summary>
	public static bool IsInstalled(IVoicePackService packs, string code) =>
		InstalledVoiceCatalog.Resolve(packs.GetInstalledVoiceAndStorytellerPacks(), code) is not null;
}

/// <summary>
/// The grown-up Language page (Slice 93, mock screen 26): the fourteen languages with Ready / Download / Downloading n%. Picking one switches
/// to it at once and downloads its voice in that row (Slice 81); the old voice keeps reading until the new one is ready.
/// </summary>
public partial class LanguagePage : ContentPage, IGrownUpPage
{
	private const double RowMinHeight = 54;
	private const double CheckSize = 26;
	private const double BarHeight = 8;
	private const int PercentScale = 100;
	private static readonly Color ReadyGreen = Color.FromArgb("#2E7D32");

	private readonly SpokenLanguage _spoken;
	private readonly VoicePackInstaller _installer;
	private readonly IVoicePackService _packs;
	private readonly ISettingsStore _settings;
	private readonly BusyGuard _busy = new();
	private readonly Dictionary<string, LanguageVoiceState> _shown = [];
	private readonly Dictionary<string, ProgressBar> _bars = [];
	private readonly Dictionary<string, Label> _statuses = [];
	private readonly AppConfigClient _config;
	private readonly UsageCounter _usage;
	private readonly StoryVoicing _voicing;
	private readonly VoicePreview _preview;
	private readonly string _openedWith;
	private View? _versionCard;
	private string? _pending;
	private bool _changed;

	public LanguagePage(SpokenLanguage spoken, VoicePackInstaller installer, IVoicePackService packs, ISettingsStore settings, AppConfigClient config, UsageCounter usage, StoryVoicing voicing, ISpeechReader reader)
	{
		InitializeComponent();
		_spoken = spoken;
		_installer = installer;
		_packs = packs;
		_settings = settings;
		_config = config;
		_usage = usage;
		_voicing = voicing;
		_preview = new VoicePreview(reader);
		_openedWith = UiLanguage.Resolve(LanguageChoice.Current(settings));
		// Slice 133: the counts switch sits above the Version card, which OnAppearing adds last.
		Cards.Children.Add(ReadingSpeedCard.Build(reader, SampleAsync, _preview.Stop));
		Cards.Children.Add(UsageCountsCard.Build(usage));
	}

	// Slice 132: the Version card closes the page (a quiet "A new version is ready" row below latestVersion); it is built again
	// whenever the page appears or a fetched config changes, so the row follows the config while the page is open.
	private void ShowVersionCard()
	{
		int index = Cards.Children.Count;
		if (_versionCard is not null)
		{
			index = Cards.Children.IndexOf(_versionCard);
			Cards.Children.RemoveAt(index);
		}

		_versionCard = VersionCard.Build(_config, _busy);
		Cards.Children.Insert(index, _versionCard);
	}

	private void OnConfigChanged(object? sender, EventArgs e) => ShowVersionCard();

	// Slice 150: the Reading speed card's Play button reads the preview line in the narrator's voice at the chosen speed.
	private async Task SampleAsync()
	{
		// The UI language's voice only when its pack is installed, else the language the voice speaks now (the voice and the line always match).
		string language = _voicing.PreviewLanguage(LanguageChoice.Current(_settings), _spoken.Current);
		string line = CastText.PreviewLineFor(language);
		try
		{
			await _preview.PlayAsync(line, _voicing.NarratorOf(language), VoiceFlavour.Storyteller);
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader speed sample failed: {ex}");
			// Not awaited: the alert would keep the spinner up until it is dismissed.
			_ = DisplayAlertAsync(CastText.Title, CastText.PreviewFailedBody, CastText.Ok);
		}
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_installer.Changed += OnInstallerChanged;
		_preview.Attach();
		_config.Changed += OnConfigChanged;
		ShowVersionCard();
		Rebuild();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_installer.Changed -= OnInstallerChanged;
		_preview.Detach();
		_config.Changed -= OnConfigChanged;
	}

	// A progress tick only moves the downloading row's bar and percentage; any change of state rebuilds the rows.
	private void OnInstallerChanged(object? sender, EventArgs e)
	{
		if (!TryUpdateProgress())
		{
			Rebuild();
		}
	}

	private bool TryUpdateProgress()
	{
		if (_shown.Count == 0 || _shown.Any(shown => StateOf(shown.Key) != shown.Value))
		{
			return false;
		}

		foreach ((string code, ProgressBar bar) in _bars)
		{
			bar.Progress = Math.Clamp(_installer.Progress, 0, 1);
			_statuses[code].Text = StatusText(LanguageVoiceState.Downloading);
		}

		return true;
	}

	/// <summary>The state of one language's voice; the one being fetched shows Downloading while the installer works on it.</summary>
	private LanguageVoiceState StateOf(string code)
	{
		if (LanguageChoice.IsInstalled(_packs, code))
		{
			return LanguageVoiceState.Ready;
		}

		string wanted = UiLanguage.Resolve(_pending ?? _settings.Language);
		bool busy = _installer.State is PackInstallState.Resolving or PackInstallState.Downloading;
		return code == wanted && busy ? LanguageVoiceState.Downloading : LanguageVoiceState.NotInstalled;
	}

	private void Rebuild()
	{
		Rows.Children.Clear();
		_shown.Clear();
		_bars.Clear();
		_statuses.Clear();
		string current = _pending is { } pending ? UiLanguage.Resolve(pending) : LanguageChoice.Current(_settings);
		foreach ((string code, string name) in LanguageNames.All)
		{
			if (Rows.Children.Count > 0)
			{
				Rows.Children.Add(SettingsRows.Divider());
			}

			LanguageVoiceState state = StateOf(code);
			_shown[code] = state;
			Rows.Children.Add(BuildRow(code, name, code == current, state));
		}
	}

	private Grid BuildRow(string code, string name, bool isCurrent, LanguageVoiceState state)
	{
		var row = new Grid
		{
			MinimumHeightRequest = RowMinHeight,
			Padding = new Thickness(14, 8),
			ColumnSpacing = 12,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
			BackgroundColor = isCurrent ? DinoTokens.Get(DinoTokens.SelectedRow) : Colors.Transparent,
		};
		row.Add(CheckCircle(isCurrent), 0);
		row.Add(NameAndBar(code, name, state), 1);
		Label status = StatusLabel(state);
		_statuses[code] = status;
		row.Add(status, 2);
		SemanticProperties.SetDescription(row, name + ", " + StatusText(state));
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) => await _busy.RunAsync(() => PickAsync(code));
		row.GestureRecognizers.Add(tap);
		return row;
	}

	private static View CheckCircle(bool filled)
	{
		var circle = new Border
		{
			WidthRequest = CheckSize,
			HeightRequest = CheckSize,
			VerticalOptions = LayoutOptions.Center,
			StrokeShape = new RoundRectangle { CornerRadius = CheckSize / 2 },
			BackgroundColor = filled ? DinoTokens.Get(DinoTokens.Brand) : Colors.Transparent,
			Stroke = filled ? DinoTokens.Get(DinoTokens.Brand) : DinoTokens.Get(DinoTokens.CheckEmpty),
			StrokeThickness = filled ? 0 : 2,
		};
		if (filled)
		{
			circle.Content = new IconView { Kind = IconKind.Check, Color = Colors.White, Size = 14, StrokeWidth = 3, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
		}

		return circle;
	}

	private View NameAndBar(string code, string name, LanguageVoiceState state)
	{
		bool rtl = DinoFonts.IsRightToLeft(code);
		var stack = new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Center };
		stack.Children.Add(new Label
		{
			Text = name,
			FontSize = 16,
			FontAttributes = FontAttributes.Bold,
			FontFamily = rtl ? DinoFonts.Persian : DinoFonts.Ui,
			FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
		});
		if (state == LanguageVoiceState.Downloading)
		{
			var bar = new ProgressBar { Progress = Math.Clamp(_installer.Progress, 0, 1), ProgressColor = DinoTokens.Get(DinoTokens.Brand), HeightRequest = BarHeight };
			_bars[code] = bar;
			stack.Children.Add(bar);
		}

		return stack;
	}

	private Label StatusLabel(LanguageVoiceState state) => new()
	{
		Text = StatusText(state),
		FontSize = 13.5,
		FontAttributes = FontAttributes.Bold,
		VerticalOptions = LayoutOptions.Center,
		TextColor = state == LanguageVoiceState.Ready ? ReadyGreen : DinoTokens.Get(state == LanguageVoiceState.Downloading ? DinoTokens.Body : DinoTokens.Brand),
	};

	private string StatusText(LanguageVoiceState state) => state switch
	{
		LanguageVoiceState.Ready => AppResources.Language_Ready,
		LanguageVoiceState.Downloading => AppResources.Format(AppResources.Language_Downloading, (int)(_installer.Progress * PercentScale)),
		_ => AppResources.Language_Download,
	};

	private Task PickAsync(string code)
	{
		_pending = code;
		_changed = true;
		LanguageChoice.Apply(code, _spoken);
		Rebuild();
		return Task.CompletedTask;
	}

	// A changed language rebuilds the root, so the gallery the grown-up returns to (and every page after it) speaks the new language.
	private async void OnBackTapped(object? sender, TappedEventArgs e) => await _busy.RunAsync(BackAsync);

	// Counted once, when the grown-up leaves with a language different from the one the page opened with (not for every tap while browsing).
	private void CountLanguageChange()
	{
		string now = UiLanguage.Resolve(_pending ?? _openedWith);
		if (now != _openedWith)
		{
			_usage.RecordGeneral(UsageEventName.ReadingLanguageChanged, now, _openedWith);
		}
	}

	private async Task BackAsync()
	{
		if (_changed)
		{
			CountLanguageChange();
			((DinoApp)Microsoft.Maui.Controls.Application.Current!).RebuildRoot();
			return;
		}

		await Navigation.PopAsync();
	}
}
