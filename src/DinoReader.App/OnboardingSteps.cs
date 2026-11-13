using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Maui.Controls.Shapes;
using ReadTheStupidText.Mobile.Shared.Waiting;

namespace DinoReader;

/// <summary>What the Record page is told when the onboarding's voice step opens it (Slice 362): the name of the character whose lines are recorded, shown in its header ("Pim's lines").</summary>
internal sealed record OnboardingRecordMode(string CharacterName);

/// <summary>
/// Where a new install is on the onboarding path after its books (Slice 362): the saved step, so a restart resumes at the same screen. Steps 1 to 3 are held by the language and book gates
/// (<see cref="FirstRunState"/>, <see cref="FirstBooksGate"/>); this holds the listen, voice and corner steps. Absent means onboarding is not running: never started, or finished. The old wizard's saved step is another key, so an old install's half-done wizard never lands in the new path.
/// </summary>
internal static class OnboardingState
{
	private const string StepKey = "dino.onboarding.step.v1";
	private const int NotRunning = -1;

	/// <summary>The step to resume at, or null when onboarding is not running.</summary>
	public static OnboardingStep? Step => OnboardingFlow.Saved(Preferences.Default.Get(StepKey, NotRunning));

	public static bool InProgress => Step is not null;

	/// <summary>Saves <paramref name="step"/> (listen, voice or corner) as the one a restart resumes at.</summary>
	public static void Begin(OnboardingStep step) => Preferences.Default.Set(StepKey, (int)step);

	/// <summary>Onboarding is over, or was skipped by a debug hook: nothing resumes.</summary>
	public static void Clear() => Preferences.Default.Set(StepKey, NotRunning);
}

/// <summary>
/// The one owned record of the voice step's try (Slice 362, Decision 1; <see cref="OnboardingTryRecord"/>): the book and the exact clips recorded in the step, saved the moment a clip is recorded and kept in a preference.
/// Adding and deleting run under one async lock, so the gallery and the steps never delete at the same time. The record is cleared only after every listed clip is gone; a failure keeps it, reports the fallback and the next start retries.
/// </summary>
internal static class OnboardingTryStore
{
	private const string RecordKey = "dino.onboarding.tryrecord.v1";
	private static readonly SemaphoreSlim Lock = new(1, 1);

	public static OnboardingTryRecord? Current => OnboardingTryRecord.FromJson(Preferences.Default.Get(RecordKey, string.Empty));

	/// <summary>A clip was recorded in the voice step: it joins the record at once (before anything else can lose track of it).</summary>
	public static async Task AddAsync(IStoryLibrary library, string bookId, StoryClipKey key, string file)
	{
		await Lock.WaitAsync();
		try
		{
			// The clip joins whatever record exists (also one of another book, left by a failed deletion): ids are never dropped, the next deletion settles every book.
			Preferences.Default.Set(RecordKey, (Current ?? new OnboardingTryRecord(bookId, [])).With(key, file, bookId).ToJson());
		}
		finally
		{
			Lock.Release();
		}
	}

	/// <summary>Deletes exactly the recorded clips and clears the record; nothing happens without one.</summary>
	public static async Task DeleteAsync(IStoryLibrary library)
	{
		await Lock.WaitAsync();
		try
		{
			if (Current is { } record)
			{
				await DeleteLockedAsync(library, record);
			}
		}
		finally
		{
			Lock.Release();
		}
	}

	/// <summary>A start or the gallery appearing: deletes an abandoned try, unless onboarding is on the voice step, which still owns it.</summary>
	public static Task DeleteAbandonedAsync(IStoryLibrary library) =>
		OnboardingTryRules.DeleteAtStart(Current is not null, OnboardingState.Step) ? DeleteAsync(library) : Task.CompletedTask;

	private static async Task DeleteLockedAsync(IStoryLibrary library, OnboardingTryRecord record)
	{
		try
		{
			await OnboardingTry.DeleteAsync(library, record);
			Preferences.Default.Remove(RecordKey);
		}
		catch (Exception ex)
		{
			DinoFallbacks.Report(ReadTheStupidText.Application.Usage.FallbackArea.Library, ReadTheStupidText.Application.Usage.FallbackReason.OnboardingTryNotDeleted); // Slice 362: the record stays and the next start retries
			Debug.WriteLine($"Dino Reader could not delete the onboarding try: {ex}");
		}
	}
}

/// <summary>The words of the onboarding steps that are not a plain string: the step's name, the privacy card's title for this device, and the copy with <c>**bold**</c> words.</summary>
internal static class OnboardingText
{
	/// <summary>The name after "Step 3 of 6 ·" (N1 Language, N2 Story languages, N3 Books, N4 Listen, N5 to N7 Your voice).</summary>
	public static string StepName(OnboardingStep step) => step switch
	{
		OnboardingStep.Language => AppResources.Onboarding_Step_Language,
		OnboardingStep.StoryLanguages => AppResources.StoryLanguages_SettingsTitle,
		OnboardingStep.Books => AppResources.Onboarding_Step_Books,
		OnboardingStep.Listen => AppResources.Onboarding_Step_Listen,
		_ => AppResources.Record_YourVoice,
	};

	/// <summary>"Your voice stays on this phone / tablet / computer" (N5).</summary>
	public static string StaysTitle() => DeviceInfo.Idiom == DeviceIdiom.Tablet ? AppResources.Onboarding_StaysTitle_Tablet
		: DeviceInfo.Idiom == DeviceIdiom.Phone ? AppResources.Onboarding_StaysTitle_Phone
		: AppResources.Onboarding_StaysTitle_Device;

	/// <summary>Puts <paramref name="text"/> in <paramref name="label"/>, its <c>**bold**</c> words as bold spans (the label's colour, size and font apply to every span).</summary>
	public static void ShowMarkup(Label label, string text)
	{
		var formatted = new FormattedString();
		foreach (MarkupRun run in OnboardingMarkup.Split(text))
		{
			formatted.Spans.Add(new Span { Text = run.Text, FontAttributes = run.Bold ? FontAttributes.Bold : FontAttributes.None });
		}

		label.FormattedText = formatted;
	}
}

/// <summary>The onboarding's usage counts (Slice 362): the first run starts, each step is finished (tried) or passed over (skipped), and the path ends. Counted through <see cref="UsageCounter"/>, so production-only as everything else.</summary>
internal static class OnboardingUsage
{
	public static void Started(UsageCounter usage) =>
		usage.RecordGeneral(UsageEventName.IntroStarted, LanguageChoice.Current(), UsageDetail.IntroFirstRun);

	public static void StepDone(UsageCounter usage, OnboardingStep step, bool tried = true) =>
		usage.RecordGeneral(UsageEventName.IntroStep, LanguageChoice.Current(), UsageDetail.IntroStepAction(OnboardingFlow.NameOf(step), tried));

	public static void Finished(UsageCounter usage) =>
		usage.RecordGeneral(UsageEventName.IntroEnded, LanguageChoice.Current(), UsageDetail.IntroFinished);
}

/// <summary>
/// The step bar every onboarding screen starts with (mock A): six segments, 5 high, the done and current ones brand blue, the later ones sand, and under them "Step {0} of {1} · {2}" in 12.5 semibold.
/// Never mirrored, like the rest of the first run.
/// </summary>
internal sealed class OnboardingStepBar : VerticalStackLayout
{
	private const double SegmentHeight = 5;
	private const double SegmentGap = 5;
	private const double SegmentCorner = 3;
	private const double LabelSize = 12.5;
	private const string LaterFill = "#e6e0d4";

	public OnboardingStepBar(OnboardingStep step)
	{
		Spacing = 6;
		FlowDirection = FlowDirection.LeftToRight;
		var segments = new Grid { ColumnSpacing = SegmentGap };
		for (int i = 0; i < OnboardingFlow.Count; i++)
		{
			segments.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
			bool filled = i < OnboardingFlow.FilledAt(step);
			var segment = new Border
			{
				HeightRequest = SegmentHeight,
				StrokeThickness = 0,
				StrokeShape = new RoundRectangle { CornerRadius = SegmentCorner },
				BackgroundColor = filled ? DinoTokens.Get(DinoTokens.Brand) : Color.FromArgb(LaterFill),
			};
			segments.Add(segment, i, 0);
		}

		string text = AppResources.Format(AppResources.Onboarding_StepOf, OnboardingFlow.NumberOf(step), OnboardingFlow.Count, OnboardingText.StepName(step));
		var label = new Label { Text = text, FontSize = LabelSize, FontAttributes = FontAttributes.Bold, TextColor = DinoTokens.Get(DinoTokens.Body), FontFamily = DinoFonts.Ui };
		OwnScript.InOwnScriptAligned(label);
		SemanticProperties.SetDescription(label, text);
		Children.Add(segments);
		Children.Add(label);
		AutomationId = "OnboardingStepBar";
	}
}

/// <summary>
/// The amber ring that points at a control (mock <c>ringPulse</c>, N4b, N8b, N9, N10): a 4 dp ring of the highlight amber that grows by a few dp and fades, over and over. With reduce-motion on the ring stays and does not move.
/// Put it over (or around) the control it points at; it never takes a touch.
/// </summary>
internal sealed class PulseRing : Border
{
	private const string AmberRing = "#F2FFC24D"; // rgba(255,194,77,.95)
	private const double RingWidth = 4;
	private const double GrowBy = 0.16;
	private const uint CycleMilliseconds = 1400;
	private const string AnimationName = "onboardingPulse";

	public PulseRing(double corner)
	{
		InputTransparent = true;
		BackgroundColor = Colors.Transparent;
		Stroke = Color.FromArgb(AmberRing);
		StrokeThickness = RingWidth;
		StrokeShape = new RoundRectangle { CornerRadius = corner };
	}

	/// <summary>Starts the pulse (a still ring when motion is reduced).</summary>
	public void Start()
	{
		this.AbortAnimation(AnimationName);
		if (ReduceMotion.IsOn)
		{
			Scale = 1;
			Opacity = 1;
			return;
		}

		var pulse = new Animation(progress =>
		{
			Scale = 1 + (GrowBy * progress);
			Opacity = 1 - progress;
		});
		pulse.Commit(this, AnimationName, 16, CycleMilliseconds, Easing.CubicOut, repeat: () => true);
	}

	/// <summary>Stops the pulse and leaves the ring still and whole.</summary>
	public void Stop()
	{
		this.AbortAnimation(AnimationName);
		Scale = 1;
		Opacity = 1;
	}
}
