using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 279 work items 1 and 2: the pure split of a book into recording pieces (with fake heights), the clip model next to today's per-page recording, and the walk rules.</summary>
public sealed class RecordSplitTests
{
    private const string Rex = "rex";
    private const string Wolf = "wolf";

    private static RecordSentence S(int page, int index, string speaker, string text, bool ends = false) => new(page, index, text, speaker, ends);

    // Page 0: narration, Rex, Rex, narration. Page 1: narration, Wolf.
    private static readonly IReadOnlyList<RecordSentence> Book =
    [
        S(0, 0, RecordSplit.Storyteller, "Once."), S(0, 1, Rex, "\"Hi!\""), S(0, 2, Rex, "\"Bye!\""), S(0, 3, RecordSplit.Storyteller, "The end.", ends: true),
        S(1, 0, RecordSplit.Storyteller, "Next page."), S(1, 1, Wolf, "\"Let me in!\"", ends: true),
    ];

    private static Story MakeStory(params string[] pages) =>
        new(StoryIds.New(), "Pigs", "en", DateTimeOffset.UnixEpoch, pages.Select(t => new StoryPage(t)).ToList(), [], CoverStyle.Colour, 0, string.Empty);

    [Fact]
    public void Per_character_keeps_the_ticked_speakers_lines_in_story_order()
    {
        IReadOnlyList<RecordPiece> pieces = RecordSplit.ByCharacter(Book, new HashSet<string> { Rex, RecordSplit.Storyteller });

        Assert.Equal(
            [new StoryClipKey(0, 0, 0), new StoryClipKey(0, 1, 2), new StoryClipKey(0, 3, 3), new StoryClipKey(1, 0, 0)],
            pieces.Select(p => p.Key));
        Assert.Equal("\"Hi!\" \"Bye!\"", pieces[1].Text);
    }

    [Fact]
    public void An_unticked_speaker_is_left_out_and_nobody_ticked_is_nothing()
    {
        Assert.DoesNotContain(RecordSplit.ByCharacter(Book, new HashSet<string> { Wolf }), p => p.Speaker != Wolf);
        Assert.Single(RecordSplit.ByCharacter(Book, new HashSet<string> { Wolf }));
        Assert.Empty(RecordSplit.ByCharacter(Book, new HashSet<string>()));
    }

    [Fact]
    public void A_run_of_narration_between_spoken_lines_is_one_line_and_never_crosses_a_page()
    {
        IReadOnlyDictionary<string, int> counts = RecordSplit.LineCounts(Book);

        Assert.Equal(3, counts[RecordSplit.Storyteller]); // "Once.", "The end.", "Next page."
        Assert.Equal(1, counts[Rex]);
        Assert.Equal(1, counts[Wolf]);
    }

    [Fact]
    public void A_line_taller_than_the_limit_is_cut_at_a_sentence()
    {
        RecordSentence[] long3 = [S(0, 0, Rex, "aaaa"), S(0, 1, Rex, "bbbb"), S(0, 2, Rex, "cccc", ends: true)];

        IReadOnlyList<RecordPiece> pieces = RecordSplit.ByCharacter(long3, new HashSet<string> { Rex }, text => text.Length, maxHeight: 9);

        Assert.Equal([new StoryClipKey(0, 0, 1), new StoryClipKey(0, 2, 2)], pieces.Select(p => p.Key));
    }

    [Fact]
    public void A_short_page_is_one_whole_page_piece()
    {
        IReadOnlyList<RecordPiece> pieces = RecordSplit.WholePages(Book, _ => 10, maxHeight: 100);

        Assert.Equal([new StoryClipKey(0, 0, 3), new StoryClipKey(1, 0, 1)], pieces.Select(p => p.Key));
        Assert.All(pieces, p => Assert.Equal(RecordSplit.Storyteller, p.Speaker));
        Assert.Equal(1, RecordSplit.PartsOf(pieces, 0));
    }

    [Fact]
    public void A_long_page_is_cut_at_the_end_of_a_paragraph()
    {
        RecordSentence[] page =
        [
            S(0, 0, RecordSplit.Storyteller, "aaaa"), S(0, 1, RecordSplit.Storyteller, "bbbb", ends: true),
            S(0, 2, RecordSplit.Storyteller, "cccc"), S(0, 3, RecordSplit.Storyteller, "dddd", ends: true),
        ];

        // Heights are the text length: two sentences are 9 ("aaaa bbbb"), the whole page 20.
        IReadOnlyList<RecordPiece> pieces = RecordSplit.WholePages(page, text => text.Length, maxHeight: 12);

        Assert.Equal([new StoryClipKey(0, 0, 1), new StoryClipKey(0, 2, 3)], pieces.Select(p => p.Key));
        Assert.Equal(2, RecordSplit.PartsOf(pieces, 0));
        Assert.Equal(2, RecordSplit.PartNumber(pieces, pieces[1]));
    }

    [Fact]
    public void A_paragraph_that_is_too_tall_alone_is_cut_at_a_sentence_and_a_neighbour_paragraph_stays_whole()
    {
        RecordSentence[] page =
        [
            S(0, 0, RecordSplit.Storyteller, "aa", ends: true),
            S(0, 1, RecordSplit.Storyteller, "bbbb"), S(0, 2, RecordSplit.Storyteller, "cccc"), S(0, 3, RecordSplit.Storyteller, "dddd", ends: true),
        ];

        IReadOnlyList<RecordPiece> pieces = RecordSplit.WholePages(page, text => text.Length, maxHeight: 10);

        Assert.Equal([new StoryClipKey(0, 0, 0), new StoryClipKey(0, 1, 2), new StoryClipKey(0, 3, 3)], pieces.Select(p => p.Key));
    }

    [Fact]
    public void A_sentence_taller_than_the_limit_is_a_piece_of_its_own()
    {
        RecordSentence[] page = [S(0, 0, RecordSplit.Storyteller, "aaaaaaaaaaaa"), S(0, 1, RecordSplit.Storyteller, "b", ends: true)];

        IReadOnlyList<RecordPiece> pieces = RecordSplit.WholePages(page, text => text.Length, maxHeight: 5);

        Assert.Equal([new StoryClipKey(0, 0, 0), new StoryClipKey(0, 1, 1)], pieces.Select(p => p.Key));
    }

    [Fact]
    public void The_book_reads_its_sentences_speakers_and_paragraphs_from_the_story()
    {
        Story story = MakeStory("Once upon a time. \"Hello,\" said Rex.\n\nThe end.", "Another page.");

        RecordBook book = RecordSplitBook.Of(story);

        Assert.Equal(RecordSplit.Storyteller, book.Speakers[0].Id);
        Assert.Equal(new[] { 0, 1, 2 }, book.Sentences.Where(s => s.Page == 0).Select(s => s.Index));
        Assert.True(book.Sentences.Single(s => s.Page == 1).EndsParagraph);
        Assert.True(book.Sentences.First(s => s.Page == 0 && s.Index == 1).EndsParagraph); // a blank line follows it
        Assert.Contains(book.Speakers, s => s.Name == "Rex");
    }

    // ---- the clip model ----

    [Fact]
    public void A_whole_page_recording_is_a_clip_covering_the_page_and_keeps_its_file_name()
    {
        Story story = StoryRecordings.With(MakeStory("A.", "B."), 1, new PageRecording("en", 7));

        KeyValuePair<StoryClipKey, PageRecording> clip = Assert.Single(StoryRecordings.Clips(story));

        Assert.Equal(StoryClipKey.OfPage(1), clip.Key);
        Assert.Equal("rec-2.m4a", StoryRecordings.FileNameOf(clip.Key));
        Assert.Equal(new PageRecording("en", 7), StoryRecordings.ClipOf(story, StoryClipKey.OfPage(1)));
        Assert.NotNull(StoryRecordings.ClipCovering(story, 1, 5));
    }

    [Fact]
    public void A_piece_file_name_counts_everything_from_one_and_only_those_names_are_recording_files()
    {
        Assert.Equal("rec-3-2-4.m4a", StoryRecordings.FileNameOf(new StoryClipKey(2, 1, 3)));
        Assert.True(StoryRecordings.IsRecordingFileName("rec-3-2-4.m4a"));
        Assert.True(StoryRecordings.IsRecordingFileName("rec-3.m4a"));
        Assert.False(StoryRecordings.IsRecordingFileName("rec-3-0-4.m4a"));
        Assert.False(StoryRecordings.IsRecordingFileName("rec-3-2.m4a"));
        Assert.False(StoryRecordings.IsRecordingFileName("rec-3-2-4.mp3"));
    }

    [Fact]
    public void Take_files_are_told_apart_from_recordings()
    {
        string take = StoryRecordings.NewTakeFileName();

        Assert.True(StoryRecordings.IsTakeFileName(take));
        Assert.False(StoryRecordings.IsRecordingFileName(take));
        Assert.False(StoryRecordings.IsTakeFileName("rec-1.m4a"));
        Assert.False(StoryRecordings.IsTakeFileName(null));
    }

    [Fact]
    public void A_piece_replaces_every_clip_it_overlaps_and_the_first_clip_turns_the_switch_on()
    {
        Story story = MakeStory("A. B. C.");
        story = StoryRecordings.WithClip(story, new StoryClipKey(0, 0, 0), new PageRecording("en", 2));
        Assert.True(story.UseRecordings);
        story = StoryRecordings.WithClip(story, new StoryClipKey(0, 2, 2), new PageRecording("en", 3));

        Story replaced = StoryRecordings.WithClip(story, new StoryClipKey(0, 0, 1), new PageRecording("en", 9));

        Assert.Equal([new StoryClipKey(0, 0, 1), new StoryClipKey(0, 2, 2)], StoryRecordings.Clips(replaced).Select(c => c.Key));
        Assert.Equal([new StoryClipKey(0, 0, 0)], StoryRecordings.Overlapping(story, new StoryClipKey(0, 0, 1)));
    }

    [Fact]
    public void A_piece_over_a_whole_page_recording_drops_it_and_a_whole_page_drops_its_pieces()
    {
        Story story = StoryRecordings.With(MakeStory("A. B."), 0, new PageRecording("en", 5));

        Story pieces = StoryRecordings.WithClip(story, new StoryClipKey(0, 1, 1), new PageRecording("en", 2));
        Assert.Null(pieces.Recordings);
        Assert.Equal([new StoryClipKey(0, 1, 1)], StoryRecordings.Clips(pieces).Select(c => c.Key));

        Story whole = StoryRecordings.WithClip(pieces, StoryClipKey.OfPage(0), new PageRecording("en", 6));
        Assert.Null(whole.PieceRecordings);
        Assert.Equal([StoryClipKey.OfPage(0)], StoryRecordings.Clips(whole).Select(c => c.Key));
    }

    [Fact]
    public void Removing_the_last_clip_turns_the_switch_off_and_leaves_no_field()
    {
        Story story = StoryRecordings.WithClip(MakeStory("A. B."), new StoryClipKey(0, 0, 0), new PageRecording("en", 2));

        Story none = StoryRecordings.WithoutClip(story, new StoryClipKey(0, 0, 0));

        Assert.Null(none.PieceRecordings);
        Assert.False(none.UseRecordings);
        Assert.Equal(0, StoryRecordings.Count(none));
    }

    [Fact]
    public void Removing_a_whole_page_clip_keeps_the_switch_on_while_a_piece_remains()
    {
        Story story = StoryRecordings.With(MakeStory("A.", "B."), 0, new PageRecording("en", 5));
        story = StoryRecordings.WithClip(story, new StoryClipKey(1, 0, 0), new PageRecording("en", 2));

        Story without = StoryRecordings.WithoutClip(story, StoryClipKey.OfPage(0));

        Assert.True(without.UseRecordings);
        Assert.Equal(1, StoryRecordings.Count(without));
    }

    [Fact]
    public void A_clip_in_another_language_does_not_play_and_a_page_with_a_playing_clip_shows_its_own_language()
    {
        Story story = StoryRecordings.WithClip(MakeStory("A. B."), new StoryClipKey(0, 0, 0), new PageRecording("en", 2));
        Assert.True(StoryRecordings.PlaysClip(story, new StoryClipKey(0, 0, 0)));
        Assert.Contains(0, StoryRecordings.PagesShownInOwnLanguage(story, [1]));

        Story german = story with { Language = "de" };
        Assert.False(StoryRecordings.PlaysClip(german, new StoryClipKey(0, 0, 0)));
        Assert.Empty(StoryRecordings.PagesShownInOwnLanguage(german, [1]));
    }

    [Fact]
    public void A_page_shows_its_own_language_only_when_clips_cover_every_sentence()
    {
        Story story = StoryRecordings.WithClip(MakeStory("A. B. C."), new StoryClipKey(0, 1, 1), new PageRecording("en", 2));
        Assert.Empty(StoryRecordings.PagesShownInOwnLanguage(story, [3])); // one line of three: the page reads translated

        story = StoryRecordings.WithClip(story, new StoryClipKey(0, 0, 0), new PageRecording("en", 2));
        story = StoryRecordings.WithClip(story, new StoryClipKey(0, 2, 2), new PageRecording("en", 2));
        Assert.Contains(0, StoryRecordings.PagesShownInOwnLanguage(story, [3]));
    }

    [Fact]
    public void A_clip_naming_sentences_the_page_lacks_does_not_make_the_page_show_its_own_language()
    {
        Story story = StoryRecordings.WithClip(MakeStory("A. B."), new StoryClipKey(0, 0, 2), new PageRecording("en", 2));
        Assert.Empty(StoryRecordings.PagesShownInOwnLanguage(story, [2])); // the clip does not fit a two-sentence page, so the reader plays nothing for it
    }

    [Fact]
    public void The_font_is_normal_when_the_reference_card_fits_and_smaller_when_the_card_is_smaller_but_never_below_the_floor()
    {
        const double Font = 21;
        Assert.Equal(Font, RecordCardFit.FontSize(Font, 303, 304, 303, 304));
        Assert.Equal(Font, RecordCardFit.FontSize(Font, 303, 304, 600, 900)); // a larger window shows the normal size

        double half = RecordCardFit.FontSize(Font, 303, 304, 303, 152);
        Assert.True(half < Font && half >= RecordCardFit.MinFontSize);
        Assert.Equal(RecordCardFit.MinFontSize, RecordCardFit.FontSize(Font, 303, 304, 20, 20));
    }

    [Fact]
    public void A_page_is_cut_the_same_way_whatever_the_window_because_the_cut_uses_the_fixed_reference_card()
    {
        string pieces = File.ReadAllText(Path.Combine(DinoReader.BookBuilder.StarterBuilder.RepoRoot(), "src", "DinoReader.App", "RecordPieces.cs"));

        Assert.Contains("public static IReadOnlyList<RecordPiece> Pieces(Story story, RecordBook book, RecordChoice choice)", pieces);
        Assert.Contains("MaxTextHeight(RecordCardFit.ReferenceWindowHeight, withSaysRow: !whole)", pieces);
        Assert.Contains("TextWidth(RecordCardFit.ReferenceWindowWidth)", pieces);
        Assert.DoesNotContain("window.Height", pieces);
        Assert.DoesNotContain("window.Width", pieces);
    }

    [Fact]
    public void A_first_take_over_a_whole_page_clip_leaves_that_clip_until_the_take_is_committed()
    {
        Story story = StoryRecordings.With(MakeStory("A. B. C. D."), 0, new PageRecording("en", 9));
        var part = new StoryClipKey(0, 0, 1);

        Assert.Equal([StoryClipKey.OfPage(0)], StoryRecordings.Overlapping(story, part)); // the old clip the new take sits over
        Assert.Equal(1, StoryRecordings.Count(story)); // nothing is changed by recording the take

        string page = File.ReadAllText(Path.Combine(DinoReader.BookBuilder.StarterBuilder.RepoRoot(), "src", "DinoReader.App", "RecordPage.cs"));
        Assert.Contains("if (OldClipsOf(take.Piece.Key).Count == 0)", page); // only a piece with no clip over it is saved at once
        Assert.True(page.IndexOf("await SaveWithClipAsync(", StringComparison.Ordinal) < page.IndexOf("await DeleteReplacedFilesAsync(", StringComparison.Ordinal)); // new file and story first, replaced files last
        Assert.True(page.IndexOf("await SaveWithClipAsync(", StringComparison.Ordinal) < page.IndexOf("TryDelete(take.Path);", page.IndexOf("await SaveWithClipAsync(", StringComparison.Ordinal), StringComparison.Ordinal)); // the take file goes only after the story saved

        Story committed = StoryRecordings.WithClip(story, part, new PageRecording("en", 3));
        Assert.Equal([part], StoryRecordings.Clips(committed).Select(c => c.Key));
    }

    [Fact]
    public void Replacing_the_only_clip_never_turns_a_switched_off_book_voice_back_on()
    {
        Story story = StoryRecordings.WithClip(MakeStory("A. B. C."), new StoryClipKey(0, 0, 1), new PageRecording("en", 2));
        story = story with { UseRecordings = false };

        Story replaced = StoryRecordings.WithClip(story, new StoryClipKey(0, 1, 2), new PageRecording("en", 2));

        Assert.False(replaced.UseRecordings);
        Assert.Equal([new StoryClipKey(0, 1, 2)], StoryRecordings.Clips(replaced).Select(c => c.Key));
    }

    [Fact]
    public void Piece_clips_survive_the_json_round_trip_and_a_bad_range_is_dropped()
    {
        Story story = StoryRecordings.WithClip(MakeStory("A. B. C."), new StoryClipKey(0, 1, 2), new PageRecording("en", 4));
        story = StoryRecordings.WithClip(story, StoryClipKey.OfPage(0), new PageRecording("en", 1)); // overlaps: the whole-page clip wins and the piece goes

        Story again = StoryJson.Deserialize(StoryJson.Serialize(story));

        Assert.Equal([StoryClipKey.OfPage(0)], StoryRecordings.Clips(again).Select(c => c.Key));

        Story pieces = StoryRecordings.WithClip(MakeStory("A. B. C."), new StoryClipKey(0, 1, 2), new PageRecording("en", 4));
        Story back = StoryJson.Deserialize(StoryJson.Serialize(pieces));
        Assert.Equal(new PageRecording("en", 4), StoryRecordings.ClipOf(back, new StoryClipKey(0, 1, 2)));
        Assert.True(back.UseRecordings);
    }

    // ---- the walk ----

    [Fact]
    public void A_piece_is_recorded_only_when_one_clip_covers_all_its_sentences()
    {
        Story story = StoryRecordings.WithClip(MakeStory("A. B. C."), new StoryClipKey(0, 0, 1), new PageRecording("en", 2));
        var inside = new RecordPiece(new StoryClipKey(0, 1, 1), RecordSplit.Storyteller, "B.");
        var across = new RecordPiece(new StoryClipKey(0, 1, 2), RecordSplit.Storyteller, "B. C.");

        Assert.True(RecordSplitWalk.IsRecorded(story, inside));
        Assert.False(RecordSplitWalk.IsRecorded(story, across));
        Assert.Equal(0, RecordSplitWalk.FirstUnrecorded(story, [across]));
        Assert.Equal(-1, RecordSplitWalk.FirstUnrecorded(story, [inside]));
    }

    [Fact]
    public void Record_the_rest_starts_at_the_first_piece_with_no_recording()
    {
        Story story = MakeStory("A. B. C.");
        story = StoryRecordings.WithClip(story, new StoryClipKey(0, 0, 0), new PageRecording("en", 2));
        RecordPiece[] pieces = [new(new StoryClipKey(0, 0, 0), "", "A."), new(new StoryClipKey(0, 1, 1), "", "B."), new(new StoryClipKey(0, 2, 2), "", "C.")];

        Assert.Equal(1, RecordSplitWalk.FirstUnrecorded(story, pieces));
        Assert.Equal(1, RecordSplitWalk.RecordedCount(story, pieces));
    }

    [Fact]
    public void A_book_with_only_whole_page_recordings_opens_as_whole_pages_and_any_other_per_character()
    {
        Story whole = StoryRecordings.With(MakeStory("A.", "B."), 0, new PageRecording("en", 5));
        Story pieces = StoryRecordings.WithClip(whole, new StoryClipKey(1, 0, 0), new PageRecording("en", 5));

        Assert.Equal(RecordMode.WholePage, RecordSplitWalk.DefaultMode(whole));
        Assert.Equal(RecordMode.PerCharacter, RecordSplitWalk.DefaultMode(pieces));
        Assert.Equal(RecordMode.PerCharacter, RecordSplitWalk.DefaultMode(MakeStory("A.")));
    }

    [Fact]
    public void The_sheet_counts_pages_for_a_whole_page_book_only_when_every_sentence_of_the_page_is_covered()
    {
        Story story = MakeStory("A. B.", "C.");
        story = StoryRecordings.WithClip(story, new StoryClipKey(0, 0, 0), new PageRecording("en", 2));
        story = StoryRecordings.WithClip(story, new StoryClipKey(1, 0, 0), new PageRecording("en", 2));
        RecordBook book = RecordSplitBook.Of(story);

        Assert.Equal(new RecordProgress(1, 2, RecordUnit.Pages), RecordSplitWalk.Progress(story, book, RecordMode.WholePage));
        Assert.Equal(RecordUnit.Lines, RecordSplitWalk.Progress(story, book, RecordMode.PerCharacter).Unit);
    }

    [Fact]
    public void Listen_and_change_visits_each_recorded_clip_once_for_the_ticked_speakers()
    {
        Story story = MakeStory("Once. \"Hi,\" said Rex. The end.");
        RecordBook book = RecordSplitBook.Of(story);
        int rexAt = book.Sentences.First(s => s.Speaker != RecordSplit.Storyteller).Index;
        story = StoryRecordings.WithClip(story, new StoryClipKey(0, rexAt, rexAt), new PageRecording("en", 2));
        story = StoryRecordings.WithClip(story, new StoryClipKey(0, 0, 0), new PageRecording("en", 2));

        IReadOnlyList<RecordPiece> all = RecordSplitWalk.Recorded(story, book, RecordMode.WholePage, new HashSet<string>());
        IReadOnlyList<RecordPiece> storytellerOnly = RecordSplitWalk.Recorded(story, book, RecordMode.PerCharacter, new HashSet<string> { RecordSplit.Storyteller });

        Assert.Equal(2, all.Count);
        Assert.Single(storytellerOnly);
        Assert.Equal(RecordSplit.Storyteller, storytellerOnly[0].Speaker);
    }

    [Fact]
    public void The_remembered_choice_round_trips_and_a_damaged_one_reads_as_none()
    {
        var choice = new RecordChoice(RecordMode.WholePage, new HashSet<string> { "b", "a" });

        RecordChoice? back = RecordSplitWalk.Read(RecordSplitWalk.Write(choice));

        Assert.Equal(RecordMode.WholePage, back!.Mode);
        Assert.Equal(new[] { "a", "b" }, back.Ticked.Order());
        Assert.Null(RecordSplitWalk.Read(null));
        Assert.Null(RecordSplitWalk.Read("{not json"));
        Assert.Null(RecordSplitWalk.Read("{\"Mode\":\"Nope\"}"));
    }

    [Fact]
    public async Task A_stored_piece_clip_travels_in_a_backup_and_never_in_a_shared_package()
    {
        string root = Path.Combine(Path.GetTempPath(), "dino-piece-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileStoryLibrary(Path.Combine(root, "a"));
            Story story = MakeStory("A. B.") with { SourceId = StoryIds.New() };
            await library.AddAsync(story);
            var key = new StoryClipKey(0, 1, 1);
            await library.StoreClipAsync(story.Id, key, path => File.WriteAllTextAsync(path, "audio"));
            await library.UpdateAsync(StoryRecordings.WithClip(story, key, new PageRecording("en", 3)));

            using var shared = new MemoryStream();
            await StoryPackageWriter.WriteAsync((await library.GetAsync(story.Id))!, library, shared);
            using var backup = new MemoryStream();
            await StoryPackageWriter.WriteAsync((await library.GetAsync(story.Id))!, library, backup, includeRecordings: true);

            var other = new FileStoryLibrary(Path.Combine(root, "b"));
            Story fromShared = await StoryPackageReader.ImportAsync(new MemoryStream(shared.ToArray()), other);
            var third = new FileStoryLibrary(Path.Combine(root, "c"));
            Story fromBackup = await StoryPackageReader.ImportAsync(new MemoryStream(backup.ToArray()), third, keepId: true);

            Assert.Equal(0, StoryRecordings.Count(fromShared));
            Assert.Equal(new PageRecording("en", 3), StoryRecordings.ClipOf(fromBackup, key));
            Assert.True(File.Exists(third.TryResolveFile(fromBackup.Id, "rec-1-2-2.m4a")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
