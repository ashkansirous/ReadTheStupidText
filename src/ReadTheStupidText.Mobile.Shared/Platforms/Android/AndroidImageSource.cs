using Android.Graphics;
using AndroidNet = Android.Net;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// Reads an image given by a location string (Slice 63): a plain path, a <c>file://</c> uri or a
/// <c>content://</c> uri. One place opens the stream, reads the stored size and reads the EXIF
/// orientation, so every loader treats a content-URI photo the same as a file.
/// </summary>
internal static class AndroidImageSource
{
    /// <summary>Opens a new stream over the image; the caller disposes it.</summary>
    public static Stream Open(Android.Content.Context context, string location)
    {
        string? local = ScannedText.ToLocalPath(location);
        if (local is not null)
        {
            return File.OpenRead(local);
        }

        AndroidNet.Uri uri = AndroidNet.Uri.Parse(location) ?? throw new InvalidOperationException($"Could not build a Uri for '{location}'.");
        return context.ContentResolver?.OpenInputStream(uri) ?? throw new FileNotFoundException(location);
    }

    /// <summary>The stored (not yet rotated) pixel size.</summary>
    public static (int Width, int Height) ReadBounds(Android.Content.Context context, string location)
    {
        var options = new BitmapFactory.Options { InJustDecodeBounds = true };
        using Stream stream = Open(context, location);
        BitmapFactory.DecodeStream(stream, null, options);
        return (options.OutWidth, options.OutHeight);
    }

    /// <summary>The EXIF orientation, or Normal when the image carries none or it cannot be read.</summary>
    public static int ReadOrientation(Android.Content.Context context, string location)
    {
        try
        {
            using Stream stream = Open(context, location);
            using var exif = new Android.Media.ExifInterface(stream);
            return exif.GetAttributeInt(Android.Media.ExifInterface.TagOrientation, (int)Android.Media.Orientation.Normal);
        }
        catch (IOException)
        {
            return (int)Android.Media.Orientation.Normal;
        }
    }
}
