using DinoReader.Core;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Mobile;
using AppImages = ReadTheStupidText.Application.Images;

namespace DinoReader;

/// <summary>
/// Turns the pages of a scan, a photo set or a PDF (Slice 63's positioned extraction) into the pages a story
/// keeps (Slice 64): the text of each page, its recognised lines with their boxes and its kept picture, copied
/// into the story folder. Pages that read no text are left out.
/// </summary>
public sealed class StoryImport(IPageImageKeeper keeper)
{
	private const string PictureNameFormat = "page-{0}.jpg";
	private const string ImportFolderPrefix = "import-";

	/// <summary>The pages that carry text.</summary>
	public static IReadOnlyList<PositionedPage> WithText(IEnumerable<PositionedPage> pages) =>
		pages.Where(p => p.Lines.Count > 0).ToList();

	/// <summary>The story text of the pages: each page's text, a blank line between pages.</summary>
	public static string TextOf(IEnumerable<PositionedPage> pages) =>
		Story.JoinPages(pages.Select(p => new StoryPage(PageTextOf(p))));

	/// <summary>Reads each scanned picture (scanner or photo locations) into a page, in order, with its picture path.</summary>
	public static async Task<IReadOnlyList<PositionedPage>> ReadPicturesAsync(
		OcrPositionedTextExtractor ocr, IReadOnlyList<string> locations)
	{
		var pages = new List<PositionedPage>();
		Exception? failure = null;
		for (int i = 0; i < locations.Count; i++)
		{
			string path = ScannedText.ToLocalPath(locations[i]) ?? locations[i];
			try
			{
				pages.Add(await ocr.ExtractPageAsync(path, i, CancellationToken.None));
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				// One unreadable page must not lose the pages that did read.
				failure = ex;
				System.Diagnostics.Debug.WriteLine($"Dino Reader could not read page {i}: {ex.Message}");
				ScannedImageFiles.TryDelete(path);
			}
		}

		return pages.Count == 0 && failure is not null ? throw failure : pages;
	}

	/// <summary>Moves each page's temporary picture into a folder of its own beside it, so a later import (which
	/// renders into the shared folder under the same names) cannot overwrite or delete these. A picture that cannot
	/// be moved stays where it is.</summary>
	public static IReadOnlyList<PositionedPage> MoveToOwnFolder(IReadOnlyList<PositionedPage> pages)
	{
		string? folder = null;
		var moved = new List<PositionedPage>();
		foreach (PositionedPage page in pages)
		{
			string? target = page.ImagePath is null ? null : TryMove(page.ImagePath, ref folder);
			moved.Add(target is null ? page : page with { ImagePath = target });
		}

		return moved;
	}

	private static string? TryMove(string source, ref string? folder)
	{
		try
		{
			folder ??= Path.Combine(Path.GetDirectoryName(source)!, ImportFolderPrefix + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(folder);
			string target = Path.Combine(folder, Path.GetFileName(source));
			File.Move(source, target, overwrite: true);
			return target;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not move {source}: {ex.Message}");
			return null;
		}
	}

	/// <summary>Deletes the scanner or picker output files (plain paths or file uris) behind locations that were never read.</summary>
	public static void DeleteLocations(IEnumerable<string> locations)
	{
		foreach (string location in locations)
		{
			ScannedImageFiles.TryDelete(location);
		}
	}

	/// <summary>Deletes the temporary pictures behind the pages (scans, photos, rendered PDF pages). The copies the
	/// story keeps are other files, so this never touches them.</summary>
	public static void DeleteSources(IEnumerable<PositionedPage> pages)
	{
		foreach (PositionedPage page in pages)
		{
			if (page.ImagePath is not null)
			{
				ScannedImageFiles.TryDelete(page.ImagePath);
				TryDeleteOwnFolder(page.ImagePath);
			}
		}
	}

	// The folder MoveToOwnFolder made, once its pictures are gone.
	private static void TryDeleteOwnFolder(string imagePath)
	{
		try
		{
			string? folder = Path.GetDirectoryName(imagePath);
			if (folder is not null && Path.GetFileName(folder).StartsWith(ImportFolderPrefix, StringComparison.Ordinal))
			{
				Directory.Delete(folder, recursive: false);
			}
		}
		catch (Exception)
		{
			// Cleanup is best effort; a leftover folder in the cache is harmless.
		}
	}

	/// <summary>Copies each page's picture into the story's folder and returns the pages a story stores.
	/// A page whose picture cannot be kept is stored as text only. <paramref name="pageTexts"/>, when given, is each
	/// page's text (a document's own flowing text, cut per page); otherwise a page's text is its recognised lines.</summary>
	public async Task<IReadOnlyList<StoryPage>> KeepAsync(
		IReadOnlyList<PositionedPage> pages, string storyId, IStoryLibrary library, IReadOnlyList<string>? pageTexts = null)
	{
		var kept = new List<StoryPage>();
		for (int i = 0; i < pages.Count; i++)
		{
			string? file = await TryKeepPictureAsync(pages[i], i, storyId, library);
			kept.Add(new StoryPage(pageTexts?[i] ?? PageTextOf(pages[i]), file, file is null ? null : LinesOf(pages[i])));
		}

		return kept;
	}

	private async Task<string?> TryKeepPictureAsync(PositionedPage page, int index, string storyId, IStoryLibrary library)
	{
		string name = string.Format(System.Globalization.CultureInfo.InvariantCulture, PictureNameFormat, index);
		string? target = library.TryResolveFile(storyId, name);
		if (page.ImagePath is null || target is null)
		{
			return null;
		}

		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(target)!);
			await keeper.KeepAsync(page.ImagePath, target);
			return name;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not keep page {index}: {ex.Message}");
			return null;
		}
	}

	private static string PageTextOf(PositionedPage page) => ImportedText.PageText(page.Lines.Select(l => l.Text));

	private static IReadOnlyList<PageLine> LinesOf(PositionedPage page) =>
		page.Lines.Select(l => new PageLine(l.Text, ToCore(l.Box))).ToList();

	private static DinoReader.Core.NormalizedRect ToCore(AppImages.NormalizedRect r) => new(r.Left, r.Top, r.Width, r.Height);
}
