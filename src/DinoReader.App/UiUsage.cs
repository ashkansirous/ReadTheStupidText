using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DinoReader;

/// <summary>
/// Slice 352: the screen the app is on. Pages report themselves through <see cref="Show"/> (most through <c>DinoApp</c>'s page-appearing hook and <see cref="ScreenOf"/>; the wizard and the grown-ups' corner name
/// their own). Every count is stamped with <see cref="Current"/> (<c>blob20</c>), and a screen change is one <c>screen</c> count with the action <c>shown</c>.
/// </summary>
internal static class UiScreen
{
	private static readonly UiScreenTracker Tracker = new();
	private static readonly object Gate = new();

	/// <summary>The screen now shown, or <see cref="UiScreens.None"/> before the first page.</summary>
	public static string Current
	{
		get
		{
			lock (Gate)
			{
				return Tracker.Current;
			}
		}
	}

	/// <summary>Makes <paramref name="screen"/> the current screen; counts <c>screen:shown</c> only when it changed.</summary>
	public static void Show(string screen, object? owner = null)
	{
		bool changed;
		lock (Gate)
		{
			changed = Tracker.Show(screen, owner);
		}

		if (changed)
		{
			CountShown();
		}
	}

	private static readonly ShownCountedThisLaunch ShownCounted = new();

	/// <summary>Counts the current screen as shown; when the counter really recorded it, the launch count has nothing left to add.</summary>
	private static void CountShown()
	{
		if (UiUsage.Screen(UiScreenActions.Shown))
		{
			ShownCounted.Recorded();
		}
	}

	/// <summary>Called right after the launch's <c>app_opened</c> is counted: counts the current screen as shown unless a <c>screen:shown</c> was already recorded this launch (the first page appeared before the config said counting is on, so its count was dropped).</summary>
	public static void CountLaunchScreen()
	{
		if (ShownCounted.TryClaim())
		{
			UiUsage.Screen(UiScreenActions.Shown);
		}
	}

	/// <summary>The modal page <paramref name="modal"/> closed: puts the screen beneath it back (counted as shown) only when that modal is the one that showed the current screen and the page below raised no appearing event of its own.</summary>
	public static void ModalClosed(Page modal)
	{
		object closed = modal is NavigationPage { RootPage: { } root } ? root : modal;
		string? restored;
		lock (Gate)
		{
			restored = Tracker.Restore(closed);
		}

		if (restored is not null)
		{
			UiUsage.Screen(UiScreenActions.Shown);
		}
	}

	/// <summary>The screen a page of this type is, or null for a page that is not a screen of its own (a host page, a modal wrapper).</summary>
	public static string? ScreenOf(Page page) => page switch
	{
		FirstBooksPage => UiScreens.FirstBooks,
		FirstRunPage => UiScreens.FirstRun,
		ReadingPage => UiScreens.Reading,
		GatePage => UiScreens.Gate,
		RecordIntroPage => UiScreens.RecordIntro,
		RecordPage => UiScreens.Record,
		UpdatePage => UiScreens.Update,
		CloseQuestionPage => UiScreens.CloseQuestion,
		DropLanguageSheet => UiScreens.DropLanguage,
		LanguagePage => UiScreens.Language,
		BackupPage => UiScreens.Backup,
		StoryLanguagesPage => UiScreens.StoryLanguages,
		ReadingSpeedPage => UiScreens.ReadingSpeed,
		AnimalSoundsPage => UiScreens.AnimalSounds,
		UsageCountsPage => UiScreens.UsageCounts,
		AboutPage => UiScreens.About,
		ReportProblemPage => UiScreens.ReportProblem,
		_ => null,
	};

	/// <summary>The gallery page's screen: the child's shelf, or the grown-ups' corner tab that is open.</summary>
	public static string OfGallery(bool grownUp, CornerTab tab) => !grownUp ? UiScreens.Shelf : tab switch
	{
		CornerTab.Store => UiScreens.CornerStore,
		CornerTab.Settings => UiScreens.CornerSettings,
		_ => UiScreens.CornerBooks,
	};
}

/// <summary>
/// Slice 352: the one way the app counts a screen action, a button press or a changed setting. Every method counts only closed values (<see cref="UiScreens"/>, <see cref="UiControls"/>, <see cref="UiSettings"/>), is about no book,
/// goes through <see cref="UsageCounter"/> (so the grown-up's switch, the remote <c>telemetry.enabled</c> and the production-build gate all apply), and never throws or changes what the control does.
/// <c>SilentButtonGuardTests</c> fails when a button or tap handler in this app has no call from here.
/// </summary>
internal static class UiUsage
{
	/// <summary>Counts <c>screen: &lt;current screen&gt;:&lt;action&gt;</c> (a <see cref="UiScreenActions"/> name).</summary>
	public static bool Screen(string action) => ScreenOf(UiScreen.Current, action);

	/// <summary>Counts <c>screen:&lt;screen&gt;:&lt;action&gt;</c> for a screen that is not the current one yet (the wizard names its own step); true when the counter queued it.</summary>
	public static bool ScreenOf(string screen, string action) =>
		screen != UiScreens.None && Record(UsageEventName.Screen, UiUsageDetail.Screen(screen, action));

	/// <summary>Counts a press of <paramref name="control"/> (a <see cref="UiControls"/> name) on the current screen.</summary>
	public static void Tap(string control)
	{
		string screen = UiScreen.Current;
		if (screen != UiScreens.None)
		{
			Record(UsageEventName.Tap, UiUsageDetail.Tap(screen, control));
		}
	}

	/// <summary>Counts that <paramref name="setting"/> now has <paramref name="value"/> (both from <see cref="UiSettings.Values"/>); anything else is dropped here, before it is queued.</summary>
	public static void Setting(string setting, string value)
	{
		if (UiSettings.Values.TryGetValue(setting, out IReadOnlyList<string>? allowed) && allowed.Contains(value))
		{
			Record(UsageEventName.SettingChanged, UiUsageDetail.Setting(setting, value));
		}
	}

	/// <summary>
	/// The one way a button gets a counted press: on every press it reads the button's live <c>IsEnabled</c> and the current screen, counts <paramref name="control"/>, and only then runs <paramref name="handler"/>, so the
	/// handler can change the screen (or disable the button) without the count carrying the wrong screen. The count is never registered apart from its handler.
	/// </summary>
	public static T Press<T>(T button, string control, Action handler)
		where T : Button
	{
		button.Clicked += (_, _) => CountThenRun(button, control, handler);
		return button;
	}

	/// <summary><see cref="Press{T}(T, string, Action)"/> for a <see cref="CoralButton"/> (the app's own pill button, which is not a <see cref="Button"/>).</summary>
	public static CoralButton Press(CoralButton button, string control, Action handler)
	{
		button.Clicked += (_, _) => CountThenRun(button, control, handler);
		return button;
	}

	/// <summary><see cref="Press{T}(T, string, Action)"/> for a <see cref="PillButton"/>.</summary>
	public static PillButton Press(PillButton button, string control, Action handler)
	{
		button.Clicked += (_, _) => CountThenRun(button, control, handler);
		return button;
	}

	/// <summary>Counts <paramref name="control"/> on the current screen when <paramref name="view"/> is enabled right now, then runs <paramref name="handler"/> either way.</summary>
	public static void CountThenRun(View view, string control, Action handler)
	{
		if (view.IsEnabled)
		{
			Tap(control);
		}

		handler();
	}

	/// <summary>Counts the app going to the background, with the screen it was on; called just before the queue is flushed.</summary>
	public static void Backgrounded()
	{
		Lifecycle.Backgrounded();
		Record(UsageEventName.AppBackgrounded, UiScreen.Current);
	}

	/// <summary>Counts the app coming back to the front; not the resume some platforms raise at cold start, which follows no background.</summary>
	public static void Resumed()
	{
		if (Lifecycle.Resumed())
		{
			Record(UsageEventName.AppResumed, UiScreen.Current);
		}
	}

	private static readonly UiLifecycle Lifecycle = new();

	private static bool Record(string name, string detail)
	{
		try
		{
			UsageCounter? counter = IPlatformApplication.Current?.Services.GetService<UsageCounter>();
			return counter?.RecordGeneral(name, UsageEvent.NoLanguage, detail) ?? false;
		}
		catch (Exception)
		{
			// no-fallback: counting is best effort and never reports itself or changes what the control does
			return false;
		}
	}
}
