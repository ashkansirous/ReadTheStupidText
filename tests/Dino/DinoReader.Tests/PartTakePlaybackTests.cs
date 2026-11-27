using DinoReader.BookBuilder;
using DinoReader.Core;
using Sirous.Reading.Application.Reading;

namespace DinoReader.Tests;

/// <summary>Slice 443: a parent's part take plays in place of only the segments it covers (the narration around it plays the book's own audio), a sentence clip wins where it and a part take cover the same words, the
/// stored cover is the only thing the overlap rule reads, and a take is never dropped for an unknown cover, an overlap on load or a page that splits differently.</summary>
[Collection(PartTakesCollection.Name)]
public sealed class PartTakePlaybackTests : IDisposable
{
    public PartTakePlaybackTests() => PartTakes.Enabled = true;

    public void Dispose() => PartTakes.Enabled = true;

    private static Story Freddy()
    {
        string md = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "content", "dino", "freddy-and-the-slipper", "story.md"));
        string[] pages = md.Replace("\r\n", "\n").Split("\n---\n", StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
        return new Story(StoryIds.New(), "Freddy", "en", DateTimeOffset.UnixEpoch, pages.Select(t => new StoryPage(t)).ToList(), [], CoverStyle.Colour, 0, string.Empty);
    }

    private static IReadOnlySet<string> Mummy(RecordBook book) => book.Speakers.Where(s => s.Name == "Mummy").Select(s => s.Id).ToHashSet();

    private static IReadOnlyList<RecordPiece> MummyCards(Story story, int page) =>
        RecordSplitWalk.InLanguage(RecordSplit.ByCharacter(RecordSplitBook.Of(story).Sentences, Mummy(RecordSplitBook.Of(story))).Where(p => p.Key.Page == page).ToList(), "en");

    private static PageRecording Take(RecordPiece card, string file) => new("en", 4, file, card.Cover);

    // The book's own segments of a page as a read of it would hand them to the plan: each with a premade clip, as a store book's are.
    private static List<VoicedSegment> OwnSegments(IReadOnlyList<RecordPart> parts, int page) =>
        parts.Where(p => p.Page == page).Select(p => PremadeClipMarker.Mark(new VoicedSegment(p.Text, "v", p.SourceStart, p.SourceEnd, p.SourceStart), $"clip-{p.Index}.mp3")).ToList();

    private static int Placed(PartTakePlan plan, RecordPart part) =>
        plan.Takes.Single(t => t.Value.SourceStart == part.SourceStart).Key;

    [Fact]
    public void A_take_stands_in_for_its_own_segments_and_the_narration_around_it_plays_the_books_audio()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        Story recorded = StoryRecordings.WithClip(story, dinner.Key, Take(dinner, "rec-en-2-p2-2-0a0a0a0a.m4a"));
        IReadOnlyList<RecordPart> parts = RecordSplitBook.PartListOf(recorded);

        PartTakePlan plan = PartTakes.Plan(OwnSegments(parts, 1), PartTakes.SpansOf(recorded, "en", parts));

        RecordPart narration = parts.First(p => p.Page == 1 && p.Index == 0);
        RecordPart dinnerPart = parts.First(p => p.Page == 1 && p.Index == 1);
        int at = Placed(plan, dinnerPart);
        Assert.Single(plan.Takes);
        Assert.Equal(1, at); // "One evening his Mummy called," (part 0) is the step before the take
        Assert.Equal("clip-0.mp3", PremadeClipMarker.PathOf(plan.Steps[0].Premade!.Value)); // the narrator's own track plays before
        Assert.Equal(narration.SourceStart, plan.Steps[0].Premade!.Value.SourceStart);
        Assert.Null(plan.Steps[at].Premade); // the take step has no premade clip of its own
        Assert.Equal([dinnerPart.SourceStart], plan.Steps[at].Run.Select(s => s.SourceStart));
        Assert.All(plan.Steps.Where((_, i) => i != at), s => Assert.NotNull(s.Premade)); // every other segment keeps the book's own clip
    }

    [Fact]
    public void Two_takes_with_said_Mummy_between_them_play_the_narrator_between_the_two()
    {
        Story story = Freddy();
        IReadOnlyList<RecordPiece> cards = MummyCards(story, 2);
        RecordPiece first = cards.First(p => p.Text == "Just one little spoonful,");
        RecordPiece second = cards.First(p => p.Text == "It's yummy! It's chicken and peas!");
        Story recorded = StoryRecordings.WithClip(StoryRecordings.WithClip(story, first.Key, Take(first, "rec-en-3-p1-1-0a0a0a0a.m4a")), second.Key, Take(second, "rec-en-3-p3-3-0b0b0b0b.m4a"));
        IReadOnlyList<RecordPart> parts = RecordSplitBook.PartListOf(recorded);

        PartTakePlan plan = PartTakes.Plan(OwnSegments(parts, 2), PartTakes.SpansOf(recorded, "en", parts));

        int[] at = plan.Takes.Keys.Order().ToArray();
        Assert.Equal(2, at.Length);
        Assert.Equal(at[0] + 2, at[1]); // exactly one step ("said Mummy.") between the two takes
        Assert.NotNull(plan.Steps[at[0] + 1].Premade);
        Assert.Equal("said Mummy.", plan.Steps[at[0] + 1].Premade!.Value.Text);
    }

    [Fact]
    public void A_take_of_only_an_animal_sound_part_still_plays()
    {
        var take = new PartTakeSpan(StoryClipKey.OfParts(0, 1, 1, "en"), 10, 15, 2);
        VoicedSegment before = new("He went", "v", 0, 9, 0);
        VoicedSegment moo = new("Moo!", "v", 10, 15, 10, new AnimalClipMarker(AnimalKind.Cow));

        PartTakePlan plan = PartTakes.Plan([before, moo], [take]);

        Assert.Equal(1, Assert.Single(plan.Takes).Key); // the step after "He went": the parent's take replaces the sound word's clip
        Assert.Null(plan.Steps[1].Animal);
        Assert.Equal([10], plan.Steps[1].Run.Select(s => s.SourceStart));
    }

    [Fact]
    public void A_read_that_starts_inside_a_take_plays_the_whole_take_and_a_take_after_the_read_plays_nothing()
    {
        var spanning = new PartTakeSpan(StoryClipKey.OfParts(0, 0, 1, "en"), 0, 20, 3);
        var later = new PartTakeSpan(StoryClipKey.OfParts(0, 4, 4, "en"), 40, 50, 1);
        VoicedSegment tapped = new("two", "v", 10, 20, 10); // the read started at the second segment of the take

        PartTakePlan plan = PartTakes.Plan([tapped, new VoicedSegment("after", "v", 25, 30, 25)], [spanning, later]);

        Assert.Equal([0], plan.Takes.Keys.ToArray()); // the take plays from its start, once; the take that begins after the read ends is not planned
        Assert.Equal(2, plan.Steps.Count);
    }

    [Fact]
    public void A_take_that_cannot_play_becomes_the_books_own_steps_and_the_takes_after_it_move_on()
    {
        var one = new PartTakeSpan(StoryClipKey.OfParts(0, 0, 1, "en"), 0, 20, 1);
        var two = new PartTakeSpan(StoryClipKey.OfParts(0, 3, 3, "en"), 40, 50, 1);
        List<VoicedSegment> segments =
        [
            new("one a", "v", 0, 9, 0), new("one b", "v", 10, 19, 10), new("between", "v", 25, 35, 25), new("two", "v", 40, 49, 40),
        ];
        PartTakePlan plan = PartTakes.Plan(segments, [one, two]);
        Assert.Equal([0, 2], plan.Takes.Keys.Order().ToArray());

        int added = PartTakes.ExpandTake(plan.Steps, plan.Takes, 0);

        Assert.Equal(2, added); // the two covered pieces become a silent step each
        Assert.Equal(4, plan.Steps.Count);
        Assert.Equal([3], plan.Takes.Keys.ToArray());
        Assert.Equal(two, plan.Takes[3]);
    }

    [Fact]
    public void A_sentence_clip_wins_where_it_and_a_part_take_cover_the_same_words()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        var sentences = new StoryClipKey(1, 0, 2, "en");
        Story both = StoryRecordings.WithClip(StoryRecordings.WithClip(story, dinner.Key, Take(dinner, "rec-en-2-p2-2-0a0a0a0a.m4a")), sentences, new PageRecording("en", 5, "rec-en-2-1-3-0b0b0b0b.m4a"));

        Assert.Equal([sentences], StoryRecordings.PlayingClips(both, "en"));
        Assert.Empty(StoryRecordings.PlayingParts(both, "en")); // the stored cover (sentences 0-2) meets the sentence clip: only the sentence clip plays
        Assert.Equal(2, StoryRecordings.Clips(both).Count()); // nothing is deleted
    }

    [Fact]
    public void A_part_take_beside_a_sentence_clip_of_other_sentences_still_plays()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        Story both = StoryRecordings.WithClip(StoryRecordings.WithClip(story, dinner.Key, Take(dinner, "rec-en-2-p2-2-0a0a0a0a.m4a")), new StoryClipKey(0, 0, 0, "en"), new PageRecording("en", 5, "rec-en-1-1-1-0b0b0b0b.m4a"));

        Assert.Equal([dinner.Key], StoryRecordings.PlayingParts(both, "en"));
    }

    [Fact]
    public void A_take_with_no_stored_cover_is_kept_by_the_story_and_hidden_in_the_read_only_where_a_sentence_clip_covers_its_words()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        var noCover = new PageRecording("en", 4, "rec-en-2-p2-2-0a0a0a0a.m4a");
        Story both = StoryRecordings.WithClip(StoryRecordings.WithClip(story, dinner.Key, noCover), new StoryClipKey(1, 0, 2, "en"), new PageRecording("en", 5, "rec-en-2-1-3-0b0b0b0b.m4a"));
        IReadOnlyList<RecordPart> parts = RecordSplitBook.PartListOf(both);

        Assert.Equal([dinner.Key], StoryRecordings.PlayingParts(both, "en")); // the story's own rule reads stored covers only: the take is kept
        Assert.Equal(2, StoryRecordings.Clips(StoryJson.Deserialize(StoryJson.Serialize(both))).Count()); // nothing is deleted
        Assert.Empty(PartTakes.SpansOf(both, "en", parts)); // the read plays the sentence clip, so the same words are not heard twice (finding 5)
    }

    [Fact]
    public void A_take_with_no_stored_cover_still_plays_beside_a_sentence_clip_of_another_page()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        var noCover = new PageRecording("en", 4, "rec-en-2-p2-2-0a0a0a0a.m4a");
        Story both = StoryRecordings.WithClip(StoryRecordings.WithClip(story, dinner.Key, noCover), new StoryClipKey(0, 0, 0, "en"), new PageRecording("en", 5, "rec-en-1-1-1-0b0b0b0b.m4a"));

        Assert.Equal([dinner.Key], PartTakes.SpansOf(both, "en", RecordSplitBook.PartListOf(both)).Select(s => s.Key));
    }

    [Fact]
    public void Saving_a_take_over_words_a_sentence_clip_covers_replaces_that_clip_so_the_new_take_plays()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        var sentences = new StoryClipKey(1, 0, 2, "en");
        Story older = StoryRecordings.WithClip(story, sentences, new PageRecording("en", 5, "rec-en-2-1-3-0b0b0b0b.m4a"));
        StoryClipKey allParts = StoryClipKey.OfParts(1, 0, 1, "en"); // the narration before the quote and the quote: every part of sentences 0-2
        PageRecording take = new("en", 4, "rec-en-2-p1-2-0a0a0a0a.m4a", dinner.Cover);

        Story saved = StoryRecordings.WithSavedTake(older, allParts, take);

        Assert.Equal(["rec-en-2-1-3-0b0b0b0b.m4a"], StoryRecordings.ReplacedFiles(older, allParts, take.Cover)); // the old file is deleted with its clip
        Assert.Equal([sentences], StoryRecordings.SentenceClipsUnder(older, allParts, take.Cover));
        Assert.False(StoryRecordings.ReplacesBeyond(older, allParts, take.Cover)); // the old clip held exactly the take's sentences
        Assert.Empty(StoryRecordings.WiderSentenceClipsOver(older, allParts, take.Cover));
        Assert.Equal([allParts], StoryRecordings.Clips(saved).Select(c => c.Key));
        Assert.Equal([allParts], StoryRecordings.PlayingParts(saved, "en"));
        Assert.True(saved.UseRecordings);
    }

    [Fact]
    public void Saving_a_take_of_only_the_quote_keeps_the_whole_sentence_clip_with_its_narration_and_the_note_shows()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        var sentences = new StoryClipKey(1, 0, 2, "en"); // narration plus the quote
        Story older = StoryRecordings.WithClip(story, sentences, new PageRecording("en", 5, "rec-en-2-1-3-0b0b0b0b.m4a"));
        PageRecording take = Take(dinner, "rec-en-2-p2-2-0a0a0a0a.m4a"); // only Mummy's words

        Story saved = StoryRecordings.WithSavedTake(older, dinner.Key, take);

        Assert.Empty(StoryRecordings.SentenceClipsUnder(older, dinner.Key, take.Cover));
        Assert.Empty(StoryRecordings.ReplacedFiles(older, dinner.Key, take.Cover)); // no file is listed for deletion
        Assert.Equal([sentences], StoryRecordings.WiderSentenceClipsOver(older, dinner.Key, take.Cover)); // the note shows
        Assert.Equal([sentences], StoryRecordings.WiderSentenceClipsOver(saved, dinner.Key, take.Cover));
        Assert.Contains(sentences, StoryRecordings.Clips(saved).Select(c => c.Key)); // the record stays
        Assert.True(StoryRecordings.IsReferenced(saved, "rec-en-2-1-3-0b0b0b0b.m4a")); // and so does its file
    }

    [Fact]
    public void Saving_a_take_leaves_a_sentence_clip_of_other_sentences_alone_and_keeps_the_switch_as_it_was()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        Story older = StoryRecordings.WithClip(story, new StoryClipKey(0, 0, 0, "en"), new PageRecording("en", 5, "rec-en-1-1-1-0b0b0b0b.m4a")) with { UseRecordings = false };

        Story saved = StoryRecordings.WithSavedTake(older, dinner.Key, Take(dinner, "rec-en-2-p2-2-0a0a0a0a.m4a"));

        Assert.Equal(2, StoryRecordings.Clips(saved).Count());
        Assert.False(saved.UseRecordings);
        Assert.Empty(StoryRecordings.ReplacedFiles(older, dinner.Key, new SentenceCover(0, 2)));
    }

    [Fact]
    public void Saving_a_take_never_deletes_a_whole_sentence_clip_wider_than_the_take_record_or_file()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        StoryClipKey wholePage = StoryClipKey.OfPage(1, "en");
        Story older = StoryRecordings.WithClip(story, wholePage, new PageRecording("en", 5, "rec-en-2-0b0b0b0b.m4a"));
        var cover = new SentenceCover(1, 1);

        Story saved = StoryRecordings.WithSavedTake(older, dinner.Key, new PageRecording("en", 4, "rec-en-2-p2-2-0a0a0a0a.m4a", cover));

        Assert.Empty(StoryRecordings.SentenceClipsUnder(older, dinner.Key, cover));
        Assert.Empty(StoryRecordings.ReplacedFiles(older, dinner.Key, cover)); // the file is never listed for deletion
        Assert.Contains(wholePage, StoryRecordings.Clips(saved).Select(c => c.Key)); // the record stays
        Assert.True(StoryRecordings.IsReferenced(saved, "rec-en-2-0b0b0b0b.m4a"));
        Assert.True(StoryRecordings.IsReferenced(saved, "rec-en-2-p2-2-0a0a0a0a.m4a")); // the take is saved and kept: it plays once the wider clip is removed
    }

    [Fact]
    public void The_wider_clip_note_shows_only_for_a_part_whose_words_a_kept_wider_sentence_clip_covers()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        var cover = new SentenceCover(1, 1);
        Story wide = StoryRecordings.WithClip(story, StoryClipKey.OfPage(1, "en"), new PageRecording("en", 5, "rec-en-2-0b0b0b0b.m4a"));

        Assert.Single(StoryRecordings.WiderSentenceClipsOver(wide, dinner.Key, cover)); // before saving
        Story saved = StoryRecordings.WithSavedTake(wide, dinner.Key, new PageRecording("en", 4, "rec-en-2-p2-2-0a0a0a0a.m4a", cover));
        Assert.Single(StoryRecordings.WiderSentenceClipsOver(saved, dinner.Key, cover)); // after saving
        Assert.Empty(StoryRecordings.WiderSentenceClipsOver(story, dinner.Key, cover)); // no clip
        Story inside = StoryRecordings.WithClip(story, new StoryClipKey(1, 1, 1, "en"), new PageRecording("en", 5, "rec-en-2-2-2-0b0b0b0b.m4a"));
        Assert.Empty(StoryRecordings.WiderSentenceClipsOver(inside, dinner.Key, cover)); // a contained clip is replaced, not kept
        Story elsewhere = StoryRecordings.WithClip(story, StoryClipKey.OfPage(0, "en"), new PageRecording("en", 5, "rec-en-1-0c0c0c0c.m4a"));
        Assert.Empty(StoryRecordings.WiderSentenceClipsOver(elsewhere, dinner.Key, cover)); // another page
        Assert.Empty(StoryRecordings.WiderSentenceClipsOver(wide, StoryClipKey.OfPage(1, "en"), cover)); // not a part card
    }

    [Fact]
    public void Saving_a_take_replaces_a_sentence_clip_that_lies_inside_the_takes_cover_record_and_file()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        var inside = new StoryClipKey(1, 1, 1, "en");
        Story older = StoryRecordings.WithClip(story, inside, new PageRecording("en", 5, "rec-en-2-2-2-0b0b0b0b.m4a"));
        var cover = new SentenceCover(0, 2);

        Story saved = StoryRecordings.WithSavedTake(older, dinner.Key, new PageRecording("en", 4, "rec-en-2-p2-2-0a0a0a0a.m4a", cover));

        Assert.Equal(["rec-en-2-2-2-0b0b0b0b.m4a"], StoryRecordings.ReplacedFiles(older, dinner.Key, cover));
        Assert.DoesNotContain(inside, StoryRecordings.Clips(saved).Select(c => c.Key));
    }

    [Fact]
    public void The_cards_warning_and_the_first_recording_flag_come_from_the_same_set_as_the_files_removed()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        var cover = new SentenceCover(0, 2);
        Story older = StoryRecordings.WithClip(story, new StoryClipKey(1, 1, 1, "en"), new PageRecording("en", 5, "rec-en-2-2-2-0b0b0b0b.m4a")); // inside, and only the quote's words: goes
        older = StoryRecordings.WithClip(older, new StoryClipKey(1, 2, 5, "en"), new PageRecording("en", 5, "rec-en-2-3-6-0c0c0c0c.m4a")); // reaches past the cover: stays
        older = StoryRecordings.WithClip(older, new StoryClipKey(0, 0, 0, "en"), new PageRecording("en", 5, "rec-en-1-1-1-0d0d0d0d.m4a")); // other page: stays
        older = StoryRecordings.WithClip(older, StoryClipKey.OfParts(1, 0, 1, "en"), new PageRecording("en", 5, "rec-en-2-p1-2-0e0e0e0e.m4a")); // a part clip wider than the take: goes

        Story saved = StoryRecordings.WithSavedTake(older, dinner.Key, new PageRecording("en", 4, "rec-en-2-p2-2-0a0a0a0a.m4a", cover));

        IEnumerable<string> removed = StoryRecordings.Clips(older).Select(c => StoryRecordings.FileOf(c.Value, c.Key)).Where(f => !StoryRecordings.IsReferenced(saved, f));
        Assert.Equal(removed.Order(), StoryRecordings.ReplacedFiles(older, dinner.Key, cover).Order()); // the warning's list equals the files the save removes
        Assert.Equal(2, StoryRecordings.ReplacedFiles(older, dinner.Key, cover).Count);
        Assert.True(StoryRecordings.ReplacesBeyond(older, dinner.Key, cover)); // the wider part clip goes with its file, and the card says so
        Assert.False(StoryRecordings.ReplacesBeyond(StoryRecordings.WithClip(story, StoryClipKey.OfPage(1, "en"), new PageRecording("en", 5, "rec-en-2-0c0c0c0c.m4a")), dinner.Key, cover)); // a kept wider sentence clip is not "replaced"
        Assert.Empty(StoryRecordings.ReplacedClips(story, dinner.Key, cover)); // nothing to replace: a first recording
    }

    [Fact]
    public async Task A_re_recorded_take_whose_old_file_left_a_marker_is_not_deleted_on_the_next_load()
    {
        string root = Path.Combine(Path.GetTempPath(), "dino-take-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileStoryLibrary(root);
            Story book = new(StoryIds.New(), "Fox", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Mr. Fox ran. He said, \"Hello there.\" Then bye.")], [], CoverStyle.Colour, 0, string.Empty);
            await library.AddAsync(book);
            StoryClipKey key = StoryClipKey.OfParts(0, 1, 1, "en");
            string file = StoryRecordings.FileNameOf(key);
            string folder = library.FolderOf(book.Id);
            await File.WriteAllTextAsync(Path.Combine(folder, file), "old", TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(folder, file + PartClipAdoption.DeletedSuffix), [], TestContext.Current.CancellationToken); // the old delete failed

            await library.StoreRecordingFileAsync(book.Id, file, p => File.WriteAllTextAsync(p, "new", TestContext.Current.CancellationToken)); // the same deterministic name again
            await library.UpdateAsync(StoryRecordings.WithClip((await library.GetAsync(book.Id))!, key, new PageRecording("en", 2, file)));

            Assert.False(File.Exists(Path.Combine(folder, file + PartClipAdoption.DeletedSuffix)));
            Assert.Single(StoryRecordings.Clips((await library.GetAsync(book.Id))!));
            Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(folder, file), TestContext.Current.CancellationToken));
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
    public async Task A_listing_never_deletes_a_marked_file_the_story_still_names()
    {
        string root = Path.Combine(Path.GetTempPath(), "dino-take-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileStoryLibrary(root);
            StoryClipKey key = StoryClipKey.OfParts(0, 1, 1, "en");
            string file = StoryRecordings.FileNameOf(key);
            Story book = StoryRecordings.WithClip(new(StoryIds.New(), "Fox", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Mr. Fox ran. He said, \"Hello there.\" Then bye.")], [], CoverStyle.Colour, 0, string.Empty), key, new PageRecording("en", 2, file));
            await library.AddAsync(book);
            string path = await library.StoreRecordingFileAsync(book.Id, file, p => File.WriteAllTextAsync(p, "audio", TestContext.Current.CancellationToken));
            await File.WriteAllBytesAsync(Path.Combine(library.FolderOf(book.Id), file + PartClipAdoption.DeletedSuffix), [], TestContext.Current.CancellationToken);

            await library.GetAsync(book.Id);

            Assert.True(File.Exists(Path.Combine(library.FolderOf(book.Id), path)));
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
    public async Task A_restore_or_import_of_a_recording_clears_a_marker_left_under_its_name()
    {
        string root = Path.Combine(Path.GetTempPath(), "dino-take-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileStoryLibrary(root);
            StoryClipKey key = StoryClipKey.OfParts(0, 1, 1, "en");
            string file = StoryRecordings.FileNameOf(key);
            Story book = StoryRecordings.WithClip(new(StoryIds.New(), "Fox", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Mr. Fox ran. He said, \"Hello there.\" Then bye.")], [], CoverStyle.Colour, 0, string.Empty), key, new PageRecording("en", 2, file));
            string folder = library.FolderOf(book.Id);
            Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(Path.Combine(folder, file + PartClipAdoption.DeletedSuffix), [], TestContext.Current.CancellationToken); // a marker from an earlier life of this book

            await library.ImportAsync(book, [new PackageFile(file, () => new MemoryStream([1, 2, 3]))]);

            Assert.False(File.Exists(Path.Combine(folder, file + PartClipAdoption.DeletedSuffix)));
            Assert.Single(StoryRecordings.Clips((await library.GetAsync(book.Id))!));
            Assert.True(File.Exists(Path.Combine(folder, file)));
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
    public async Task A_marker_is_never_written_for_a_file_that_is_not_a_part_take()
    {
        string root = Path.Combine(Path.GetTempPath(), "dino-take-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileStoryLibrary(root);
            Story book = new(StoryIds.New(), "Fox", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Mr. Fox ran.")], [], CoverStyle.Colour, 0, string.Empty);
            await library.AddAsync(book);
            const string File3 = "rec-en-1.m4a";
            await library.StoreRecordingFileAsync(book.Id, File3, p => File.WriteAllTextAsync(p, "audio", TestContext.Current.CancellationToken));

            using (new FileStream(Path.Combine(library.FolderOf(book.Id), File3), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                await library.DeleteFileAsync(book.Id, File3); // locked on Windows: the delete fails
            }

            Assert.False(File.Exists(Path.Combine(library.FolderOf(book.Id), File3 + PartClipAdoption.DeletedSuffix)));
            Assert.False(PartClipAdoption.IsPartFileName(File3));
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
    public void A_take_that_does_not_stand_or_failed_this_session_is_left_out_of_the_read()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        Story recorded = StoryRecordings.WithClip(story, dinner.Key, Take(dinner, "rec-en-2-p2-2-0a0a0a0a.m4a"));
        IReadOnlyList<RecordPart> parts = RecordSplitBook.PartListOf(recorded);

        Assert.Single(PartTakes.SpansOf(recorded, "en", parts, _ => true));
        Assert.Empty(PartTakes.SpansOf(recorded, "en", parts, key => key != dinner.Key)); // the screen's words are not the reading language's, or the take failed to play: the book's own audio reads
    }

    [Fact]
    public void The_voice_count_leaves_out_the_words_a_take_stands_in_for()
    {
        var take = new PartTakeSpan(StoryClipKey.OfParts(0, 1, 1, "en"), 10, 20, 2);
        VoicedSegment before = new("He said", "v", 0, 9, 0);
        VoicedSegment inside = new("Hello", "v", 10, 20, 10);
        VoicedSegment after = new("Then bye.", "v", 21, 30, 21);

        Assert.Equal([0, 21], PartTakes.WithoutTakeWords([before, inside, after], [take]).Select(s => s.SourceStart));
        Assert.Equal(3, PartTakes.WithoutTakeWords([before, inside, after], []).Count);
    }

    [Fact]
    public void A_read_that_starts_inside_a_take_marks_the_take_as_one_unit_for_next_and_back_and_a_read_after_it_plans_no_take()
    {
        var take = new PartTakeSpan(StoryClipKey.OfParts(0, 1, 3, "en"), 10, 40, 5);
        Func<int, int> sentenceAt = position => position < 10 ? 0 : position < 20 ? 1 : position < 30 ? 2 : position < 40 ? 3 : 4; // sentences 1-3 hold the take

        Assert.Equal((1, 3), PartTakes.SentenceRangeOf([take], 1, sentenceAt)); // from the middle of the take, Next leaves from its last sentence and Back from its first
        Assert.Equal((1, 3), PartTakes.SentenceRangeOf([take], 3, sentenceAt));
        Assert.Null(PartTakes.SentenceRangeOf([take], 0, sentenceAt));
        Assert.Null(PartTakes.SentenceRangeOf([take], 4, sentenceAt));
        Assert.Null(PartTakes.TakeAt([take], 40)); // the take's end is outside it
        Assert.Equal(take, PartTakes.TakeAt([take], 10));

        PartTakePlan fromTheNext = PartTakes.Plan([new VoicedSegment("after", "v", 40, 50, 40)], [take]);

        Assert.Empty(fromTheNext.Takes); // Next from the take starts after its range: the take is never restarted
        Assert.Single(fromTheNext.Steps);
    }

    [Fact]
    public void Two_overlapping_takes_name_the_hidden_one_and_the_one_that_hides_it()
    {
        Story story = Freddy();
        IReadOnlyList<RecordPart> parts = RecordSplitBook.PartListOf(story);
        StoryClipKey wide = StoryClipKey.OfParts(2, 0, 2, "en");
        StoryClipKey inner = StoryClipKey.OfParts(2, 1, 1, "en");
        Story both = StoryRecordings.WithClip(StoryRecordings.WithClip(story, wide, new PageRecording("en", 3, StoryRecordings.NewFileName(wide)), keepOverlappingParts: true), inner, new PageRecording("en", 3, StoryRecordings.NewFileName(inner)), keepOverlappingParts: true);
        var hidden = new List<(StoryClipKey Hidden, StoryClipKey By)>();

        IReadOnlyList<PartTakeSpan> spans = PartTakes.SpansOf(both, "en", parts, null, (h, by) => hidden.Add((h, by)));

        Assert.Equal([wide], spans.Select(s => s.Key));
        Assert.Equal([(inner, wide)], hidden);
    }

    [Fact]
    public void A_starter_update_that_splits_a_page_into_other_sentences_clears_the_cover_of_a_take_it_refreshes()
    {
        const string Text = "Mr. Fox ran. He said, \"Hello there.\" Then bye.";
        Story english = new(StoryIds.New(), "Fox", "en", DateTimeOffset.UnixEpoch, [new StoryPage(Text)], [], CoverStyle.Colour, 0, string.Empty);
        Story norwegian = english with { Id = StoryIds.New(), Language = "nb" };
        StoryClipKey part = StoryClipKey.OfParts(0, 1, 1, "en");
        Story stored = StoryRecordings.WithClip(english, part, new PageRecording("en", 2, StoryRecordings.NewFileName(part), new SentenceCover(1, 1)));
        StarterUpdate.ClipMove move = Assert.Single(StarterUpdate.PieceMoves(stored, norwegian));
        Assert.True(move.DropCover);

        Story refreshed = StarterUpdate.Refresh(stored, norwegian, [], [move with { File = StoryRecordings.NewFileName(part) }]);

        PageRecording moved = StoryRecordings.ClipOf(refreshed, part)!;
        Assert.Null(moved.Cover); // the stale numbers would hide or show the wrong take
        Assert.Equal(2, moved.Seconds);
    }

    [Fact]
    public async Task A_take_file_that_could_not_be_deleted_is_not_adopted_again_on_the_next_load()
    {
        string root = Path.Combine(Path.GetTempPath(), "dino-take-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileStoryLibrary(root);
            Story book = new(StoryIds.New(), "Fox", "en", DateTimeOffset.UnixEpoch, [new StoryPage("Mr. Fox ran. He said, \"Hello there.\" Then bye.")], [], CoverStyle.Colour, 0, string.Empty);
            await library.AddAsync(book);
            string file = StoryRecordings.NewFileName(StoryClipKey.OfParts(0, 1, 1, "en"));
            string path = await library.StoreRecordingFileAsync(book.Id, file, p => File.WriteAllTextAsync(p, "audio", TestContext.Current.CancellationToken));
            Assert.Single(StoryRecordings.Clips((await library.GetAsync(book.Id))!)); // before the delete the file is found and adopted

            using (var held = new FileStream(Path.Combine(library.FolderOf(book.Id), file), FileMode.Open, FileAccess.Read, FileShare.None))
            {
                await library.DeleteFileAsync(book.Id, file); // locked on Windows: the delete fails; elsewhere it succeeds and there is nothing to find
                Assert.Empty(StoryRecordings.Clips((await library.GetAsync(book.Id))!)); // the deleted take does not come back
            }

            Assert.Empty(StoryRecordings.Clips((await library.GetAsync(book.Id))!)); // a listing never deletes a file: the marked file stays, unadopted
            Assert.True(File.Exists(Path.Combine(library.FolderOf(book.Id), file + PartClipAdoption.DeletedSuffix)));
            await library.DeleteFileAsync(book.Id, file); // the retry that goes through
            Assert.Empty(StoryRecordings.Clips((await library.GetAsync(book.Id))!));
            Assert.False(File.Exists(Path.Combine(library.FolderOf(book.Id), file)));
            Assert.False(File.Exists(Path.Combine(library.FolderOf(book.Id), file + PartClipAdoption.DeletedSuffix))); // the next listing drops the marker of a file that is gone
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
    public void A_part_take_plays_only_in_its_own_language_and_only_while_read_with_my_voice_is_on()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        Story recorded = StoryRecordings.WithClip(story, dinner.Key, Take(dinner, "rec-en-2-p2-2-0a0a0a0a.m4a"));

        Assert.Empty(StoryRecordings.PlayingParts(recorded, "fa"));
        Assert.Empty(StoryRecordings.PlayingParts(recorded with { UseRecordings = false }, "en"));
        PartTakes.Enabled = false;
        Assert.Empty(StoryRecordings.PlayingParts(recorded, "en"));
        Assert.False(PartTakes.HasPlayableTakes(recorded, "en"));
    }

    [Fact]
    public void The_stored_cover_survives_the_story_file_and_a_damaged_one_reads_as_no_cover_never_as_no_take()
    {
        Story story = Freddy();
        RecordPiece dinner = MummyCards(story, 1).Single(p => p.Text.StartsWith("Dinner"));
        Story recorded = StoryRecordings.WithClip(story, dinner.Key, Take(dinner, StoryRecordings.NewFileName(dinner.Key)));

        string json = StoryJson.Serialize(recorded);
        Story back = StoryJson.Deserialize(json);
        string damagedJson = System.Text.RegularExpressions.Regex.Replace(json, "\"CoverLast\":\\s*2", "\"CoverLast\": -5");
        Assert.NotEqual(json, damagedJson);
        Story damaged = StoryJson.Deserialize(damagedJson);

        Assert.Equal(new SentenceCover(0, 2), StoryRecordings.ClipOf(back, dinner.Key)!.Cover);
        Assert.Contains("CoverFirst", json, StringComparison.OrdinalIgnoreCase);
        Assert.Null(StoryRecordings.ClipOf(damaged, dinner.Key)!.Cover);
        Assert.Single(StoryRecordings.Clips(damaged));
    }

    [Fact]
    public void Two_part_takes_that_overlap_both_load_and_playback_takes_the_earlier_so_no_words_are_heard_twice()
    {
        Story story = Freddy();
        IReadOnlyList<RecordPart> parts = RecordSplitBook.PartListOf(story);
        StoryClipKey wide = StoryClipKey.OfParts(2, 0, 2, "en");
        StoryClipKey inner = StoryClipKey.OfParts(2, 1, 1, "en");
        Story both = StoryRecordings.WithClip(StoryRecordings.WithClip(story, wide, new PageRecording("en", 3, StoryRecordings.NewFileName(wide)), keepOverlappingParts: true), inner, new PageRecording("en", 3, StoryRecordings.NewFileName(inner)), keepOverlappingParts: true);

        Story back = StoryJson.Deserialize(StoryJson.Serialize(both));

        Assert.Equal(2, StoryRecordings.Clips(back).Count()); // load drops neither take, whichever is newer
        Assert.Equal([wide], PartTakes.SpansOf(back, "en", parts).Select(s => s.Key));
    }

    [Fact]
    public void A_part_take_moves_with_its_cover_cleared_when_the_new_page_splits_into_other_sentences_and_a_sentence_clip_beside_it_moves_too()
    {
        Story stored = Freddy();
        RecordPiece dinner = MummyCards(stored, 1).Single(p => p.Text.StartsWith("Dinner"));
        var sentences = new StoryClipKey(1, 0, 2, "en");
        stored = StoryRecordings.WithClip(StoryRecordings.WithClip(stored, dinner.Key, Take(dinner, "rec-en-2-p2-2-0a0a0a0a.m4a")), sentences, new PageRecording("en", 5, "rec-en-2-1-3-0b0b0b0b.m4a"));
        Story bundled = Freddy();

        IReadOnlyList<StarterUpdate.ClipMove> moves = StarterUpdate.PieceMoves(stored, bundled);

        Assert.Equal(2, moves.Count); // the part move never blocks or drops the sentence clip, and the other way round
        Assert.All(moves, m => Assert.False(m.DropCover)); // the same text splits the same way: the stored cover stays
        Story merged = StarterUpdate.WithMovedClips(bundled, stored, moves);
        Assert.Equal(new SentenceCover(0, 2), StoryRecordings.ClipOf(merged, dinner.Key)!.Cover);
        Assert.NotNull(StoryRecordings.ClipOf(merged, sentences));
    }

    [Fact]
    public void A_part_take_whose_page_has_the_same_parts_but_other_sentences_moves_with_its_cover_cleared_and_is_not_dropped()
    {
        const string Text = "Mr. Fox ran. He said, \"Hello there.\" Then bye."; // English cuts three sentences, Norwegian four, and both cut three parts
        Story English = new(StoryIds.New(), "Fox", "en", DateTimeOffset.UnixEpoch, [new StoryPage(Text)], [], CoverStyle.Colour, 0, string.Empty);
        Story Norwegian = English with { Id = StoryIds.New(), Language = "nb" };
        StoryClipKey part = StoryClipKey.OfParts(0, 1, 1, "en");
        Story stored = StoryRecordings.WithClip(English, part, new PageRecording("en", 2, StoryRecordings.NewFileName(part), new SentenceCover(1, 1)));

        IReadOnlyList<StarterUpdate.ClipMove> moves = StarterUpdate.PieceMoves(stored, Norwegian);

        StarterUpdate.ClipMove move = Assert.Single(moves);
        Assert.True(move.DropCover);
        Story merged = StarterUpdate.WithMovedClips(Norwegian, stored, [move with { File = StoryRecordings.NewFileName(part) }]);
        Assert.Null(StoryRecordings.ClipOf(merged, part)!.Cover);
        Assert.Equal([part], StoryRecordings.PlayingParts(merged, "en")); // kept, and it plays: a take with no cover is never hidden
    }

    [Fact]
    public void A_move_marked_to_drop_the_cover_keeps_the_take_and_clears_only_the_cover()
    {
        Story stored = Freddy();
        RecordPiece dinner = MummyCards(stored, 1).Single(p => p.Text.StartsWith("Dinner"));
        stored = StoryRecordings.WithClip(stored, dinner.Key, Take(dinner, "rec-en-2-p2-2-0a0a0a0a.m4a"));
        var move = new StarterUpdate.ClipMove(dinner.Key, dinner.Key, "rec-en-2-p2-2-0c0c0c0c.m4a", DropCover: true);

        Story merged = StarterUpdate.WithMovedClips(Freddy(), stored, [move]);

        PageRecording moved = StoryRecordings.ClipOf(merged, dinner.Key)!;
        Assert.Null(moved.Cover);
        Assert.Equal("rec-en-2-p2-2-0c0c0c0c.m4a", moved.File);
        Assert.Equal(4, moved.Seconds);
    }
}
