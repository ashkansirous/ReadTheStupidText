using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>
/// A blank cream page that stands in as the root while a restart resumes the onboarding at its listen or voice step (Slice 362): the step needs the demo book from the library, which is read asynchronously, so the page appears
/// first and the route replaces it as soon as it is on screen.
/// </summary>
internal sealed class OnboardingResumePage : ContentPage
{
	private readonly IServiceProvider _services;
	private readonly OnboardingStep _step;
	private readonly WaitOverlay _waitOverlay = DinoWait.OverPage();
	private readonly WaitController _wait;
	private bool _started;

	public OnboardingResumePage(IServiceProvider services, OnboardingStep step)
	{
		_services = services;
		_step = step;
		NavigationPage.SetHasNavigationBar(this, false);
		BackgroundColor = DinoTokens.Get(DinoTokens.Cream);
		AutomationId = "OnboardingResume";
		_wait = new WaitController(_waitOverlay);
		Content = new Grid { Children = { _waitOverlay } };
	}

	// The book is read from the library first; behind the shared wait overlay (shown only after 500 ms) so a slow disk never leaves a bare page.
	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (!_started)
		{
			_started = true;
			_ = _wait.RunAsync(WaitStage.GettingFile, AppResources.Wait_GettingBook, canCancel: false, async _ =>
			{
				await OnboardingRoute.ShowStepAsync(_services, _step);
				return true;
			});
		}
	}
}

/// <summary>
/// The path from the books to the shelf (Slice 362): which screen follows which, each one a root of its own so Back can never return to an earlier step. Listen and voice need one of the four free books on the shelf (Decision 2);
/// with none (a debug shelf, a failed read) they are passed over and the path goes on to the corner tour. A step that throws is reported and passed over too: onboarding never traps a person on a broken screen.
/// </summary>
internal static class OnboardingRoute
{
	/// <summary>The shelf holds the grown-up's books: counts the books step, starts the pre-made audio of every added story language (the books were not on the shelf when the languages were picked), then shows the listen step.</summary>
	public static async Task AfterBooksAsync(IServiceProvider services)
	{
		StartShelfAudio(services);
		OnboardingUsage.StepDone(services.GetRequiredService<UsageCounter>(), OnboardingStep.Books);
		await ShowStepAsync(services, OnboardingStep.Listen);
	}

	// Every added language starts its book audio at once, on any network (Slice 360, Decision 3: no question).
	private static void StartShelfAudio(IServiceProvider services)
	{
		ShelfAudioPacks shelf = services.GetRequiredService<ShelfAudioPacks>();
		foreach (string language in StoryLanguages.Current().Sides)
		{
			shelf.Start(language);
		}
	}

	/// <summary>Shows <paramref name="step"/> as the window's root, or the next one when it has nothing to show.</summary>
	public static async Task ShowStepAsync(IServiceProvider services, OnboardingStep step)
	{
		OnboardingState.Begin(step); // saved before any async read, so a kill while the library loads still resumes at this step
		try
		{
			await DeleteAbandonedTryAsync(services);
			await ShowCoreAsync(services, step);
		}
		catch (Exception ex)
		{
			// no-fallback: a step that cannot be built is passed over so the grown-up is never stuck; the failure is logged
			Debug.WriteLine($"Dino Reader could not show onboarding step {step}: {ex}");
			await MoveOnAsync(services, step);
		}
	}

	private static async Task ShowCoreAsync(IServiceProvider services, OnboardingStep step)
	{
		switch (step)
		{
			case OnboardingStep.Listen when await DemoAsync(services) is { } found:
				ShowListen(services, found.Demo, found.Story, OnboardingListenKind.Listen);
				return;
			case OnboardingStep.Voice when await DemoAsync(services) is { } found:
				ShowVoice(services, found.Demo, found.Story);
				return;
			case OnboardingStep.Corner:
				ShowCorner(services);
				return;
			default:
				await MoveOnAsync(services, step); // no demo book on the shelf: this step and its pair have nothing to play
				return;
		}
	}

	// A step with nothing to show, or one that failed: Listen goes to the corner (the voice step needs the same page), Voice and Corner go to the corner. A corner that cannot be built goes to the plain shelf and onboarding is
	// saved as done, so a restart never lands on a blank resume page.
	private static async Task MoveOnAsync(IServiceProvider services, OnboardingStep from)
	{
		if (from != OnboardingStep.Corner)
		{
			try
			{
				OnboardingState.Begin(OnboardingStep.Corner);
				await ShowCoreAsync(services, OnboardingStep.Corner);
				return;
			}
			catch (Exception ex)
			{
				// no-fallback: the corner could not be built either; the plain shelf below is the way on and the failure is logged
				Debug.WriteLine($"Dino Reader could not show the corner tour: {ex}");
			}
		}

		ShowShelfInstead(services);
	}

	private static void ShowShelfInstead(IServiceProvider services)
	{
		OnboardingState.Clear();
		FirstRunState.HandoverPending = false;
		OnboardingUsage.Finished(services.GetRequiredService<UsageCounter>()); // the same finished event as the tour's end, so intro_started always has its intro_ended
		((DinoApp)Microsoft.Maui.Controls.Application.Current!).ReplaceRoot(services.GetRequiredService<GalleryPage>());
	}

	private static void ShowListen(IServiceProvider services, OnboardingDemoPage demo, Story story, OnboardingListenKind kind)
	{
		OnboardingState.Begin(kind == OnboardingListenKind.Listen ? OnboardingStep.Listen : OnboardingStep.Voice); // a restart during "That's you" resumes the voice step from its start
		var page = services.GetRequiredService<ReadingPage>();
		Func<Task> continued = kind == OnboardingListenKind.Listen ? () => ListenDoneAsync(services) : () => VoiceDoneAsync(services, tried: true);
		page.EnterOnboarding(new OnboardingListenPlan(demo, story, kind), continued);
		((DinoApp)Microsoft.Maui.Controls.Application.Current!).ReplaceRoot(page);
	}

	private static void ShowVoice(IServiceProvider services, OnboardingDemoPage demo, Story story)
	{
		OnboardingState.Begin(OnboardingStep.Voice);
		var page = new OnboardingVoicePage(services, services.GetRequiredService<IStoryLibrary>(), services.GetRequiredService<UsageCounter>());
		page.Load(demo, story);
		((DinoApp)Microsoft.Maui.Controls.Application.Current!).ReplaceRoot(page);
	}

	/// <summary>The recording is in: "That's you" (mock N7) plays the demo page with the grown-up's clips, in the same step.</summary>
	public static async Task ShowHearYourselfAsync(IServiceProvider services, OnboardingDemoPage demo)
	{
		IStoryLibrary library = services.GetRequiredService<IStoryLibrary>();
		Story? fresh = (await library.ListAsync()).FirstOrDefault(s => string.Equals(s.SourceId, demo.StoreId, StringComparison.OrdinalIgnoreCase));
		if (fresh is null)
		{
			await VoiceDoneAsync(services, tried: true);
			return;
		}

		ShowListen(services, demo, fresh, OnboardingListenKind.HearYourself);
	}

	private static Task ListenDoneAsync(IServiceProvider services)
	{
		OnboardingUsage.StepDone(services.GetRequiredService<UsageCounter>(), OnboardingStep.Listen);
		return ShowStepAsync(services, OnboardingStep.Voice);
	}

	/// <summary>The voice step is over (Continue on N7, or Continue without recording when the microphone is off): the try is deleted and the corner tour starts.</summary>
	public static async Task VoiceDoneAsync(IServiceProvider services, bool tried)
	{
		OnboardingUsage.StepDone(services.GetRequiredService<UsageCounter>(), OnboardingStep.Voice, tried);
		// The child leaves the voice step: exactly the clips recorded in it go now (a failure keeps the record, reports the fallback, and the gallery retries). One book's delete, well under 500 ms, so no wait overlay.
		await OnboardingTryStore.DeleteAsync(services.GetRequiredService<IStoryLibrary>());
		await ShowStepAsync(services, OnboardingStep.Corner);
	}

	private static void ShowCorner(IServiceProvider services)
	{
		services.GetRequiredService<GrownUpMode>().Enter(); // the grown-up is the one setting up: the tour opens in the corner (GalleryPage starts it)
		((DinoApp)Microsoft.Maui.Controls.Application.Current!).ReplaceRoot(services.GetRequiredService<GalleryPage>());
	}

	/// <summary>An abandoned try (the setup was left on the voice step or killed, or an earlier delete failed) is deleted at every onboarding step and every gallery appearing, unless onboarding is on the voice step. Nothing happens without a record.</summary>
	public static Task DeleteAbandonedTryAsync(IServiceProvider services) =>
		OnboardingTryStore.DeleteAbandonedAsync(services.GetRequiredService<IStoryLibrary>());

	/// <summary>The tour is over (Start reading on the child's shelf): onboarding is finished, later launches open on the shelf.</summary>
	public static void Finish(IServiceProvider services)
	{
		OnboardingState.Clear();
		FirstRunState.HandoverPending = false;
		OnboardingUsage.Finished(services.GetRequiredService<UsageCounter>());
	}

	private static async Task<(OnboardingDemoPage Demo, Story Story)?> DemoAsync(IServiceProvider services)
	{
		IReadOnlyList<Story> shelf = await services.GetRequiredService<IStoryLibrary>().ListAsync();
		OnboardingDemoPage? demo = OnboardingDemo.Pick(shelf.Select(s => s.SourceId));
		Story? story = demo is null ? null : shelf.FirstOrDefault(s => string.Equals(s.SourceId, demo.StoreId, StringComparison.OrdinalIgnoreCase));
		return demo is null || story is null || story.Pages.Count <= demo.PageIndex ? null : (demo, story);
	}
}
