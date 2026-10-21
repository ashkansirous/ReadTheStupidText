using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Application.Logging;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ReadTheStupidText.Mobile;

/// <summary>
/// <see cref="IPdfPageRenderer"/> over <c>Windows.Data.Pdf</c> (Slice 67): renders a page on a
/// white background scaled so its long edge is <see cref="IPageImageKeeper.MaxLongEdge"/>, and
/// writes a JPEG. The document is opened once per call; a page that fails to render is left out of
/// the result, a document that cannot be opened throws.
/// </summary>
public sealed class WindowsPdfPageRenderer : IPdfPageRenderer
{
	private static readonly Windows.UI.Color Paper = Windows.UI.Color.FromArgb(255, 255, 255, 255);

	private readonly ISystemLog _log;

	public WindowsPdfPageRenderer(ISystemLog log) => _log = log;

	public async Task<IReadOnlyDictionary<int, string>> RenderPagesAsync(string pdfPath, IReadOnlyList<PdfPageTarget> pages)
	{
		StorageFile file = await StorageFile.GetFileFromPathAsync(pdfPath).AsTask().ConfigureAwait(false);
		PdfDocument document = await PdfDocument.LoadFromFileAsync(file).AsTask().ConfigureAwait(false);
		var rendered = new Dictionary<int, string>();
		foreach (PdfPageTarget target in pages.Where(p => p.PageIndex >= 0 && p.PageIndex < document.PageCount))
		{
			if (await TryRenderPageAsync(document, target).ConfigureAwait(false))
			{
				rendered[target.PageIndex] = target.OutputPath;
			}
		}

		return rendered;
	}

	// Written to a temp file beside the target and moved into place, so a failed write never leaves
	// a partial image at the final path.
	private async Task<bool> TryRenderPageAsync(PdfDocument document, PdfPageTarget target)
	{
		string temp = target.OutputPath + ".tmp";
		try
		{
			using PdfPage page = document.GetPage((uint)target.PageIndex);
			await RenderToFileAsync(page, temp).ConfigureAwait(false);
			File.Move(temp, target.OutputPath, overwrite: true);
			return true;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			_log.Warning($"Could not render page {target.PageIndex} of the PDF", error: ex);
			TryDelete(temp);
			return false;
		}
	}

	// Its own try, so a cleanup failure cannot mask the render error.
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

	// The page's own rotation is already in Size.
	private static async Task RenderToFileAsync(PdfPage page, string path)
	{
		Windows.Foundation.Size size = page.Size;
		double scale = IPageImageKeeper.MaxLongEdge / Math.Max(size.Width, size.Height);
		var options = new PdfPageRenderOptions
		{
			DestinationWidth = WindowsImageScale.Scaled(size.Width, scale),
			DestinationHeight = WindowsImageScale.Scaled(size.Height, scale),
			BackgroundColor = Paper,
		};

		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		using var stream = new InMemoryRandomAccessStream();
		await page.RenderToStreamAsync(stream, options).AsTask().ConfigureAwait(false);
		// RenderToStreamAsync leaves the position at the end; the decoder reads from the current position.
		stream.Seek(0);
		BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream).AsTask().ConfigureAwait(false);
		using SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync().AsTask().ConfigureAwait(false);
		await WindowsJpeg.WriteAsync(bitmap, path).ConfigureAwait(false);
	}
}
