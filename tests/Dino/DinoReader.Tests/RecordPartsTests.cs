using DinoReader.BookBuilder;
using DinoReader.Core;
using Sirous.Reading.Application.Reading;

namespace DinoReader.Tests;

/// <summary>Slice 395: a character's card holds only that character's words (a run of one speaker's quote parts), a part clip stands in for its parts only, and the older sentence clips keep working.</summary>
public sealed class RecordPartsTests
{
    private static Story Freddy()
    {
        string md = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "content", "dino", "freddy-and-the-slipper", "story.md"));
        string[] pages = md.Replace("\r\n", "\n").Split("\n---\n", StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
        return new Story(StoryIds.New(), "Freddy", "en", DateTimeOffset.UnixEpoch, pages.Select(t => new StoryPage(t)).ToList(), [], CoverStyle.Colour, 0, string.Empty);
    }

    private static IReadOnlySet<string> Ticked(RecordBook book, params string[] names) =>
        book.Speakers.Where(s => names.Contains(s.Name)).Select(s => s.Id).ToHashSet();

    private static string IdOf(RecordBook book, string name) => book.Speakers.Single(s => s.Name == name).Id;

    [Fact]
    public void Mummys_cards_hold_only_her_words_and_a_said_Mummy_splits_them()
    {
        RecordBook book = RecordSplitBook.Of(Freddy());

        IReadOnlyList<RecordPiece> pieces = RecordSplit.ByCharacter(book.Sentences, Ticked(book, "Mummy", "Tummy"));

        Assert.Contains(pieces, p => p.Text == "Dinner, my dear!\nI've made you some stew. Come and sit over here!".Replace('\n', ' '));
        Assert.Contains(pieces, p => p.Text == "Just one little spoonful,");
        Assert.Contains(pieces, p => p.Text == "It's yummy! It's chicken and peas!");
        Assert.Contains(pieces, p => p.Text == "Psst! Freddy! I'm empty!");
        Assert.All(pieces, p => Assert.True(p.Key.IsPart));
        Assert.Contains(pieces, p => p.Text.StartsWith("Dinner") && p.Key.Page == 1 && p.Key.First == 1 && p.Key.Last == 1); // the narration "One evening his Mummy called," is part 0 of page 2: the file is rec-en-2-p2-2-*.m4a (the Maestro flow reads it)
    }

    [Fact]
    public void No_narration_is_on_a_characters_card_and_Freddys_line_is_Freddys()
    {
        RecordBook book = RecordSplitBook.Of(Freddy());
        string mummy = IdOf(book, "Mummy");

        IReadOnlyList<RecordPiece> mummyCards = RecordSplit.ByCharacter(book.Sentences, new HashSet<string> { mummy });
        IReadOnlyList<RecordPiece> freddyCards = RecordSplit.ByCharacter(book.Sentences, Ticked(book, "Freddy"));

        Assert.DoesNotContain(mummyCards, p => p.Text.Contains("One evening") || p.Text.Contains("said Mummy") || p.Text.Contains("No room"));
        Assert.Contains(freddyCards, p => p.Text.Contains("No room in my tummy!"));
    }

    [Fact]
    public void A_part_clip_is_recorded_for_its_parts_and_an_old_sentence_clip_still_counts()
    {
        Story story = Freddy();
        RecordBook book = RecordSplitBook.Of(story);
        RecordPiece dinner = RecordSplit.ByCharacter(book.Sentences, Ticked(book, "Mummy")).First(p => p.Text.StartsWith("Dinner"));
        RecordPiece keyed = RecordSplitWalk.InLanguage([dinner], "en")[0];
        Assert.False(RecordSplitWalk.IsRecorded(story, keyed));

        Story old = StoryRecordings.WithClip(story, new StoryClipKey(keyed.Key.Page, keyed.Cover!.Value.First, keyed.Cover.Value.Last, "en"), new PageRecording("en", 3, "rec-en-2-1-2.m4a"));
        Assert.True(RecordSplitWalk.IsRecorded(old, keyed)); // Decision 5

        Story fresh = StoryRecordings.WithClip(old, keyed.Key, new PageRecording("en", 3, "rec-en-2-p2-2-0a0a0a0a.m4a"), keyed.Cover);
        Assert.True(RecordSplitWalk.IsRecorded(fresh, keyed));
        Assert.Single(StoryRecordings.Clips(fresh)); // the part clip replaced the sentence clip it sits in
    }

    [Fact]
    public void A_part_clip_has_its_own_file_name_and_survives_story_json()
    {
        StoryClipKey key = StoryClipKey.OfParts(2, 1, 3, "en");
        Story story = StoryRecordings.WithClip(Freddy(), key, new PageRecording("en", 4, StoryRecordings.NewFileName(key)));

        Story back = StoryJson.Deserialize(StoryJson.Serialize(story));

        Assert.Equal("rec-en-3-p2-4.m4a", StoryRecordings.FileNameOf(key));
        Assert.True(StoryRecordings.IsRecordingFileName(StoryRecordings.FileNameOf(key)));
        Assert.True(StoryRecordings.IsRecordingFileName(StoryRecordings.NewFileName(key)));
        Assert.Equal(key, Assert.Single(StoryRecordings.Clips(back)).Key);
        Assert.NotEqual(key, new StoryClipKey(2, 1, 3, "en")); // never a sentence clip
    }

    private static (StoryClipKey Part, StoryRecordings.SentenceCover Cover) MummyDinner(Story story)
    {
        RecordBook book = RecordSplitBook.Of(story);
        RecordPiece dinner = RecordSplit.ByCharacter(book.Sentences, Ticked(book, "Mummy")).First(p => p.Text.StartsWith("Dinner"));
        return (RecordSplitWalk.InLanguage([dinner], "en")[0].Key, dinner.Cover!.Value);
    }

    [Fact]
    public void A_sentence_clip_that_is_saved_replaces_the_part_clips_it_overlaps()
    {
        Story story = Freddy();
        (StoryClipKey part, StoryRecordings.SentenceCover cover) = MummyDinner(story);
        Story withPart = StoryRecordings.WithClip(story, part, new PageRecording("en", 3, "rec-en-2-p2-2-0a0a0a0a.m4a"), cover);
        var sentences = new StoryClipKey(part.Page, cover.First, cover.Last + 1, "en");

        Story saved = StoryRecordings.WithClip(withPart, sentences, new PageRecording("en", 5, "rec-en-2-1-3-0b0b0b0b.m4a"));

        Assert.Equal(sentences, Assert.Single(StoryRecordings.Clips(saved)).Key); // Decision 5: the line is recorded again, so the older clip goes
        Assert.Equal(["rec-en-2-p2-2-0a0a0a0a.m4a"], StoryRecordings.ReplacedFiles(withPart, sentences));
    }

    [Fact]
    public void A_backup_never_brings_a_clip_that_overlaps_one_of_the_phone_in_either_kind()
    {
        Story story = Freddy();
        (StoryClipKey part, StoryRecordings.SentenceCover cover) = MummyDinner(story);
        Story withPart = StoryRecordings.WithClip(story, part, new PageRecording("en", 3, "rec-en-2-p2-2-0a0a0a0a.m4a"), cover);
        var sentences = new StoryClipKey(part.Page, cover.First, cover.Last, "en");
        Story withSentences = StoryRecordings.WithClip(story, sentences, new PageRecording("en", 3, "rec-en-2-1-2-0b0b0b0b.m4a"));

        Assert.Empty(StoryRecordings.MergeableClips(withSentences, withPart, _ => true));
        Assert.Empty(StoryRecordings.MergeableClips(withPart, withSentences, _ => true));
        Assert.True(StoryRecordings.Occupies(withSentences, part, cover));
    }

    [Fact]
    public void A_starter_update_moves_a_part_clip_only_when_the_page_has_the_same_parts()
    {
        Story story = Freddy();
        (StoryClipKey part, StoryRecordings.SentenceCover cover) = MummyDinner(story);
        Story withPart = StoryRecordings.WithClip(story, part, new PageRecording("en", 3, "rec-en-2-p2-2-0a0a0a0a.m4a"), cover);

        Assert.Single(StarterUpdate.PieceMoves(withPart, story with { Id = StoryIds.New() }));
        Assert.Equal(RecordSplitBook.PartCounts(withPart)[part.Page], RecordSplitBook.PartCounts(story)[part.Page]);
    }

    [Fact]
    public void A_take_replaces_only_the_segments_in_its_range_and_plays_once()
    {
        VoicedSegment Seg(int start, int end) => new("t", "v", start, end, start);
        VoicedSegment[] segments = [Seg(0, 10), Seg(10, 20), Seg(20, 30), Seg(30, 40)];

        IReadOnlyList<VoicedSegment> placed = RecordPartTakes.Apply(segments, [new PartTake(10, 30, "take.m4a")]);

        Assert.Equal(3, placed.Count);
        Assert.Null(PremadeClipMarker.PathOf(placed[0]));
        Assert.Equal("take.m4a", PremadeClipMarker.PathOf(placed[1]));
        Assert.Null(PremadeClipMarker.PathOf(placed[2]));
        Assert.Equal(30, placed[2].SourceStart);
    }

    private static VoicedSegment Seg(int start, int end) => new("t", "v", start, end, start);

    [Fact]
    public void A_take_that_starts_mid_take_on_a_tap_still_knows_where_it_ends_and_that_it_is_a_take()
    {
        VoicedSegment[] fromTheSecond = [Seg(10, 20), Seg(20, 30), Seg(30, 40)]; // the read began on the second segment of the take

        IReadOnlyList<VoicedSegment> placed = RecordPartTakes.Apply(fromTheSecond, [new PartTake(0, 30, "take.m4a")]);

        Assert.Equal(2, placed.Count);
        Assert.True(PremadeClipMarker.IsTake(placed[0]));
        Assert.Equal(30, PremadeClipMarker.TakeEndOf(placed[0])); // the marker carries the end: no lookup by the segment the take landed on
        Assert.False(PremadeClipMarker.IsTake(placed[1]));
        Assert.Equal(-1, PremadeClipMarker.TakeEndOf(placed[1]));
        Assert.Equal(0, placed[0].SourceStart); // the take plays from its start, so the step (and the highlight) starts there, not at the sentence that was tapped
    }

    [Fact]
    public void A_part_clip_whose_parts_the_page_lacks_overlaps_every_other_clip_of_the_page()
    {
        Story phone = Freddy();
        (StoryClipKey part, _) = MummyDinner(phone);
        int count = RecordSplitBook.PartListOf(phone).Count(p => p.Page == part.Page);
        StoryClipKey beyond = StoryClipKey.OfParts(part.Page, count - 1, count, "en");
        var sentences = new StoryClipKey(part.Page, 0, 0, "en");
        Story both = phone with { PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [beyond] = new("en", 3, "a.m4a"), [sentences] = new("en", 3, "b.m4a") } };

        Assert.Equal([beyond], StoryRecordings.Overlapping(both, sentences));
        Assert.Equal(sentences, Assert.Single(StoryRecordings.Clips(StoryRecordings.WithoutOverlaps(both))).Key); // the first clip in page order stays
    }

    [Fact]
    public void A_story_file_with_a_sentence_clip_and_a_part_clip_over_the_same_words_loads_with_one()
    {
        Story story = Freddy();
        (StoryClipKey part, StoryRecordings.SentenceCover cover) = MummyDinner(story);
        var sentences = new StoryClipKey(part.Page, cover.First, cover.Last, "en");
        Story both = story with { PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [part] = new("en", 3, null), [sentences] = new("en", 3, null) } };

        Story loaded = StoryJson.Deserialize(StoryJson.Serialize(both));

        Assert.Single(StoryRecordings.Clips(loaded)); // never both on the page: one would speak the words twice
    }

    [Fact]
    public void A_pack_clip_named_like_a_recording_is_no_take()
    {
        VoicedSegment premade = PremadeClipMarker.Mark(Seg(0, 10), "/pack/rec-en-1-0-0.m4a");

        Assert.False(PremadeClipMarker.IsTake(premade));
        Assert.Equal(-1, PremadeClipMarker.TakeEndOf(premade));
    }

    [Fact]
    public void An_animal_slot_inside_a_take_is_dropped_and_one_outside_stays()
    {
        VoicedSegment Animal(int at) => new("moo", string.Empty, at, at, at, new AnimalClipMarker(AnimalKind.Owl));
        VoicedSegment[] segments = [Animal(5), Seg(10, 20), Animal(15), Seg(20, 30), Animal(35)];

        IReadOnlyList<VoicedSegment> placed = RecordPartTakes.Apply(segments, [new PartTake(10, 30, "take.m4a")]);

        Assert.Equal(3, placed.Count);
        Assert.IsType<AnimalClipMarker>(placed[0].Transform);
        Assert.True(PremadeClipMarker.IsTake(placed[1]));
        Assert.IsType<AnimalClipMarker>(placed[2].Transform);
        Assert.Equal(35, placed[2].SourceStart);
    }

    [Fact]
    public void Every_view_of_a_pages_parts_agrees_with_the_one_list_on_every_Freddy_page()
    {
        Story story = Freddy();
        IReadOnlyList<RecordPart> list = RecordSplitBook.PartListOf(story);
        IReadOnlyList<int> counts = RecordSplitBook.PartCounts(story);

        for (int page = 0; page < story.Pages.Count; page++)
        {
            List<RecordPart> onPage = list.Where(p => p.Page == page).ToList();
            Assert.Equal(Enumerable.Range(0, onPage.Count), onPage.Select(p => p.Index)); // no gap, whatever was dropped from a card
            Assert.Equal(onPage.Count, counts[page]);
            Assert.False(RecordSplitBook.PartFits(list, StoryClipKey.OfParts(page, 0, onPage.Count, "en")));
            Assert.All(onPage.Where(p => p.InSentences), p => AssertViewsAgree(story, list, p));
        }

        RecordBook book = RecordSplitBook.Of(story);
        Assert.Equal(list.Where(p => p.InSentences), RecordSplit.PartsIn(book.Sentences)); // the cards hold exactly the list's parts that sit in a sentence
    }

    private static void AssertViewsAgree(Story story, IReadOnlyList<RecordPart> list, RecordPart part)
    {
        StoryClipKey key = StoryClipKey.OfParts(part.Page, part.Index, part.Index, "en");
        StoryRecordings.SentenceCover? cover = RecordSplitBook.CoverOfParts(list, key);

        Assert.Equal(new StoryRecordings.SentenceCover(part.FirstSentence, part.LastSentence), cover);
        Assert.Equal(cover, StoryRecordings.CoverOf(story, key)); // the overlap rule reads the same list
        Assert.True(RecordSplitBook.PartFits(list, key));
    }

    [Fact]
    public void A_part_in_no_sentence_keeps_its_number_and_never_widens_a_cover()
    {
        RecordPart Part(int index, int first, int last) => new(0, index, string.Empty, "x", first, last, index * 10, index * 10 + 10);
        IReadOnlyList<RecordPart> list = [Part(0, 0, 0), Part(1, -1, -1), Part(2, 1, 1)];

        Assert.Equal([3], RecordSplitBook.PartCounts(list, 1));
        Assert.True(RecordSplitBook.PartFits(list, StoryClipKey.OfParts(0, 0, 2, "en")));
        Assert.False(RecordSplitBook.PartFits(list, StoryClipKey.OfParts(0, 0, 3, "en")));
        Assert.Equal(new StoryRecordings.SentenceCover(0, 1), RecordSplitBook.CoverOfParts(list, StoryClipKey.OfParts(0, 0, 2, "en")));
        Assert.Null(RecordSplitBook.CoverOfParts(list, StoryClipKey.OfParts(0, 1, 1, "en"))); // it holds no sentence: it overlaps no sentence clip
        Assert.Null(RecordSplitBook.CoverOfParts(list, StoryClipKey.OfParts(0, 2, 3, "en"))); // the page has no part 3
    }

    [Fact]
    public void A_translated_page_that_fell_back_to_sentence_cards_still_sees_its_part_clips()
    {
        Story story = Freddy();
        (StoryClipKey first, _) = MummyDinner(story);
        int page = first.Page;
        IReadOnlyList<RecordPart> list = RecordSplitBook.PartListOf(story);
        int count = list.Count(p => p.Page == page);
        StoryClipKey all = StoryClipKey.OfParts(page, 0, count - 1, "en");
        Story recorded = StoryRecordings.WithClip(story, all, new PageRecording("en", 9, StoryRecordings.NewFileName(all)));
        RecordBook fallback = RecordSplitBook.WithTranslatedText(RecordSplitBook.Of(story), new TranslatedStory(story.Pages, story.Text, RecordSplitBook.SentencesOf(story)))!;
        Assert.All(fallback.Sentences, s => Assert.Null(s.Parts)); // sentence cards only

        IReadOnlyList<RecordPiece> cards = RecordSplitWalk.InLanguage(RecordSplit.ByCharacter(fallback.Sentences, fallback.Speakers.Select(s => s.Id).ToHashSet()), "en");

        Assert.All(cards.Where(c => c.Key.Page == page), c => Assert.True(RecordSplitWalk.IsRecorded(recorded, c)));
        Assert.Equal(1, RecordSplitWalk.Progress(recorded, fallback, RecordMode.WholePage, "en").Done);
        Assert.Equal(all, Assert.Single(RecordSplitWalk.Recorded(recorded, fallback, RecordMode.WholePage, new HashSet<string>(), "en")).Key);
        var sentenceKey = new StoryClipKey(page, 0, 0, "en");
        Assert.Equal([all], StoryRecordings.Overlapping(recorded, sentenceKey)); // re-recording a card replaces the part clip by the same rule, and the screen says so
        Assert.True(StoryRecordings.ReplacesBeyond(recorded, sentenceKey));
    }

    [Fact]
    public void A_backup_part_clip_for_parts_the_phones_page_lacks_is_never_merged()
    {
        Story phone = Freddy();
        (StoryClipKey part, _) = MummyDinner(phone);
        int count = RecordSplitBook.PartListOf(phone).Count(p => p.Page == part.Page);
        StoryClipKey beyond = StoryClipKey.OfParts(part.Page, count - 1, count, "en");
        Story withBeyond = StoryRecordings.WithClip(phone, beyond, new PageRecording("en", 3, StoryRecordings.NewFileName(beyond)));
        Story withFitting = StoryRecordings.WithClip(phone, part, new PageRecording("en", 3, StoryRecordings.NewFileName(part)));

        Assert.Empty(StoryRecordings.MergeableClips(phone, withBeyond, _ => true));
        Assert.Equal(part, Assert.Single(StoryRecordings.MergeableClips(phone, withFitting, _ => true)));
    }
}
