using System.Diagnostics;
using DinoReader.Core;

namespace DinoReader;

/// <summary>Library actions the manage and edit screens share.</summary>
public sealed class StoryActions(IStoryLibrary library)
{
	private const string KeepText = "Keep";
	private const string DeleteText = "Delete";
	private const string OkText = "OK";
	private const string DeleteFailedTitle = "Couldn't delete that story";
	private const string DeleteFailedBody = "Something is still using its files. Try again in a moment.";
	private const string DeleteBody = "Its pages, pictures and voices are removed from this phone.";

	/// <summary>Saves the cast, merges and narrator of <paramref name="edited"/> onto the stored story, leaving
	/// everything else in the stored file (its reading mode, say) as it is.</summary>
	public async Task SaveCastAsync(Story edited)
	{
		Story current = await library.GetAsync(edited.Id) ?? throw new InvalidOperationException("The story is no longer saved.");
		await library.UpdateAsync(current with
		{
			Cast = edited.Cast, CharacterMerges = edited.Merges, NarratorVoiceId = edited.NarratorVoiceId,
		});
	}

	/// <summary>Asks for confirmation, then deletes the story's folder. Returns true if it was deleted.</summary>
	public async Task<bool> ConfirmDeleteAsync(Page page, Story story)
	{
		bool confirmed = await page.DisplayAlertAsync(GalleryText.DeleteTitle(story.Title), DeleteBody, DeleteText, KeepText);
		return confirmed && await TryDeleteAsync(page, story);
	}

	private async Task<bool> TryDeleteAsync(Page page, Story story)
	{
		try
		{
			await library.DeleteAsync(story.Id);
			return true;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader delete failed: {ex}");
			await page.DisplayAlertAsync(DeleteFailedTitle, DeleteFailedBody, OkText);
			return false;
		}
	}
}
