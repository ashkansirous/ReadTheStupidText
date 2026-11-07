#if ANDROID
using PlatformTextMeasurer = DinoReader.AndroidTextMeasurer;
#elif IOS
using PlatformTextMeasurer = DinoReader.IosTextMeasurer;
#elif WINDOWS
using PlatformTextMeasurer = DinoReader.WindowsTextMeasurer;
#endif
using DinoReader.Core;
using DinoReader.Resources.Strings;

namespace DinoReader;

/// <summary>What the Record screen walks (Slice 279): the story, how it was read into sentences and speakers (in the picked story language, Slice 286), the grown-up's choice, the pieces that choice makes at this window size
/// (their keys carry <paramref name="Language"/>), and whether the screen only plays recordings back.</summary>
internal sealed record RecordSetup(Story Story, RecordBook Book, RecordChoice Choice, IReadOnlyList<RecordPiece> Pieces, bool Listening, string Language);

/// <summary>The remembered choice of one book in one story language (Slice 279, per language since Slice 286): the mode and the ticked characters are kept per book and language in the app's preferences,
/// so the intro opens on what was chosen last in each. A choice made before Slice 286 was kept per book (the old key) and carries over to the app's language.</summary>
internal static class RecordChoices
{
	private const string KeyPrefix = "dino.record.choice.";
	private const char LanguageSeparator = '.';

	private static string KeyOf(string storyId, string language) => KeyPrefix + storyId + LanguageSeparator + UiLanguage.Resolve(language);

	/// <summary>The choice remembered for <paramref name="story"/> in <paramref name="language"/>, cleaned of characters the book does not have; the defaults (mode from its recordings in that language, everybody ticked) when none is.</summary>
	public static RecordChoice Load(Story story, RecordBook book, string language)
	{
		RecordChoice? saved = Saved(story.Id, language);
		HashSet<string> everyone = book.Speakers.Select(s => s.Id).ToHashSet();
		if (saved is null)
		{
			return new RecordChoice(RecordSplitWalk.DefaultMode(story, language), everyone);
		}

		return new RecordChoice(saved.Mode, saved.Ticked.Where(everyone.Contains).ToHashSet());
	}

	/// <summary>The way <paramref name="language"/> is recorded in (the language card's count is in its unit): the remembered mode, else the one its recordings were made in.</summary>
	public static RecordMode ModeOf(Story story, string language) => Saved(story.Id, language)?.Mode ?? RecordSplitWalk.DefaultMode(story, language);

	private static RecordChoice? Saved(string storyId, string language)
	{
		RecordChoice? saved = RecordSplitWalk.Read(Preferences.Default.Get(KeyOf(storyId, language), string.Empty));
		if (saved is null && UiLanguage.Resolve(language) == StoryLanguages.Current().Main)
		{
			saved = RecordSplitWalk.Read(Preferences.Default.Get(KeyPrefix + storyId, string.Empty)); // the per-book choice of an older version
		}

		return saved;
	}

	public static void Save(string storyId, string language, RecordChoice choice) =>
		Preferences.Default.Set(KeyOf(storyId, language), RecordSplitWalk.Write(choice));

	public static void Forget(string storyId)
	{
		Preferences.Default.Remove(KeyPrefix + storyId);
		foreach ((string code, _) in LanguageNames.All)
		{
			Preferences.Default.Remove(KeyOf(storyId, code));
		}
	}
}

/// <summary>The words card's size rules and the measuring that cuts a page into parts that fit it (Slice 279, mock frames 180 and 183): the card never covers more than half the screen.</summary>
internal static class RecordCard
{
	public const double WholePageFontSize = 21;
	public const double LineFontSize = 22;
	public const double LineHeightFactor = 1.45;
	public const double PictureSideMargin = 12;
	public const double CardInset = 8;
	public const double CardPaddingX = 16;
	public const double CardPaddingY = 14;
	public const double SaysRowHeight = 24;
	public const double ColumnMaxWidth = 640;
	public const double Half = 0.5;

	// The label's real line is taller than the font size times the LineHeight multiplier (the font's own leading); a margin keeps a part from running past the card.
	private const double LeadingMargin = 1.2;

	/// <summary>The most height the card may take: half of the window's height.</summary>
	public static double MaxCardHeight(double windowHeight) => windowHeight * Half;

	/// <summary>The width the words get inside the card, in a column no wider than <see cref="ColumnMaxWidth"/>.</summary>
	public static double TextWidth(double windowWidth) =>
		Math.Min(windowWidth, ColumnMaxWidth) - 2 * (PictureSideMargin + CardInset + CardPaddingX);

	/// <summary>The most height the words of one piece may take: the card less its padding (and the "says" row for a character's line).</summary>
	public static double MaxTextHeight(double windowHeight, bool withSaysRow) =>
		MaxCardHeight(windowHeight) - 2 * CardPaddingY - (withSaysRow ? SaysRowHeight : 0);

	/// <summary>The font size the card shows its words at in a window <paramref name="windowWidth"/> by <paramref name="windowHeight"/>: the normal size, or smaller when the card there is smaller than the
	/// reference one the pieces were cut for, so the words always fit (<see cref="RecordCardFit"/>).</summary>
	public static double FittedFontSize(bool wholePage, double windowWidth, double windowHeight)
	{
		double font = wholePage ? WholePageFontSize : LineFontSize;
		return RecordCardFit.FontSize(
			font,
			TextWidth(RecordCardFit.ReferenceWindowWidth), MaxTextHeight(RecordCardFit.ReferenceWindowHeight, withSaysRow: !wholePage),
			TextWidth(windowWidth), MaxTextHeight(windowHeight, withSaysRow: !wholePage));
	}

	/// <summary>The pieces of <paramref name="book"/> for <paramref name="choice"/>, cut against the fixed reference card (never the live window), so a clip's key means the same words on every device and after a rotation.
	/// Heights come from the platform's text measurer with the reading font.</summary>
	public static IReadOnlyList<RecordPiece> Pieces(Story story, RecordBook book, RecordChoice choice, string language) =>
		RecordSplitWalk.InLanguage(Cut(book, choice, language), language);

	// Cut and measured in the picked language: its reading font, the words of its translation (Slice 286).
	private static IReadOnlyList<RecordPiece> Cut(RecordBook book, RecordChoice choice, string language)
	{
		bool whole = choice.Mode == RecordMode.WholePage;
		double fontSize = whole ? WholePageFontSize : LineFontSize;
		double maxHeight = MaxTextHeight(RecordCardFit.ReferenceWindowHeight, withSaysRow: !whole);
		double width = TextWidth(RecordCardFit.ReferenceWindowWidth);
#if ANDROID || IOS || WINDOWS
		using var measurer = new PlatformTextMeasurer(DinoFonts.ReadingFor(language), fontSize);
		Func<string, double> heightOf = text => HeightOf(text, width, fontSize, measurer);
		return whole ? RecordSplit.WholePages(book.Sentences, heightOf, maxHeight) : RecordSplit.ByCharacter(book.Sentences, choice.Ticked, heightOf, maxHeight);
#else
		return whole ? RecordSplit.WholePages(book.Sentences, t => t.Length, double.MaxValue) : RecordSplit.ByCharacter(book.Sentences, choice.Ticked);
#endif
	}

	/// <summary>The height <paramref name="text"/> takes at <paramref name="width"/>: its wrapped lines (a blank line between paragraphs counts as a line) at the label's line height.</summary>
	public static double HeightOf(string text, double width, double fontSize, ITextMeasurer measurer)
	{
		int lines = LineWrapper.WrapParagraphs(text, 0, width, measurer).Count;
		return lines * fontSize * LineHeightFactor * LeadingMargin;
	}

	/// <summary>The name shown for a speaker: the Storyteller is a word of the app, a character keeps the name the book gives (a label that shows it goes through <see cref="OwnScript"/>).</summary>
	public static string NameOf(RecordSpeaker speaker) =>
		speaker.Id == RecordSplit.Storyteller ? AppResources.Record_Storyteller : speaker.Name;

	/// <summary>The "Rex says:" line above a character's words.</summary>
	public static string SaysOf(RecordBook book, string speakerId)
	{
		RecordSpeaker speaker = book.Speakers.FirstOrDefault(s => s.Id == speakerId) ?? new RecordSpeaker(speakerId, speakerId);
		return AppResources.Format(AppResources.Record_Says, NameOf(speaker));
	}
}
