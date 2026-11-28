using System.Text.RegularExpressions;
using DinoReader.Core;
using Sirous.Reading.Application.Usage;

namespace DinoReader.Tests;

/// <summary>Slice 425: each unreadable-book site reports its own reason, a store book names its catalogue id, an own book never does, and one bad book counts once per launch.</summary>
public sealed class UnreadableStoryFallbackTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dino-425-" + Guid.NewGuid().ToString("N"));

    public UnreadableStoryFallbackTests() => FileStoryLibrary.ResetUnreadableReported();

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task A_newer_version_store_book_reports_its_reason_with_the_catalogue_id()
    {
        string folder = StoryIds.New();
        string catalogue = StoryIds.New();

        var sink = await ListWithAsync(folder, $$"""{"Version":999,"Id":"{{folder}}","SourceId":"{{catalogue}}"}""");

        Assert.Equal([(FallbackReason.StoryNewerVersion, catalogue)], sink.Books);
    }

    [Fact]
    public async Task A_missing_or_malformed_id_reports_invalid_not_newer_version()
    {
        string folder = StoryIds.New();
        string catalogue = StoryIds.New();

        var sink = await ListWithAsync(folder, $$"""{"Version":1,"Id":"abc","SourceId":"{{catalogue}}"}""");

        Assert.Equal([(FallbackReason.StoryInvalid, catalogue)], sink.Books);
    }

    [Fact]
    public async Task Missing_fields_report_their_reason_with_the_catalogue_id()
    {
        string folder = StoryIds.New();
        string catalogue = StoryIds.New();

        var sink = await ListWithAsync(folder, $$"""{"Version":1,"Id":"{{folder}}","Title":null,"SourceId":"{{catalogue}}"}""");

        Assert.Equal([(FallbackReason.StoryMissingFields, catalogue)], sink.Books);
    }

    [Fact]
    public async Task An_empty_file_and_a_broken_file_report_their_own_reasons_with_no_id()
    {
        var empty = await ListWithAsync(StoryIds.New(), "null");
        var broken = await ListWithAsync(StoryIds.New(), "{nope");

        Assert.Equal([(FallbackReason.StoryEmpty, (string?)null)], empty.Books);
        Assert.Equal([(FallbackReason.StoryInvalid, (string?)null)], broken.Books);
    }

    [Fact]
    public async Task A_store_book_whose_id_differs_from_its_folder_reports_the_mismatch_with_its_catalogue_id()
    {
        string folder = StoryIds.New();
        string catalogue = StoryIds.New();
        Story other = new(StoryIds.New(), "T", "en", DateTimeOffset.UnixEpoch, [new StoryPage("p")], [], CoverStyle.Colour, 0, "F1", SourceId: catalogue);

        var sink = await ListWithAsync(folder, StoryJson.Serialize(other));

        Assert.Equal([(FallbackReason.StoryIdMismatch, catalogue)], sink.Books);
    }

    [Fact]
    public async Task An_own_book_sends_no_id_even_when_it_is_damaged()
    {
        string folder = StoryIds.New();
        Story own = new(StoryIds.New(), "T", "en", DateTimeOffset.UnixEpoch, [new StoryPage("p")], [], CoverStyle.Colour, 0, "F1");

        var library = new FileStoryLibrary(_root);
        var sink = new BookSink();
        library.Fallbacks = sink;
        Directory.CreateDirectory(library.FolderOf(folder));
        await File.WriteAllTextAsync(Path.Combine(library.FolderOf(folder), "story.json"), StoryJson.Serialize(own), TestContext.Current.CancellationToken);

        Assert.Null(await library.GetAsync(folder)); // the shelf listing never shows an own book, a lookup by id does

        Assert.Equal([(FallbackReason.StoryIdMismatch, (string?)null)], sink.Books);
    }

    [Fact]
    public async Task A_second_listing_in_the_same_launch_reports_nothing()
    {
        string folder = StoryIds.New();
        var library = new FileStoryLibrary(_root);
        var sink = new BookSink();
        library.Fallbacks = sink;
        Directory.CreateDirectory(library.FolderOf(folder));
        await File.WriteAllTextAsync(Path.Combine(library.FolderOf(folder), "story.json"), $$"""{"version":999,"id":"{{folder}}","sourceId":"{{StoryIds.New()}}"}""", TestContext.Current.CancellationToken);

        await library.ListAsync();
        await library.ListAsync();

        Assert.Single(sink.Books);
    }

    [Fact]
    public void Each_format_error_has_its_reason_and_the_shelf_rebuild_kinds_are_told_apart()
    {
        Assert.Equal(FallbackReason.StoryEmpty, FallbackUsage.ReasonOf(StoryFormatError.Empty));
        Assert.Equal(FallbackReason.StoryInvalid, FallbackUsage.ReasonOf(StoryFormatError.Invalid));
        Assert.Equal(FallbackReason.StoryNewerVersion, FallbackUsage.ReasonOf(StoryFormatError.NewerVersion));
        Assert.Equal(FallbackReason.StoryNoPages, FallbackUsage.ReasonOf(StoryFormatError.NoPages));
        Assert.Equal(FallbackReason.StoryMissingFields, FallbackUsage.ReasonOf(StoryFormatError.MissingFields));

        Assert.Equal(FallbackReason.ShelfRebuildTimeout, FallbackUsage.ShelfRebuildReasonOf(new TimeoutException()));
        Assert.Equal(FallbackReason.ShelfRebuildTimeout, FallbackUsage.ShelfRebuildReasonOf(new TaskCanceledException("t", new TimeoutException())));
        Assert.Equal(FallbackReason.ShelfRebuildCancelled, FallbackUsage.ShelfRebuildReasonOf(new OperationCanceledException()));
        Assert.Equal(FallbackReason.ShelfRebuildIo, FallbackUsage.ShelfRebuildReasonOf(new IOException()));
        Assert.Equal(FallbackReason.ShelfRebuildIo, FallbackUsage.ShelfRebuildReasonOf(new HttpRequestException()));
        Assert.Equal(FallbackReason.ShelfRebuildLibrary, FallbackUsage.ShelfRebuildReasonOf(new StoryFormatException("x")));
        Assert.Equal(FallbackReason.ShelfRebuildLayout, FallbackUsage.ShelfRebuildReasonOf(new InvalidOperationException()));
        Assert.Equal(FallbackReason.ShelfRebuildOther, FallbackUsage.ShelfRebuildReasonOf(new NullReferenceException()));
        Assert.Equal(FallbackReason.ShelfRebuildOther, FallbackUsage.ShelfRebuildReasonOf(new KeyNotFoundException()));
        Assert.Equal(FallbackReason.ShelfRebuildOther, FallbackUsage.ShelfRebuildReasonOf(new ObjectDisposedException("x")));
        Assert.Equal(FallbackReason.ShelfRebuildOther, FallbackUsage.ShelfRebuildReasonOf(new NotSupportedException()));
    }

    [Fact]
    public void The_detail_takes_a_third_part_only_for_a_store_book_reason_and_a_real_id()
    {
        string id = StoryIds.New();

        Assert.Equal($"library:story_empty:{id}", FallbackUsage.Detail(FallbackArea.Library, FallbackReason.StoryEmpty, id.ToUpperInvariant()));
        Assert.Equal("library:story_empty", FallbackUsage.Detail(FallbackArea.Library, FallbackReason.StoryEmpty, "not an id"));
        Assert.Equal("library:story_empty", FallbackUsage.Detail(FallbackArea.Library, FallbackReason.StoryEmpty, null));
        Assert.Equal("library:own_book_delete_failed", FallbackUsage.Detail(FallbackArea.Library, FallbackReason.OwnBookDeleteFailed, id));
        Assert.Null(FallbackUsage.Detail(FallbackArea.Library, "nope", id));
    }

    [Fact]
    public void Only_an_id_the_catalogue_accepts_may_ride_as_the_third_part()
    {
        string store = StoryIds.New();
        string own = StoryIds.New(); // an imported own package's random id

        Assert.Equal(store, FallbackUsage.CatalogueBookOf(store.ToUpperInvariant(), id => id == store));
        Assert.Null(FallbackUsage.CatalogueBookOf(own, id => id == store));
        Assert.Null(FallbackUsage.CatalogueBookOf("not an id", _ => true));
        Assert.Null(FallbackUsage.CatalogueBookOf(null, _ => true));
        Assert.Equal("library:story_empty", FallbackUsage.Detail(FallbackArea.Library, FallbackReason.StoryEmpty, FallbackUsage.CatalogueBookOf(own, id => id == store)));
    }

    [Fact]
    public async Task A_listing_with_no_sink_does_not_use_up_the_report_of_a_later_listing()
    {
        var library = new FileStoryLibrary(Path.Combine(_root, Guid.NewGuid().ToString("N")));
        string folder = StoryIds.New();
        Directory.CreateDirectory(library.FolderOf(folder));
        await File.WriteAllTextAsync(Path.Combine(library.FolderOf(folder), "story.json"), "{nope", TestContext.Current.CancellationToken);
        await library.ListAsync(); // default sink: nothing delivered
        var sink = new BookSink();
        library.Fallbacks = sink;

        await library.ListAsync();

        Assert.Single(sink.Books);
    }

    [Fact]
    public void The_Worker_takes_a_third_part_for_exactly_the_reasons_the_app_sends_one_for()
    {
        string source = File.ReadAllText(Path.Combine(RepoRoot(), "worker", "events.ts"));
        Match list = Regex.Match(source, @"FALLBACK_BOOK_REASONS = \[(?<names>[^\]]*)\]");
        string[] worker = Regex.Matches(list.Groups["names"].Value, "\"(?<n>[a-z_]+)\"").Select(m => m.Groups["n"].Value).Order().ToArray();

        string[] app = FallbackReason.All.Where(FallbackUsage.NamesStoreBook).Order().ToArray();

        Assert.True(list.Success);
        Assert.Equal(app, worker);
    }

    private async Task<BookSink> ListWithAsync(string folder, string json)
    {
        var library = new FileStoryLibrary(Path.Combine(_root, Guid.NewGuid().ToString("N")));
        var sink = new BookSink();
        library.Fallbacks = sink;
        Directory.CreateDirectory(library.FolderOf(folder));
        await File.WriteAllTextAsync(Path.Combine(library.FolderOf(folder), "story.json"), json, TestContext.Current.CancellationToken);

        await library.ListAsync();
        return sink;
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ReadTheStupidText.slnx")))
        {
            dir = dir.Parent;
        }

        return dir!.FullName;
    }

    private sealed class BookSink : IBookFallbackSink
    {
        public List<(string Reason, string? Book)> Books { get; } = [];

        public void Report(string area, string reason, string? language = null) => Books.Add((reason, null));

        public void ReportBook(string area, string reason, string book) => Books.Add((reason, book));
    }
}
