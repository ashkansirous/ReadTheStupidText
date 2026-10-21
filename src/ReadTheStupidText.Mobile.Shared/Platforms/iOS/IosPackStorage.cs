using Foundation;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Keeps downloaded language packs out of the iCloud and iTunes backup (Slice 55). A pack can be
/// tens of megabytes and is re-downloadable, and Apple rejects apps that back up data they can
/// recreate. Marking a folder excluded covers everything below it, so the flag is set once on the
/// packs folder (<see cref="MobileVoicePackService.PacksRoot"/>) before the first download creates a pack inside it.
/// </summary>
public static class IosPackStorage
{
    /// <summary>Creates the packs folder if needed and marks it as excluded from backups. Best effort.</summary>
    public static void ExcludePacksFromBackup()
    {
        try
        {
            string folder = MobileVoicePackService.PacksRoot(); // creates it
            using var url = NSUrl.FromFilename(folder);
            url.SetResource(NSUrl.IsExcludedFromBackupKey, NSNumber.FromBoolean(true), out NSError? error);
            if (error is not null)
            {
                System.Diagnostics.Debug.WriteLine($"Could not exclude the packs folder from backup: {error.LocalizedDescription}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not prepare the packs folder: {ex.Message}");
        }
    }
}
