namespace ReadTheStupidText.Application.Reading;

/// <summary>
/// Where an installed pack's files live (Slice 89). A pack is never replaced in place: each version is
/// installed into its own <c>v&lt;version&gt;</c> folder inside the pack's folder, next to the old one, and
/// a small <c>active</c> file naming the folder is replaced atomically once the new one is complete. A
/// reader that holds the old model files open keeps working, and picks the new folder up the next time it
/// builds an engine. Folders of old versions (and the loose files of the first, unversioned layout) are
/// deleted at the next app start, when nothing holds them. A pack with no <c>active</c> file is the
/// unversioned layout: its files sit directly in the pack's folder.
/// </summary>
public static class PackVersionFolders
{
    private const string ActiveFileName = "active";
    private const string ActiveTempSuffix = ".tmp";
    private const string VersionFolderPrefix = "v";

    /// <summary>The folder holding the pack's files: the active version's folder, else the pack's own folder.</summary>
    public static string Resolve(string packFolder)
    {
        string active = ReadActive(packFolder);
        return active.Length > 0 && Directory.Exists(Path.Combine(packFolder, active))
            ? Path.Combine(packFolder, active)
            : packFolder;
    }

    /// <summary>The name of the folder for a version, inside the pack's folder.</summary>
    public static string VersionFolderName(string version) => VersionFolderPrefix + version;

    /// <summary>
    /// Moves a validated payload into its own version folder and makes that the active one. The previous
    /// version is left untouched where it is; a stale folder of the same version (a killed install) is replaced,
    /// but the folder that is the active version is never touched (a live engine may hold its files): the
    /// payload is dropped instead.
    /// </summary>
    public static void Install(string payload, string packFolder, string version)
    {
        string name = VersionFolderName(version);
        string target = Path.Combine(packFolder, name);
        Directory.CreateDirectory(packFolder);
        if (Directory.Exists(target))
        {
            if (ReadActive(packFolder) == name)
            {
                TryDelete(payload);
                return;
            }

            Directory.Delete(target, recursive: true);
        }

        Directory.Move(payload, target);
        Activate(packFolder, name);
    }

    /// <summary>Deletes, in every pack folder under <paramref name="packsRoot"/>, everything but the active version.</summary>
    public static void DeleteOldVersions(string packsRoot)
    {
        if (!Directory.Exists(packsRoot))
        {
            return;
        }

        foreach (string packFolder in Directory.EnumerateDirectories(packsRoot).Where(f => PackNaming.IsValidId(Path.GetFileName(f))))
        {
            DeleteInactive(packFolder);
        }
    }

    private static void Activate(string packFolder, string versionFolder)
    {
        string pointer = Path.Combine(packFolder, ActiveFileName);
        string temp = pointer + ActiveTempSuffix;
        WriteDurably(temp, versionFolder);
        File.Move(temp, pointer, overwrite: true);
    }

    // Flushed to disk before the rename, so a power loss never leaves a zero-length pointer.
    private static void WriteDurably(string path, string text)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(System.Text.Encoding.UTF8.GetBytes(text));
        stream.Flush(flushToDisk: true);
    }

    private static string ReadActive(string packFolder)
    {
        string pointer = Path.Combine(packFolder, ActiveFileName);
        try
        {
            string name = File.Exists(pointer) ? File.ReadAllText(pointer).Trim() : string.Empty;
            bool valid = name.StartsWith(VersionFolderPrefix, StringComparison.Ordinal) && PackNaming.IsValidVersion(name[VersionFolderPrefix.Length..]);
            return valid ? name : string.Empty;
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    // A pack with no active version keeps whatever it has. A version folder newer than the active one is a
    // complete install the app was killed before activating: it is kept, not deleted. Anything that cannot be
    // deleted (a file still held open) is left for the next start.
    private static void DeleteInactive(string packFolder)
    {
        string active = ReadActive(packFolder);
        if (active.Length == 0 || !Directory.Exists(Path.Combine(packFolder, active)))
        {
            return;
        }

        foreach (string entry in Directory.EnumerateFileSystemEntries(packFolder))
        {
            string name = Path.GetFileName(entry);
            if (name != active && name != ActiveFileName && !IsNewerVersionFolder(name, active))
            {
                TryDelete(entry);
            }
        }
    }

    private static bool IsNewerVersionFolder(string name, string active) =>
        name.StartsWith(VersionFolderPrefix, StringComparison.Ordinal)
        && PackNaming.IsValidVersion(name[VersionFolderPrefix.Length..])
        && PackVersions.IsNewer(name[VersionFolderPrefix.Length..], active[VersionFolderPrefix.Length..]);

    private static void TryDelete(string entry)
    {
        try
        {
            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Still in use: it is retried at the next start.
        }
    }
}
