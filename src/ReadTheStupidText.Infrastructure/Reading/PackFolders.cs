using Windows.Storage;

namespace ReadTheStupidText.Infrastructure.Reading;

/// <summary>
/// Where voice packs live on disk (Slice 56): <c>LocalCacheFolder\packs\&lt;id&gt;</c>. The cache folder is
/// not backed up or roamed, which suits a 100+ MB download that can always be fetched again.
/// </summary>
public static class PackFolders
{
    private const string PacksSubfolder = "packs";

    public static string PacksRoot() => Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, PacksSubfolder);
}
