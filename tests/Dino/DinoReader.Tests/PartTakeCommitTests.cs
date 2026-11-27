using DinoReader.BookBuilder;
using DinoReader.Core;

namespace DinoReader.Tests;

/// <summary>Slice 443 (emulator run): a part take saved the way the Record page commits it (fresh file, story saved with the clip, replaced files deleted) is still on disk, non-empty, and named by the stored story.</summary>
[Collection(PartTakesCollection.Name)]
public sealed class PartTakeCommitTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "dino-parttake-" + Guid.NewGuid().ToString("N"));

    public PartTakeCommitTests() => PartTakes.Enabled = true;

    public void Dispose()
    {
        PartTakes.Enabled = true;
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static Story Freddy()
    {
        string md = File.ReadAllText(Path.Combine(StarterBuilder.RepoRoot(), "content", "dino", "freddy-and-the-slipper", "story.md"));
        string[] pages = md.Replace("\r\n", "\n").Split("\n---\n", StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
        return new Story(StoryIds.New(), "Freddy", "en", DateTimeOffset.UnixEpoch, pages.Select(t => new StoryPage(t)).ToList(), [], CoverStyle.Colour, 0, string.Empty);
    }

    // What RecordPage.CommitAsync does for one take: the file is placed, then the real Core step (Slice 464) saves the story and the replaced files go.
    private static async Task<Story> CommitAsync(FileStoryLibrary library, string storyId, RecordPiece card) =>
        await CommitFileAsync(library, storyId, card.Key, StoryRecordings.NewFileName(card.Key), card.Cover);

    private static async Task<Story> CommitFileAsync(FileStoryLibrary library, string storyId, StoryClipKey key, string fileName, SentenceCover? cover) =>
        (await CommitSavedAsync(library, storyId, key, fileName, cover)).Story;

    private static async Task<PartTakeSaved> CommitSavedAsync(FileStoryLibrary library, string storyId, StoryClipKey key, string fileName, SentenceCover? cover)
    {
        await library.StoreRecordingFileAsync(storyId, fileName, path => File.WriteAllBytesAsync(path, [1, 2, 3, 4]));
        PartTakeSaved saved = await PartTakeCommit.SaveAsync(library, storyId, (await library.GetAsync(storyId))!, key, new PageRecording("en", 9, fileName, cover), cover);
        foreach (string name in saved.ReplacedFiles)
        {
            await library.DeleteFileAsync(storyId, name);
        }

        return saved;
    }

    private async Task<(FileStoryLibrary Library, Story Story)> InlineBookAsync()
    {
        var library = new FileStoryLibrary(_folder);
        var story = new Story(StoryIds.New(), "Inline", "en", DateTimeOffset.UnixEpoch, [new StoryPage("One. Two. Three."), new StoryPage("Four.")], [], CoverStyle.Colour, 0, string.Empty);
        await library.AddAsync(story);
        return (library, story);
    }

    private static StoryClipKey Part(int first, int last) => StoryClipKey.OfParts(0, first, last, "en");

    private static Task PlaceOrphanAsync(FileStoryLibrary library, string storyId, string name) =>
        library.StoreRecordingFileAsync(storyId, name, path => File.WriteAllBytesAsync(path, [9, 9]));

    private const string OlderOrphan = "rec-en-1-p2-3.m4a";
    private const string FreshFile = "rec-en-1-p1-2-abcd1234.m4a"; // sorts before the orphan, so adoption keeps it and skips the orphan

    [Fact]
    public async Task A_new_take_deletes_an_older_overlapping_orphan_that_sorts_after_it()
    {
        (FileStoryLibrary library, Story story) = await InlineBookAsync();
        await PlaceOrphanAsync(library, story.Id, OlderOrphan);

        await CommitFileAsync(library, story.Id, Part(0, 1), FreshFile, null);

        Assert.False(File.Exists(library.TryResolveFile(story.Id, OlderOrphan)!), "the older overlapping file is gone from disk");
        Assert.True(File.Exists(library.TryResolveFile(story.Id, FreshFile)!));
    }

    [Fact]
    public async Task A_new_take_removed_later_does_not_bring_the_older_overlapping_file_back()
    {
        (FileStoryLibrary library, Story story) = await InlineBookAsync();
        await PlaceOrphanAsync(library, story.Id, OlderOrphan);
        await CommitFileAsync(library, story.Id, Part(0, 1), FreshFile, null);

        await library.UpdateAsync(StoryRecordings.WithoutClip((await library.GetAsync(story.Id))!, Part(0, 1)));
        await library.DeleteFileAsync(story.Id, FreshFile);

        Assert.Empty(StoryRecordings.Files((await library.GetAsync(story.Id))!));
    }

    [Fact]
    public async Task A_new_take_keeps_a_part_file_that_does_not_overlap_it()
    {
        (FileStoryLibrary library, Story story) = await InlineBookAsync();
        await PlaceOrphanAsync(library, story.Id, "rec-en-1-p3-3.m4a");

        await CommitFileAsync(library, story.Id, Part(0, 1), FreshFile, null);

        Assert.True(File.Exists(library.TryResolveFile(story.Id, "rec-en-1-p3-3.m4a")!), "words the new take does not cover keep their recording");
    }

    [Fact]
    public async Task A_new_take_keeps_an_overlapping_file_of_another_language_or_page()
    {
        (FileStoryLibrary library, Story story) = await InlineBookAsync();
        await PlaceOrphanAsync(library, story.Id, "rec-de-1-p2-3.m4a");
        await PlaceOrphanAsync(library, story.Id, "rec-en-2-p2-3.m4a");

        await CommitFileAsync(library, story.Id, Part(0, 1), FreshFile, null);

        Assert.True(File.Exists(library.TryResolveFile(story.Id, "rec-de-1-p2-3.m4a")!), "another language");
        Assert.True(File.Exists(library.TryResolveFile(story.Id, "rec-en-2-p2-3.m4a")!), "another page");
    }

    [Fact]
    public async Task A_new_take_replaces_an_orphan_that_reaches_past_it_as_it_would_an_adopted_clip()
    {
        (FileStoryLibrary library, Story story) = await InlineBookAsync();
        await PlaceOrphanAsync(library, story.Id, "rec-en-1-p2-4.m4a");
        Story adopted = PartClipAdoption.WithOrphansAdopted(story, Part(0, 1), FreshFile, ["rec-en-1-p2-4.m4a"]);

        PartTakeSaved saved = await CommitSavedAsync(library, story.Id, Part(0, 1), FreshFile, null);

        Assert.True(StoryRecordings.ReplacesBeyond(adopted, Part(0, 1)), "the card would warn, as for an adopted clip");
        Assert.True(saved.ReplacesBeyond, "the commit-time answer carries the warning the page's earlier story could not");
        Assert.Equal(["rec-en-1-p2-4.m4a"], saved.ReplacedFiles);
        Assert.False(File.Exists(library.TryResolveFile(story.Id, "rec-en-1-p2-4.m4a")!));
    }

    [Theory]
    [InlineData("rec-en-1-p2-3.m4a", 0, 1, "rec-en-1-p1-2-abcd1234.m4a")] // the new file sorts before the orphan
    [InlineData("rec-en-1-p1-2.m4a", 1, 2, "rec-en-1-p2-3-abcd1234.m4a")] // the new file sorts after it
    public async Task The_first_recording_flag_does_not_depend_on_where_the_new_file_sorts(string orphan, int first, int last, string fresh)
    {
        (FileStoryLibrary library, Story story) = await InlineBookAsync();
        await PlaceOrphanAsync(library, story.Id, orphan);

        PartTakeSaved saved = await CommitSavedAsync(library, story.Id, Part(first, last), fresh, null);

        Assert.False(saved.First, "an earlier recording of those words existed");
    }

    [Fact]
    public async Task An_orphan_only_book_keeps_the_switch_on_after_a_take_is_saved()
    {
        (FileStoryLibrary library, Story story) = await InlineBookAsync();
        await PlaceOrphanAsync(library, story.Id, OlderOrphan);

        Story saved = await CommitFileAsync(library, story.Id, Part(0, 1), FreshFile, null);

        Assert.True(saved.UseRecordings);
    }

    [Fact]
    public async Task With_the_part_take_switch_off_no_overlapping_file_is_listed_or_deleted()
    {
        (FileStoryLibrary library, Story story) = await InlineBookAsync();
        await PlaceOrphanAsync(library, story.Id, OlderOrphan);
        PartTakes.Enabled = false;

        PartTakeSaved saved = await CommitSavedAsync(library, story.Id, Part(0, 1), FreshFile, null);

        Assert.Empty(saved.ReplacedFiles);
        Assert.True(File.Exists(library.TryResolveFile(story.Id, OlderOrphan)!), "nothing was deleted with the switch off");
    }

    [Fact]
    public void OverlappingOrphans_lists_only_unnamed_overlapping_part_files_of_the_same_page_and_language()
    {
        var story = new Story(StoryIds.New(), "Inline", "en", DateTimeOffset.UnixEpoch, [new StoryPage("One. Two. Three."), new StoryPage("Four.")], [], CoverStyle.Colour, 0, string.Empty);
        string[] files = [OlderOrphan, "rec-en-1-p3-3.m4a", "rec-en-1-p1-1.m4a", "rec-de-1-p2-3.m4a", "rec-en-2-p2-3.m4a", "rec-en-1-p1-2-abcd1234.m4a", "rec-en-1-p1-2.m4a", "notes.txt"];

        IReadOnlyList<string> orphans = PartClipAdoption.OverlappingOrphans(story, Part(0, 1), "rec-en-1-p1-2-abcd1234.m4a", files);

        Assert.Equal(["rec-en-1-p1-1.m4a", "rec-en-1-p1-2.m4a", OlderOrphan], orphans);
    }

    [Fact]
    public void OverlappingOrphans_skips_a_file_a_clip_already_names()
    {
        var story = new Story(StoryIds.New(), "Inline", "en", DateTimeOffset.UnixEpoch, [new StoryPage("One. Two. Three.")], [], CoverStyle.Colour, 0, string.Empty);
        Story named = StoryRecordings.WithClip(story, Part(1, 2), new PageRecording("en", 0, OlderOrphan));

        Assert.Empty(PartClipAdoption.OverlappingOrphans(named, Part(0, 1), FreshFile, [OlderOrphan]));
    }

    [Fact]
    public async Task Part_takes_saved_one_after_another_are_all_on_disk_and_turn_the_switch_on()
    {
        var library = new FileStoryLibrary(_folder);
        Story story = Freddy();
        await library.AddAsync(story);
        RecordBook book = RecordSplitBook.Of(story);
        IReadOnlySet<string> mummy = book.Speakers.Where(s => s.Name == "Mummy").Select(s => s.Id).ToHashSet();
        IReadOnlyList<RecordPiece> cards = RecordSplitWalk.InLanguage(RecordSplit.ByCharacter(book.Sentences, mummy).ToList(), "en");
        RecordPiece[] two = cards.Where(c => c.Key.IsPart).Take(2).ToArray();
        Assert.Equal(2, two.Length);

        foreach (RecordPiece card in two)
        {
            Story saved = await CommitAsync(library, story.Id, card);
            Assert.True(saved.UseRecordings, "saved story " + card.Key);
            Assert.True((await library.GetAsync(story.Id))!.UseRecordings, "re-read " + card.Key);
        }

        Story read = (await library.GetAsync(story.Id))!;
        Assert.True(read.UseRecordings);
        string[] files = StoryRecordings.Files(read).ToArray();
        Assert.Equal(2, files.Length);
        Assert.All(files, f => Assert.True(File.Exists(library.TryResolveFile(story.Id, f)!) && new FileInfo(library.TryResolveFile(story.Id, f)!).Length > 0, f));
    }
}
