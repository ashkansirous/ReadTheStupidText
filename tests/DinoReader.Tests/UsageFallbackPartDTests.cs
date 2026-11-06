using DinoReader.Core;
using ReadTheStupidText.Application.Usage;

namespace DinoReader.Tests;

/// <summary>Slice 273 part D: the shelf's swallowed read errors are counted as fallbacks, and every part D kind is on the closed lists.</summary>
public sealed class UsageFallbackPartDTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-part-d-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task A_story_that_cannot_be_read_is_left_off_the_shelf_and_counted()
    {
        var library = new FileStoryLibrary(_root);
        var sink = new ListSink();
        library.Fallbacks = sink;
        string broken = library.FolderOf(StoryIds.New());
        Directory.CreateDirectory(broken);
        await File.WriteAllTextAsync(Path.Combine(broken, "story.json"), "{nope", TestContext.Current.CancellationToken);

        IReadOnlyList<Story> stories = await library.ListAsync();

        Assert.Empty(stories);
        Assert.Equal([(FallbackArea.Library, FallbackReason.StoryUnreadable)], sink.Reported);
    }

    [Fact]
    public async Task A_library_with_no_sink_set_reads_as_before()
    {
        var library = new FileStoryLibrary(_root);
        string broken = library.FolderOf(StoryIds.New());
        Directory.CreateDirectory(broken);
        await File.WriteAllTextAsync(Path.Combine(broken, "story.json"), "{nope", TestContext.Current.CancellationToken);

        Assert.Empty(await library.ListAsync());
    }

    [Fact]
    public void Every_part_D_kind_is_on_the_closed_lists_and_has_a_detail()
    {
        (string Area, string Reason)[] kinds =
        [
            (FallbackArea.Speech, FallbackReason.StorytellerPackMissing), (FallbackArea.Speech, FallbackReason.AnimalClipUnplayable),
            (FallbackArea.PremadeAudio, FallbackReason.ClipUnplayable), (FallbackArea.PremadeAudio, FallbackReason.ClipSpeedUnset),
            (FallbackArea.Store, FallbackReason.InAppUpdateFailed), (FallbackArea.Window, FallbackReason.WindowFrameFailed),
            (FallbackArea.Window, FallbackReason.DisplaysUnreadable), (FallbackArea.Font, FallbackReason.FontFallback),
            (FallbackArea.Library, FallbackReason.ShelfOrderUnreadable), (FallbackArea.Library, FallbackReason.StoryUnreadable),
        ];

        Assert.All(kinds, kind => Assert.Equal($"{kind.Area}:{kind.Reason}", FallbackUsage.Detail(kind.Area, kind.Reason)));
        Assert.Equal(FallbackReason.All.Count, FallbackReason.All.Distinct().Count());
        Assert.Equal(FallbackArea.All.Count, FallbackArea.All.Distinct().Count());
    }

    private sealed class ListSink : IFallbackSink
    {
        public List<(string Area, string Reason)> Reported { get; } = [];

        public void Report(string area, string reason, string? language = null) => Reported.Add((area, reason));
    }
}
