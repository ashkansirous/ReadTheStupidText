#if DEBUG
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Application.Reading;
using ReadTheStupidText.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Slice 262: <c>shelf-audio lang=&lt;code&gt;</c> adds that language as a side language the way the Story languages page does (saved at once) and starts the fetch of the shelf books' pre-made audio
/// (<see cref="ShelfAudioPacks"/>). No voice is downloaded for the language (Slice 278 part B), so a flow
/// can see a pack arrive from the test store (<c>test-store mode=ok</c>, which lists one for the gingerbread-man book in <see cref="TestStore.AudioLanguage"/>). Outcome: <c>HOOK shelf-audio started &lt;lang&gt;</c>
/// or <c>failed ...</c>; the run's own lines are <c>[AudioPack] shelf audio ...</c>.
/// </summary>
internal static partial class DinoTestCommands
{
	private const string ShelfAudioName = "shelf-audio";

	private const string ForcePackFailureName = "force-pack-failure";

	private const string PackRepairName = "pack-repair";

	/// <summary>
	/// Slice 360: <c>pack-repair mode=advance|tick|open|offline|online|state [id=&lt;book id&gt; lang=&lt;code&gt;]</c>. <c>advance</c> moves this process's clock 61 minutes forward (so the hourly retry is due),
	/// <c>tick</c> runs the hourly pass now, <c>open id=</c> is the retry made when a book opens, <c>offline|online</c> force the reading page's "no internet" state (the downloads are untouched), and
	/// <c>no-listing|listing</c> hide or show the known store listing (as on an offline first launch), <c>state id= lang=</c> writes <c>[PackRepair] state &lt;kind&gt; ...</c> for that pack. Outcome: <c>HOOK pack-repair ok &lt;mode&gt;</c>.
	/// </summary>
	private static string PackRepairHook(string mode, string id, string language)
	{
		PackRepair repair = IPlatformApplication.Current!.Services.GetRequiredService<ShelfAudioPacks>().Repair;
		switch (mode)
		{
			case "advance":
				PackRepair.AdvanceClock(PackRepairAdvanceMinutes);
				break;
			case "tick":
				repair.TickNow();
				break;
			case "open":
				repair.OnBookOpened(id, string.IsNullOrEmpty(language) ? [] : [language]);
				break;
			case "no-listing":
			case "listing":
				StoreClient.HideListing = mode == "no-listing"; // the known listing is hidden, as on a first launch that is offline (the expectation rule then decides without it)
				break;
			case "offline":
			case "online":
				PackRepair.ForcedOffline = mode == "offline";
				break;
			case "state":
				PackSoundView view = repair.ViewOf(id, language, null, PackExpectationKind.Expected, installed: false);
				TestHookLog.Write($"[PackRepair] state {id} {language} kind={view.Kind} percent={view.Percent}");
				break;
			default:
				return $"{TestHookDispatcher.Failed} {TestHookDispatcher.Unknown} {mode}";
		}

		return $"{TestHookDispatcher.Ok} {mode}";
	}

	private const int PackRepairAdvanceMinutes = 61;

	/// <summary>Slice 310: <c>force-pack-failure mode=network|corrupt|disk-full|missing|off</c> makes every pack download fail that way until <c>off</c> (or the app is killed). Outcome: <c>HOOK force-pack-failure ok &lt;mode&gt;</c>.</summary>
	private static string ForcePackFailure(string mode)
	{
		if (!ForcedPackFailures.TryParse(mode, out PackFailureInjection kind))
		{
			return $"{TestHookDispatcher.Failed} {TestHookDispatcher.Unknown} {mode}";
		}

		AudioPackStore.ForcedFailure = kind;
		return $"{TestHookDispatcher.Ok} {mode}";
	}

	private static string StartShelfAudio(string language)
	{
		if (!UiLanguage.IsSupported(language))
		{
			return $"{TestHookDispatcher.Failed} {TestHookDispatcher.Unknown} {language}";
		}

		IServiceProvider services = IPlatformApplication.Current!.Services;
		StoryLanguageSet now = StoryLanguages.Current();
		StoryLanguages.SaveSides(now.WithSide(language));
		services.GetRequiredService<ShelfAudioPacks>().Start(language);
		return $"started {UiLanguage.Resolve(language)}";
	}
}
#endif
