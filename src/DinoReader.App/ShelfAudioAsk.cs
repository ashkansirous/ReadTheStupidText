using DinoReader.Core;
using DinoReader.Resources.Strings;
using ReadTheStupidText.Mobile.Shared.Network;

namespace DinoReader;

/// <summary>
/// The one question a grown-up screen asks before a language's pre-made audio is fetched on a phone that has only mobile data: "Download now" allows mobile data for this add, "Wait for Wi-Fi"
/// leaves the run to the ordinary Wi-Fi rule. Wi-Fi, Windows and a quiet launch run are not asked (Slice 278 part B review: the question is the one the voice download asked before).
/// </summary>
internal static class ShelfAudioAsk
{
	/// <summary>The grown-up's answer for <paramref name="languages"/>; <see cref="VoiceRequestOutcome.NothingAsked"/> when no question was needed. Only a grown-up screen calls this.</summary>
	public static async Task<VoiceRequestOutcome> AskAsync(Page host, IReadOnlyList<string> languages)
	{
		if (languages.Count == 0 || !OnMobileDataOnly())
		{
			return VoiceRequestOutcome.NothingAsked;
		}

		string named = string.Join(", ", languages.Select(LanguageNames.NativeName));
		bool now = await host.DisplayAlertAsync(
			AppResources.StoryLanguages_WifiTitle, AppResources.Format(AppResources.StoryLanguages_WifiBody, named),
			AppResources.StoryLanguages_WifiNow, AppResources.StoryLanguages_WifiWait);
		return now ? VoiceRequestOutcome.DownloadNow : VoiceRequestOutcome.WaitForWifi;
	}

	// Cellular is all there is (offline is not asked about: the download would only fail).
	private static bool OnMobileDataOnly() =>
		(OperatingSystem.IsAndroid() || OperatingSystem.IsIOS()) && PackResumeWatcher.CellularOnlyKnown(); // a failed read is not cellular-only: no prompt, a normal start
}
