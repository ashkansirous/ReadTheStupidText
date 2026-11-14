

namespace DinoReader.Core;

/// <summary>The editable copy of a story while the edit screens are open; nothing is saved until Save.</summary>
public sealed class StoryDraft(Story original)
{
	public Story Original { get; } = original;

	public string Title { get; set; } = original.Title;

	public CoverStyle CoverStyle { get; set; } = original.CoverStyle;

	/// <summary>The cover choices that go with <see cref="CoverStyle"/> Picture (Slice 77): a stored photo, or a page's index.</summary>
	public string? CoverFile { get; set; } = original.CoverFile;

	public int? CoverPageIndex { get; set; } = original.CoverPageIndex;

	/// <summary>A gallery photo picked as the cover but not saved yet: its temporary file. Copied into the story at Save.</summary>
	public string? PendingCoverPhoto { get; set; }

	/// <summary>Whether Save must store <see cref="PendingCoverPhoto"/> as the cover.</summary>
	public bool HasPendingCover => CoverStyle == CoverStyle.Picture && CoverPageIndex is null && PendingCoverPhoto is not null;

	public int CoverColourIndex { get; set; } = original.CoverColourIndex;

	/// <summary>Switches to a flat colour or the first page's picture, dropping the page index and photo that only go with a Picture cover.</summary>
	public void ChooseCover(CoverStyle style)
	{
		CoverStyle = style;
		if (style != CoverStyle.Picture)
		{
			CoverPageIndex = null;
			CoverFile = null;
			PendingCoverPhoto = null;
		}
	}

	/// <summary>The story's language code (Slice 66): changing it recasts the story, because who speaks is worked out in that language.</summary>
	public string Language { get; set; } = original.Language;

	public bool TranslateToMyLanguage { get; set; } = original.TranslateToMyLanguage;

	public bool LanguageChanged => Language != Original.Language;

	/// <summary>The view the book opens in; null keeps the default (Slice 151).</summary>
	public ReadingMode? PreferredMode { get; set; } = original.PreferredMode;

	/// <summary>Records that pages were built from camera photos, gallery photos of pages or PDF pages: their words are already in the pictures, so the book opens picture only. A choice already made stays; the reader can still switch.</summary>
	public void MarkPicturesAsPages() => PreferredMode ??= ReadingMode.PictureOnly;

	/// <summary>The cast as the cast screen last left them (Slice 65); written with Save.</summary>
	public IReadOnlyList<CastMember> Cast { get; set; } = original.Cast;

	/// <summary>Whether animals make their sounds in the reading (Slice 117), set on the cast page.</summary>
	public bool AnimalSoundsOn { get; set; } = original.AnimalSoundsOn;

	/// <summary>The casts of the other reading languages as the cast screen last left them (Slice 108); written with Save.</summary>
	public IReadOnlyDictionary<string, IReadOnlyList<CastMember>>? LanguageCasts { get; set; } = original.LanguageCasts;

	public List<StoryPage> Pages { get; } = [.. original.Pages];

	public const string DraftImagePrefix = "draft-";

	/// <summary>The wizard's pages (Slice 78), in grid order. <see cref="Pages"/> follows them (<see cref="SyncPages"/>) so the cast
	/// and the cover picker keep reading the story-shaped list; a picture is named <see cref="DraftImageName"/> there.</summary>
	public List<PageDraft> PageDrafts { get; } = [];

	/// <summary>The file name a draft page's picture goes by in <see cref="Pages"/> until Save writes the real file.</summary>
	public static string DraftImageName(string pageId) => DraftImagePrefix + pageId;

	/// <summary>Loads the original story's pages as drafts; <paramref name="resolve"/> turns a stored picture name into its path.</summary>
	public void LoadPageDrafts(Func<string?, string?> resolve)
	{
		PageDrafts.Clear();
		PageDrafts.AddRange(Original.Pages.Select(p => PageDraft.FromStory(p, resolve(p.ImageFile))));
		SyncPages();
	}

	/// <summary>Counts every change to the pages, so a screen can tell whether what it shows is out of date.</summary>
	public int Version { get; private set; }

	/// <summary>Makes <see cref="Pages"/> the story-shaped copy of <see cref="PageDrafts"/>.</summary>
	public void SyncPages()
	{
		Version++;
		Pages.Clear();
		Pages.AddRange(PageDrafts.Select(ToStoryPage));
	}

	private static StoryPage ToStoryPage(PageDraft draft) => new(
		draft.Text,
		draft.HasPicture ? DraftImageName(draft.Id) : null,
		draft.Ocr is { HasText: true } ocr ? ocr.Lines : null,
		draft.Ocr is { HasText: true } read ? read.Regions : null);

	public void AddPageDraft(PageDraft page) => InsertPageDraft(PageDrafts.Count, page);

	/// <summary>Adds several pages at the end, syncing <see cref="Pages"/> once (a long PDF is one change, not one per page).</summary>
	public void AddPageDrafts(IEnumerable<PageDraft> pages)
	{
		bool added = false;
		foreach (PageDraft page in pages)
		{
			InsertWithoutSync(PageDrafts.Count, page);
			added = true;
		}

		if (added)
		{
			SyncPages();
		}
	}

	public void InsertPageDraft(int at, PageDraft page)
	{
		InsertWithoutSync(at, page);
		SyncPages();
	}

	private void InsertWithoutSync(int at, PageDraft page)
	{
		int index = Math.Clamp(at, 0, PageDrafts.Count);
		PageOrder.InsertAt(PageDrafts, index, page);
		if (CoverStyle == CoverStyle.Picture)
		{
			CoverPageIndex = PageOrder.CoverAfterInsert(CoverPageIndex, index);
		}
	}

	/// <summary>Removes a page; a page cover stays on its picture, and falls back to the first page's when its own page goes.</summary>
	public void RemovePageDraft(int index)
	{
		if (index < 0 || index >= PageDrafts.Count)
		{
			return;
		}

		PageOrder.Remove(PageDrafts, index);
		if (CoverStyle == CoverStyle.Picture && CoverPageIndex is not null)
		{
			CoverPageIndex = PageOrder.CoverAfterRemove(CoverPageIndex, index);
			CoverStyle = CoverPageIndex is null ? CoverStyle.FirstPage : CoverStyle.Picture;
		}

		SyncPages();
	}

	public void MovePageDraft(int from, int to)
	{
		PageOrder.Move(PageDrafts, from, to);
		if (CoverStyle == CoverStyle.Picture)
		{
			CoverPageIndex = PageOrder.CoverAfterMove(CoverPageIndex, from, to);
		}

		SyncPages();
	}

	/// <summary>Puts the pages in the order of <paramref name="ids"/> (a drag in the grid). Ignored unless it names exactly the
	/// current pages. A page cover follows its page.</summary>
	public void ReorderTo(IReadOnlyList<string> ids)
	{
		if (ids.Count != PageDrafts.Count || !ids.Order().SequenceEqual(PageDrafts.Select(p => p.Id).Order()))
		{
			return;
		}

		string? coverId = CoverStyle == CoverStyle.Picture && CoverPageIndex is { } c && c < PageDrafts.Count ? PageDrafts[c].Id : null;
		List<PageDraft> ordered = ids.Select(id => PageDrafts.First(p => p.Id == id)).ToList();
		PageDrafts.Clear();
		PageDrafts.AddRange(ordered);
		if (coverId is not null)
		{
			CoverPageIndex = ids.ToList().IndexOf(coverId);
		}

		SyncPages();
	}

	/// <summary>Swaps in the changed version of a page (same <see cref="PageDraft.Id"/>).</summary>
	public void ReplacePageDraft(PageDraft page)
	{
		int index = PageDrafts.FindIndex(p => p.Id == page.Id);
		if (index >= 0)
		{
			PageDrafts[index] = page;
			SyncPages();
		}
	}

	/// <summary>Swaps in a page that was worked on in the background (its reading), only if the page is still there and still shows
	/// the same picture; a page rotated, cropped or deleted meanwhile keeps its newer state. True when it was applied.</summary>
	public bool ReplaceIfSamePicture(PageDraft worked)
	{
		int index = PageDrafts.FindIndex(p => p.Id == worked.Id);
		if (index < 0 || !PageDrafts[index].SamePictureAs(worked))
		{
			return false;
		}

		PageDrafts[index] = worked;
		SyncPages();
		return true;
	}

	/// <summary>The story as the draft stands: the original with every edit made so far.</summary>
	public Story Current => Original with
	{
		Title = Title,
		Language = Language,
		TranslateToMyLanguage = TranslateToMyLanguage,
		CoverStyle = CoverStyle,
		CoverFile = CoverFile,
		CoverPageIndex = CoverPageIndex,
		CoverColourIndex = CoverColourIndex,
		Pages = Pages.ToList(),
		Cast = Cast,
		LanguageCasts = RecastNeeded ? null : LanguageCasts,
		AnimalSoundsOn = AnimalSoundsOn,
		PreferredMode = PreferredMode,
	};

	public bool HasPageImages => Pages.Any(p => p.ImageFile is not null);

	/// <summary>Other languages' casts were made from the original text and language: an edit of either has them rebuilt on the next read (Slice 108). The draft keeps them, so undoing the edit brings them back.</summary>
	public bool RecastNeeded => TextChanged || LanguageChanged;

	public bool TextChanged => Story.JoinPages(Pages) != Original.Text;
}
