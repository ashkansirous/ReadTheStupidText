using CoreGraphics;
using Foundation;
using PdfKit;
using ReadTheStupidText.Application.Images;
using UIKit;

namespace ReadTheStupidText.Mobile.Shared;

/// <summary>
/// <see cref="IPdfPageRenderer"/> over PDFKit (<see cref="PdfDocument"/>, Slice 67): renders a
/// page on a white background, scaled so its long edge is <see cref="IPageImageKeeper.MaxLongEdge"/>,
/// and writes it as JPEG. The document is opened once per call; a page that fails to render is
/// left out of the result, a document that cannot be opened throws.
/// </summary>
public sealed class IosPdfPageRenderer : IPdfPageRenderer
{
    private const float JpegQuality = 0.85f;
    private const int QuarterTurnDegrees = 90;
    private const int HalfTurnDegrees = 180;

    public Task<IReadOnlyDictionary<int, string>> RenderPagesAsync(string pdfPath, IReadOnlyList<PdfPageTarget> pages) =>
        Task.Run(() => RenderAll(pdfPath, pages));

    private static IReadOnlyDictionary<int, string> RenderAll(string pdfPath, IReadOnlyList<PdfPageTarget> pages)
    {
        using var url = NSUrl.FromFilename(pdfPath);
        using PdfDocument document = new PdfDocument(url)
            ?? throw new FileNotFoundException(pdfPath);
        var rendered = new Dictionary<int, string>();
        foreach (PdfPageTarget target in pages.Where(p => p.PageIndex >= 0 && p.PageIndex < (int)document.PageCount))
        {
            if (TryRenderPage(document, target))
            {
                rendered[target.PageIndex] = target.OutputPath;
            }
        }

        return rendered;
    }

    private static bool TryRenderPage(PdfDocument document, PdfPageTarget target)
    {
        try
        {
            using PdfPage page = document.GetPage(target.PageIndex)
                ?? throw new IOException($"Could not open page {target.PageIndex}.");
            using UIImage image = RenderImage(page);
            WriteAtomically(image, target.OutputPath);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Could not render page {target.PageIndex} of '{target.OutputPath}': {ex.Message}");
            return false;
        }
    }

    // The thumbnail applies the page's own rotation; it is drawn onto white so a transparent
    // page reads as dark text on paper.
    private static UIImage RenderImage(PdfPage page)
    {
        CGSize size = TargetSize(page);
        using UIImage thumbnail = page.GetThumbnail(size, PdfDisplayBox.Media);
        var format = new UIGraphicsImageRendererFormat { Scale = 1, Opaque = true };
        using var renderer = new UIGraphicsImageRenderer(size, format);
        return renderer.CreateImage(context =>
        {
            UIColor.White.SetFill();
            context.FillRect(new CGRect(CGPoint.Empty, size));
            thumbnail.Draw(new CGRect(CGPoint.Empty, size));
        });
    }

    // Long edge = MaxLongEdge, after the page's rotation (a quarter turn swaps width and height).
    private static CGSize TargetSize(PdfPage page)
    {
        CGRect bounds = page.GetBoundsForBox(PdfDisplayBox.Media);
        bool quarterTurn = (int)(page.Rotation % HalfTurnDegrees) == QuarterTurnDegrees;
        double width = quarterTurn ? bounds.Height : bounds.Width;
        double height = quarterTurn ? bounds.Width : bounds.Height;
        double scale = IPageImageKeeper.MaxLongEdge / Math.Max(width, height);
        return new CGSize(Math.Max(1, Math.Round(width * scale)), Math.Max(1, Math.Round(height * scale)));
    }

    // Written to a temp file beside the target and moved into place, so a failed or interrupted
    // write never leaves a partial image at the final path; the temp file is removed on failure.
    private static void WriteAtomically(UIImage image, string outputPath)
    {
        string temp = outputPath + ".tmp";
        try
        {
            using NSData jpeg = image.AsJPEG(JpegQuality) ?? throw new IOException($"Could not encode '{outputPath}'.");
            using var tempUrl = NSUrl.FromFilename(temp);
            if (!jpeg.Save(tempUrl, NSDataWritingOptions.Atomic, out NSError? error))
            {
                throw new IOException($"Could not write '{outputPath}': {error?.LocalizedDescription}");
            }

            File.Move(temp, outputPath, overwrite: true);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    // Its own try, so a cleanup failure cannot mask the write error.
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // the stray temp file is overwritten by the next attempt
        }
    }
}
