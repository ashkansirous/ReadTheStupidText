using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;

namespace DinoReader;

/// <summary>How a delete request ended: the story is gone, the user kept it, or the delete failed (the library may be half-deleted).</summary>
public enum DeleteOutcome
{
	Deleted,
	Declined,
	Failed,
}

/// <summary>Library actions the gallery's grown-up overlay and the edit screens share.</summary>
public sealed class StoryActions(IStoryLibrary library, IServiceProvider services, GrownUpMode mode)
{
	/// <summary>Opens the reading screen on <paramref name="story"/>.</summary>
	public async Task ReadAsync(INavigation navigation, Story story)
	{
		var reading = services.GetRequiredService<ReadingPage>();
		reading.Load(story);
		await navigation.PushAsync(reading);
	}

	/// <summary>Opens the wizard to edit <paramref name="story"/>, or to add a story when it is null (Slice 78).</summary>
	public async Task EditAsync(INavigation navigation, Story? story)
	{
		if (!mode.IsGrownUp)
		{
			return;
		}

		var wizard = services.GetRequiredService<StoryWizardPage>();
		await wizard.LoadAsync(story);
		if (mode.IsGrownUp)
		{
			await navigation.PushAsync(wizard);
		}
	}

	/// <summary>Opens the cast screen on <paramref name="story"/>; its edits are saved as they are made.</summary>
	public async Task CastAsync(INavigation navigation, Story story)
	{
		if (!mode.IsGrownUp)
		{
			return;
		}

		var cast = services.GetRequiredService<CastPage>();
		cast.Load(story, SaveCastAsync);
		await navigation.PushAsync(cast);
	}

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

	/// <summary>Asks for confirmation, then deletes the story's folder. </summary>
	public async Task<DeleteOutcome> ConfirmDeleteAsync(Page page, Story story)
	{
		if (!mode.IsGrownUp)
		{
			return DeleteOutcome.Declined;
		}

		bool confirmed = await page.DisplayAlertAsync(GalleryText.DeleteTitle(story.Title), AppResources.Delete_Body, AppResources.Common_Delete, AppResources.Common_Keep);
		// The alert can outlive the grown-up mode (app left and returned): a confirm then deletes nothing.
		return confirmed && mode.IsGrownUp ? await TryDeleteAsync(page, story) : DeleteOutcome.Declined;
	}

	private async Task<DeleteOutcome> TryDeleteAsync(Page page, Story story)
	{
		try
		{
			await library.DeleteAsync(story.Id);
			return DeleteOutcome.Deleted;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Debug.WriteLine($"Dino Reader delete failed: {ex}");
			await page.DisplayAlertAsync(AppResources.Delete_FailedTitle, AppResources.Delete_FailedBody, AppResources.Common_Ok);
			return DeleteOutcome.Failed;
		}
	}
}
