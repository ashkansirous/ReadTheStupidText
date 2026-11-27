using DinoReader.BookBuilder;
using DinoReader.Core;
using Sirous.Reading.Application.Reading;

namespace DinoReader.Tests;

/// <summary>Slice 395: a character's card holds only that character's words (a run of one speaker's quote parts), a part clip is keyed by parts, and the older sentence clips keep working untouched. A part clip and a
/// sentence clip never replace or drop each other. Playing a part take in place of only its words is Slice 443.</summary>
[Collection(PartTakesCollection.Name)]
public sealed class RecordPartsTests : IDisposable
{
    public RecordPartsTests() => PartTakes.Enabled = true; // Slice 443: the switch is on in the app; the tests that turn it off run alone (PartTakesCollection) and every test starts from on

    public void Dispose() => PartTakes.Enabled = true;

    private static Story Freddy()
    {
        string md = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "content", "dino", "freddy-and-the-slipper", "story.md"));
        string[] pages = md.Replace("\r\n", "\n").Split("\n---\n", StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
        return new Story(StoryIds.New(), "Freddy", "en", DateTimeOffset.UnixEpoch, pages.Select(t => new StoryPage(t)).ToList(), [], CoverStyle.Colour, 0, string.Empty);
    }

    private static IReadOnlySet<string> Ticked(RecordBook book, params string[] names) =>
        book.Speakers.Where(s => names.Contains(s.Name)).Select(s => s.Id).ToHashSet();

    private static string IdOf(RecordBook book, string name) => book.Speakers.Single(s => s.Name == name).Id;

    private static RecordPiece MummyDinner(Story story)
    {
        RecordBook book = RecordSplitBook.Of(story);
        RecordPiece dinner = RecordSplit.ByCharacter(book.Sentences, Ticked(book, "Mummy")).First(p => p.Text.StartsWith("Dinner"));
        return RecordSplitWalk.InLanguage([dinner], "en")[0];
    }

    private static PageRecording Rec(string file) => new("en", 3, file);

    [Fact]
    public void Mummys_cards_hold_only_her_words_and_a_said_Mummy_splits_them()
    {
        RecordBook book = RecordSplitBook.Of(Freddy());

        IReadOnlyList<RecordPiece> pieces = RecordSplit.ByCharacter(book.Sentences, Ticked(book, "Mummy", "Tummy"));

        Assert.Contains(pieces, p => p.Text == "Dinner, my dear! I've made you some stew. Come and sit over here!");
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
    public void The_line_counts_count_parts_and_agree_with_the_cards()
    {
        RecordBook book = RecordSplitBook.Of(Freddy());
        IReadOnlyDictionary<string, int> counts = RecordSplit.LineCounts(book.Sentences);

        foreach (RecordSpeaker speaker in book.Speakers)
        {
            Assert.Equal(counts.GetValueOrDefault(speaker.Id), RecordSplit.ByCharacter(book.Sentences, new HashSet<string> { speaker.Id }).Count);
        }

        Assert.True(counts[IdOf(book, "Mummy")] >= 3);
    }

    [Fact]
    public void A_part_clip_has_its_own_file_name_and_survives_story_json()
    {
        StoryClipKey key = StoryClipKey.OfParts(2, 1, 3, "en");
        string fresh = StoryRecordings.NewFileName(key);
        Story story = StoryRecordings.WithClip(Freddy(), key, new PageRecording("en", 4, fresh));

        Story back = StoryJson.Deserialize(StoryJson.Serialize(story));

        Assert.Equal("rec-en-3-p2-4.m4a", StoryRecordings.FileNameOf(key));
        Assert.True(StoryRecordings.IsRecordingFileName(StoryRecordings.FileNameOf(key)));
        Assert.True(StoryRecordings.IsRecordingFileName(StoryRecordings.NewFileName(key)));
        Assert.Equal(key, Assert.Single(StoryRecordings.Clips(back)).Key);
        Assert.NotEqual(key, new StoryClipKey(2, 1, 3, "en")); // never a sentence clip
        Assert.Equal(fresh, StoryRecordings.FileOf(back, key));
    }

    [Fact]
    public void A_story_file_written_before_part_clips_loads_unchanged()
    {
        var key = new StoryClipKey(1, 0, 1, "en");
        Story story = StoryRecordings.WithClip(Freddy(), key, Rec("rec-en-2-1-2.m4a"));
        string json = StoryJson.Serialize(story);

        Assert.DoesNotContain("PartRecordings", json, StringComparison.OrdinalIgnoreCase); // no part clip: the field is not written, so an older app reads the file as it always did
        Assert.Equal(key, Assert.Single(StoryRecordings.Clips(StoryJson.Deserialize(json))).Key);
    }

    [Fact]
    public void A_part_clip_is_recorded_for_its_parts_and_an_old_sentence_clip_still_counts()
    {
        Story story = Freddy();
        RecordPiece keyed = MummyDinner(story);
        Assert.False(RecordSplitWalk.IsRecorded(story, keyed));

        Story old = StoryRecordings.WithClip(story, new StoryClipKey(keyed.Key.Page, keyed.FirstSentence, keyed.LastSentence, "en"), Rec("rec-en-2-1-2.m4a"));
        Assert.True(RecordSplitWalk.IsRecorded(old, keyed)); // Decision 5: the old line covers every sentence the part sits in

        Story fresh = StoryRecordings.WithClip(old, keyed.Key, Rec("rec-en-2-p2-2-0a0a0a0a.m4a"));
        Assert.True(RecordSplitWalk.IsRecorded(fresh, keyed));
        Assert.Equal(2, StoryRecordings.Clips(fresh).Count()); // the old sentence clip is kept: nothing is deleted
        Assert.True(keyed.Key.IsPart && RecordSplitWalk.ClipOf(fresh, keyed) == keyed.Key); // the part clip is the one that answers
    }

    [Fact]
    public void An_old_sentence_clip_that_holds_only_some_of_the_sentences_does_not_mark_the_part_recorded()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyDinner(story);
        RecordBook book = RecordSplitBook.Of(story);
        RecordPiece spoonful = RecordSplitWalk.InLanguage([RecordSplit.ByCharacter(book.Sentences, Ticked(book, "Mummy")).First(p => p.Text == "It's yummy! It's chicken and peas!")], "en")[0];
        Story oneSentence = StoryRecordings.WithClip(story, new StoryClipKey(dinner.Key.Page, dinner.FirstSentence, dinner.FirstSentence, "en"), Rec("rec-en-2-1-1.m4a"));

        Assert.Equal(dinner.FirstSentence == dinner.LastSentence, RecordSplitWalk.IsRecorded(oneSentence, dinner));
        Assert.False(RecordSplitWalk.IsRecorded(oneSentence, spoonful));
    }

    [Fact]
    public void The_progress_counts_a_recorded_part_once()
    {
        Story story = Freddy();
        RecordBook book = RecordSplitBook.Of(story);
        string mummy = IdOf(book, "Mummy");
        RecordProgress before = RecordSplitWalk.SpeakerProgress(story, book, mummy, "en");
        RecordPiece dinner = MummyDinner(story);

        Story recorded = StoryRecordings.WithClip(story, dinner.Key, Rec("rec-en-2-p2-2-0a0a0a0a.m4a"));
        RecordProgress after = RecordSplitWalk.SpeakerProgress(recorded, book, mummy, "en");

        Assert.Equal(0, before.Done);
        Assert.Equal(1, after.Done);
        Assert.Equal(before.Total, after.Total);
        IReadOnlyList<RecordPiece> cards = RecordSplitWalk.InLanguage(RecordSplit.ByCharacter(book.Sentences, new HashSet<string> { mummy }), "en");
        Assert.Equal(cards.ToList().FindIndex(c => c.Key != dinner.Key), RecordSplitWalk.FirstUnrecorded(recorded, cards)); // "Record the rest" skips the recorded part
    }

    [Fact]
    public void A_part_clip_and_a_sentence_clip_never_replace_or_drop_each_other()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyDinner(story);
        var sentences = new StoryClipKey(dinner.Key.Page, dinner.FirstSentence, dinner.LastSentence + 1, "en");

        Story withPart = StoryRecordings.WithClip(story, dinner.Key, Rec("rec-en-2-p2-2-0a0a0a0a.m4a"));
        Story both = StoryRecordings.WithClip(withPart, sentences, Rec("rec-en-2-1-3-0b0b0b0b.m4a"));
        Story partAgain = StoryRecordings.WithClip(both, dinner.Key, Rec("rec-en-2-p2-2-0c0c0c0c.m4a"));

        Assert.Equal(2, StoryRecordings.Clips(both).Count()); // a sentence clip saved over the same words leaves the part clip
        Assert.Empty(StoryRecordings.ReplacedFiles(withPart, sentences));
        Assert.Empty(StoryRecordings.Overlapping(both, sentences));
        Assert.Equal(["rec-en-2-p2-2-0a0a0a0a.m4a"], StoryRecordings.ReplacedFiles(both, dinner.Key)); // only the part clip it is re-recorded over
        Assert.Equal(2, StoryRecordings.Clips(partAgain).Count());
        Assert.NotNull(StoryRecordings.ClipOf(partAgain, sentences)); // the sentence clip survives a part re-record
    }

    [Fact]
    public void A_part_clip_is_no_sentence_clip_and_covers_no_sentence_for_the_reader()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyDinner(story);
        var sentence = new StoryClipKey(0, 0, 0, "en");
        Story recorded = StoryRecordings.WithClip(StoryRecordings.WithClip(story, dinner.Key, Rec("rec-en-2-p2-2-0a0a0a0a.m4a")), sentence, Rec("rec-en-1-1-1-0b0b0b0b.m4a"));

        Assert.Equal([sentence], StoryRecordings.PlayingClips(recorded, "en")); // the sentence-clip rule never lists a part clip: it plays through PlayingParts (Slice 443)
        Assert.False(StoryRecordings.PlaysClip(recorded, dinner.Key, "en"));
        Assert.Equal([dinner.Key.In("en")], StoryRecordings.PlayingParts(recorded, "en"));
        Assert.Null(StoryRecordings.ClipCovering(recorded, dinner.Key.Page, dinner.FirstSentence, "en"));
        Assert.Equal([dinner.Key.In("en")], RecordSplitWalk.Recorded(StoryRecordings.WithoutClip(recorded, sentence), RecordSplitBook.Of(story), RecordMode.WholePage, new HashSet<string>(), "en").Select(p => p.Key)); // "Listen and change" lists the part take as its own card
    }

    [Fact]
    public void A_part_clip_whose_parts_the_page_lacks_never_marks_a_card_recorded_and_is_kept()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyDinner(story);
        StoryClipKey gone = StoryClipKey.OfParts(dinner.Key.Page, 90, 91, "en");
        Story recorded = StoryRecordings.WithClip(story, gone, Rec("rec-en-2-p91-92-0c0c0c0c.m4a"));
        StoryClipKey wide = StoryClipKey.OfParts(dinner.Key.Page, 0, 90, "en");
        Story wideRecorded = StoryRecordings.WithClip(story, wide, Rec("rec-en-2-p1-91-0d0d0d0d.m4a"));

        Assert.False(RecordSplitWalk.IsRecorded(wideRecorded, dinner)); // recorded for other words: it does not fit the page
        Assert.NotNull(StoryRecordings.ClipOf(recorded, gone)); // nothing was deleted
    }

    [Fact]
    public void Two_part_clips_that_overlap_in_a_story_file_are_both_kept_on_load()
    {
        Story story = Freddy();
        StoryClipKey first = StoryClipKey.OfParts(1, 1, 2, "en");
        StoryClipKey newer = StoryClipKey.OfParts(1, 2, 3, "en");
        Story file = story with { UseRecordings = true, PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [first] = Rec(StoryRecordings.NewFileName(first)), [newer] = Rec(StoryRecordings.NewFileName(newer)) } };

        Story back = StoryJson.Deserialize(StoryJson.Serialize(file));

        Assert.Equal(2, StoryRecordings.Clips(back).Count()); // which is the newer take is not known on load, so sort order never drops a recording
    }

    [Fact]
    public void A_whole_page_clip_beside_part_clips_loads_with_every_clip()
    {
        Story story = Freddy();
        StoryClipKey part = StoryClipKey.OfParts(1, 1, 1, "en");
        Story file = StoryRecordings.WithClip(StoryRecordings.WithClip(story, StoryClipKey.OfPage(1, "en"), Rec("rec-en-2-0a0a0a0a.m4a")), part, Rec(StoryRecordings.NewFileName(part)));

        Story back = StoryJson.Deserialize(StoryJson.Serialize(file));

        Assert.Equal(2, StoryRecordings.Clips(back).Count());
        Assert.Equal([StoryClipKey.OfPage(1, "en")], StoryRecordings.PlayingClips(back, "en"));
    }

    [Fact]
    public void A_part_clip_in_an_older_schema_file_keeps_its_own_name_and_never_another_clips_file()
    {
        StoryClipKey part = StoryClipKey.OfParts(1, 1, 1, "en");
        Story story = StoryRecordings.WithClip(Freddy(), part, Rec(StoryRecordings.NewFileName(part)));
        string json = StoryJson.Serialize(story).Replace("\"RecordingSchema\": 2,", string.Empty, StringComparison.Ordinal);

        Story back = StoryJson.Deserialize(json);

        Assert.Equal(part.Page, Assert.Single(StoryRecordings.Clips(back)).Key.Page);
        Assert.True(StoryRecordings.IsRecordingFileName(StoryRecordings.FileOf(back, Assert.Single(StoryRecordings.Clips(back)).Key)));
    }

    [Fact]
    public void A_starter_update_moves_a_part_clip_only_when_the_page_has_the_same_parts()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyDinner(story);
        Story withPart = StoryRecordings.WithClip(story, dinner.Key, Rec("rec-en-2-p2-2-0a0a0a0a.m4a"));
        Story same = story with { Id = StoryIds.New() };

        StarterUpdate.ClipMove move = Assert.Single(StarterUpdate.PieceMoves(withPart, same));
        Assert.True(move.To.IsPart);
        Assert.Equal(dinner.Key, move.To);

        Story edited = same with { Pages = same.Pages.Select((p, i) => i == dinner.Key.Page ? new StoryPage(p.Text + " Extra words, said Mummy.") : p).ToList() };
        Assert.Empty(StarterUpdate.PieceMoves(withPart, edited)); // the page's text changed: the part numbers may mean other words
    }

    [Fact]
    public void A_part_move_never_blocks_or_drops_the_sentence_clip_of_the_same_page()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyDinner(story);
        var sentence = new StoryClipKey(dinner.Key.Page, 0, 0, "en");
        Story recorded = StoryRecordings.WithClip(StoryRecordings.WithClip(story, dinner.Key, Rec("rec-en-2-p2-2-0a0a0a0a.m4a")), sentence, Rec("rec-en-2-1-1-0b0b0b0b.m4a"));

        IReadOnlyList<StarterUpdate.ClipMove> moves = StarterUpdate.PieceMoves(recorded, story with { Id = StoryIds.New() });

        Assert.Equal(2, moves.Count);
        Assert.Contains(moves, m => m.To == sentence && !m.To.IsPart);
        Assert.Contains(moves, m => m.To == dinner.Key);
    }

    [Fact]
    public void A_whole_page_move_lands_beside_the_kept_copys_part_clips_and_loses_none()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyDinner(story);
        Story keep = StoryRecordings.WithClip(story, dinner.Key, Rec("rec-en-2-p2-2-0a0a0a0a.m4a"));
        Story extra = StoryRecordings.WithClip(story with { Id = StoryIds.New() }, StoryClipKey.OfPage(dinner.Key.Page, "en"), Rec("rec-en-2-0b0b0b0b.m4a"));

        IReadOnlyList<StarterUpdate.RecordingMove> moves = StarterUpdate.RecordingMoves(extra, keep);
        Story merged = StarterUpdate.WithMovedRecordings(keep, extra, moves);

        Assert.Equal(2, StoryRecordings.Clips(merged).Count()); // the part clip is still there
        Assert.NotNull(StoryRecordings.ClipOf(merged, dinner.Key));
    }

    [Fact]
    public void A_backup_part_clip_for_parts_the_phones_page_lacks_is_never_merged()
    {
        Story phone = Freddy();
        RecordPiece dinner = MummyDinner(phone);
        int count = RecordSplitBook.PartListOf(phone).Count(p => p.Page == dinner.Key.Page);
        StoryClipKey beyond = StoryClipKey.OfParts(dinner.Key.Page, count - 1, count, "en");
        Story withBeyond = StoryRecordings.WithClip(phone, beyond, new PageRecording("en", 3, StoryRecordings.NewFileName(beyond)));
        Story withFitting = StoryRecordings.WithClip(phone, dinner.Key, new PageRecording("en", 3, StoryRecordings.NewFileName(dinner.Key)));

        Assert.Empty(StoryRecordings.MergeableClips(phone, withBeyond, _ => true));
        Assert.Equal(dinner.Key, Assert.Single(StoryRecordings.MergeableClips(phone, withFitting, _ => true)));
    }

    [Fact]
    public void A_backup_part_clip_lands_beside_the_phones_sentence_clip_and_drops_nothing()
    {
        Story phone = Freddy();
        RecordPiece dinner = MummyDinner(phone);
        var sentences = new StoryClipKey(dinner.Key.Page, dinner.FirstSentence, dinner.LastSentence, "en");
        Story phoneWith = StoryRecordings.WithClip(phone, sentences, Rec("rec-en-2-1-2-0b0b0b0b.m4a"));
        Story backed = StoryRecordings.WithClip(phone, dinner.Key, Rec(StoryRecordings.NewFileName(dinner.Key)));

        IReadOnlyList<StoryClipKey> keys = StoryRecordings.MergeableClips(phoneWith, backed, _ => true);
        Story merged = StoryRecordings.WithMergedClips(phoneWith, backed, keys);

        Assert.Equal(dinner.Key, Assert.Single(keys));
        Assert.Equal(2, StoryRecordings.Clips(merged).Count());
        Assert.NotNull(StoryRecordings.ClipOf(merged, sentences));
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
            Assert.True(onPage.Count == 0 || RecordSplitBook.PartFits(list, StoryClipKey.OfParts(page, 0, onPage.Count - 1, "en")));
            Assert.False(RecordSplitBook.PartFits(list, StoryClipKey.OfParts(page, 0, onPage.Count, "en")));
        }

        RecordBook book = RecordSplitBook.Of(story);
        Assert.Equal(list.Where(p => p.InSentences), RecordSplit.PartsIn(book.Sentences)); // the cards hold exactly the list's parts that sit in a sentence
    }

    [Fact]
    public void A_part_in_no_sentence_keeps_its_number_so_a_clip_range_still_fits()
    {
        RecordPart Part(int index, int first, int last) => new(0, index, string.Empty, "x", first, last, index * 10, index * 10 + 10);
        IReadOnlyList<RecordPart> list = [Part(0, 0, 0), Part(1, -1, -1), Part(2, 1, 1)];

        Assert.Equal([3], RecordSplitBook.PartCounts(list, 1));
        Assert.True(RecordSplitBook.PartFits(list, StoryClipKey.OfParts(0, 0, 2, "en")));
        Assert.False(RecordSplitBook.PartFits(list, StoryClipKey.OfParts(0, 0, 3, "en")));
    }

    [Fact]
    public void A_translated_page_whose_pieces_line_up_shows_each_parts_own_translated_words()
    {
        Story story = Freddy();
        RecordBook source = RecordSplitBook.Of(story);
        IReadOnlyList<RecordPart> parts = RecordSplit.PartsIn(source.Sentences);
        var translated = new TranslatedStory(story.Pages, story.Text, RecordSplitBook.SentencesOf(story), parts.Select(p => new TranslatedPart(p.SourceStart, p.SourceEnd, "T:" + p.Text)).ToList());

        RecordBook book = RecordSplitBook.WithTranslatedText(source, translated)!;
        IReadOnlyList<RecordPiece> cards = RecordSplit.ByCharacter(book.Sentences, Ticked(book, "Mummy"));

        Assert.NotEmpty(cards);
        Assert.All(cards, c => Assert.StartsWith("T:", c.Text));
        Assert.All(cards, c => Assert.True(c.Key.IsPart)); // the same part keys as the source, so a clip is the same in every language
        Assert.Equal(RecordSplit.ByCharacter(source.Sentences, Ticked(source, "Mummy")).Select(c => c.Key), cards.Select(c => c.Key));
    }

    [Fact]
    public void A_translated_page_whose_pieces_do_not_line_up_falls_back_to_sentence_cards()
    {
        Story story = Freddy();
        RecordBook source = RecordSplitBook.Of(story);

        RecordBook fallback = RecordSplitBook.WithTranslatedText(source, new TranslatedStory(story.Pages, story.Text, RecordSplitBook.SentencesOf(story)))!;

        Assert.All(fallback.Sentences, s => Assert.Null(s.Parts)); // Decision 6: sentence cards in that language
        Assert.All(RecordSplit.ByCharacter(fallback.Sentences, fallback.Speakers.Select(s => s.Id).ToHashSet()), c => Assert.False(c.Key.IsPart));
    }

    [Fact]
    public void A_pack_clip_named_like_a_recording_is_no_take()
    {
        Assert.False(StoryRecordings.IsTakeFileName("rec-en-2-p2-2.m4a"));
        Assert.False(StoryRecordings.IsTakeFileName("take-0.m4a"));
        Assert.True(StoryRecordings.IsRecordingFileName("rec-en-2-p2-2.m4a"));
        Assert.False(StoryRecordings.IsRecordingFileName("rec-en-2-p0-2.m4a"));
    }

    [Fact]
    public void With_the_switch_off_every_card_is_a_sentence_card_and_no_part_key_is_made()
    {
        PartTakes.Enabled = false;
        RecordBook book = RecordSplitBook.Of(Freddy());

        IReadOnlyList<RecordPiece> pieces = RecordSplit.ByCharacter(book.Sentences, book.Speakers.Select(s => s.Id).ToHashSet());

        Assert.All(book.Sentences, s => Assert.Null(s.Parts));
        Assert.NotEmpty(pieces);
        Assert.All(pieces, p => Assert.False(p.Key.IsPart));
    }

    [Fact]
    public void A_part_file_an_older_apps_save_dropped_from_the_story_is_adopted_on_load()
    {
        Story story = Freddy();
        StoryClipKey known = StoryClipKey.OfParts(1, 1, 1, "en");
        Story withKnown = StoryRecordings.WithClip(story, known, Rec("rec-en-2-p2-2-0a0a0a0a.m4a")) with { UseRecordings = false };
        string[] files = ["rec-en-2-p2-2-0a0a0a0a.m4a", "rec-en-3-p1-2-0b0b0b0b.m4a", "rec-en-3-p2-3-0c0c0c0c.m4a", "rec-en-2-p4-4.m4a", "rec-en-2-1-2-0d0d0d0d.m4a", "rec-en-999-p1-1.m4a", "rec-en-1-p1-1-ZZZZZZZZ.m4a"];

        Story adopted = PartClipAdoption.Adopt(withKnown, files);

        Assert.Equal([StoryClipKey.OfParts(1, 1, 1, "en"), StoryClipKey.OfParts(1, 3, 3, "en"), StoryClipKey.OfParts(2, 0, 1, "en")], StoryRecordings.Clips(adopted).Select(c => c.Key).ToList()); // the second part file of page 3 overlaps the first: the first by name stays; no sentence file, no missing page, no bad name
        Assert.Equal("rec-en-3-p1-2-0b0b0b0b.m4a", StoryRecordings.FileOf(adopted, StoryClipKey.OfParts(2, 0, 1, "en")));
        Assert.False(adopted.UseRecordings); // finding a file never turns the switch on

        Story newer = PartClipAdoption.Adopt(StoryRecordings.WithClip(story, StoryClipKey.OfParts(2, 0, 1, "en"), Rec("rec-en-3-p1-2-0e0e0e0e.m4a")), files);
        Assert.Equal("rec-en-3-p1-2-0e0e0e0e.m4a", StoryRecordings.FileOf(newer, StoryClipKey.OfParts(2, 0, 1, "en"))); // a take the story already has for those parts is the newer one
    }

    [Fact]
    public async Task A_library_read_adopts_the_part_files_in_the_books_folder()
    {
        string root = Path.Combine(Path.GetTempPath(), "dino-adopt-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileStoryLibrary(root);
            Story story = Freddy();
            await library.AddAsync(story);
            string file = "rec-en-2-p2-2-0a0a0a0a.m4a";
            await library.StoreRecordingFileAsync(story.Id, file, path => File.WriteAllBytesAsync(path, [1, 2, 3]));

            Story read = (await library.GetAsync(story.Id))!;

            Assert.Equal(StoryClipKey.OfParts(1, 1, 1, "en"), Assert.Single(StoryRecordings.Clips(read)).Key);
            Assert.Equal(file, StoryRecordings.FileOf(read, StoryClipKey.OfParts(1, 1, 1, "en")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task With_the_switch_off_a_library_read_adopts_nothing()
    {
        string root = Path.Combine(Path.GetTempPath(), "dino-adopt-off-" + Guid.NewGuid().ToString("N"));
        try
        {
            PartTakes.Enabled = false;
            var library = new FileStoryLibrary(root);
            Story story = Freddy();
            await library.AddAsync(story);
            await library.StoreRecordingFileAsync(story.Id, "rec-en-2-p2-2-0a0a0a0a.m4a", path => File.WriteAllBytesAsync(path, [1, 2, 3]));

            Story read = (await library.GetAsync(story.Id))!;

            Assert.Empty(StoryRecordings.Clips(read));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void With_the_switch_off_part_clips_are_kept_and_saved_but_not_counted()
    {
        StoryClipKey part = StoryClipKey.OfParts(1, 1, 1, "en");
        Story story = StoryRecordings.WithClip(Freddy(), part, Rec("rec-en-2-p2-2-0a0a0a0a.m4a"));
        PartTakes.Enabled = false;

        Assert.Equal(0, StoryRecordings.Count(story));
        Assert.Equal(0, StoryRecordings.Count(story, "en"));
        Assert.Equal(0, StoryRecordings.PagesRecorded(story, "en"));
        Assert.Single(StoryRecordings.Clips(story));
        Assert.Equal(1, StoryRecordings.CountStored(story));
        Story saved = StoryJson.Deserialize(StoryJson.Serialize(story), out _, "en");
        Assert.Equal(part, Assert.Single(StoryRecordings.Clips(saved)).Key);
    }

    [Fact]
    public void A_lined_page_whose_sentences_have_no_parts_falls_back_to_sentence_cards()
    {
        Story story = Freddy();
        RecordBook source = RecordSplitBook.Of(story);
        RecordBook noParts = source with { Sentences = source.Sentences.Select(s => s with { Parts = null }).ToList() };
        var translated = new TranslatedStory(story.Pages, story.Text, RecordSplitBook.SentencesOf(story));

        RecordBook book = RecordSplitBook.WithTranslatedText(noParts, translated)!;

        Assert.All(book.Sentences, s => Assert.Null(s.Parts));
    }

    [Fact]
    public void Overlapping_part_clips_of_a_backup_both_merge_and_both_move()
    {
        Story phone = Freddy();
        StoryClipKey first = StoryClipKey.OfParts(1, 0, 1, "en");
        StoryClipKey second = StoryClipKey.OfParts(1, 1, 2, "en");
        Story source = phone with { Id = StoryIds.New(), UseRecordings = true, PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [first] = Rec(StoryRecordings.NewFileName(first)), [second] = Rec(StoryRecordings.NewFileName(second)) } };

        IReadOnlyList<StoryClipKey> keys = StoryRecordings.MergeableClips(phone, source, _ => true);
        Story merged = StoryRecordings.WithMergedClips(phone, source, keys);
        Story moved = StarterUpdate.WithMovedClips(phone, source, StarterUpdate.PieceMoves(source, phone).Select(m => m with { File = StoryRecordings.FileOf(source, m.From) }).ToList());

        Assert.Equal(2, StoryRecordings.Clips(merged).Count());
        Assert.Equal(2, StoryRecordings.Clips(moved).Count());
        Assert.All(StoryRecordings.Clips(merged), c => Assert.True(StoryRecordings.Files(merged).Contains(StoryRecordings.FileOf(c.Value, c.Key))));
    }

    [Fact]
    public void A_part_key_in_the_onboarding_try_record_stays_a_part_key_across_a_restart()
    {
        StoryClipKey part = StoryClipKey.OfParts(1, 1, 1, "en");
        var record = new OnboardingTryRecord("book", [new TryClip(part, "rec-en-2-p2-2-0a0a0a0a.m4a")]);

        OnboardingTryRecord back = OnboardingTryRecord.FromJson(record.ToJson())!;

        Assert.Equal(part, Assert.Single(back.Clips).Key);
        Assert.True(Assert.Single(back.Clips).Key.IsPart);
    }

    [Fact]
    public void An_old_sentence_take_of_another_speaker_does_not_mark_a_characters_card_recorded()
    {
        Story story = Freddy();
        RecordBook book = RecordSplitBook.Of(story);
        RecordPiece freddy = RecordSplitWalk.InLanguage([RecordSplit.ByCharacter(book.Sentences, Ticked(book, "Freddy")).First(p => p.Text.Contains("No room in my tummy!"))], "en")[0];
        Story oldTake = StoryRecordings.WithClip(story, new StoryClipKey(freddy.Key.Page, freddy.FirstSentence, freddy.LastSentence, "en"), Rec("rec-en-2-1-2-0b0b0b0b.m4a")); // the sentence opens with Mummy's quote

        Assert.False(RecordSplitWalk.IsRecorded(oldTake, freddy));
    }

    [Fact]
    public void A_whole_page_clip_stays_first_beside_a_piece_of_the_same_start()
    {
        Story story = Freddy();
        StoryClipKey whole = StoryClipKey.OfPage(1, "en");
        StoryClipKey piece = new(1, 0, 1, "en");
        Story file = story with { UseRecordings = true, Recordings = new Dictionary<int, PageRecording> { [1] = Rec("rec-en-2-0a0a0a0a.m4a") }, PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [piece] = Rec("rec-en-2-1-2-0b0b0b0b.m4a") } };

        Assert.Equal([whole, piece], StoryRecordings.Clips(file).Select(c => c.Key).ToList());
    }
}
