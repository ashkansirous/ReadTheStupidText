#if DEBUG
using DinoReader.Core;
using Microsoft.Extensions.DependencyInjection;
using ReadTheStupidText.Mobile.Shared.Testing;

namespace DinoReader.Testing;

/// <summary>
/// Slice 279: <c>record-state id=&lt;book&gt; [clips=&lt;n&gt;]</c>. The scripted record flow taps the Record screen's buttons, so what the library really holds afterwards is read here instead of guessed from the
/// screen: the line <c>HOOK record-state ok clips=n files=m switch=True|False</c> when every clip the story names has a non-empty file (and, when <c>clips</c> is given, the count matches), else
/// <c>HOOK record-state failed ...</c> naming what is wrong. It also lists each clip as <c>[RecordState] clip rec-1-2-3.m4a bytes=n</c>.
/// </summary>
internal static partial class DinoTestCommands
{
	private const string RecordStateName = "record-state";
	private const string ClipsKey = "clips";

	private static async Task RecordStateAsync(string id, string expected)
	{
		try
		{
			IStoryLibrary library = IPlatformApplication.Current!.Services.GetRequiredService<IStoryLibrary>();
			Story? story = id.Trim().Length == 0 ? null : await FindWhenImportedAsync(library, id.Trim());
			Log(story is null ? $"{RecordStateName} failed no book {id}" : DescribeRecordings(library, story, expected));
		}
		catch (Exception ex)
		{
			// no-fallback: Debug test hook, logs the fault
			Log($"{RecordStateName} fault {TestHookLog.FaultSummary(ex)}");
		}
	}

	private static string DescribeRecordings(IStoryLibrary library, Story story, string expected)
	{
		int clips = 0;
		int missing = 0;
		foreach (string name in StoryRecordings.Files(story))
		{
			string? path = library.TryResolveFile(story.Id, name);
			long bytes = path is not null && File.Exists(path) ? new FileInfo(path).Length : 0;
			TestHookLog.Write($"[RecordState] clip {name} bytes={bytes}");
			clips++;
			missing += bytes > 0 ? 0 : 1;
		}

		bool countOk = expected.Length == 0 || (int.TryParse(expected, out int wanted) && wanted == clips);
		string detail = $"clips={clips} files={clips - missing} switch={story.UseRecordings}";
		return missing == 0 && countOk ? $"{RecordStateName} ok {detail}" : $"{RecordStateName} failed {detail} expected={expected}";
	}
}
#endif
