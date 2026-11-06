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
