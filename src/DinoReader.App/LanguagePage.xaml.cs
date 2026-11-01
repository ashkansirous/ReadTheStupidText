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
			return Preferences.Default.Get(UiLanguageKey, (string?)null) is { } code ? CultureInfo.GetCultureInfo(UiLanguage.Resolve(code)) : AppResources.Culture;
		}
		catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException)
		{
			return AppResources.Culture;
		}
	}

	/// <summary>The language the grown-up picked, else the one the screens speak.</summary>
	public static string Current() => UiLanguage.Resolve(Preferences.Default.Get(UiLanguageKey, (string?)null) ?? AppResources.Culture.Name);

	/// <summary>The language the voice speaks: the stored one, else <see cref="Current()"/> (a fresh install that has not picked yet).</summary>
	public static string Current(ISettingsStore settings) => settings.Language is { } stored ? UiLanguage.Resolve(stored) : Current();

	/// <summary>Switches the voice, the screens' words and the translation language to <paramref name="code"/>.</summary>
	public static void Apply(string code, SpokenLanguage spoken, PackStartReason reason)
	{
		string language = ApplyScreens(code);
		spoken.Use(language, reason);
	}

	/// <summary>Switches the words on the screens (and the translation language) to <paramref name="code"/>; the voice is left as it is.</summary>
	public static string ApplyScreens(string code)
	{
		string language = UiLanguage.Resolve(code);
		Preferences.Default.Set(UiLanguageKey, language);
		UiCulture.Apply(CultureInfo.GetCultureInfo(language));
		return language;
	}

	/// <summary>Whether a voice pack for <paramref name="code"/> is installed.</summary>
	public static bool IsInstalled(IVoicePackService packs, string code) =>
		InstalledVoiceCatalog.Resolve(packs.GetInstalledVoiceAndStorytellerPacks(), code) is not null;
}

/// <summary>
/// The grown-up Language page (Slice 93, mock screen 26): the fifteen languages with Ready / Download / Downloading n%. Picking one switches
/// to it at once and downloads its voice in that row (Slice 81); the old voice keeps reading until the new one is ready.
/// </summary>
public partial class LanguagePage : ContentPage, IGrownUpPage
{
	private const int LoadWaitMilliseconds = 2000;
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
	private readonly UsageCounter _usage;
	private readonly IServiceProvider _services;
	private readonly string _openedWith;
	private string? _pending;
	private StoryLanguageSet? _sides; // the story languages as the taps so far leave them; saved only when the page is left with a changed language
	private bool _changed;

	public LanguagePage(SpokenLanguage spoken, VoicePackInstaller installer, IVoicePackService packs, ISettingsStore settings, UsageCounter usage, IServiceProvider services)
	{
		InitializeComponent();
		_spoken = spoken;
		_installer = installer;
		_packs = packs;
		_settings = settings;
		_usage = usage;
		_services = services;
		_openedWith = UiLanguage.Resolve(LanguageChoice.Current(settings));
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_installer.Changed += OnInstallerChanged;
		Rebuild();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_installer.Changed -= OnInstallerChanged;
		CommitSides();
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

	private async Task PickAsync(string code)
	{
		if (!await KeepStoryLanguagesAsync(code))
		{
			return; // the grown-up cancelled the "which one goes" question: the app language stays as it was
		}

		_pending = code;
		_changed = true;
		LanguageChoice.Apply(code, _spoken, PackStartReason.User); // the grown-up's own tap
		Rebuild();
	}

	// The new app language is always a story language (Slice 173, Decision 168): a side language swaps places with the old one, a new one takes the old one's
	// place as a side when there is room, and with both sides taken the grown-up says which of the three goes. Kept in memory per tap and saved when the page is
	// left (BackAsync); Cancel on the question, or never changing the language, saves nothing.
	private async Task<bool> KeepStoryLanguagesAsync(string code)
	{
		StoryLanguageSet current = _sides ?? StoryLanguages.Current();
		MainLanguageChange change = current.ChangeMain(code);
		if (change.Set is { } set)
		{
			_sides = set;
			return true;
		}

		string? dropped = await DropLanguageSheet.AskAsync(Navigation, UiLanguage.Resolve(code), current.Main, change.DropChoices);
		if (dropped is null)
		{
			return false;
		}

		_sides = current.ChangeMainDropping(code, dropped);
		return true;
	}

	// Hardware back is the same as the Back button: a changed language is committed (with its story languages) and the root rebuilt.
	protected override bool OnBackButtonPressed()
	{
		_ = _busy.RunAsync(BackAsync);
		return true;
	}

	// A changed language rebuilds the root, so the shelf and Settings the grown-up returns to (and every page after them) speak the new language.
	private async void OnBackTapped(object? sender, EventArgs e) => await _busy.RunAsync(BackAsync);

	// Counted once, when the grown-up leaves with a language different from the one the page opened with (not for every tap while browsing).
	private void CountLanguageChange()
	{
		string now = UiLanguage.Resolve(_pending ?? _openedWith);
		if (now != _openedWith)
		{
			_usage.RecordGeneral(UsageEventName.ReadingLanguageChanged, now, _openedWith);
		}
	}

	// The story languages go with the app language, which applies per tap: saved whenever the page is left (Back, a swipe back, the sheet over it), so no way out loses the choice.
	private void CommitSides()
	{
		if (_changed && _sides is { } sides)
		{
			StoryLanguages.SaveSides(sides);
		}
	}

	private async Task BackAsync()
	{
		if (_changed)
		{
			CommitSides();
			CountLanguageChange();
			_installer.Changed -= OnInstallerChanged;
			await RebuildAndReturnToSettingsAsync();
			return;
		}

		await Navigation.PopAsync();
	}

	// The new language is read when a page is built, so the whole stack is rebuilt in it. Settings is reopened on the new shelf only when the new root is
	// the shelf (never over the forced-update page), and only once the new root is on screen, so the push has a handler to land on.
	private async Task RebuildAndReturnToSettingsAsync()
	{
		var app = (DinoApp)Microsoft.Maui.Controls.Application.Current!;
		app.RebuildRoot();
		if (app.Windows.FirstOrDefault()?.Page is not NavigationPage { RootPage: GalleryPage } navigation)
		{
			return;
		}

		try
		{
			if (await WhenLoadedAsync(navigation) && navigation.RootPage is GalleryPage && navigation.CurrentPage is not SettingsPage)
			{
				await navigation.PushAsync(_services.GetRequiredService<SettingsPage>());
			}
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"Dino Reader could not reopen Settings after a language change: {ex}");
		}
	}

	// True when the new root is on screen; false when the wait ran out, so no push lands on a page without a handler.
	private static async Task<bool> WhenLoadedAsync(NavigationPage navigation)
	{
		if (navigation.IsLoaded)
		{
			return true;
		}

		var loaded = new TaskCompletionSource();
		void OnLoaded(object? sender, EventArgs e) => loaded.TrySetResult();
		navigation.Loaded += OnLoaded;
		try
		{
			return await Task.WhenAny(loaded.Task, Task.Delay(LoadWaitMilliseconds)) == loaded.Task;
		}
		finally
		{
			navigation.Loaded -= OnLoaded;
		}
	}
}
