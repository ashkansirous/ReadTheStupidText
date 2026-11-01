#if DEBUG
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Slice 230: <c>backup-recordings id=&lt;starter&gt;</c>. The microphone cannot be driven by a script, so this writes a short known audio file as page 1's recording of that book,
/// backs the book up to a file, deletes the recording and restores the file into the same library (the book is still on the shelf, so the restore merges). Outcome lines:
/// <c>HOOK backup-recordings written bytes=n</c>, <c>backed-up</c>, <c>deleted</c>, <c>restored merged=m ...</c>, then <c>HOOK backup-recordings ok bytes=n</c> when page 1's entry exists and
/// its file is present with the same non-zero size, else <c>failed ...</c>. Hearing the restored recording play is not scripted.
/// </summary>
internal static partial class DinoTestCommands
{
	private const string BackupRecordingsName = "backup-recordings";
	private const int FakeAudioBytes = 4096;
	private const int FakeAudioSeed = 230;
	private const int FirstPage = 0;
	private const int FakeSeconds = 3;
	private const string BackupFileName = "dino-test-backup-recordings.zip";

	private static async Task BackupRecordingsAsync(string id)
	{
		try
		{
			IStoryLibrary library = IPlatformApplication.Current!.Services.GetRequiredService<IStoryLibrary>();
			Story? story = id.Trim().Length == 0 ? null : await FindWhenImportedAsync(library, id.Trim());
			Log(story is null ? $"{BackupRecordingsName} failed no book {id}" : await RoundTripAsync(library, story));
		}
		catch (Exception ex)
		{
			Log($"{BackupRecordingsName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	private static async Task<string> RoundTripAsync(IStoryLibrary library, Story story)
	{
		string zip = Path.Combine(FileSystem.CacheDirectory, BackupFileName);
		try
		{
			int bytes = await WriteRecordingAsync(library, story);
			await BackupToAsync(library, story.Id, zip);
			await DeleteRecordingAsync(library, story.Id);
			StoryRestoreResult result = await RestoreFromAsync(library, zip);
			Log($"{BackupRecordingsName} restored merged={result.Merged} added={result.Added} skipped={result.Skipped}");
			return await VerifyAsync(library, story.Id, bytes);
		}
		finally
		{
			File.Delete(zip);
		}
	}

	private static async Task<int> WriteRecordingAsync(IStoryLibrary library, Story story)
	{
		var audio = new byte[FakeAudioBytes];
		new Random(FakeAudioSeed).NextBytes(audio);
		await library.StoreRecordingAsync(story.Id, FirstPage, path => File.WriteAllBytesAsync(path, audio));
		await library.UpdateAsync(StoryRecordings.With(story, FirstPage, new PageRecording(story.Language, FakeSeconds)));
		Log($"{BackupRecordingsName} written bytes={audio.Length}");
		return audio.Length;
	}

	private static async Task BackupToAsync(IStoryLibrary library, string storyId, string zip)
	{
		Story current = (await library.GetAsync(storyId))!;
		await using FileStream output = File.Create(zip);
		await StoryBackupWriter.WriteAsync([current], library, output);
		Log($"{BackupRecordingsName} backed-up");
	}

	private static async Task DeleteRecordingAsync(IStoryLibrary library, string storyId)
	{
		Story current = (await library.GetAsync(storyId))!;
		await library.UpdateAsync(StoryRecordings.Without(current, FirstPage));
		await library.DeleteFileAsync(storyId, StoryRecordings.FileNameOf(FirstPage));
		Log($"{BackupRecordingsName} deleted");
	}

	private static async Task<StoryRestoreResult> RestoreFromAsync(IStoryLibrary library, string zip)
	{
		await using FileStream input = File.OpenRead(zip);
		return await StoryBackupReader.RestoreAsync(input, library);
	}

	private static async Task<string> VerifyAsync(IStoryLibrary library, string storyId, int bytes)
	{
		Story? restored = await library.GetAsync(storyId);
		string? path = library.TryResolveFile(storyId, StoryRecordings.FileNameOf(FirstPage));
		bool entry = restored is not null && StoryRecordings.Of(restored, FirstPage) is not null;
		long size = path is not null && File.Exists(path) ? new FileInfo(path).Length : 0;
		return entry && size == bytes ? $"{BackupRecordingsName} ok bytes={size}" : $"{BackupRecordingsName} failed entry={entry} bytes={size}";
	}
}
#endif
