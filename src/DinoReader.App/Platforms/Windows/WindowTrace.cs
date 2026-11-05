namespace DinoReader;

/// <summary>
/// A small trace of the Windows window frame (Slice 235 part A), kept in <c>window-trace.log</c> in the app data folder so a
/// Release build on the VM can be read back (Debug.WriteLine is invisible there). Keeps only the last
/// <see cref="MaxLines"/> lines; never throws.
/// </summary>
internal static class WindowTrace
{
	/// <summary>The file name inside <see cref="FileSystem.AppDataDirectory"/>; the VM check reads it by this name.</summary>
	internal const string FileName = "window-trace.log";

	internal const int MaxLines = 200;

	private static readonly TimeSpan SampleGap = TimeSpan.FromSeconds(1);

	private static readonly object Gate = new();
	private static readonly Dictionary<string, DateTime> LastSampled = [];
	private static bool _trimmed;

	/// <summary>For lines a mouse sweep or a held arrow key can make hundreds of (hover, focus): the same message is written at most once a second, so the UI thread does not hit the disk on every event.</summary>
	internal static void WriteSampled(string message)
	{
		DateTime now = DateTime.UtcNow;
		lock (Gate)
		{
			if (LastSampled.TryGetValue(message, out DateTime last) && now - last < SampleGap)
			{
				return;
			}

			LastSampled[message] = now;
		}

		Write(message);
	}

	internal static void Write(string message)
	{
		try
		{
			string path = Path.Combine(Microsoft.Maui.Storage.FileSystem.AppDataDirectory, FileName);
			string line = $"{DateTime.Now:HH:mm:ss.fff} {message}";
			lock (Gate)
			{
				TrimOnce(path);
				File.AppendAllText(path, line + Environment.NewLine);
			}
		}
		catch (Exception)
		{
			// a trace must never break the window
		}
	}

	// One append per write; the file is cut to the last MaxLines only on the first write of a run (a mid-run cut would drop lines the VM checks count). WriteSampled keeps a long session's growth small.
	private static void TrimOnce(string path)
	{
		if (_trimmed)
		{
			return;
		}

		_trimmed = true;
		if (File.Exists(path))
		{
			string[] lines = File.ReadAllLines(path);
			File.WriteAllLines(path, lines.Skip(Math.Max(0, lines.Length - MaxLines)));
		}
	}
}
