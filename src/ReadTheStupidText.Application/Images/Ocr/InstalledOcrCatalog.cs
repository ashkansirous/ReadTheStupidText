using ReadTheStupidText.Application.Reading;

namespace ReadTheStupidText.Application.Images;

/// <summary>An installed OCR pack ready to load: the folder that holds its models.</summary>
public sealed record OcrModelPaths(string Folder, string Language, string Version)
{
    public string Detection => Path.Combine(Folder, OcrPackLayout.DetectionModel);

    public string Recognition => Path.Combine(Folder, OcrPackLayout.RecognitionModel);

    public string Dictionary => Path.Combine(Folder, OcrPackLayout.Dictionary);

    public bool RightToLeft => OcrLanguages.IsRightToLeft(Language);
}

/// <summary>
/// Picks the installed OCR pack that serves a language, or null (ML Kit reads). Like
/// <see cref="InstalledVoiceCatalog"/>, the pack serving the fewest languages is the most specific.
/// </summary>
public static class InstalledOcrCatalog
{
    public static OcrModelPaths? Resolve(IEnumerable<InstalledVoicePack> installed, string language)
    {
        InstalledVoicePack? pack = PackSelection.Serving(installed, PackKind.Ocr, language).FirstOrDefault();

        return pack is null ? null : new OcrModelPaths(pack.Folder, language, pack.Manifest.Version);
    }
}

/// <summary>Picks the catalog entry of the OCR pack that serves a language.</summary>
public static class OcrPackSelector
{
    public static PackInfo? Select(IEnumerable<PackInfo> catalog, string language) =>
        PackSelection.FromCatalog(catalog, PackKind.Ocr, language);
}
