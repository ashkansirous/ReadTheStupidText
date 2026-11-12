using System.Diagnostics;
using DinoReader.Core;
using DinoReader.Resources.Strings;
using Microsoft.Extensions.DependencyInjection;
using FallbackArea = ReadTheStupidText.Application.Usage.FallbackArea;
using FallbackReason = ReadTheStupidText.Application.Usage.FallbackReason;

namespace DinoReader;

/// <summary>How a delete request ended: the story is gone, the user kept it, or the delete failed (the library may be half-deleted).</summary>
public enum DeleteOutcome
{
	Deleted,
	Declined,
	Failed,
}

/// <summary>The words of a delete question: its title, body and the accepting button (Slice 189: a book's "Remove from the shelf" says more than the default).</summary>
public sealed record DeletePrompt(string Title, string Body, string Accept);

/// <summary>Library actions the gallery's grown-up overlay and the Record screen shares (Slice 278: the story wizard and the cast screen are gone).</summary>
public sealed class StoryActions(IStoryLibrary library, IServiceProvider services, GrownUpMode mode)
{
	private const string FallbackFileName = "story";
	private static readonly TimeSpan PackDeleteWait = TimeSpan.FromSeconds(3);

	/// <summary>Opens the reading screen on <paramref name="story"/>.</summary>
	public async Task ReadAsync(INavigation navigation, Story story, string origin = ReadOrigins.Shelf)
	{
		var reading = services.GetRequiredService<ReadingPage>();
		reading.ReadOrigin = origin; // Slice 352: the read says where it started
		reading.Load(story);
		await navigation.PushAsync(reading);
	}

	/// <summary>Opens the Record intro on <paramref name="story"/> (Slice 111, Slice 279): every tap on Record starts there, with the words that apply to every recording, then the recording pages.</summary>
	public async Task<bool> RecordAsync(INavigation navigation, Story story)
	{
		if (!mode.IsGrownUp)
		{
			return false;
		}

		var intro = services.GetRequiredService<RecordIntroPage>();
		await intro.LoadAsync(story);
		if (!mode.IsGrownUp)
		{
			return false;
		}

		await navigation.PushAsync(intro);
		return true;
	}

	/// <summary>Developer-only (Slice 91): writes <paramref name="story"/> as a <c>.dino</c> package and hands it to the share sheet.
	/// Does nothing unless grown-up mode and the developer switch are both on.</summary>
	public async Task ExportAsync(Story story)
	{
		if (!mode.IsGrownUp || !DeveloperMode.Enabled)
		{
			return;
		}

		try
		{
			string path = await WriteExportAsync(story);
			await services.GetRequiredService<ExternalActivityGuard>().RunAsync(async () =>
			{
				await Share.Default.RequestAsync(new ShareFileRequest(story.Title, new ShareFile(path)));
				return true;
			});
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or FeatureNotSupportedException)
		{
			DinoFallbacks.Report(FallbackArea.Ui, FallbackReason.TapFailed); // Slice 275 part 3: the export did nothing
			Debug.WriteLine($"Dino Reader export failed: {ex}");
		}
	}

	private async Task<string> WriteExportAsync(Story story)
	{
		string folder = ExportFolders.CreateNew();
		string path = Path.Combine(folder, ExportFileName(story.Title));
		await using FileStream output = File.Create(path);
		await StoryPackageWriter.WriteAsync(story, library, output);
		return path;
	}

	private const int MaxExportNameLength = 60;

	private static string ExportFileName(string title)
	{
		string clean = string.Concat(title.Where(char.IsLetterOrDigit).Take(MaxExportNameLength));
		return (clean.Length > 0 ? clean : FallbackFileName) + StoryPackageManifest.Extension;
	}

	/// <summary>Asks for confirmation, then deletes the story's folder. <paramref name="prompt"/> null asks the plain delete question.</summary>
	public async Task<DeleteOutcome> ConfirmDeleteAsync(Page page, Story story, DeletePrompt? prompt = null)
	{
		if (!mode.IsGrownUp)
		{
			return DeleteOutcome.Declined;
		}

		prompt ??= new DeletePrompt(GalleryText.DeleteTitle(story.Title), AppResources.Delete_Body, AppResources.Common_Delete);
		bool confirmed = await page.DisplayAlertAsync(prompt.Title, prompt.Body, prompt.Accept, AppResources.Common_Keep);
		// The alert can outlive the grown-up mode (app left and returned): a confirm then deletes nothing.
		return confirmed && mode.IsGrownUp ? await TryDeleteAsync(page, story) : DeleteOutcome.Declined;
	}

	private async Task<DeleteOutcome> TryDeleteAsync(Page page, Story story)
	{
		try
		{
			await library.DeleteAsync(story.Id);
			RecordChoices.Forget(story.Id); // the remembered record choice goes with the book (Slice 279)
			StarterStories.ForgetStoreInstall(story.SourceId);
			await DeletePacksAsync(story);
			return DeleteOutcome.Deleted;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			DinoFallbacks.Report(FallbackArea.Ui, FallbackReason.TapFailed); // Slice 275 part 3: the delete failed; the alert below tells the person
			Debug.WriteLine($"Dino Reader delete failed: {ex}");
			await page.DisplayAlertAsync(AppResources.Delete_FailedTitle, AppResources.Delete_FailedBody, AppResources.Common_Ok);
			return DeleteOutcome.Failed;
		}
	}

	// A deleted store book takes its pre-made audio with it (Slice 246 C2). Audio is housekeeping: a failure here never fails the delete (the next cold start's sweep catches what is left).
	private async Task DeletePacksAsync(Story story)
	{
		if (story.SourceId is not { } bookId)
		{
			return;
		}

		using var wait = new CancellationTokenSource(PackDeleteWait); // behind a long pack download the delete does not hold the screen: the sweep takes the folder later
		try
		{
			await services.GetRequiredService<AudioPackStore>().DeleteBookAsync(bookId, wait.Token);
		}
		catch (OperationCanceledException)
		{
			// no-fallback: a timeout or other cancellation, the sweep takes the folder later by design
			Debug.WriteLine($"Dino Reader did not wait for the audio packs of {bookId} to be deleted; the sweep takes them.");
		}
		catch (Exception ex)
		{
			DinoFallbacks.Report(FallbackArea.Pack, FallbackReason.PackSyncFailed); // Slice 275 part 3: the next cold start's sweep takes the folder
			Debug.WriteLine($"Dino Reader could not delete the audio packs of {bookId}: {ex.Message}");
		}
	}
}
