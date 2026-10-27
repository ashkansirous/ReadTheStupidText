using DinoReader.Core;
using ReadTheStupidText.Application.Images;
using ReadTheStupidText.Mobile.Shared;
using AppImages = ReadTheStupidText.Application.Images;

namespace DinoReader;

/// <summary>How far reading the wizard's pages has got: page <see cref="Current"/> of <see cref="Total"/> (only the pages that need reading count).</summary>
public readonly record struct ReadProgress(int Current, int Total);

/// <summary>The pages after reading, and how many pictures could not be read (those are kept as picture-only pages).</summary>
public sealed record ReadResult(IReadOnlyList<PageDraft> Pages, int Failed);

/// <summary>
/// The import work behind the page-review wizard (Slice 78, building on Slice 63's positioned extraction and Slice 64's kept
/// pictures): copies a picked picture into the wizard's work folder, reads the pages that have not been read, and writes the
/// pictures a story keeps into its folder.
/// </summary>
public sealed class StoryImport(IPageImageKeeper keeper)
{
	private const string PictureNameFormat = "page-{0}-{1}.jpg";
	private const string WorkNameFormat = "pick-{0}.jpg";
	private const double SquareAspect = 1;

	/// <summary>Deletes the scanner or picker output files (plain paths or file uris) that are no longer needed.</summary>
	public static void DeleteLocations(IEnumerable<string> locations)
	{
		foreach (string location in locations)
		{
			ScannedImageFiles.TryDelete(location);
		}
	}

	/// <summary>Copies a picked or scanned picture into <paramref name="workFolder"/> (upright, JPEG, long edge capped) and returns the copy.</summary>
	public async Task<string> CopyIntoAsync(string location, string workFolder)
	{
		string target = Path.Combine(workFolder, string.Format(System.Globalization.CultureInfo.InvariantCulture, WorkNameFormat, Guid.NewGuid().ToString("N")));
		Directory.CreateDirectory(workFolder);
		await keeper.KeepAsync(location, target);
		return target;
	}

	/// <summary>A page the PDF path already read (text layer or its own OCR): the picture <paramref name="copy"/> with those lines, and the
	/// document's own text for the page when it differs from them (<paramref name="pageText"/>).</summary>
	public static PageDraft DraftFromPositioned(PositionedPage page, string copy, string? pageText)
	{
		PageReading reading = ReadingOf(page, copy);
		PageDraft draft = PageDraft.ForPicture(copy, reading);
		return pageText is not null && pageText != reading.Text ? draft.WithText(pageText) : draft;
	}

	/// <summary>Reads every page that has a picture and no reading yet, in order, reporting each. One unreadable picture does not lose
	/// the others: it becomes a picture-only page and is counted in <see cref="ReadResult.Failed"/>.</summary>
	public static async Task<ReadResult> ReadPagesAsync(
		OcrPositionedTextExtractor ocr, IReadOnlyList<PageDraft> pages, IProgress<ReadProgress>? progress, CancellationToken cancel)
	{
		int total = pages.Count(p => p.NeedsOcr);
		var result = new List<PageDraft>(pages.Count);
		int done = 0;
		int failed = 0;
		foreach (PageDraft page in pages)
		{
			if (!page.NeedsOcr)
			{
				result.Add(page);
				continue;
			}

			progress?.Report(new ReadProgress(++done, total));
			(PageDraft read, bool ok) = await TryReadAsync(ocr, page, result.Count, cancel);
			failed += ok ? 0 : 1;
			result.Add(read);
		}

		return new ReadResult(result, failed);
	}

	private static async Task<(PageDraft Page, bool Ok)> TryReadAsync(OcrPositionedTextExtractor ocr, PageDraft page, int index, CancellationToken cancel)
	{
		string? file = page.PictureFile;
		if (file is null)
		{
			return (page.WithOcr(new PageReading([])), false);
		}

		try
		{
			PositionedPage read = await ocr.ExtractPageAsync(file, index, cancel);
			return (page.WithOcr(await Task.Run(() => ReadingOf(read, file), cancel)), true);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not read page {index}: {ex.Message}");
			return (page.WithOcr(new PageReading([])), false);
		}
	}

	/// <summary>Copies each page's picture into the story's folder under a fresh name (<c>page-&lt;token&gt;-&lt;n&gt;.jpg</c>, so an edit of an
	/// existing story never overwrites a picture it still reads from) and returns the pages a story stores. A picture that cannot be
	/// kept fails the whole call (after removing what it copied), so a save never drops a picture and then deletes the original.</summary>
	public static async Task<IReadOnlyList<StoryPage>> KeepAsync(IReadOnlyList<PageDraft> pages, string storyId, IStoryLibrary library, string token)
	{
		var kept = new List<StoryPage>();
		try
		{
			for (int i = 0; i < pages.Count; i++)
			{
				string? name = await KeepPictureAsync(pages[i], i, storyId, library, token);
				PageReading? ocr = name is not null && pages[i].Ocr is { HasText: true } read ? read : null;
				kept.Add(new StoryPage(pages[i].Text, name, ocr?.Lines, ocr?.Regions));
			}
		}
		catch
		{
			DeletePictures(library, storyId, kept.Select(p => p.ImageFile));
			throw;
		}

		return kept;
	}

	/// <summary>Removes the named story pictures; one that cannot be removed is logged and skipped.</summary>
	public static void DeletePictures(IStoryLibrary library, string storyId, IEnumerable<string?> names)
	{
		foreach (string? name in names)
		{
			try
			{
				if (library.TryResolveFile(storyId, name) is { } path)
				{
					File.Delete(path);
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Dino Reader could not remove a page picture: {ex.Message}");
			}
		}
	}

	// Null for a text-only page; anything else that stops the picture being written throws.
	private static async Task<string?> KeepPictureAsync(PageDraft page, int index, string storyId, IStoryLibrary library, string token)
	{
		if (!page.HasPicture)
		{
			return null;
		}

		string name = string.Format(System.Globalization.CultureInfo.InvariantCulture, PictureNameFormat, token, index);
		string source = page.PictureFile ?? throw new IOException($"Page {index} has a picture that was never written.");
		string target = library.TryResolveFile(storyId, name) ?? throw new IOException($"Page {index} has no place in the story folder.");
		Directory.CreateDirectory(Path.GetDirectoryName(target)!);
		try
		{
			await Task.Run(() => File.Copy(source, target, overwrite: true));
		}
		catch
		{
			DeletePictures(library, storyId, [name]);
			throw;
		}

		return name;
	}

	private static PageReading ReadingOf(PositionedPage page, string pictureFile)
	{
		List<PageLine> lines = page.Lines.Select(l => new PageLine(l.Text, ToCore(l.Box))).ToList();
		Size? size = PageImageSize.Read(pictureFile);
		double aspect = size is { Width: > 0, Height: > 0 } s ? s.Width / s.Height : SquareAspect;
		return new PageReading(lines, RegionsOf(lines, aspect));
	}

	/// <summary>The page's text regions (Slice 79): its lines grouped into blocks, each marked story or background by
	/// <see cref="TextRegions"/>; the grown-up can flip any of them. Null for a page with no lines. The boxes are 0..1 on each axis,
	/// so x is scaled by the picture's <paramref name="aspect"/> (width over height) to make both axes the same unit, as the
	/// classifier compares heights with widths and gaps.</summary>
	public static IReadOnlyList<PageRegion>? RegionsOf(IReadOnlyList<PageLine> lines, double aspect = SquareAspect)
	{
		var ocrLines = lines.Select(l => new OcrLine(l.Text, new OcrBox((float)(l.Box.X * aspect), (float)l.Box.Y, (float)(l.Box.Right * aspect), (float)l.Box.Bottom))).ToList();
		var indexOf = new Dictionary<OcrLine, int>(ReferenceEqualityComparer.Instance);
		for (int i = 0; i < ocrLines.Count; i++)
		{
			indexOf[ocrLines[i]] = i;
		}

		IReadOnlyList<TextRegion> regions = TextRegions.Classify(new OcrReading(ocrLines, 1, 1));
		return regions.Count == 0 ? null : regions.Select(r => ToPageRegion(r, indexOf, aspect)).ToList();
	}

	private static PageRegion ToPageRegion(TextRegion region, Dictionary<OcrLine, int> indexOf, double aspect)
	{
		OcrBox b = region.Bounds;
		var box = new DinoReader.Core.NormalizedRect(b.Left / aspect, b.Top, b.Width / aspect, b.Height);
		return new PageRegion(box, region.Lines.Select(l => indexOf[l]).Order().ToList(), region.Class == RegionClass.Background);
	}

	private static DinoReader.Core.NormalizedRect ToCore(AppImages.NormalizedRect r) => new(r.Left, r.Top, r.Width, r.Height);
}

/// <summary>
/// Writes the wizard's draft as a story (Slice 78): the page pictures under fresh names, the cover photo (Slice 77's
/// <c>cover-&lt;id&gt;.jpg</c>), the recast cast, then the story file. A failed save removes what it wrote, and a saved edit removes the
/// pictures the story no longer uses, so the story folder only ever holds files the story reads.
/// </summary>
public sealed class StorySaver(IStoryLibrary library, IPageImageKeeper keeper, StoryVoicing voicing)
{
	private const int TokenLength = 8;

	public async Task SaveAsync(StoryDraft draft, bool isNew, string title)
	{
		string id = draft.Original.Id;
		string token = Guid.NewGuid().ToString("N")[..TokenLength];
		IReadOnlyList<StoryPage> pages = [];
		string? coverFile = null;
		try
		{
			pages = await StoryImport.KeepAsync(draft.PageDrafts, id, library, token);
			coverFile = await StoreCoverFileAsync(draft);
			string text = Story.JoinPages(pages);
			bool recast = isNew || draft.RecastNeeded;
			IReadOnlyList<CastMember> cast = recast
				? await voicing.CastAsync(draft.Language, text, draft.Cast, Story.BreakMapOf(pages))
				: draft.Cast;
			Story story = draft.Original with
			{
				Title = title, Language = draft.Language, TranslateToMyLanguage = draft.TranslateToMyLanguage, Pages = pages,
				CoverStyle = draft.CoverStyle, CoverFile = coverFile, CoverPageIndex = draft.CoverPageIndex,
				CoverColourIndex = draft.CoverColourIndex, Cast = cast, LanguageCasts = recast ? null : draft.LanguageCasts, AnimalSoundsOn = draft.AnimalSoundsOn, PreferredMode = draft.PreferredMode,
			};
			story = KeepMatchingRecordings(draft.Original, story);
			await (isNew ? library.AddAsync(story) : library.UpdateAsync(story));
		}
		catch
		{
			await DiscardWrittenAsync(draft, isNew, pages, coverFile);
			throw;
		}

		PickedPhotoCache.Delete(draft.PendingCoverPhoto);
		if (!isNew)
		{
			await TryDeleteReplacedAsync(draft.Original, pages, coverFile);
		}
	}

	// A parent's recording stays only with a page that is still there with the same text (Slice 111); the rest read in the generated voice again, and their files are deleted once the story is saved.
	private static Story KeepMatchingRecordings(Story original, Story edited)
	{
		IReadOnlyDictionary<int, PageRecording>? kept = StoryRecordings.Kept(original, edited.Pages);
		return edited with { Recordings = kept, UseRecordings = kept is not null && original.UseRecordings };
	}

	// The story is saved by now: a file that cannot be removed is only left behind, it must not make the save look failed.
	private async Task TryDeleteReplacedAsync(Story original, IReadOnlyList<StoryPage> pages, string? coverFile)
	{
		try
		{
			await DeleteReplacedAsync(original, pages, coverFile);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not remove a replaced file: {ex.Message}");
		}
	}

	// A photo picked as the cover is copied into the story only now, under a fresh name, so cancelling leaves nothing behind and
	// the image cache never shows the old photo. A cover that is not a photo keeps no file.
	private async Task<string?> StoreCoverFileAsync(StoryDraft draft)
	{
		if (draft.HasPendingCover)
		{
			return await library.StoreFileAsync(draft.Original.Id, Story.CoverFilePrefix, Story.CoverFileExtension,
				target => keeper.KeepAsync(draft.PendingCoverPhoto!, target));
		}

		return draft.CoverStyle == CoverStyle.Picture && draft.CoverPageIndex is null ? draft.CoverFile : null;
	}

	// Clearing half-written files must never hide the error that made the save fail.
	private async Task DiscardWrittenAsync(StoryDraft draft, bool isNew, IReadOnlyList<StoryPage> pages, string? coverFile)
	{
		try
		{
			if (isNew)
			{
				await library.DeleteAsync(draft.Original.Id);
				return;
			}

			StoryImport.DeletePictures(library, draft.Original.Id, pages.Select(p => p.ImageFile));
			if (coverFile is not null && coverFile != draft.Original.CoverFile)
			{
				await library.DeleteFileAsync(draft.Original.Id, coverFile);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Dino Reader could not remove a half-saved story's files: {ex.Message}");
		}
	}

	private async Task DeleteReplacedAsync(Story original, IReadOnlyList<StoryPage> pages, string? coverFile)
	{
		foreach (string name in StoryRecordings.Dropped(original, StoryRecordings.Kept(original, pages)))
		{
			await library.DeleteFileAsync(original.Id, name);
		}

		var keep = pages.Select(p => p.ImageFile).ToHashSet();
		StoryImport.DeletePictures(library, original.Id, original.Pages.Select(p => p.ImageFile).Where(f => !keep.Contains(f)));
		if (original.CoverFile is not null && original.CoverFile != coverFile)
		{
			await library.DeleteFileAsync(original.Id, original.CoverFile);
		}
	}
}
