using Foundation;
using ReadTheStupidText.Application.Images;
using UIKit;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// <see cref="IPageImageKeeper"/> for iOS (Slice 67): ImageIO decodes the page upright (its EXIF
/// orientation applied) with the long edge capped at <see cref="IPageImageKeeper.MaxLongEdge"/>,
/// and it is written as JPEG.
/// </summary>
public sealed class IosPageImageKeeper : IPageImageKeeper
{
    private const float JpegQuality = 0.85f;

    public Task KeepAsync(string location, string destinationPath) =>
        Task.Run(() => Keep(location, destinationPath));

    private static void Keep(string location, string destinationPath)
    {
        string source = ScannedText.ToLocalPath(location)
            ?? throw new IOException($"Not a local image: '{location}'.");
        RejectOverwritingSource(source, destinationPath);
        using var decoded = IosPixelImageLoader.DecodeUpright(source, IPageImageKeeper.MaxLongEdge);
        using var image = UIImage.FromImage(decoded);
        using NSData jpeg = image.AsJPEG(JpegQuality) ?? throw new IOException($"Could not encode '{destinationPath}'.");
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        using var destination = NSUrl.FromFilename(destinationPath);
        if (!jpeg.Save(destination, NSDataWritingOptions.Atomic, out NSError? error))
        {
            throw new IOException($"Could not write '{destinationPath}': {error?.LocalizedDescription}");
        }
    }

    // The kept copy must be a different file from the source, which the caller deletes afterwards.
    private static void RejectOverwritingSource(string source, string destinationPath)
    {
        if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destinationPath), StringComparison.Ordinal))
        {
            throw new ArgumentException("The kept page image must not be the scanned source file.", nameof(destinationPath));
        }
    }
}
