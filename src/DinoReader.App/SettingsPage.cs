using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using Sirous.Reading.Application.Settings;
using Sirous.Reading.Mobile.Shared.Crashes;

namespace DinoReader;

/// <summary>A view the grown-ups' corner shows as one of its tabs (Slice 325 part B): told when it comes on screen and when it goes, so it starts and stops what it watches (the page itself is the Gallery).</summary>
internal interface ICornerTabView
{
	/// <summary>The tab is on screen now (the gallery is showing, in the corner, on this tab).</summary>
	void Appearing();

	/// <summary>The tab went off screen (another tab, the shelf, or a page over the gallery).</summary>
	void Disappearing();
}

/// <summary>
/// A page the Settings tab shows inside its content area (Slice 325 part B, grown-up mock frame W1: "sub-pages open inside the content area with their own back arrow, and the rail stays"). The page keeps
/// its content and its logic; <see cref="HostIn"/> hands the content over and the tab puts it in its own stage. Nothing is pushed on the navigation stack.
/// </summary>
public interface ICornerSubPage
{
	/// <summary>Moves the page's content out to be hosted by <paramref name="owner"/> (the gallery: its navigation and alerts are the page's now); <paramref name="close"/> goes back up to the list. Called once per page.</summary>
	View HostIn(Page owner, Func<Task> close);

	/// <summary>The same appearing a pushed page gets (its events and previews start there).</summary>
	void HostAppearing();

	/// <summary>The same disappearing a pushed page gets.</summary>
	void HostDisappearing();

	/// <summary>The window's size, for a page whose arrangement follows it (Backup's two cards).</summary>
	void HostShape(double width, double height);

	/// <summary>Back (the arrow, Android Back): the page may save first, then goes up one level.</summary>
	Task HostBackAsync();
}

/// <summary>
/// The page every grown-up setting hangs off (Slice 172, Decision 167): a back arrow and a title over a scrolling column of cards. Each settings sub-page (Reading speed, Animal sounds, Usage counts,
/// About &amp; updates, Story languages, Report a problem) is one of these. Since Slice 325 part B it is never pushed: the Settings tab hosts its content (<see cref="ICornerSubPage"/>), and Back goes up to the list.
/// </summary>
public abstract class SettingsSubPage : ContentPage, IGrownUpPage, ICornerSubPage
{
	private const string SubTitleIdPrefix = "SettingsTitle";
	private readonly GrownUpHeader _header = new();
	private readonly Grid _root;
	private Page? _hostPage;
	private Func<Task>? _hostClose;

	protected SettingsSubPage(string title)
	{
		NavigationPage.SetHasNavigationBar(this, false);
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
		BackgroundColor = DinoTokens.Get(DinoTokens.Sand);
		Body.HorizontalOptions = LayoutOptions.Start; // frame W1: the column is at most 620 dp wide, at the start of the content area, never stretched
		var scroll = new ScrollView { Content = Body };
		scroll.SizeChanged += (_, _) => FitBody(scroll.Width);
		_root = new Grid { RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)] };
		_header.TitleLabel.AutomationId = SubTitleIdPrefix + GetType().Name; // set once, here
		_root.Add(Header(title), 0, 0);
		_root.Add(scroll, 0, 1);
		Content = _root;
	}

	/// <summary>The navigation to push on: the gallery's, whose content area shows this page.</summary>
	protected INavigation Nav => _hostPage?.Navigation ?? Navigation;

	/// <summary>An alert on the page the user sees (the gallery).</summary>
	protected Task AlertAsync(string title, string message, string ok) => (_hostPage ?? this).DisplayAlertAsync(title, message, ok);

	View ICornerSubPage.HostIn(Page owner, Func<Task> close)
	{
		_hostPage = owner;
		_hostClose = close;
		Content = null;
		return _root;
	}

	void ICornerSubPage.HostAppearing() => OnAppearing();

	void ICornerSubPage.HostDisappearing() => OnDisappearing();

	void ICornerSubPage.HostShape(double width, double height)
	{
		// the column follows its scroll area, not the window
	}

	Task ICornerSubPage.HostBackAsync() => Busy.RunAsync(CloseAsync);

	private Task CloseAsync() => _hostClose is { } close ? close() : Task.CompletedTask;

	/// <summary>Lays a view (a wait overlay) over the whole page, header included, without re-parenting the page's content.</summary>
	protected void AddOverlay(View overlay)
	{
		Grid.SetRowSpan(overlay, _root.RowDefinitions.Count);
		_root.Add(overlay);
	}

	protected BusyGuard Busy { get; } = new();

	/// <summary>The column the page puts its cards in.</summary>
	protected VerticalStackLayout Body { get; } = new() { Padding = new Thickness(0, 0, 0, 20), Spacing = 10 };

	// A column at the start does not fill, so its width is the scroll area's, capped (a size change only sets a width; no view is rebuilt).
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
		_header.Title = title;
		_header.ButtonVisible = false; // no Done in the corner: the back arrow goes up one level, the coral pill leaves (Slice 325 decision 5)
		_header.BackRequested += async (_, _) => await Busy.RunAsync(CloseAsync);
		return _header;
	}
}

/// <summary>
/// The corner's Settings tab (Slice 325 part B, grown-up mock frame W1; was the pushed Settings page of Slice 172): "Reading" (Language, Story languages, Reading speed, Animal sounds) and "This phone" ("This
/// computer" on Windows: Backup, Usage counts, About &amp; updates, and Report a problem off Windows), each row showing its current value and opening its own page. The list is one column at most 620 dp wide
/// at every size. A row opens its page in this tab's own area (<see cref="ICornerSubPage"/>) with its own back arrow; the corner's chrome (bar or rail) stays. The store is never a row (Decision 167).
/// </summary>
internal sealed class SettingsTab : ContentView, ICornerTabView
{
	private const string ListMarkerId = "SettingsTitle"; // the list's first heading: flows wait for it to know the Settings list is on screen (the page that owned this id, with its own title, is gone)
	private const string ReadingSpeedRowId = "SettingsReadingSpeedRow";
	private const string ReportProblemRowId = "SettingsReportProblemRow";

	private readonly IServiceProvider _services;
	private readonly AppConfigClient _config;
	private readonly UsageCounter _usage;
	private readonly BusyGuard _busy = new();
	private readonly VerticalStackLayout _body = new() { Spacing = 10, Padding = new Thickness(0, 0, 0, 20), HorizontalOptions = LayoutOptions.Start };
	private readonly ScrollView _scroll;
	private readonly Grid _stage = new();
	private Page? _owner;
	private ICornerSubPage? _open;
	private View? _openView;
	private bool _appeared;
	private Size _window;

	public SettingsTab(IServiceProvider services, AppConfigClient config, UsageCounter usage)
	{
		_services = services;
		_config = config;
		_usage = usage;
		_scroll = new ScrollView { Content = _body };
		_scroll.SizeChanged += (_, _) => FitBody(_scroll.Width);
		_stage.Add(_scroll);
		Content = _stage;
		Unloaded += (_, _) => Release();
	}

	/// <summary>The gallery that shows this tab: the owner of the pages it hosts (their navigation and alerts).</summary>
	public void Attach(Page owner) => _owner = owner;

	/// <summary>True while a sub-page is open instead of the list: the gallery hides the tab's own title row and sends Android Back here first.</summary>
	public bool SubPageOpen => _open is not null;

	/// <summary>A sub-page opened or closed.</summary>
	public event Action? SubPageChanged;

	public void Appearing()
	{
		_appeared = true;
		_config.Changed -= OnConfigChanged; // never twice
		_config.Changed += OnConfigChanged;
		Rebuild();
		_open?.HostAppearing();
	}

	public void Disappearing()
	{
		Release();
		_open?.HostDisappearing();
	}

	// Lets go of what the tab subscribed to (Disappearing and Unloaded both come here).
	private void Release()
	{
		_config.Changed -= OnConfigChanged;
		_appeared = false;
	}

	/// <summary>The window's size, for the hosted page that arranges itself by it.</summary>
	public void ApplyShape(double windowWidth, double windowHeight)
	{
		_window = new Size(windowWidth, windowHeight);
		_open?.HostShape(windowWidth, windowHeight);
	}

	/// <summary>Back from a sub-page to the list; false when the list is showing (the caller then leaves the corner).</summary>
	public async Task<bool> GoBackAsync()
	{
		if (_open is not { } page)
		{
			return false;
		}

		await page.HostBackAsync();
		return true;
	}

	/// <summary>The corner was left: the next visit starts at the list. The sub-page is only discarded here, never sent its Back (that would raise alerts or rebuild the app in kids' mode); the ways out of the corner that
	/// must save or refuse go through the sub-page's own Back first (GalleryPage.CloseSettingsSubPageAsync).</summary>
	public void Reset()
	{
		CloseSubPage(); // synchronous: nothing is left running unobserved while the other leave-the-corner work goes on, and a throw reaches the caller
	}

	private void OnConfigChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(Rebuild);

	private void FitBody(double available)
	{
		if (available > 0)
		{
			_body.WidthRequest = Math.Min(available, SettingsLayout.MaxContentWidth);
		}
	}

	// Values change on the sub-pages, so the list is built again each time it shows.
	private void Rebuild()
	{
		_body.Children.Clear();
		Grid[] reading = [LanguageRow(), StoryLanguagesRow(), SpeedRow(), AnimalRow()];
		Grid[] device = DeviceRows();
		_body.Children.Add(SettingsRows.GroupHeading(AppResources.Settings_GroupReading, ListMarkerId));
		_body.Children.Add(SettingsRows.RowsCard(reading));
		_body.Children.Add(SettingsRows.GroupHeading(DeviceGroupTitle()));
		_body.Children.Add(SettingsRows.RowsCard(device));
	}

	// Frame W1: the Windows head's second group is "This computer".
	private static string DeviceGroupTitle()
	{
#if WINDOWS
		return AppResources.Settings_GroupComputer;
#else
		return AppResources.Settings_GroupPhone;
#endif
	}

	private Grid LanguageRow() =>
		SettingsRows.NavRow(AppResources.Language_Title, LanguageNames.NativeName(LanguageChoice.Current()), OpenAsync<LanguagePage>, _busy);

	// The app language and the side languages the child can switch between (Slice 173), like "English, فارسی".
	private Grid StoryLanguagesRow() =>
		SettingsRows.NavRow(AppResources.StoryLanguages_SettingsTitle, StoryLanguages.Names(StoryLanguages.Current().All), OpenAsync<StoryLanguagesPage>, _busy);

	private Grid SpeedRow() =>
		SettingsRows.NavRow(AppResources.Speed_Title, AppResources.Format(AppResources.Speed_Value, ReadingSpeedPreference.Load().Value), OpenAsync<ReadingSpeedPage>, _busy, automationId: ReadingSpeedRowId);

	private Grid AnimalRow() =>
		SettingsRows.NavRow(AppResources.Settings_AnimalSounds_Title, SettingsRows.OnOff(AnimalSoundsPreference.Load()), OpenAsync<AnimalSoundsPage>, _busy);

	// Slice 249: "Report a problem" follows About; the Windows head has no crash reporting, so it has no row.
	private Grid[] DeviceRows()
	{
#if WINDOWS
		return [BackupRow(), UsageRow(), AboutRow()];
#else
		return [BackupRow(), UsageRow(), AboutRow(), ReportProblemRow()];
#endif
	}

	private Grid ReportProblemRow() =>
		SettingsRows.NavRow(AppResources.ReportProblem_Title, null, () => ShowAsync(new ReportProblemPage()), _busy, automationId: ReportProblemRowId);

	private Grid BackupRow() => SettingsRows.NavRow(AppResources.Backup_Title, null, OpenAsync<BackupPage>, _busy);

	private Grid UsageRow() =>
		SettingsRows.NavRow(AppResources.Usage_Title, SettingsRows.OnOff(_usage.Enabled), OpenAsync<UsageCountsPage>, _busy);

	// The "New version" tag shows only while the remote config names a newer release.
	private Grid AboutRow()
	{
		string? tag = _config.NewerVersionExists ? AppResources.Settings_NewVersion : null;
		return SettingsRows.NavRow(AppResources.Settings_About, null, OpenAsync<AboutPage>, _busy, tag);
	}

	private Task OpenAsync<TPage>() where TPage : Page, ICornerSubPage => ShowAsync(_services.GetRequiredService<TPage>());

	// The one hosting path (Slice 325 B2): the page's content goes into this tab's stage in place of the list.
	private Task ShowAsync(ICornerSubPage page)
	{
		if (_open is not null || _owner is null)
		{
			return Task.CompletedTask;
		}

		_open = page;
		_openView = page.HostIn(_owner, CloseSubPageAsync);
		_scroll.IsVisible = false;
		_stage.Add(_openView);
		page.HostShape(_window.Width, _window.Height);
		if (_appeared)
		{
			page.HostAppearing();
		}

		UiScreen.Show((page is Page hosted ? UiScreen.ScreenOf(hosted) : null) ?? UiScreens.CornerSettings); // Slice 352: a hosted page is a screen of its own
		AppTrace.Line(TraceLines.PageShown(page.GetType().Name)); // a hosted page is never pushed, so Application.PageAppearing does not see it: the report's log tail still names it (report-problem flow)
#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write($"[Redesign] settings page={page.GetType().Name} hosted=true");
#endif
		SubPageChanged?.Invoke();
		return Task.CompletedTask;
	}

	private Task CloseSubPageAsync()
	{
		CloseSubPage();
		return Task.CompletedTask;
	}

	private void DetachOpenView()
	{
		if (_openView is not null)
		{
			_stage.Remove(_openView);
			_openView = null;
		}
	}

	private void CloseSubPage()
	{
		if (_open is not { } page)
		{
			return;
		}

		_open = null;
		try
		{
			if (_appeared)
			{
				page.HostDisappearing();
			}

			AppTrace.Line(TraceLines.PageLeft(page.GetType().Name)); // the twin of the page shown line above
		}
		finally
		{
			DetachOpenView(); // even when the sub-page throws while leaving, its view must not stay attached for the next visit
			ShowListAgain(); // and the list must show again, whatever the sub-page did while leaving: the exception still goes on to the caller
		}
	}

	private void ShowListAgain()
	{
		_scroll.IsVisible = true;
		Rebuild();
		if (_appeared)
		{
			UiScreen.Show(UiScreens.CornerSettings); // Slice 352: back from a settings page to the list
		}

#if DEBUG
		Sirous.Reading.Mobile.Shared.Testing.TestHookLog.Write("[Redesign] settings page=list hosted=false");
#endif
		SubPageChanged?.Invoke();
	}

	// Slice 235 part C (mock Batch 46 frame 157): on the Windows window the Right arrow opens the focused row, like Enter does. Up and down move the focus by the window's own arrow-key navigation.
	// False when the list is not showing or the focus is not on a row, so the arrow goes on to the focused control. Nothing reaches this tab off Windows.
	public bool OpenFocusedRow()
	{
#if WINDOWS
		if (_open is null
			&& Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement { XamlRoot: { } root }
			&& Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(root) is Microsoft.UI.Xaml.Controls.Button { IsEnabled: true } button
			&& IsRowButton(button)
			&& new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(button) is { } peer)
		{
			WindowTrace.Write("Dino keyboard: the Right arrow opened a Settings row");
			peer.Invoke();
			return true;
		}
#endif
		return false;
	}

#if WINDOWS
	// Only a Settings row opens with Right; a button of a hosted page is left to the arrow (which then moves the focus).
	private bool IsRowButton(Microsoft.UI.Xaml.Controls.Button focused) => RowButtonsUnder(_scroll).Any(row => ReferenceEquals(row.Handler?.PlatformView, focused));

	private static IEnumerable<RowButton> RowButtonsUnder(IVisualTreeElement element)
	{
		foreach (IVisualTreeElement child in element.GetVisualChildren())
		{
			if (child is RowButton row)
			{
				yield return row;
			}

			foreach (RowButton nested in RowButtonsUnder(child))
			{
				yield return nested;
			}
		}
	}
#endif
}
