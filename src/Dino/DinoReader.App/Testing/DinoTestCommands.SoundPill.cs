#if DEBUG
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Sirous.Reading.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Slice 452: <c>sound-pill state=getting|wifi|preparing|nosound|off [percent=&lt;0-100&gt;] [lang=&lt;code&gt;]</c> holds the open reading page's sound pill in one state (Getting with a count,
/// Wi-Fi, Preparing, no sound) until <c>off</c>, whatever the page's own status line does, so a flow can screenshot each state (Slice 290, Batch 50 frames 197-199). The words are the app's own strings;
/// <c>lang</c> (default <c>fa</c>, the right-to-left frame 197) picks the native name set in the Getting line. Outcome: <c>HOOK sound-pill ok &lt;state&gt;</c> or <c>failed &lt;reason&gt;</c>; the pill
/// itself writes <c>[Reading] sound-pill held kind=... text=...</c>.
/// </summary>
internal static partial class DinoTestCommands
{
	private const string SoundPillName = "sound-pill";
	private const string PercentKey = SoundPillHookPlan.PercentKey;
	private const string PillDefaultLanguage = "fa";

	private static string SoundPillHook(TestHookCommand command)
	{
		_ = SoundPillAsync(command.Get(StateKey, string.Empty), command.Get(PercentKey, string.Empty), command.Get(TestHookDispatcher.LanguageKey, PillDefaultLanguage));
		return StartedOutcome;
	}

	private static async Task SoundPillAsync(string state, string percentText, string language)
	{
		try
		{
			SoundStatusKind? kind = SoundPillHookPlan.KindOf(state);
			if (kind is null && !SoundPillHookPlan.IsOff(state))
			{
				Log($"{SoundPillName} failed bad state {state}");
				return;
			}

			if (!SoundPillHookPlan.TryParsePercent(percentText, out int percent, out string error))
			{
				Log($"{SoundPillName} failed {error}");
				return;
			}

			ReadingPage? page = await WaitForAsync(() => MainThread.InvokeOnMainThreadAsync(OpenReadingPage));
			if (page is null)
			{
				Log($"{SoundPillName} failed no reading page open");
				return;
			}

			string why = await MainThread.InvokeOnMainThreadAsync(() => ApplySoundPill(page, kind, percent, language));
			Log(why.Length == 0 ? $"{SoundPillName} ok {state}" : $"{SoundPillName} failed {why}");
		}
		catch (Exception ex)
		{
			// no-fallback: Debug test hook, logs the fault
			Log($"{SoundPillName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	private static ReadingPage? OpenReadingPage() => GalleryRoot()?.Navigation.NavigationStack.LastOrDefault() as ReadingPage;

	/// <summary>Runs on the main thread: finds the open reading page's pill and holds it in <paramref name="kind"/> (null releases it). Returns the failure reason, or empty.</summary>
	private static string ApplySoundPill(ReadingPage page, SoundStatusKind? kind, int percent, string language)
	{
		SoundStatusPill? pill = ((IVisualTreeElement)page).GetVisualTreeDescendants().OfType<SoundStatusPill>().FirstOrDefault();
		if (pill is null)
		{
			return "no pill on the page";
		}

		if (kind is not { } shown)
		{
			pill.ReleaseTestHold();
			return string.Empty;
		}

		string nativeName = LanguageNames.NativeName(language);
		pill.ShowHeld(SoundPillHookPlan.StateFor(shown, percent), SoundPillLine(shown, nativeName, percent), nativeName);
		return string.Empty;
	}

	private static string SoundPillLine(SoundStatusKind kind, string nativeName, int percent) => kind switch
	{
		SoundStatusKind.Getting => AppResources.Format(AppResources.Reading_GettingBookSound, nativeName, percent),
		SoundStatusKind.WaitingForInternet => AppResources.ShelfAudio_WaitingInternet,
		SoundStatusKind.Preparing => AppResources.ShelfAudio_Preparing,
		_ => AppResources.Reading_NoSound,
	};
}
#endif
