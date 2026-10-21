using Android.Graphics;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// <see cref="IPageImageKeeper"/> for Android (Slice 63): decodes a scanned or photographed page
/// sampled down toward <see cref="IPageImageKeeper.MaxLongEdge"/>, turns it upright from its EXIF
/// orientation, scales it so the long edge fits, and writes a JPEG.
/// </summary>
public sealed class AndroidPageImageKeeper : IPageImageKeeper
{
    private const int JpegQuality = 85;

    private readonly Android.Content.Context _context;

    public AndroidPageImageKeeper(Android.Content.Context context) => _context = context;

    public Task KeepAsync(string location, string destinationPath) =>
        Task.Run(() => Keep(location, destinationPath));

    private void Keep(string location, string destinationPath)
    {
        RejectOverwritingSource(location, destinationPath);
        using Bitmap decoded = Decode(location);
        using Bitmap upright = ImageOrientation.Apply(decoded, AndroidImageSource.ReadOrientation(_context, location));
        using Bitmap fitted = Fit(upright);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destinationPath)!);
        using FileStream output = File.Create(destinationPath);
        fitted.Compress(Bitmap.CompressFormat.Jpeg!, JpegQuality, output);
    }

    // The kept copy must be a different file from the source, which the caller deletes afterwards.
    private static void RejectOverwritingSource(string location, string destinationPath)
    {
        string? source = ScannedText.ToLocalPath(location);
        if (source is not null && string.Equals(System.IO.Path.GetFullPath(source), System.IO.Path.GetFullPath(destinationPath), StringComparison.Ordinal))
        {
            throw new ArgumentException("The kept page image must not be the scanned source file.", nameof(destinationPath));
        }
    }

    private Bitmap Decode(string location)
    {
        var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
        using (Stream probe = AndroidImageSource.Open(_context, location))
        {
            BitmapFactory.DecodeStream(probe, null, bounds);
        }

        var options = new BitmapFactory.Options { InSampleSize = SampleSize(Math.Max(bounds.OutWidth, bounds.OutHeight)) };
        using Stream stream = AndroidImageSource.Open(_context, location);
        return BitmapFactory.DecodeStream(stream, null, options)
            ?? throw new IOException($"Could not decode the image '{location}'.");
    }

    // Largest power of two that keeps the long edge at or above the cap.
    private static int SampleSize(int longestSide)
    {
        int sample = 1;
        while (longestSide / (sample * 2) >= IPageImageKeeper.MaxLongEdge)
        {
            sample *= 2;
        }

        return sample;
    }

    // The source itself when it already fits, as ImageOrientation.Apply does; callers dispose both.
    private static Bitmap Fit(Bitmap source)
    {
        int longest = Math.Max(source.Width, source.Height);
        if (longest <= IPageImageKeeper.MaxLongEdge)
        {
            return source;
        }

        float scale = (float)IPageImageKeeper.MaxLongEdge / longest;
        return Bitmap.CreateScaledBitmap(source, (int)Math.Round(source.Width * scale), (int)Math.Round(source.Height * scale), true)!;
    }
}
