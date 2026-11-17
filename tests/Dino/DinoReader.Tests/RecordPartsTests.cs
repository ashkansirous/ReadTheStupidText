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
    public void A_part_clip_whose_first_part_is_gone_is_kept_and_not_listed_without_throwing()
    {
        Story story = Freddy();
        (StoryClipKey first, _) = MummyDinner(story);
        StoryClipKey gone = StoryClipKey.OfParts(first.Page, 90, 91, "en"); // the story no longer has parts 90-91 on that page
        Story recorded = StoryRecordings.WithClip(story, gone, new PageRecording("en", 3, "rec-en-2-p91-92-0c0c0c0c.m4a"), new StoryRecordings.SentenceCover(0, 1));
        RecordBook fallback = RecordSplitBook.WithTranslatedText(RecordSplitBook.Of(story), new TranslatedStory(story.Pages, story.Text, RecordSplitBook.SentencesOf(story)))!;

        IReadOnlyList<RecordPiece> walked = RecordSplitWalk.Recorded(recorded, fallback, RecordMode.WholePage, new HashSet<string>(), "en");

        Assert.Empty(walked);
        Assert.NotNull(StoryRecordings.ClipOf(recorded, gone)); // nothing was deleted
    }

    [Fact]
    public void Which_of_two_overlapping_clips_plays_does_not_depend_on_the_order_they_were_stored()
    {
        Story story = Freddy();
        var wide = new StoryClipKey(0, 0, 1, "en");
        var narrow = new StoryClipKey(0, 0, 0, "en");
        PageRecording Rec(string file) => new("en", 2, file);
        Story a = story with { UseRecordings = true, PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [wide] = Rec("w.m4a"), [narrow] = Rec("n.m4a") } };
        Story b = story with { UseRecordings = true, PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [narrow] = Rec("n.m4a"), [wide] = Rec("w.m4a") } };

        Assert.Equal(StoryRecordings.PlayingClips(a, "en"), StoryRecordings.PlayingClips(b, "en"));
        Assert.Equal([narrow], StoryRecordings.PlayingClips(a, "en")); // the shorter run of the same start wins
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
    public void A_part_clip_with_no_stored_cover_overlaps_no_sentence_clip_and_a_save_never_drops_a_sentence_clip_for_it()
    {
        Story phone = Freddy();
        (StoryClipKey part, _) = MummyDinner(phone);
        int count = RecordSplitBook.PartListOf(phone).Count(p => p.Page == part.Page);
        StoryClipKey beyond = StoryClipKey.OfParts(part.Page, count - 1, count, "en"); // parts the page lacks: its cover can never be placed
        var sentences = new StoryClipKey(part.Page, 0, 0, "en");
        Story both = phone with { PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [beyond] = new("en", 3, "a.m4a"), [sentences] = new("en", 3, "b.m4a") } };

        Assert.Empty(StoryRecordings.Overlapping(both, sentences)); // review fault 1: an unknown cover is no overlap
        Assert.DoesNotContain("b.m4a", StoryRecordings.ReplacedFiles(both, StoryClipKey.OfParts(part.Page, 0, 0, "en"), null)); // a new part clip with no cover to compare replaces no sentence clip
        Story saved = StoryRecordings.WithClip(both, new StoryClipKey(part.Page, 1, 1, "en"), new PageRecording("en", 2, "c.m4a"));
        Assert.Equal(3, StoryRecordings.Clips(saved).Count()); // the part clip and the older sentence clip both stay
        Assert.Equal(3, StoryRecordings.Clips(StoryJson.Deserialize(StoryJson.Serialize(saved))).Count());
    }

    [Fact]
    public void A_part_clip_in_no_sentence_never_drops_a_sentence_clip_beside_it()
    {
        Story story = Freddy();
        (StoryClipKey part, StoryRecordings.SentenceCover cover) = MummyDinner(story);
        var sentences = new StoryClipKey(part.Page, cover.First, cover.Last, "en");
        Story withSentences = StoryRecordings.WithClip(story, sentences, new PageRecording("en", 3, "rec-en-2-1-2-0b0b0b0b.m4a"));

        Story saved = StoryRecordings.WithClip(withSentences, part, new PageRecording("en", 3, "rec-en-2-p2-2-0a0a0a0a.m4a"), null); // the part sits in no sentence: nothing to compare

        Assert.Equal(2, StoryRecordings.Clips(saved).Count());
        Assert.Empty(StoryRecordings.ReplacedFiles(withSentences, part, null));
    }

    [Fact]
    public void The_cover_is_stored_with_the_clip_and_is_the_one_the_overlap_rule_uses_whatever_the_text_says_now()
    {
        Story story = Freddy();
        (StoryClipKey part, _) = MummyDinner(story);
        var stored = new StoryRecordings.SentenceCover(5, 6); // not where the text puts the part today: the saved cover wins, nothing is re-derived
        Story withPart = StoryRecordings.WithClip(story, part, new PageRecording("en", 3, "rec-en-2-p2-2-0a0a0a0a.m4a"), stored);

        Story back = StoryJson.Deserialize(StoryJson.Serialize(withPart));

        Assert.Equal(stored, StoryRecordings.ClipOf(back, part)!.Cover);
        Assert.Equal(stored, StoryRecordings.StoredCoverOf(back, part));
        Assert.Empty(StoryRecordings.Overlapping(back, new StoryClipKey(part.Page, 0, 1, "en")));
        Assert.Equal(part, Assert.Single(StoryRecordings.Overlapping(back, new StoryClipKey(part.Page, 6, 7, "en"))));
    }

    [Fact]
    public void A_story_file_with_a_whole_page_clip_beside_newer_part_clips_loads_with_every_clip()
    {
        Story story = Freddy();
        (StoryClipKey part, StoryRecordings.SentenceCover cover) = MummyDinner(story);
        Story withParts = StoryRecordings.WithClip(story, part, new PageRecording("en", 3, "rec-en-2-p2-2-0a0a0a0a.m4a"), cover);
        Story both = StoryRecordings.With(withParts, part.Page, new PageRecording("en", 9, null)); // a restored whole-page clip: it used to win by sort order and drop the part takes

        Story loaded = StoryJson.Deserialize(StoryJson.Serialize(both));

        Assert.Equal(2, StoryRecordings.Clips(loaded).Count()); // review fault 2: load never drops a clip for overlapping another
        Assert.Contains(StoryRecordings.Clips(loaded), c => c.Key.IsPart && c.Value.File == "rec-en-2-p2-2-0a0a0a0a.m4a");
    }

    [Fact]
    public void A_story_file_with_a_sentence_clip_and_a_part_clip_over_the_same_words_keeps_both_and_plays_the_sentence_clip_only()
    {
        Story story = Freddy();
        (StoryClipKey part, StoryRecordings.SentenceCover cover) = MummyDinner(story);
        var sentences = new StoryClipKey(part.Page, cover.First, cover.Last, "en");
        Story both = story with
        {
            UseRecordings = true,
            PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [part] = new PageRecording("en", 3, null).WithCover(cover), [sentences] = new("en", 3, null) },
        };

        Story loaded = StoryJson.Deserialize(StoryJson.Serialize(both));

        Assert.Equal(2, StoryRecordings.Clips(loaded).Count());
        Assert.Empty(StoryRecordings.PlayingParts(loaded, "en")); // one would speak the words twice
        Assert.Equal(sentences, Assert.Single(StoryRecordings.PlayingSentenceClips(loaded, "en")));
    }

    [Fact]
    public void A_part_clip_saved_before_covers_were_stored_loads_plays_and_is_kept()
    {
        Story story = Freddy();
        (StoryClipKey part, _) = MummyDinner(story);
        Story legacy = story with { UseRecordings = true, PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [part] = new("en", 3, null) } };

        Story loaded = StoryJson.Deserialize(StoryJson.Serialize(legacy));

        Assert.Null(StoryRecordings.ClipOf(loaded, part)!.Cover);
        Assert.Equal(part, Assert.Single(StoryRecordings.PlayingParts(loaded, "en")));
    }

    [Fact]
    public void A_part_clip_with_no_stored_cover_does_not_play_on_a_sentence_clip_that_holds_its_words()
    {
        Story story = Freddy();
        (StoryClipKey part, StoryRecordings.SentenceCover cover) = MummyDinner(story);
        var sentences = new StoryClipKey(part.Page, cover.First, cover.Last, "en");
        Story both = story with { UseRecordings = true, PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [part] = new("en", 3, null), [sentences] = new("en", 3, null) } };

        Story loaded = StoryJson.Deserialize(StoryJson.Serialize(both));

        Assert.Equal(2, StoryRecordings.Clips(loaded).Count()); // the part clip is kept, with its file
        Assert.Null(StoryRecordings.ClipOf(loaded, part)!.Cover);
        Assert.Empty(StoryRecordings.PlayingParts(loaded, "en")); // review fault 3: it would speak the sentence clip's words twice
        Assert.Equal(sentences, Assert.Single(StoryRecordings.PlayingSentenceClips(loaded, "en")));
    }

    [Fact]
    public void A_part_clip_in_an_older_schema_file_keeps_the_file_name_it_stores_when_the_language_is_filed()
    {
        Story story = Freddy();
        (StoryClipKey part, StoryRecordings.SentenceCover cover) = MummyDinner(story);
        Story saved = StoryRecordings.WithClip(story, part, new PageRecording("en", 3, "rec-en-2-p2-2-0a0a0a0a.m4a"), cover);
        string oldFile = System.Text.RegularExpressions.Regex.Replace(StoryJson.Serialize(saved), "\"RecordingSchema\":\\s*2,?", string.Empty);

        Story migrated = StoryJson.Deserialize(oldFile, out bool wasOld, mainLanguage: "fa");

        Assert.True(wasOld);
        Assert.Equal("rec-en-2-p2-2-0a0a0a0a.m4a", StoryRecordings.FileOf(migrated, part.In("fa"))); // review fault 2: the clip still points at its real file, never a name nobody wrote
    }

    [Fact]
    public void A_take_that_will_not_play_carries_the_segments_it_stood_for_so_they_are_still_read()
    {
        VoicedSegment[] segments = [Seg(0, 10), Seg(10, 20), Seg(20, 30), Seg(30, 40)];

        IReadOnlyList<VoicedSegment> placed = RecordPartTakes.Apply(segments, [new PartTake(10, 30, "take.m4a")]);

        Assert.Equal([segments[1], segments[2]], PremadeClipMarker.CoveredOf(placed[1])); // review fault 1: a failed take is replaced by these
        Assert.Empty(PremadeClipMarker.CoveredOf(placed[0]));
        Assert.Equal(2, PlaybackSteps.Plan(PremadeClipMarker.CoveredOf(placed[1])).Count);
    }

    [Fact]
    public void A_starter_update_never_moves_a_whole_page_clip_onto_a_page_holding_part_clips()
    {
        Story keep = Freddy();
        (StoryClipKey part, StoryRecordings.SentenceCover cover) = MummyDinner(keep);
        Story kept = StoryRecordings.WithClip(keep, part, new PageRecording("en", 3, "rec-en-2-p2-2-0a0a0a0a.m4a"), cover);
        Story extra = StoryRecordings.With(keep with { Id = StoryIds.New() }, part.Page, new PageRecording("en", 9, null));

        Assert.True(StoryRecordings.Occupies(kept, StoryClipKey.OfPage(part.Page, "en"))); // the filter in StarterStories uses this
        Story moved = StarterUpdate.WithMovedRecordings(kept, extra, [new StarterUpdate.RecordingMove(part.Page, part.Page)]);

        Assert.Null(StoryRecordings.Of(moved, part.Page)); // review fault 3: the part clip stays alone
        Assert.Equal(part, Assert.Single(StoryRecordings.Clips(moved)).Key);
    }

    [Fact]
    public void A_starter_update_moves_a_part_clip_with_its_stored_cover_and_never_over_an_overlapping_sentence_clip()
    {
        Story story = Freddy();
        (StoryClipKey part, StoryRecordings.SentenceCover cover) = MummyDinner(story);
        Story extra = StoryRecordings.WithClip(story with { Id = StoryIds.New() }, part, new PageRecording("en", 3, "rec-en-2-p2-2-0a0a0a0a.m4a"), cover);
        var sentences = new StoryClipKey(part.Page, cover.First, cover.Last, "en");
        Story target = StoryRecordings.WithClip(story, sentences, new PageRecording("en", 3, "rec-en-2-1-2-0b0b0b0b.m4a"));
        IReadOnlyList<StarterUpdate.ClipMove> moves = StarterUpdate.PieceMoves(extra, story);

        Story onto = StarterUpdate.WithMovedClips(target, extra, moves);
        Story free = StarterUpdate.WithMovedClips(story, extra, moves);

        Assert.Equal(sentences, Assert.Single(StoryRecordings.Clips(onto)).Key);
        Assert.Equal(cover, StoryRecordings.StoredCoverOf(free, part));
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
        List<RecordPart> inSentences = list.Where(p => p.Page == page && p.InSentences).ToList();
        var saved = new StoryRecordings.SentenceCover(inSentences.Min(p => p.FirstSentence), inSentences.Max(p => p.LastSentence)); // what the Record screen stores when it saves the clip
        Story recorded = StoryRecordings.WithClip(story, all, new PageRecording("en", 9, StoryRecordings.NewFileName(all)), saved);
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

    private static Story WithPieces(Story story, params (StoryClipKey Key, PageRecording Recording)[] clips) =>
        story with { UseRecordings = true, PieceRecordings = clips.ToDictionary(c => c.Key, c => c.Recording) };

    [Fact]
    public void Review1_a_move_is_judged_by_the_clips_cover_and_by_the_moves_before_it_so_no_file_is_copied_for_a_clip_that_is_then_skipped()
    {
        Story story = Freddy();
        (StoryClipKey part, StoryRecordings.SentenceCover cover) = MummyDinner(story);
        var sentences = new StoryClipKey(part.Page, cover.First, cover.Last, "en");
        Story keep = story with { Id = StoryIds.New() };
        Story bothInExtra = WithPieces(story with { Id = StoryIds.New() }, (part, new PageRecording("en", 3, null).WithCover(cover)), (sentences, new PageRecording("en", 3, null)));
        Story sentenceInKeep = StoryRecordings.WithClip(keep, sentences, new PageRecording("en", 3, "rec-en-2-1-2-0b0b0b0b.m4a"));
        Story partInExtra = WithPieces(story with { Id = StoryIds.New() }, (part, new PageRecording("en", 3, null).WithCover(cover)));

        Assert.Empty(StarterUpdate.FreePieceMoves(partInExtra, sentenceInKeep, sentenceInKeep)); // the part clip overlaps a sentence clip the kept copy holds: no file copied
        Assert.Single(StarterUpdate.FreePieceMoves(bothInExtra, keep, keep)); // the source holds both over the same sentences: one file copied, not two
        Assert.Single(StarterUpdate.FreePieceMoves(partInExtra, keep, keep));
    }

    [Fact]
    public void Review2_a_part_in_no_sentence_never_takes_the_cover_from_the_run_it_belongs_to()
    {
        var none = new RecordPart(0, 0, RecordSplit.Storyteller, "x", -1, -1, 0, 1);
        var inTwoToThree = new RecordPart(0, 1, RecordSplit.Storyteller, "y", 2, 3, 2, 3);
        StoryClipKey key = StoryClipKey.OfParts(0, 0, 1, "en");

        Assert.Equal(new StoryRecordings.SentenceCover(2, 3), RecordSplit.PartPiece(key, RecordSplit.Storyteller, "x y", [none, inTwoToThree]).Cover);
        Assert.Null(RecordSplit.PartPiece(key, RecordSplit.Storyteller, "x", [none]).Cover);
    }

    [Fact]
    public void Review3_clips_of_one_kind_that_overlap_are_both_kept_on_load_and_only_the_first_plays()
    {
        Story story = Freddy();
        int page = MummyDinner(story).Part.Page;
        var first = new StoryClipKey(page, 0, 1, "en");
        var second = new StoryClipKey(page, 1, 2, "en");
        var partA = StoryClipKey.OfParts(page, 0, 1, "en");
        var partB = StoryClipKey.OfParts(page, 1, 2, "en");
        var cover = new StoryRecordings.SentenceCover(0, 1);
        Story both = WithPieces(story, (first, new("en", 3, "a.m4a")), (second, new("en", 3, "b.m4a")), (partA, new PageRecording("en", 3, "c.m4a").WithCover(cover)), (partB, new PageRecording("en", 3, "d.m4a").WithCover(new StoryRecordings.SentenceCover(1, 2))));

        Story loaded = StoryJson.Deserialize(StoryJson.Serialize(both));

        Assert.Equal(4, StoryRecordings.Clips(loaded).Count()); // no clip, and so no audio file, is dropped for an overlap
        Assert.Equal(first, Assert.Single(StoryRecordings.PlayingSentenceClips(loaded, "en")));
        Assert.Empty(StoryRecordings.PlayingParts(loaded, "en")); // the sentence clip that plays holds the part clips' sentences
        Story partsOnly = WithPieces(story, (partA, new PageRecording("en", 3, "c.m4a")), (partB, new PageRecording("en", 3, "d.m4a")));
        Assert.Equal(partA, Assert.Single(StoryRecordings.PlayingParts(StoryJson.Deserialize(StoryJson.Serialize(partsOnly)), "en")));
    }

    [Fact]
    public void Review4_backup_clips_that_overlap_each_other_are_all_merged_and_none_replaces_another()
    {
        Story phone = Freddy();
        (StoryClipKey part, StoryRecordings.SentenceCover cover) = MummyDinner(phone);
        var sentences = new StoryClipKey(part.Page, cover.First, cover.Last, "en");
        Story backed = WithPieces(phone, (part, new PageRecording("en", 3, "rec-en-2-p2-2-0a0a0a0a.m4a").WithCover(cover)), (sentences, new PageRecording("en", 3, "rec-en-2-1-2-0b0b0b0b.m4a")));

        IReadOnlyList<StoryClipKey> keys = StoryRecordings.MergeableClips(phone, backed, _ => true);
        Story merged = StoryRecordings.WithMergedClips(phone, backed, keys);

        Assert.Equal(2, keys.Count);
        Assert.Equal(2, StoryRecordings.Clips(merged).Count()); // the file StageAndPlace placed for each is still named
        Assert.Equal(sentences, Assert.Single(StoryRecordings.PlayingSentenceClips(merged, "en")));
        Assert.Empty(StoryRecordings.PlayingParts(merged, "en"));
    }

    [Fact]
    public void Review5_a_moved_part_clip_whose_page_splits_into_other_sentences_keeps_no_stale_cover_but_is_kept()
    {
        Story story = Freddy();
        (StoryClipKey part, StoryRecordings.SentenceCover cover) = MummyDinner(story);
        Story extra = WithPieces(story with { Id = StoryIds.New() }, (part, new PageRecording("en", 3, null).WithCover(cover)));
        Story keep = story with { Id = StoryIds.New() };

        Assert.False(Assert.Single(StarterUpdate.PieceMoves(extra, keep)).DropCover); // the same split: the cover moves with the clip
        Story dropped = StarterUpdate.WithMovedClips(keep, extra, [new StarterUpdate.ClipMove(part, part, null, DropCover: true)]);
        Story refreshed = StarterUpdate.Refresh(extra, keep, [], [new StarterUpdate.ClipMove(part, part, null, DropCover: true)]);

        Assert.Null(StoryRecordings.ClipOf(dropped, part)!.Cover);
        Assert.Null(StoryRecordings.ClipOf(refreshed, part)!.Cover);
        Assert.Equal(part, Assert.Single(StoryRecordings.Clips(dropped)).Key);
    }

    [Fact]
    public void Review7_whole_page_moves_are_judged_against_the_story_as_built_so_one_page_takes_one_clip()
    {
        Story keep = Freddy();
        Story extra = StoryRecordings.With(StoryRecordings.With(keep with { Id = StoryIds.New() }, 0, new PageRecording("en", 4, null)), 1, new PageRecording("en", 5, null));

        Story moved = StarterUpdate.WithMovedRecordings(keep, extra, [new StarterUpdate.RecordingMove(0, 0), new StarterUpdate.RecordingMove(1, 0)]);

        Assert.Equal(4, Assert.Single(moved.Recordings!).Value.Seconds); // the first move wins; the second is not added over it
    }
}
