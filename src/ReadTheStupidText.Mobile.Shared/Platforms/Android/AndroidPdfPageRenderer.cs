using Android.Graphics;
using Android.Graphics.Pdf;
using Android.OS;
using ReadTheStupidText.Application.Images;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// <see cref="IPdfPageRenderer"/> over Android's <see cref="PdfRenderer"/> (Slice 63): renders a
/// page on a white background, scaled so its long edge is <see cref="IPageImageKeeper.MaxLongEdge"/>,
/// and writes it as JPEG. The document is opened once per call; the renderer allows one open page at a time, so calls are serialized.
/// </summary>
public sealed class AndroidPdfPageRenderer : IPdfPageRenderer
{
    private const int JpegQuality = 85;

    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyDictionary<int, string>> RenderPagesAsync(string pdfPath, IReadOnlyList<PdfPageTarget> pages)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() => RenderAll(pdfPath, pages)).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    // One PdfRenderer for the whole set; a page that fails to render is skipped, not fatal.
    private static Dictionary<int, string> RenderAll(string pdfPath, IReadOnlyList<PdfPageTarget> pages)
    {
        using ParcelFileDescriptor descriptor = ParcelFileDescriptor.Open(new Java.IO.File(pdfPath), ParcelFileMode.ReadOnly)
            ?? throw new FileNotFoundException(pdfPath);
        using var renderer = new PdfRenderer(descriptor);
        var rendered = new Dictionary<int, string>();
        foreach (PdfPageTarget target in pages.Where(p => p.PageIndex >= 0 && p.PageIndex < renderer.PageCount))
        {
            if (TryRenderPage(renderer, target))
            {
                rendered[target.PageIndex] = target.OutputPath;
            }
        }

        return rendered;
    }

    private static bool TryRenderPage(PdfRenderer renderer, PdfPageTarget target)
    {
        try
        {
            using PdfRenderer.Page page = renderer.OpenPage(target.PageIndex) ?? throw new IOException($"Could not open page {target.PageIndex}.");
            using Bitmap bitmap = RenderBitmap(page);
            WriteAtomically(bitmap, target.OutputPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or Java.Lang.Exception or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine($"Could not render page {target.PageIndex} of '{target.OutputPath}': {ex.Message}");
            return false;
        }
    }

    // Written to a temp file beside the target and moved into place, so a failed or interrupted
    // write never leaves a partial image at the final path; the temp file is removed on failure.
    private static void WriteAtomically(Bitmap bitmap, string outputPath)
    {
        string temp = outputPath + ".tmp";
        try
        {
            using (FileStream output = File.Create(temp))
            {
                if (!bitmap.Compress(Bitmap.CompressFormat.Jpeg!, JpegQuality, output))
                {
                    throw new IOException($"Could not encode '{outputPath}'.");
                }
            }

            File.Move(temp, outputPath, overwrite: true);
        }
        catch
        {
            File.Delete(temp);
            throw;
        }
    }

    private static Bitmap RenderBitmap(PdfRenderer.Page page)
    {
        float scale = (float)IPageImageKeeper.MaxLongEdge / Math.Max(page.Width, page.Height);
        int width = Math.Max(1, (int)Math.Round(page.Width * scale));
        int height = Math.Max(1, (int)Math.Round(page.Height * scale));
        Bitmap bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!)!;
        bitmap.EraseColor(Android.Graphics.Color.White);
        page.Render(bitmap, null, null, PdfRenderMode.ForDisplay);
        return bitmap;
    }
}
