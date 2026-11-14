using DinoReader.Core;
using Sirous.Reading.Application.Usage;

namespace DinoReader.Tests;

// Slice 275 (review finding 7): the library's read path catches an unreadable shelf-order.json, counts it once and keeps the shelf readable.
public sealed class ShelfOrderUnreadableReportTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-order-bad-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static Story Make(string id, int day) => new(
        id, id, "en", new DateTimeOffset(2026, 10, day, 0, 0, 0, TimeSpan.Zero),
        [new StoryPage("Text.")], [], CoverStyle.Colour, 0, "F1", SourceId: StoryIds.New());

    private sealed class ListSink : IFallbackSink
    {
        public List<(string Area, string Reason)> Reported { get; } = [];

        public void Report(string area, string reason, string? language = null) => Reported.Add((area, reason));
    }

    private async Task<(FileStoryLibrary Library, ListSink Sink, string[] Ids)> ShelfWithCorruptOrderAsync()
    {
        var sink = new ListSink();
        var library = new FileStoryLibrary(_root) { Fallbacks = sink };
        string older = StoryIds.New();
        string newer = StoryIds.New();
        await library.AddAsync(Make(older, 1));
        await library.AddAsync(Make(newer, 2));
        await File.WriteAllTextAsync(Path.Combine(_root, ShelfOrder.FileName), "not json");
        return (library, sink, [newer, older]);
    }

    [Fact]
    public async Task ListingWithACorruptOrderFileKeepsTheShelfAndReportsOnce()
    {
        (FileStoryLibrary library, ListSink sink, string[] ids) = await ShelfWithCorruptOrderAsync();

        IReadOnlyList<Story> listed = await library.ListAsync();

        Assert.Equal(ids, listed.Select(s => s.Id));
        Assert.Equal([(FallbackArea.Library, FallbackReason.ShelfOrderUnreadable)], sink.Reported);
    }

    [Fact]
    public async Task SavingTheOrderOverACorruptFileReportsOnceAndTheNewOrderIsReadable()
    {
        (FileStoryLibrary library, ListSink sink, string[] ids) = await ShelfWithCorruptOrderAsync();

        await library.SaveOrderAsync([ids[1], ids[0]]);

        Assert.Equal([(FallbackArea.Library, FallbackReason.ShelfOrderUnreadable)], sink.Reported);
        Assert.Equal([ids[1], ids[0]], (await library.ListAsync()).Select(s => s.Id));
        Assert.Single(sink.Reported);
    }

    // Slice 275 part 3 (review finding 1): a delete over a corrupt order file counts it once and still deletes the book.
    [Fact]
    public async Task DeletingABookOverACorruptOrderFileReportsOnceAndStillDeletes()
    {
        (FileStoryLibrary library, ListSink sink, string[] ids) = await ShelfWithCorruptOrderAsync();

        await library.DeleteAsync(ids[0]);

        Assert.Equal([(FallbackArea.Library, FallbackReason.ShelfOrderUnreadable)], sink.Reported);
        Assert.Null(await library.GetAsync(ids[0]));
    }
}
