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
	private const double HeaderSpacing = 6;
	private const double BackSize = 44;
	private const double BackIconSize = 22;
	private const double TitleSize = 20;

	private Label _heading = new();
	private readonly Grid _root;

	protected SettingsSubPage(string title)
	{
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		_root = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
		_root.Add(Header(title), 0, 0);
		_root.Add(new ScrollView { Content = Body }, 0, 1);
		Content = _root;
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

	/// <summary>Changes the title after the page is built (a page whose title names a language chosen later).</summary>
	protected void SetTitle(string title)
	{
		_heading.Text = title;
		CastText.InOwnScriptAligned(_heading);
	}

	private Grid Header(string title)
	{
		var back = new Border
		{
			WidthRequest = BackSize,
			HeightRequest = BackSize,
			StrokeThickness = 0,
			BackgroundColor = Colors.Transparent,
			Content = new IconView { Kind = IconKind.Back, Color = DinoTokens.Get(DinoTokens.Ink), Size = BackIconSize, StrokeWidth = 2.6 },
		};
		SemanticProperties.SetDescription(back, AppResources.Common_Back);
		CastViews.AddTap(back, () => Busy.RunAsync(() => Navigation.PopAsync()));
		Label heading = _heading = new Label { Text = title, FontSize = TitleSize, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
		CastText.InOwnScriptAligned(heading);
		SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level1);
		var header = new Grid
		{
			Padding = new Thickness(HeaderSpacing, 8, 14, 10),
			ColumnSpacing = HeaderSpacing,
			ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
		};
		header.Add(back, 0);
		header.Add(heading, 1);
		return header;
	}
}

/// <summary>
/// The Settings page (Slice 172, mock frames 80 and 87): opened from the gear on the shelf, after the gate. A Books row on top (grown-up mode),
/// then "Reading" (Language, Narrator voice, Reading speed, Animal sounds) and "This phone" (Backup, Usage counts, About &amp; updates), each row
/// showing its current value and opening its own page. The store is never a row (Decision 167). The Story languages row waits for Slice 173.
/// </summary>
public sealed class SettingsPage : SettingsSubPage
{
	private const string CodeSeparator = ", ";
	private const int SummaryLanguages = 2;

	private readonly IServiceProvider _services;
	private readonly GrownUpMode _mode;
	private readonly AppConfigClient _config;
	private readonly ISettingsStore _settings;
	private readonly StoryVoicing _voicing;
	private readonly UsageCounter _usage;

	public SettingsPage(IServiceProvider services, GrownUpMode mode, AppConfigClient config, ISettingsStore settings, StoryVoicing voicing, UsageCounter usage)
		: base(AppResources.Settings_Title)
	{
		_services = services;
		_mode = mode;
		_config = config;
		_settings = settings;
		_voicing = voicing;
		_usage = usage;
		Unloaded += (_, _) => _config.Changed -= OnConfigChanged; // a page dropped without disappearing (the root rebuilt after a language change) lets go of the config
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_config.Changed += OnConfigChanged;
		SetTitle(AppResources.Settings_Title); // the page can be built right after a language change (LanguagePage), before the new words apply
		Rebuild();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_config.Changed -= OnConfigChanged;
	}

	private void OnConfigChanged(object? sender, EventArgs e) => Rebuild();

	// Values change on the sub-pages, so the list is built again each time the page shows.
	private void Rebuild()
	{
		Body.Children.Clear();
		Body.Children.Add(SettingsRows.RowsCard([BooksRow()]));
		Body.Children.Add(SettingsRows.GroupHeading(AppResources.Settings_GroupReading));
		Body.Children.Add(SettingsRows.RowsCard([LanguageRow(), NarratorRow(), SpeedRow(), AnimalRow()]));
		Body.Children.Add(SettingsRows.GroupHeading(AppResources.Settings_GroupPhone));
		Body.Children.Add(SettingsRows.RowsCard([BackupRow(), UsageRow(), AboutRow()]));
	}

	private Grid BooksRow() => SettingsRows.NavRow(AppResources.Settings_Books, AppResources.Settings_BooksValue, EnterBooksAsync, Busy);

	// Books opens grown-up mode on the shelf (the gate was passed to get here); Back from there goes to the kids' shelf.
	private async Task EnterBooksAsync()
	{
		_mode.Enter();
		await Navigation.PopAsync();
	}

	private Grid LanguageRow() =>
		SettingsRows.NavRow(AppResources.Language_Title, LanguageNames.NativeName(LanguageChoice.Current(_settings)), OpenAsync<LanguagePage>, Busy);

	private Grid NarratorRow() =>
		SettingsRows.NavRow(AppResources.Settings_NarratorVoice, NarratorSummary(), OpenAsync<NarratorLanguagesPage>, Busy);

	private Grid SpeedRow() =>
		SettingsRows.NavRow(AppResources.Speed_Title, AppResources.Format(AppResources.Speed_Value, ReadingSpeedPreference.Load().Value), OpenAsync<ReadingSpeedPage>, Busy);

	private Grid AnimalRow() =>
		SettingsRows.NavRow(AppResources.Settings_AnimalSounds_Title, OnOff(AnimalSoundsPreference.Load()), OpenAsync<AnimalSoundsPage>, Busy);

	private Grid BackupRow() => SettingsRows.NavRow(AppResources.Backup_Title, null, OpenAsync<BackupPage>, Busy);

	private Grid UsageRow() => SettingsRows.NavRow(AppResources.Usage_Title, OnOff(_usage.Enabled), OpenAsync<UsageCountsPage>, Busy);

	// The "New version" tag shows only while the remote config names a newer release.
	private Grid AboutRow()
	{
		string? tag = _config.NewerVersionExists ? AppResources.Settings_NewVersion : null;
		return SettingsRows.NavRow(AppResources.Settings_About, null, OpenAsync<AboutPage>, Busy, tag);
	}

	private static string OnOff(bool on) => on ? AppResources.Settings_On : AppResources.Settings_Off;

	private Task OpenAsync<TPage>() where TPage : Page => Navigation.PushAsync(_services.GetRequiredService<TPage>());

	// "Story Granny" for one language, "Story Granny (EN), Mana (FA)" for several; past SummaryLanguages the rest is "+n" (the value is one short line).
	private string NarratorSummary()
	{
		IReadOnlyList<string> languages = _voicing.NarratorLanguages(LanguageChoice.Current(_settings));
		if (languages.Count == 1)
		{
			return CastText.VoiceName(_voicing.NarratorOf(languages[0]), languages[0]);
		}

		IEnumerable<string> shown = languages.Take(SummaryLanguages).Select(language => $"{CastText.VoiceName(_voicing.NarratorOf(language), language)} ({language.ToUpperInvariant()})");
		string summary = string.Join(CodeSeparator, shown);
		return languages.Count > SummaryLanguages ? $"{summary} +{languages.Count - SummaryLanguages}" : summary;
	}
}
