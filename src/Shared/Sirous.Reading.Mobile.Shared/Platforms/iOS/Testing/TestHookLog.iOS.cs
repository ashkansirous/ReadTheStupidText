#if DEBUG
namespace Sirous.Reading.Mobile.Shared.Testing;

public static partial class TestHookLog
{
	/// <summary>Slice 404: the file the lines are also appended to, in the app's data folder (Library), so the CI script can print them after a failed flow (a simulator app's console output is not reachable there).</summary>
	public const string FileName = "test-hook.log";

	static partial void WritePlatform(string marker)
	{
		string line = $"{Tag}: {marker}";
		Console.WriteLine(line);
		AppendToFile(line);
	}

	private static void AppendToFile(string line)
	{
		try
		{
			File.AppendAllText(Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, FileName), line + "\n");
		}
		catch (Exception)
		{
			// no-fallback: Debug-only test log; a write that fails must never break the hook it describes
		}
	}
}
#endif
