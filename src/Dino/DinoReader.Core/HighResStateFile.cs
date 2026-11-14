using System.Text.Json;

namespace DinoReader.Core;

/// <summary>
/// Slice 375 D (review round 6 #4): reads and writes the small high-res state files. A read that fails for an IO or permission reason is reported as NOT READ (null), never as an empty file,
/// so the caller skips its write instead of overwriting the whole file with one change. A missing file or a corrupt one is a real empty state. A write goes to a temp name and is renamed over the file.
/// </summary>
public static class HighResStateFile
{
    private const string PartExtension = ".part";

    /// <summary>The text of the file: <c>string.Empty</c> when it does not exist; null when it exists but could not be read.</summary>
    public static string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // no-fallback: an unreadable state file is reported as not read; the caller skips its write and the next pass reads again
            return null;
        }
    }

    /// <summary>The id-to-version map in the file: empty when the file is missing or corrupt; null when it could not be read (the caller must not write then).</summary>
    public static Dictionary<string, int>? ReadMap(string path)
    {
        string? text = ReadText(path);
        if (text is null)
        {
            return null;
        }

        try
        {
            Dictionary<string, int>? saved = text.Length == 0 ? null : JsonSerializer.Deserialize<Dictionary<string, int>>(text);
            return new Dictionary<string, int>(saved ?? [], StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            // no-fallback: a damaged record means no copy is known to be high-res; at worst one same-version swap repeats
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Writes through a temp name and a rename. False when it could not be written (the state then stays as it was).</summary>
    public static bool WriteAtomic(string path, string text)
    {
        try
        {
            string part = path + PartExtension;
            File.WriteAllText(part, text);
            File.Move(part, path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // no-fallback: a lost save only means the state is not remembered after a restart; the old file stays whole
            return false;
        }
    }
}
