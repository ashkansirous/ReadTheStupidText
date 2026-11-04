using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Settings;

namespace DinoReader;

/// <summary>
/// The page every grown-up setting hangs off (Slice 172, Decision 167): a back button and a title over a scrolling column of cards. Each
/// settings sub-page (Reading speed, Animal sounds, Usage counts, About &amp; updates) is one of these, and Back goes up to Settings.
/// Leaving the app closes it like every grown-up page (<see cref="IGrownUpPage"/>).
/// </summary>
public abstract class SettingsSubPage : ContentPage, IGrownUpPage
{
	private const string DoneIdPrefix = "GrownUpDone";
	private const string ListTitleId = "SettingsTitle";
	private const string SubTitleIdPrefix = "SettingsTitle";
	private GrownUpHeader _header = new();
	private readonly Grid _root;
	private Page? _hostPage;
	private Func<Task>? _hostClose;

	protected SettingsSubPage(string title)
	{
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		Body.HorizontalOptions = LayoutOptions.Center; // Slice 232 part D: held sideways or on a tablet the column stays at most 640 dp wide, centred, never stretched
		var scroll = new ScrollView { Content = Body };
		scroll.SizeChanged += (_, _) => FitBody(scroll.Width);
		_root = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
		_root.Add(Header(title), 0, 0);
		_root.Add(scroll, 0, 1);
		Content = _root;
	}

	/// <summary>The page's whole content (title row and body): both the pushed page and Settings' right pane show this one view, so the two cannot drift.</summary>
	protected Grid Root => _root;

	/// <summary>The title row, for a page that arranges its own content (Settings' two panes).</summary>
	protected GrownUpHeader PageHeader => _header;

	/// <summary>The navigation to push on: the page's own, or the Settings page's when this page is shown in its right pane.</summary>
	protected INavigation Nav => _hostPage?.Navigation ?? Navigation;

	/// <summary>An alert on the page the user sees (the Settings page when this one is in its right pane).</summary>
	protected Task AlertAsync(string title, string message, string ok) => (_hostPage ?? this).DisplayAlertAsync(title, message, ok);

	/// <summary>
	/// Slice 232 part D: moves the content into Settings' right pane and returns it. Back and Done there close Settings as a whole (<paramref name="close"/>); the Back
	/// button is hidden because the list's own header has it. Called once per page; the page is never shown on its own after this.
	/// </summary>
	internal View HostIn(Page owner, Func<Task> close)
	{
		_hostPage = owner;
		_hostClose = close;
		_header.BackVisible = false;
		// No AutomationId is set here: MAUI allows one set only, so each id is given where its view is created (see Header).

		Content = null;
		return _root;
	}

	/// <summary>A hosted page gets the same appearing and disappearing a pushed one does (its events and previews start and stop there).</summary>
	internal void HostAppearing() => OnAppearing();

	internal void HostDisappearing() => OnDisappearing();

	private Task CloseAsync() => _hostClose is { } close ? close() : Navigation.PopAsync();

	// A page held sideways keeps clear of the camera cut-out at its start, as the other sideways screens do.
	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		if (width > 0 && height > 0)
		{
			_root.Margin = new Thickness(LandscapeLayout.IsLandscape(width, height) ? LandscapeLayout.SidewaysInset : 0, 0, 0, 0);
		}
	}

	/// <summary>Lays a view (a wait overlay) over the whole page, header included, without re-parenting the page's content.</summary>
	protected void AddOverlay(View overlay)
	{
		Grid.SetRowSpan(overlay, _root.RowDefinitions.Count);
		_root.Add(overlay);
	}

	protected BusyGuard Busy { get; } = new();

	/// <summary>The column the page puts its cards in.</summary>
	protected VerticalStackLayout Body { get; } = new() { Padding = new Thickness(14, 0, 14, 20), Spacing = 10 };

	// A centred column does not fill, so its width is the scroll area's, capped (a size change only sets a width; no view is rebuilt).
	private void FitBody(double available)
	{
		if (available > 0)
		{
			Body.WidthRequest = Math.Min(available, SettingsLayout.MaxContentWidth);
		}
	}

	/// <summary>Changes the title after the page is built (a page whose title names a language chosen later).</summary>
	protected void SetTitle(string title)
	{
		_header.Title = title;
	}

	private GrownUpHeader Header(string title)
	{
		var header = _header = new GrownUpHeader(DoneIdPrefix + GetType().Name) { Title = title }; // each page in the stack has its own Done id (Slice 229)
		// The Settings list keeps SettingsTitle; every sub-page's title has its own id, so the list and the page in the right pane never share one. Set once, here.
		header.TitleLabel.AutomationId = this is SettingsPage ? ListTitleId : SubTitleIdPrefix + GetType().Name;
		header.BackRequested += async (_, _) => await Busy.RunAsync(CloseAsync);
		header.DoneRequested += async (_, _) => await Busy.RunAsync(CloseAsync); // one level up, like Back: Done never skips a page (Slice 229); in the right pane both close Settings
		return header;
	}
}

/// <summary>The Settings rows whose page can show in the right pane (Slice 232 part D); the first is what a first open shows.</summary>
public enum SettingsSection
{
	ReadingSpeed,
	AnimalSounds,
	UsageCounts,
	About,
}

/// <summary>
/// The Settings page (Slice 172, mock frames 80 and 87): opened from the gear in grown-up mode (Slice 189; the gate was passed to get there), and Back returns
/// to grown-up mode. "Reading" (Language, Narrator voice, Reading speed, Animal sounds) and "This phone" (Backup, Usage counts, About &amp; updates), each row
/// showing its current value and opening its own page. The store is never a row (Decision 167). The Story languages row (Slice 173) sits under Language.
/// </summary>
public sealed class SettingsPage : SettingsSubPage
{
	private const string CodeSeparator = ", ";
	private const int SummaryLanguages = 2;
	private const string ReadingSpeedRowId = "SettingsReadingSpeedRow";
	private const string ReportProblemRowId = "SettingsReportProblemRow";

	private readonly IServiceProvider _services;
	private readonly AppConfigClient _config;
	private readonly ISettingsStore _settings;
	private readonly StoryVoicing _voicing;
	private readonly UsageCounter _usage;

	// Slice 232 part D (mock Batch 44 frames 118, 128). This page owns which section the right pane shows, so a rotation keeps it; the pane's page is built again only when the
	// selection changes, never on a size change (the page is re-parented, not rebuilt).
	private readonly Grid _split;
	private const double UnsetWidth = -1; // VisualElement.WidthRequest's "no request"
	private readonly ContentView _pane = new() { HorizontalOptions = LayoutOptions.Start, BackgroundColor = DinoTokens.Get(DinoTokens.Sand) };
	private SettingsSection _selected = SettingsSection.ReadingSpeed;
	private SettingsSubPage? _hosted;
	private SettingsSubPage? _shown; // the hosted page last told it appeared (see SyncHosted)
	private readonly Dictionary<SettingsSection, Grid> _sectionRows = [];
	private bool _twoPanes;
	private bool _valueUnderName;
	private IReadOnlyList<Grid> _navRows = [];
	private bool _appeared;
	private Size _windowSize;

	public SettingsPage(IServiceProvider services, AppConfigClient config, ISettingsStore settings, StoryVoicing voicing, UsageCounter usage)
		: base(AppResources.Settings_Title)
	{
		_services = services;
		_config = config;
		_settings = settings;
		_voicing = voicing;
		_usage = usage;
		_split = new Grid { ColumnDefinitions = [new ColumnDefinition(new GridLength(LandscapeLayout.PhoneListPane)), new ColumnDefinition(GridLength.Star)] };
		_split.Add(_pane, 1);
		Unloaded += (_, _) => Release(); // a page dropped without disappearing (the root rebuilt after a language change) lets go of the config and tells its hosted page
	}

	// Lets go of everything the page subscribed to and tells the hosted page it is gone, once (OnDisappearing and Unloaded both come here).
	private void Release()
	{
		_config.Changed -= OnConfigChanged;
		SettingsRows.SettingChanged -= RefreshValue;
		_appeared = false;
		SyncHosted();
	}

	// The one place the hosted page is told it appeared or disappeared: it is shown only while this page is appeared and in two panes, and each change is told once.
	private void SyncHosted()
	{
		SettingsSubPage? wanted = _appeared && _twoPanes ? _hosted : null;
		if (ReferenceEquals(wanted, _shown))
		{
			return;
		}

		_shown?.HostDisappearing();
		_shown = wanted;
		_shown?.HostAppearing();
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_appeared = true;
		_ = _services.GetRequiredService<StoryTranslator>().ReconcilePacksAsync(); // behind the grown-up gate: adds the packs an older install already has to the record
		_config.Changed -= OnConfigChanged; // never twice
		_config.Changed += OnConfigChanged;
		SettingsRows.SettingChanged -= RefreshValue; // never twice
		SettingsRows.SettingChanged += RefreshValue;
		SetTitle(AppResources.Settings_Title); // the page can be built right after a language change (LanguagePage), before the new words apply
		Rebuild();
		SyncHosted();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		Release();
	}

	private void OnConfigChanged(object? sender, EventArgs e) => Rebuild();

	// A page in the right pane wrote a setting: only the value text of the list's rows changes (never the list or the pane).
	// The writer passes the new value text, so nothing is reloaded here.
	private void RefreshValue(SettingsSection section, string value)
	{
		if (_sectionRows.TryGetValue(section, out Grid? row))
		{
			SettingsRows.SetNavValue(row, value);
		}
	}

	protected override void OnSizeAllocated(double width, double height)
	{
		base.OnSizeAllocated(width, height);
		if (width <= 0 || height <= 0 || (width == _windowSize.Width && height == _windowSize.Height))
		{
			return;
		}

		_windowSize = new Size(width, height);
		bool two = SettingsLayout.TwoPanes(width, height);
		_split.ColumnDefinitions[0].Width = new GridLength(LandscapeLayout.SideListWidth(width, height));
		_valueUnderName = SettingsLayout.ValueUnderName(width, height);
		ApplyRowValues();
		double paneWidth = SettingsLayout.PaneWidth(width, height); // the width the tests check is the width drawn
		_pane.WidthRequest = paneWidth > 0 ? paneWidth : UnsetWidth;
		if (two != _twoPanes)
		{
			SetPanes(two);
		}
	}

	// A size change only moves the list and shows or hides the right pane: it never navigates, and a page already pushed on top stays pushed.
	private void SetPanes(bool two)
	{
		_twoPanes = two;
		PageHeader.ButtonVisible = !two; // in two panes the right pane's title row carries Done
		if (two)
		{
			Content = null;
			_split.Add(Root, 0);
			Content = _split;
			ShowSelected();
		}
		else
		{
			Content = null;
			_split.Remove(Root);
			Content = Root;
		}

		SyncHosted();
		Retint(); // the list is built on appearing and on real content changes only: a size change just retints the rows already there
#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] settings panes={(two ? 2 : 1)} selected={_selected} valueUnder={_valueUnderName}");
#endif
	}

	// Builds the right pane's page the first time and whenever the selection changed; later calls (a rotation back to two panes) keep the page already there.
	private void ShowSelected()
	{
		if (_hosted is null)
		{
			_hosted = CreateSection(_selected);
			_pane.Content = _hosted.HostIn(this, ClosePageAsync);
		}
	}

	private Task ClosePageAsync() => Navigation.PopAsync();

	private SettingsSubPage CreateSection(SettingsSection section) => section switch
	{
		SettingsSection.AnimalSounds => _services.GetRequiredService<AnimalSoundsPage>(),
		SettingsSection.UsageCounts => _services.GetRequiredService<UsageCountsPage>(),
		SettingsSection.About => _services.GetRequiredService<AboutPage>(),
		_ => _services.GetRequiredService<ReadingSpeedPage>(),
	};

	// Two panes: a tap on a row whose page lives in the pane swaps the pane (no push). One pane: the page is pushed as before.
	private Task OpenSectionAsync<TPage>(SettingsSection section) where TPage : SettingsSubPage
	{
		if (!_twoPanes)
		{
			return OpenAsync<TPage>();
		}

		Select(section);
		return Task.CompletedTask;
	}

	private void Select(SettingsSection section)
	{
		if (section == _selected && _hosted is not null)
		{
			return;
		}

		_hosted = null;
		_selected = section;
		ShowSelected();
		SyncHosted(); // tells the page being replaced it is gone, and the new one it appeared
		Retint();

#if DEBUG
		ReadTheStupidText.Mobile.Shared.Testing.TestHookLog.Write($"[Landscape] settings pane={section}");
#endif
	}

	// Only the selected row is tinted, and only while the right pane is open (in place: the list is not rebuilt).
	private void Retint()
	{
		foreach ((SettingsSection key, Grid row) in _sectionRows)
		{
			row.BackgroundColor = _twoPanes && key == _selected ? DinoTokens.Get(DinoTokens.BrandTint) : Colors.Transparent;
		}
	}

	// The selected row of the list is tinted while the right pane is open.
	private Grid Tinted(Grid row, SettingsSection section)
	{
		_sectionRows[section] = row;
		if (_twoPanes && section == _selected)
		{
			row.BackgroundColor = DinoTokens.Get(DinoTokens.BrandTint);
		}

		return row;
	}

	// Values change on the sub-pages, so the list is built again each time the page shows.
	private void Rebuild()
	{
		Body.Children.Clear();
		Grid[] reading = [LanguageRow(), StoryLanguagesRow(), NarratorRow(), SpeedRow(), AnimalRow()];
		Grid[] phone = PhoneRows();
		_navRows = [.. reading, .. phone];
		Body.Children.Add(SettingsRows.GroupHeading(AppResources.Settings_GroupReading));
		Body.Children.Add(SettingsRows.RowsCard(reading));
		Body.Children.Add(SettingsRows.GroupHeading(AppResources.Settings_GroupPhone));
		Body.Children.Add(SettingsRows.RowsCard(phone));
		ApplyRowValues();
	}

	// Slice 234 part B (mock Batch 45 frame 140): in the 300 dp list of a tablet held upright each row shows its value under its name. Only the rows' sizes and cells change.
	private void ApplyRowValues()
	{
		foreach (Grid row in _navRows)
		{
			SettingsRows.StackNavValue(row, _valueUnderName);
		}
	}

	private Grid LanguageRow() =>
		SettingsRows.NavRow(AppResources.Language_Title, LanguageNames.NativeName(LanguageChoice.Current()), OpenAsync<LanguagePage>, Busy);

	// The app language and the side languages the child can switch between (Slice 173), like "English, فارسی".
	private Grid StoryLanguagesRow() =>
		SettingsRows.NavRow(AppResources.StoryLanguages_SettingsTitle, StoryLanguages.Names(StoryLanguages.Current().All), OpenAsync<StoryLanguagesPage>, Busy);

	private Grid NarratorRow() =>
		SettingsRows.NavRow(AppResources.Settings_NarratorVoice, NarratorSummary(), OpenAsync<NarratorLanguagesPage>, Busy);

	// Reading speed, Animal sounds, Usage counts and About show in the right pane in two panes; Language, Story languages, Narrator voice and Backup push in both.
	private Grid SpeedRow() => Tinted(
		SettingsRows.NavRow(AppResources.Speed_Title, AppResources.Format(AppResources.Speed_Value, ReadingSpeedPreference.Load().Value), () => OpenSectionAsync<ReadingSpeedPage>(SettingsSection.ReadingSpeed), Busy, automationId: ReadingSpeedRowId),
		SettingsSection.ReadingSpeed);

	private Grid AnimalRow() => Tinted(
		SettingsRows.NavRow(AppResources.Settings_AnimalSounds_Title, SettingsRows.OnOff(AnimalSoundsPreference.Load()), () => OpenSectionAsync<AnimalSoundsPage>(SettingsSection.AnimalSounds), Busy),
		SettingsSection.AnimalSounds);

	// Slice 249: "Report a problem" follows About; the Windows head has no crash reporting, so it has no row.
	private Grid[] PhoneRows()
	{
#if WINDOWS
		return [BackupRow(), UsageRow(), AboutRow()];
#else
		return [BackupRow(), UsageRow(), AboutRow(), ReportProblemRow()];
#endif
	}

	private Grid ReportProblemRow() =>
		SettingsRows.NavRow(AppResources.ReportProblem_Title, null, () => Navigation.PushAsync(new ReportProblemPage()), Busy, automationId: ReportProblemRowId);

	private Grid BackupRow() => SettingsRows.NavRow(AppResources.Backup_Title, null, OpenAsync<BackupPage>, Busy);

	private Grid UsageRow() => Tinted(
		SettingsRows.NavRow(AppResources.Usage_Title, SettingsRows.OnOff(_usage.Enabled), () => OpenSectionAsync<UsageCountsPage>(SettingsSection.UsageCounts), Busy),
		SettingsSection.UsageCounts);

	// The "New version" tag shows only while the remote config names a newer release.
	private Grid AboutRow()
	{
		string? tag = _config.NewerVersionExists ? AppResources.Settings_NewVersion : null;
		return Tinted(SettingsRows.NavRow(AppResources.Settings_About, null, () => OpenSectionAsync<AboutPage>(SettingsSection.About), Busy, tag), SettingsSection.About);
	}

	private Task OpenAsync<TPage>() where TPage : Page => Navigation.PushAsync(_services.GetRequiredService<TPage>());

	// "Story Granny" for one language, "Story Granny (EN), Mana (FA)" for several; past SummaryLanguages the rest is "+n" (the value is one short line).
	private string NarratorSummary()
	{
		IReadOnlyList<string> languages = _voicing.NarratorLanguages();
		if (languages.Count == 1)
		{
			return CastText.VoiceName(_voicing.NarratorOf(languages[0]), languages[0]);
		}

		IEnumerable<string> shown = languages.Take(SummaryLanguages).Select(language => $"{CastText.VoiceName(_voicing.NarratorOf(language), language)} ({language.ToUpperInvariant()})");
		string summary = string.Join(CodeSeparator, shown);
		return languages.Count > SummaryLanguages ? $"{summary} +{languages.Count - SummaryLanguages}" : summary;
	}
}
