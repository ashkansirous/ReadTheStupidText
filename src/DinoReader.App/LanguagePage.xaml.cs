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

/// <summary>
/// The grown-up's language choice (Slice 93, Decision 98), shared by the first run and the Language page: one pick sets the stored language,
/// the words on the screens and the language stories are read in (both follow the UI culture, <see cref="StoryTranslator.MyLanguage"/>). The words apply to
/// screens opened from now on. Slice 278 part B: no voice pack is fetched with it; the books' pre-made audio follows the story languages (<see cref="ShelfAudioPacks"/>).
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
			DinoFallbacks.Report(ReadTheStupidText.Application.Usage.FallbackArea.Config, ReadTheStupidText.Application.Usage.FallbackReason.PreferenceUnsaved); // Slice 275: the saved language could not be read; the app's current culture is shown as selected
			return AppResources.Culture;
		}
	}

	/// <summary>The language the grown-up picked, else the one the screens speak.</summary>
	public static string Current() => UiLanguage.Resolve(Preferences.Default.Get(UiLanguageKey, (string?)null) ?? AppResources.Culture.Name);

	/// <summary>The language stored as the grown-up's: the stored one, else <see cref="Current()"/> (a fresh install that has not picked yet).</summary>
	public static string Current(ISettingsStore settings) => settings.Language is { } stored ? UiLanguage.Resolve(stored) : Current();

	/// <summary>Stores <paramref name="code"/> as the language and switches the screens' words and the reading language to it.</summary>
	public static void Apply(string code, ISettingsStore settings) => settings.Language = ApplyScreens(code);

	/// <summary>Switches the words on the screens (and the reading language) to <paramref name="code"/>; the stored language is left as it is.</summary>
	public static string ApplyScreens(string code)
	{
		string language = UiLanguage.Resolve(code);
		Preferences.Default.Set(UiLanguageKey, language);
		UiCulture.Apply(CultureInfo.GetCultureInfo(language));
		return language;
	}
}

/// <summary>
/// The grown-up Language page (Slice 93, mock screen 26): the fifteen languages. Picking one switches to it at once (Slice 278 part B: nothing is downloaded for it; the
/// books' pre-made audio in a story language is fetched by the shelf audio run).
/// </summary>
public partial class LanguagePage : ContentPage, IGrownUpPage, ICornerSubPage
{
	private const double RowMinHeight = 54;
	private const double CheckSize = 26;

	private readonly ISettingsStore _settings;
	private readonly BusyGuard _busy = new();
	private readonly UsageCounter _usage;
	private readonly IServiceProvider _services;
	private readonly string _openedWith;
	private readonly StoryLanguageSet _openedSet; // the story languages when the page opened: what a change adds is compared with it (Slice 285)
	private bool _asking; // the mobile-data question is open: a leave event must not start the audio before the answer
	private readonly HashSet<string> _audioStarted = []; // languages whose shelf audio run this page already started quietly (a swipe back), so it is not started twice
	private string? _pending;
	private StoryLanguageSet? _sides; // the story languages as the taps so far leave them; saved only when the page is left with a changed language
	private bool _changed;
	private Page? _hostPage; // Slice 325 part B: the gallery whose Settings tab shows this page; its navigation and alerts are this page's
	private Func<Task>? _hostClose;

	public LanguagePage(ISettingsStore settings, UsageCounter usage, IServiceProvider services)
	{
		InitializeComponent();
		_settings = settings;
		_usage = usage;
		_services = services;
		_openedWith = UiLanguage.Resolve(LanguageChoice.Current(settings));
		_openedSet = StoryLanguages.Current();
	}

	// Slice 325 part B: the Settings tab hosts this page in its content area (nothing is pushed), so the page's appearing, disappearing and Back come from the tab.
	View ICornerSubPage.HostIn(Page owner, Func<Task> close)
	{
		_hostPage = owner;
		_hostClose = close;
		Content = null;
		return LanguageRoot;
	}

	void ICornerSubPage.HostShape(double width, double height)
	{
		// the list follows its scroll area, not the window
	}

	void ICornerSubPage.HostAppearing() => Rebuild();

	void ICornerSubPage.HostDisappearing()
	{
		CommitSides();
		Dispatcher.Dispatch(StartAudioIfLeft); // one tick later, so a sheet that is about to cover the page is seen as the page still being here
	}

	Task ICornerSubPage.HostBackAsync() => _busy.RunAsync(BackAsync);

	private Page Owner => _hostPage ?? this;

	private Task CloseAsync() => _hostClose is { } close ? close() : Task.CompletedTask;

	// A sheet covering the page (the drop-language sheet, the mobile-data question) fires the gallery's disappearing too, with this page still open: only a page that has left starts the audio of what it changed.
	private void StartAudioIfLeft()
	{
		if (!_asking && Owner.Navigation.ModalStack.Count == 0) // while the mobile-data question or a sheet is open, its answer starts the audio
		{
			StartShelfAudioSafely(VoiceRequestOutcome.NothingAsked); // leaving some other way never asks; Back asks first (BackAsync) and starts with the answer
		}
	}

	private void Rebuild()
	{
		Rows.Children.Clear();
		string current = _pending is { } pending ? UiLanguage.Resolve(pending) : LanguageChoice.Current(_settings);
		foreach ((string code, string name) in LanguageNames.All)
		{
			if (Rows.Children.Count > 0)
			{
				Rows.Children.Add(SettingsRows.Divider());
			}

			Rows.Children.Add(BuildRow(code, name, code == current));
		}
	}

	private Grid BuildRow(string code, string name, bool isCurrent)
	{
		var row = new Grid
		{
			MinimumHeightRequest = RowMinHeight,
			Padding = new Thickness(14, 8),
			ColumnSpacing = 12,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
			BackgroundColor = isCurrent ? DinoTokens.Get(DinoTokens.SelectedRow) : Colors.Transparent,
		};
		row.Add(CheckCircle(isCurrent), 0);
		row.Add(NameLabel(code, name), 1);
		SemanticProperties.SetDescription(row, name);
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) =>
		{
			UiUsage.Tap(UiControls.Language); // Slice 352
			await _busy.RunAsync(() => PickAsync(code));
		};
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

	private static Label NameLabel(string code, string name)
	{
		bool rtl = DinoFonts.IsRightToLeft(code);
		return new Label
		{
			Text = name,
			FontSize = 16,
			FontAttributes = FontAttributes.Bold,
			FontFamily = rtl ? DinoFonts.Persian : DinoFonts.Ui,
			FlowDirection = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
			VerticalOptions = LayoutOptions.Center,
		};
	}

	private async Task PickAsync(string code)
	{
		if (!await KeepStoryLanguagesAsync(code))
		{
			return; // the grown-up cancelled the "which one goes" question: the app language stays as it was
		}

		string before = _pending is { } pending ? UiLanguage.Resolve(pending) : LanguageChoice.Current(_settings);
		_pending = code;
		_changed = true;
		LanguageChoice.Apply(code, _settings); // the grown-up's own tap
		if (UiLanguage.Resolve(code) != before)
		{
			UiUsage.Setting(UiSettings.AppLanguage, code); // Slice 352: only a real change, and only once the "which one goes" question was answered; dropped unless it is one of the 15 app languages
		}

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

		string? dropped = await DropLanguageSheet.AskAsync(Owner.Navigation, UiLanguage.Resolve(code), current.Main, change.DropChoices);
		if (dropped is null)
		{
			return false;
		}

		_sides = current.ChangeMainDropping(code, dropped);
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

	// Slice 285: the language change fetches the books' sound in every language it added (the new main one too), as the other two ways to add a language do. Start joins a run already going.
	// Each language starts once per page (Back and a swipe back share _audioStarted), and a throw never reaches the caller: the grown-up is never stranded on this page by it.
	private void StartShelfAudioSafely(VoiceRequestOutcome outcome)
	{
		ShelfAudioPacks shelf;
		IReadOnlyList<string> toStart;
		try
		{
			shelf = _services.GetRequiredService<ShelfAudioPacks>();
			toStart = ShelfAudioLanguages.ToStartOnLeaving(_openedSet, StoryLanguages.Current(), _audioStarted);
		}
		catch (Exception ex)
		{
			// no-fallback: the language change itself is saved; the launch and return checks fetch the audio later
			AudioPackLog.Write($"the shelf audio start failed ({ex.GetType().Name})");
			return;
		}

		foreach (string language in toStart)
		{
			StartOneLanguage(shelf, language, outcome);
		}
	}

	// One language's start never stops the next: it counts as started only once Start returned, so a throw leaves it to be tried again on this page.
	private void StartOneLanguage(ShelfAudioPacks shelf, string language, VoiceRequestOutcome outcome)
	{
		try
		{
			shelf.Start(language, outcome);
			_audioStarted.Add(language);
		}
		catch (Exception ex)
		{
			// no-fallback: the language change itself is saved; the launch and return checks fetch the audio later
			AudioPackLog.Write($"the shelf audio start for {language} failed ({ex.GetType().Name})");
		}
	}

	private async Task AskAndStartShelfAudioAsync()
	{
		VoiceRequestOutcome outcome = VoiceRequestOutcome.NothingAsked;
		_asking = true;
		try
		{
			outcome = await ShelfAudioAsk.AskAsync(Owner,ShelfAudioLanguages.Added(_openedSet, StoryLanguages.Current()));
		}
		catch (Exception ex)
		{
			// no-fallback: the question could not be shown; the audio then follows the ordinary Wi-Fi rule
			AudioPackLog.Write($"the mobile data question failed ({ex.GetType().Name})");
		}
		finally
		{
			_asking = false;
		}

		StartShelfAudioSafely(outcome);
	}

	private async Task BackAsync()
	{
		if (_changed)
		{
			CommitSides();
			CountLanguageChange();
			await AskAndStartShelfAudioAsync();
			await RebuildAndReturnToSettingsAsync();
			return;
		}

		await CloseAsync();
	}

	// The new language is read when a page is built, so the whole stack is rebuilt in it. The grown-up mode and its tab are the app's, not the page's, so the new gallery comes up in the corner on
	// the Settings tab: that is the return to Settings (Slice 325 part B; nothing is pushed any more). A forced-update root has no gallery, so there is nothing to return to.
	private Task RebuildAndReturnToSettingsAsync()
	{
		var app = (DinoApp)Microsoft.Maui.Controls.Application.Current!;
		_services.GetRequiredService<GrownUpMode>().SelectTab(CornerTab.Settings);
		app.RebuildRoot();
		return Task.CompletedTask;
	}
}
