using DinoReader.Core;

namespace DinoReader;

/// <summary>
/// Where a new install is in the first-run intro (Slice 155, Decision 160): the saved step index, so a restart mid-intro resumes at the same
/// step. Absent (or outside <see cref="IntroSteps"/>) means the intro is not running: never started, finished or skipped. A replay from
/// grown-up settings is never saved, so it does not come back after a restart.
/// </summary>
public static class IntroState
{
	private const string StepKey = "dino.intro.step";
	private const int NotRunning = -1;

	/// <summary>The saved step, or -1 when the intro is not running.</summary>
	public static int Step
	{
		get => Preferences.Default.Get(StepKey, NotRunning);
		set => Preferences.Default.Set(StepKey, value);
	}

	/// <summary>True while a first-run intro waits to be finished or skipped.</summary>
	public static bool InProgress => IntroSteps.IsStep(Step);

	/// <summary>Marks the first-run intro as running, at its first step.</summary>
	public static void Start() => Step = 0;

	/// <summary>Marks the intro as finished or skipped.</summary>
	public static void Clear() => Step = NotRunning;
}
