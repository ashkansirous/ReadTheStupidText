

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

	/// <summary>The cast, merges and narrator as the cast screen last left them (Slice 65); written with Save.</summary>
	public IReadOnlyList<CastMember> Cast { get; set; } = original.Cast;

	public IReadOnlyDictionary<string, string> Merges { get; set; } = original.Merges;

	public string NarratorVoiceId { get; set; } = original.NarratorVoiceId;

	public List<StoryPage> Pages { get; } = [.. original.Pages];

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
		CharacterMerges = Merges,
		NarratorVoiceId = NarratorVoiceId,
	};

	public bool HasPageImages => Pages.Any(p => p.ImageFile is not null);

	public bool TextChanged => Story.JoinPages(Pages) != Original.Text;

	/// <summary>Removes a page and keeps the page cover pointing at the same picture: the index shifts down for a page
	/// removed before it; if the cover page itself goes, the cover falls back to the first page's picture.</summary>
	public void RemoveAt(int index)
	{
		if (index < 0 || index >= Pages.Count)
		{
			return;
		}

		Pages.RemoveAt(index);
		if (CoverStyle != CoverStyle.Picture || CoverPageIndex is not { } cover)
		{
			return;
		}

		if (index < cover)
		{
			CoverPageIndex = cover - 1;
		}
		else if (index == cover)
		{
			CoverPageIndex = null;
			CoverStyle = CoverStyle.FirstPage;
		}
	}

	/// <summary>Moves a page up (-1) or down (+1); does nothing at either end.</summary>
	public void Move(int index, int direction)
	{
		int target = index + direction;
		if (index < 0 || index >= Pages.Count || target < 0 || target >= Pages.Count)
		{
			return;
		}

		(Pages[index], Pages[target]) = (Pages[target], Pages[index]);
		if (CoverStyle == CoverStyle.Picture && (CoverPageIndex == index || CoverPageIndex == target))
		{
			CoverPageIndex = CoverPageIndex == index ? target : index;
		}
	}
}
