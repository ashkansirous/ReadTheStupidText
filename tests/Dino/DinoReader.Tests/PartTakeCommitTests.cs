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

    // What RecordPage.CommitAsync does for one take.
    private static async Task<Story> CommitAsync(FileStoryLibrary library, string storyId, RecordPiece card)
    {
        string fileName = StoryRecordings.NewFileName(card.Key);
        await library.StoreRecordingFileAsync(storyId, fileName, path => File.WriteAllBytesAsync(path, [1, 2, 3, 4]));
        Story latest = StoryRecordings.WithoutFile((await library.GetAsync(storyId))!, fileName); // as RecordPage.SaveWithClipAsync reads it
        IReadOnlyList<string> replaced = StoryRecordings.ReplacedFiles(latest, card.Key, card.Cover);
        Story updated = StoryRecordings.WithSavedTake(latest, card.Key, new PageRecording("en", 9, fileName, card.Cover));
        await library.UpdateAsync(updated);
        foreach (string name in replaced)
        {
            await library.DeleteFileAsync(storyId, name);
        }

        return updated;
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
