using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>Asks which of the nine languages a story is in, listed by their own names (an action sheet, like the cast screen's pickers).</summary>
public static class LanguageChooser
{
	/// <summary>The picked language code, or null when the grown-up cancelled.</summary>
	public static async Task<string?> ChooseAsync(Page page)
	{
		string[] names = LanguageNames.All.Select(l => l.NativeName).ToArray();
		string picked = await page.DisplayActionSheetAsync(AppResources.Story_Language, AppResources.Common_Cancel, null, names);
		return picked is null || picked == AppResources.Common_Cancel ? null : LanguageNames.CodeOf(picked);
	}
}
