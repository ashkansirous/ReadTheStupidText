#if DEBUG
using System.Reflection;
using System.Runtime.CompilerServices;
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Domain.Reading;
using ReadTheStupidText.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Dino Reader's own Debug test command (Slice 175): <c>open-book id=&lt;starter&gt; [page=n] [play=1|page] [speed=2]</c> opens that book's reading screen on page n
/// (1-based, default 1), like a child tapping the tile, and with <c>play=1</c> reads from that page on to the end of the book on the app's own reader (the same session the Play
/// button uses); <c>play=page</c> stops once the page is read, so a timing run is finite. <c>id</c> is the book's package id, its story id or its title as a slug (<c>three-little-pigs</c>). It registers itself with the
/// dispatcher, so no app start-up code changes; Release has none of it. Outcome lines: <c>HOOK open-book opened ...</c> or <c>failed ...</c>;
/// with play, <c>HOOK read start</c> just before the read begins (the line the phone timing script counts from).
/// Slice 189: <c>enter-grown-up</c> puts the app in grown-up mode as a passed gate does (the gate's hold and sum are not scriptable), so a flow can
/// start from the grown-up page; the gate itself is tapped through by hand. Outcome: <c>HOOK enter-grown-up ok</c>.
/// </summary>
internal static class DinoTestCommands
{
	private const string Name = "open-book";
	private const string EnterGrownUpName = "enter-grown-up";
	private const string IdKey = "id";
	private const string PageKey = "page";
	private const string PlayKey = "play";
	private const string PlayOn = "1";
	private const string PlayPageOnly = "page";
	private const string SpeedKey = "speed";
	private const string SessionField = "_session";
	private const int RetryMilliseconds = 250;
	private const int StableChecks = 4;
	private const int StableCheckMilliseconds = 500;
	private const int StableLimitMilliseconds = 20000;
	private const int WaitMilliseconds = 30000;
	private const int ImportWaitMilliseconds = 20000;

	// A module initializer on purpose: the command registers itself, so the app's start-up code (outside this slice) stays untouched.
#pragma warning disable CA2255
	[ModuleInitializer]
#pragma warning restore CA2255
	internal static void Register()
	{
		TestHookDispatcher.AppCommands = Run;
		TestHookDispatcher.AppLaunchKeys = [IdKey, PageKey, PlayKey];
	}

	private static string? Run(TestHookCommand command)
	{
		if (command.Name == EnterGrownUpName)
		{
			_ = EnterGrownUpAsync();
			return TestHookDispatcher.Ok;
		}

		if (command.Name != Name)
		{
			return null;
		}

		_ = OpenAsync(command);
		return TestHookDispatcher.Ok;
	}

	// Waits for the gallery to be the window's root (a new window starts in kids' mode and would undo an earlier Enter), then enters grown-up mode on the UI thread.
	// The one-time "Storyteller voices are ready" scrim would cover the gear and the book "..." buttons, so the hook marks it as seen before entering.
	private static void EnterAcknowledgingReadyNote()
	{
		IServiceProvider services = IPlatformApplication.Current!.Services;
		services.GetRequiredService<StorytellerDefault>().AcknowledgeReady();
		services.GetRequiredService<GrownUpMode>().Enter();
	}

	// The window's start-up stop/resume calls can still reset kids' mode: enter, then wait until grown-up mode has stayed on for several checks in a row (entering again when it was reset), within a bound.
	private static async Task<bool> EnterUntilStableAsync()
	{
		GrownUpMode mode = IPlatformApplication.Current!.Services.GetRequiredService<GrownUpMode>();
		DateTime deadline = DateTime.UtcNow.AddMilliseconds(StableLimitMilliseconds);
		int stable = 0;
		while (DateTime.UtcNow < deadline)
		{
			if (!await MainThread.InvokeOnMainThreadAsync(() => mode.IsGrownUp))
			{
				stable = 0;
				await MainThread.InvokeOnMainThreadAsync(EnterAcknowledgingReadyNote);
			}
			else if (++stable >= StableChecks)
			{
				return true;
			}

			await Task.Delay(StableCheckMilliseconds);
		}

		return false;
	}

	private static async Task EnterGrownUpAsync()
	{
		try
		{
			NavigationPage? root = await WaitForAsync(() => Task.FromResult(GalleryRoot()));
			if (root is null)
			{
				Log($"{EnterGrownUpName} failed no gallery");
				return;
			}

			if (!await EnterUntilStableAsync())
			{
				Log($"{EnterGrownUpName} failed mode kept resetting");
				return;
			}

			Log($"{EnterGrownUpName} entered");
		}
		catch (Exception ex)
		{
			Log($"{EnterGrownUpName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	private static async Task OpenAsync(TestHookCommand command)
	{
		try
		{
			string id = command.Get(IdKey, string.Empty);
			int page = int.TryParse(command.Get(PageKey, "1"), out int parsed) && parsed >= 1 ? parsed : 1;
			string play = command.Get(PlayKey, string.Empty);
			if (!TrySetSpeed(command.Get(SpeedKey, string.Empty)))
			{
				Log($"{Name} failed bad speed");
				return;
			}

			await OpenBookAsync(id, page, play);
		}
		catch (Exception ex)
		{
			Log($"{Name} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	// The reader's own speed, as the read hook sets it (nothing is stored); no speed given leaves it alone.
	private static bool TrySetSpeed(string text)
	{
		if (text.Length == 0)
		{
			return true;
		}

		if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double speed))
		{
			return false;
		}

		IPlatformApplication.Current!.Services.GetRequiredService<ISpeechReader>().SetSpeed(new PlaybackRate(speed));
		return true;
	}

	private static async Task OpenBookAsync(string id, int page, string play)
	{
		if (id.Trim().Length == 0)
		{
			Log($"{Name} failed no book");
			return;
		}

		IServiceProvider services = IPlatformApplication.Current!.Services;
		NavigationPage? root = await WaitForAsync(() => Task.FromResult(GalleryRoot()));
		if (root is null)
		{
			Log($"{Name} failed no gallery");
			return;
		}

		string? refusal = Refusal(root, services.GetRequiredService<GrownUpMode>());
		Story? story = refusal is null ? await FindWhenImportedAsync(services.GetRequiredService<IStoryLibrary>(), id) : null;
		if (refusal is not null || story is null)
		{
			Log($"{Name} failed {refusal ?? "no book " + id}");
			return;
		}

		ReadingPage? reading = await MainThread.InvokeOnMainThreadAsync(() => ReadingForAsync(services, root, story));
		if (reading is null)
		{
			Log($"{Name} failed no reading screen");
			return;
		}

		await MainThread.InvokeOnMainThreadAsync(() => GoToPageAsync(reading, story, page, play));
	}

	// The starter books are imported one after another on a cold start, so "no book" only counts once the import had its time (bounded).
	private static Task<Story?> FindWhenImportedAsync(IStoryLibrary library, string id)
	{
		return WaitForAsync(async () => BookMatch.Find(await library.ListAsync(), id), ImportWaitMilliseconds);
	}

	// On the UI thread, the same way a tile tap opens it (StoryActions.ReadAsync); the page it pushed is the top of the stack.
	private static async Task<ReadingPage?> OpenReadingAsync(IServiceProvider services, NavigationPage root, Story story)
	{
		await services.GetRequiredService<StoryActions>().ReadAsync(root.Navigation, story);
		return root.Navigation.NavigationStack.LastOrDefault() as ReadingPage;
	}

	// The states in which a child cannot open a book from the gallery: grown-up mode (a tap edits), the intro wizard over the gallery (a reading screen already open is reused).
	private static string? Refusal(NavigationPage root, GrownUpMode mode)
	{
		if (mode.IsGrownUp)
		{
			return "grown-up";
		}

		if (root.Navigation.ModalStack.Any(p => p is NavigationPage { RootPage: IntroWizardPage }))
		{
			return "intro";
		}

		return null;
	}

	// Slice 177: the reading screen on top of the stack is reused only when it is a ReadingPage loaded for this same book (a second read in the same
	// process, the warm timing run). Any other ReadingPage in the stack (under another page, or for another book) is closed first by going back to the
	// gallery, so a second reading screen is never pushed. On the UI thread, which owns the navigation stack.
	private static async Task<ReadingPage?> ReadingForAsync(IServiceProvider services, NavigationPage root, Story story)
	{
		if (root.Navigation.NavigationStack.LastOrDefault() is ReadingPage top && top.LoadedStoryId == story.Id)
		{
			return top;
		}

		if (root.Navigation.NavigationStack.Any(p => p is ReadingPage))
		{
			await root.Navigation.PopToRootAsync();
		}

		return await OpenReadingAsync(services, root, story);
	}

	// The gallery is the root of the window's navigation once first run is done; before that there is nothing to open a book from.
	private static NavigationPage? GalleryRoot() =>
		Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page is NavigationPage { RootPage: GalleryPage } nav ? nav : null;

	private static async Task<T?> WaitForAsync<T>(Func<Task<T?>> probe, int limitMilliseconds = WaitMilliseconds) where T : class
	{
		for (int waited = 0; waited < limitMilliseconds; waited += RetryMilliseconds)
		{
			if (await probe() is { } found)
			{
				return found;
			}

			await Task.Delay(RetryMilliseconds);
		}

		return null;
	}

	private static async Task GoToPageAsync(ReadingPage reading, Story story, int page, string play)
	{
		(int First, int Last)? range = BookMatch.SentencesOfPage(story, page - 1);
		FieldInfo? field = typeof(ReadingPage).GetField(SessionField, BindingFlags.Instance | BindingFlags.NonPublic);
		if (range is not { } sentences || field?.GetValue(reading) is not ReadingSession session)
		{
			Log($"{Name} failed page {page} of {story.Pages.Count}");
			return;
		}

		Log($"{Name} opened page={page} of={story.Pages.Count} sentence={sentences.First}");
		if (play != PlayOn && play != PlayPageOnly)
		{
			session.MoveTo(sentences.First);
			return;
		}

		if (play == PlayPageOnly)
		{
			StopAfter(session, sentences.Last);
		}

		TestHookLog.NoteReadRequested();
		Log("read start");
		await session.StartAtAsync(sentences.First);
	}

	// Stops the read as soon as it moves past the page's last sentence (once), so a one-page timing run ends by itself.
	private static void StopAfter(ReadingSession session, int lastSentence)
	{
		EventHandler<int>? handler = null;
		handler = (_, sentence) =>
		{
			if (sentence > lastSentence)
			{
				session.SentenceChanged -= handler;
				MainThread.BeginInvokeOnMainThread(session.Stop);
			}
		};
		session.SentenceChanged += handler;
	}

	private static void Log(string text) => TestHookLog.Write($"{TestHookLog.HookPrefix} {text}");
}
#endif
