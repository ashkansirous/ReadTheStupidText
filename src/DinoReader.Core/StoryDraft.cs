

namespace DinoReader.Core;

/// <summary>The editable copy of a story while the edit screens are open; nothing is saved until Save.</summary>
public sealed class StoryDraft(Story original)
{
	public Story Original { get; } = original;

	public string Title { get; set; } = original.Title;

	public CoverStyle CoverStyle { get; set; } = original.CoverStyle;

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
		Pages = Pages.ToList(),
		Cast = Cast,
		CharacterMerges = Merges,
		NarratorVoiceId = NarratorVoiceId,
	};

	public bool HasPageImages => Pages.Any(p => p.ImageFile is not null);

	public bool TextChanged => Story.JoinPages(Pages) != Original.Text;

	/// <summary>Moves a page up (-1) or down (+1); does nothing at either end.</summary>
	public void Move(int index, int direction)
	{
		int target = index + direction;
		if (index < 0 || index >= Pages.Count || target < 0 || target >= Pages.Count)
		{
			return;
		}

		(Pages[index], Pages[target]) = (Pages[target], Pages[index]);
	}
}
