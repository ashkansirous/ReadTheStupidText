using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 279, design 2: the pure rules behind the Record screen's commit (a fresh file per commit, which clips a take replaces; no sweep of files no clip names, design 3) and the piece moves of a starter refresh.</summary>
public sealed class RecordCommitTests
{
    private static Story MakeStoryIn(string language, params string[] pages) =>
        new(StoryIds.New(), "Pigs", language, DateTimeOffset.UnixEpoch, pages.Select(t => new StoryPage(t)).ToList(), [], CoverStyle.Colour, 0, string.Empty);

    private static Story MakeStory(params string[] pages) => MakeStoryIn("en", pages);

    [Fact]
    public void A_stored_file_is_read_back_only_for_its_own_clip()
    {
        var piece = new StoryClipKey(2, 0, 1);
        string own = StoryRecordings.NewFileName(piece);
        string ofPage = StoryRecordings.NewFileName(StoryClipKey.OfPage(0));
        Story story = MakeStory("A.", "B.", "C. D.") with
        {
            Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 1, ofPage), [1] = new("en", 1, "rec-1-0123abcd.m4a") },
            PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [piece] = new("en", 2, own) },
        };

        Story read = StoryJson.Deserialize(StoryJson.Serialize(story).Replace("rec-1-0123abcd.m4a", "rec-5.m4a"));

        Assert.Equal(ofPage, read.Recordings![0].File);
        Assert.Null(read.Recordings[1].File); // page 2's clip naming another page's file reads as its own usual name
        Assert.Equal(own, read.PieceRecordings![piece].File);
    }

    [Fact]
    public void A_stored_file_of_another_clip_or_a_duplicate_is_never_read_back()
    {
        var piece = new StoryClipKey(1, 0, 0);
        string pageFile = StoryRecordings.NewFileName(StoryClipKey.OfPage(0));
        Story story = MakeStory("A.", "B.") with
        {
            Recordings = new Dictionary<int, PageRecording> { [0] = new("en", 1, pageFile) },
            PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [piece] = new("en", 1, StoryRecordings.NewFileName(piece)) },
        };
        string pieceFile = story.PieceRecordings![piece].File!;

        Story read = StoryJson.Deserialize(StoryJson.Serialize(story).Replace(pieceFile, pageFile));

        Assert.Equal(pageFile, read.Recordings![0].File);
        Assert.Null(read.PieceRecordings![piece].File);
    }

    [Fact]
    public void A_clip_fits_a_page_only_when_all_its_sentences_exist()
    {
        Assert.True(StoryClipKey.OfPage(0).FitsPage(1));
        Assert.True(new StoryClipKey(0, 0, 2).FitsPage(3));
        Assert.False(new StoryClipKey(0, 0, 2).FitsPage(2));
        Assert.False(new StoryClipKey(0, 1, 1).FitsPage(1));
    }

    [Fact]
    public void A_starter_move_carries_the_fresh_file_name_it_was_stored_under()
    {
        var piece = new StoryClipKey(0, 0, 0);
        string fresh = StoryRecordings.NewFileName(piece);
        Story stored = MakeStory("A.", "B.") with
        {
            Recordings = new Dictionary<int, PageRecording> { [1] = new("en", 3) },
            PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [piece] = new("en", 2) },
            UseRecordings = true,
        };
        Story bundled = MakeStory("A.", "B.") with { Id = "new" };

        Story refreshed = StarterUpdate.Refresh(stored, bundled, [new StarterUpdate.RecordingMove(1, 1, "rec-2-0123abcd.m4a")], [new StarterUpdate.ClipMove(piece, piece, fresh)]);
        Story merged = StarterUpdate.WithMovedClips(bundled, stored, [new StarterUpdate.ClipMove(piece, piece, fresh)]);

        Assert.Equal("rec-2-0123abcd.m4a", refreshed.Recordings![1].File);
        Assert.Equal(fresh, refreshed.PieceRecordings![piece].File);
        Assert.Equal(fresh, merged.PieceRecordings![piece].File);
    }

    [Fact]
    public void A_fresh_file_name_is_a_recording_name_of_its_clip_and_never_repeats()
    {
        var piece = new StoryClipKey(2, 0, 1);

        string first = StoryRecordings.NewFileName(piece);
        string second = StoryRecordings.NewFileName(piece);

        Assert.NotEqual(first, second);
        Assert.True(StoryRecordings.IsRecordingFileName(first));
        Assert.True(StoryRecordings.IsFreshFileNameOf(first, piece));
        Assert.False(StoryRecordings.IsFreshFileNameOf(first, new StoryClipKey(2, 0, 2)));
        Assert.StartsWith("rec-3-1-2-", first);
        Assert.StartsWith("rec-4-", StoryRecordings.NewFileName(StoryClipKey.OfPage(3)));
        Assert.False(StoryRecordings.IsFreshFileNameOf("rec-3-1-2.m4a", piece));
    }

    [Fact]
    public void A_clip_without_a_stored_file_name_uses_its_usual_one()
    {
        var piece = new StoryClipKey(0, 1, 1);
        Story old = StoryRecordings.WithClip(MakeStory("A. B."), piece, new PageRecording("en", 3));
        string fresh = StoryRecordings.NewFileName(piece);
        Story named = StoryRecordings.WithClip(old, piece, new PageRecording("en", 3, fresh));

        Assert.Equal("rec-1-2-2.m4a", StoryRecordings.FileOf(old, piece));
        Assert.Equal(fresh, StoryRecordings.FileOf(named, piece));
        Assert.Equal([fresh], StoryRecordings.Files(named));
    }

    [Fact]
    public void A_stored_name_that_is_not_a_recording_file_name_is_not_trusted()
    {
        var piece = new StoryClipKey(0, 1, 1);
        Story story = StoryRecordings.WithClip(MakeStory("A. B."), piece, new PageRecording("en", 3, "../evil.m4a"));

        Assert.Equal("rec-1-2-2.m4a", StoryRecordings.FileOf(story, piece));
    }

    [Fact]
    public void A_stored_file_name_survives_the_story_file_and_is_left_out_when_it_is_the_usual_one()
    {
        var piece = new StoryClipKey(0, 1, 1);
        string fresh = StoryRecordings.NewFileName(piece);
        Story named = StoryRecordings.WithClip(MakeStory("A. B."), piece, new PageRecording("en", 3, fresh));
        string json = StoryJson.Serialize(named);

        Story back = StoryJson.Deserialize(json);
        Story foreign = StoryJson.Deserialize(json.Replace(fresh, "rec-9-aaaaaaaa.m4a"));

        Assert.Contains(fresh, json);
        Assert.Equal(fresh, StoryRecordings.ClipOf(back, piece)!.File);
        Assert.Null(StoryRecordings.ClipOf(foreign, piece)!.File); // a recording name that is not this clip's own fresh name is not read back (check 2, finding 1)
        Assert.Equal("rec-1-2-2.m4a", StoryRecordings.FileOf(foreign, piece, name => name == "rec-9-aaaaaaaa.m4a"));
        Assert.Equal("rec-1-2-2.m4a", StoryRecordings.FileOf(foreign, piece, _ => false)); // a stored name whose file is not there reads the clip's usual name
        Assert.Null(StoryRecordings.ClipOf(StoryJson.Deserialize(json.Replace(fresh, "../evil.m4a")), piece)!.File);
        Assert.DoesNotContain("\"File\"", StoryJson.Serialize(StoryRecordings.WithClip(MakeStory("A. B."), piece, new PageRecording("en", 3))));
    }

    [Fact]
    public void A_new_clip_replaces_the_files_of_every_clip_it_shares_a_sentence_with_its_own_old_one_included()
    {
        Story story = MakeStory("A. B. C. D.");
        var exact = new StoryClipKey(0, 0, 1);
        var other = new StoryClipKey(0, 3, 3);
        string exactFile = StoryRecordings.NewFileName(exact);
        story = StoryRecordings.WithClip(story, exact, new PageRecording("en", 2, exactFile));
        story = StoryRecordings.WithClip(story, other, new PageRecording("en", 2));

        Assert.Equal([exactFile], StoryRecordings.ReplacedFiles(story, exact));
        Assert.Equal([exactFile], StoryRecordings.ReplacedFiles(story, new StoryClipKey(0, 1, 2)));
        Assert.Empty(StoryRecordings.ReplacedFiles(story, new StoryClipKey(0, 2, 2)));
    }

    [Fact]
    public void A_take_narrower_than_an_old_clip_replaces_beyond_itself_and_an_equal_or_wider_one_does_not()
    {
        Story whole = StoryRecordings.With(MakeStory("A. B. C."), 0, new PageRecording("en", 5));
        Story piece = StoryRecordings.WithClip(MakeStory("A. B. C."), new StoryClipKey(0, 0, 1), new PageRecording("en", 5));

        Assert.True(StoryRecordings.ReplacesBeyond(whole, new StoryClipKey(0, 1, 1)));
        Assert.True(StoryRecordings.ReplacesBeyond(piece, new StoryClipKey(0, 1, 2)));
        Assert.False(StoryRecordings.ReplacesBeyond(piece, new StoryClipKey(0, 0, 1)));
        Assert.False(StoryRecordings.ReplacesBeyond(piece, new StoryClipKey(0, 0, 2)));
        Assert.False(StoryRecordings.ReplacesBeyond(piece, new StoryClipKey(0, 2, 2)));
    }

    [Fact]
    public async Task A_commit_never_overwrites_a_file_and_nothing_deletes_a_recording_but_the_replaced_ones()
    {
        string root = Path.Combine(Path.GetTempPath(), "dino-commit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var library = new FileStoryLibrary(root);
            Story story = MakeStory("A. B.");
            await library.AddAsync(story);
            var key = new StoryClipKey(0, 0, 1);
            string oldName = StoryRecordings.NewFileName(key);
            string newName = StoryRecordings.NewFileName(key);
            await library.StoreRecordingFileAsync(story.Id, oldName, path => File.WriteAllTextAsync(path, "old"));
            await library.StoreRecordingFileAsync(story.Id, newName, path => File.WriteAllTextAsync(path, "new"));
            await library.StoreRecordingFileAsync(story.Id, "rec-9-0123abcd.m4a", path => File.WriteAllTextAsync(path, "stray"));
            await File.WriteAllTextAsync(library.TryResolveFile(story.Id, "notes.txt")!, "keep");
            Story saved = StoryRecordings.WithClip(story, key, new PageRecording("en", 2, newName));

            await library.UpdateAsync(saved);
            foreach (string replaced in StoryRecordings.ReplacedFiles(story, key).Append(oldName).Distinct())
            {
                await library.DeleteFileAsync(story.Id, replaced);
            }

            Assert.Equal("new", await File.ReadAllTextAsync(library.TryResolveFile(story.Id, newName)!));
            Assert.False(File.Exists(library.TryResolveFile(story.Id, oldName)));
            Assert.True(File.Exists(library.TryResolveFile(story.Id, "rec-9-0123abcd.m4a"))); // a file no clip names is never swept
            Assert.True(File.Exists(library.TryResolveFile(story.Id, "notes.txt")));
            await Assert.ThrowsAsync<ArgumentException>(() => library.StoreRecordingFileAsync(story.Id, "../evil.m4a", _ => Task.CompletedTask));
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
    public void A_piece_clip_does_not_move_when_the_page_splits_into_a_different_number_of_sentences()
    {
        var piece = new StoryClipKey(0, 1, 1);
        const string Page = "こんにちは。元気ですか。"; // two Japanese sentences, which English rules do not split
        Story stored = MakeStoryIn("en", Page) with { PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [piece] = new("en", 3) }, UseRecordings = true };
        Story sameSplit = MakeStoryIn("en", Page);
        Story otherSplit = MakeStoryIn("ja", Page);

        Assert.Equal(1, RecordSplitBook.SentenceCounts(sameSplit)[0]);
        Assert.NotEqual(RecordSplitBook.SentenceCounts(sameSplit)[0], RecordSplitBook.SentenceCounts(otherSplit)[0]);
        Assert.Single(StarterUpdate.PieceMoves(stored, sameSplit));
        Assert.Empty(StarterUpdate.PieceMoves(stored, otherSplit));
    }

    [Fact]
    public void A_moved_clip_does_not_carry_the_old_story_files_name()
    {
        var piece = new StoryClipKey(0, 0, 0);
        string fresh = StoryRecordings.NewFileName(piece);
        Story stored = MakeStory("A.") with { PieceRecordings = new Dictionary<StoryClipKey, PageRecording> { [piece] = new("en", 3, fresh) }, UseRecordings = true };

        Story refreshed = StarterUpdate.Refresh(stored, MakeStory("A.") with { Id = StoryIds.New() });

        Assert.Null(refreshed.PieceRecordings![piece].File);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("pt-BR")]
    [InlineData("ja")]
    [InlineData("xx")]
    public void Recording_and_reading_cut_the_same_sentences_so_a_clip_keeps_its_key(string language)
    {
        Story story = MakeStoryIn(language, "Hello there. How are you?", "こんにちは。元気ですか。");

        IReadOnlyList<SentenceSpan> read = RecordSplitBook.SentencesOf(story);
        RecordBook recorded = RecordSplitBook.Of(story);
        IReadOnlyList<int> perPage = RecordSplitBook.SentenceCounts(story);

        Assert.Equal(read.Count, recorded.Sentences.Count);
        Assert.Equal(StoryRecordings.SentencesPerPage(story.Pages, read), perPage);
        Assert.Equal(perPage, recorded.Sentences.GroupBy(s => s.Page).OrderBy(g => g.Key).Select(g => g.Count()).ToList());
    }

    [Fact]
    public void A_whole_page_clip_covers_a_page_that_holds_no_sentence()
    {
        Story story = StoryRecordings.With(MakeStory("A.", "..."), 1, new PageRecording("en", 2));

        Assert.Contains(1, StoryRecordings.PagesShownInOwnLanguage(story, [1, 0]));
    }
}
